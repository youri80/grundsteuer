using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Persistence.Abstractions;
using GrundsteuerPortal.Persistence.Entities;
using GrundsteuerPortal.Persistence.Mapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GrundsteuerPortal.Persistence;

/// <summary>
/// EF-Core-Implementierung der lokalen Datenhaltung.
///
/// Lebensdauer: <b>scoped</b>. Blazor Server hält je Circuit (Browser-Tab) einen Scope, damit
/// <see cref="GrundsteuerDbContext"/> nicht über Circuits hinweg geteilt wird - sonst kollidieren
/// nebenläufige Änderungen und ein DbContext ist nicht thread-safe.
///
/// Grundsatz: Jede schreibende Operation läuft in einer Transaktion und protokolliert den
/// Statuswechsel im Verlauf. Ein Steuerformular ohne nachvollziehbaren Verlauf wäre wertlos.
/// </summary>
public sealed class GrundsteuerRepository : IGrundsteuerRepository
{
    private readonly GrundsteuerDbContext _db;
    private readonly ILogger<GrundsteuerRepository> _logger;

    public GrundsteuerRepository(GrundsteuerDbContext db, ILogger<GrundsteuerRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    // -----------------------------------------------------------------------------------------
    //  Lesen
    // -----------------------------------------------------------------------------------------
    public async Task<List<GrundsteuerUebersichtDto>> GetUebersichtAsync(CancellationToken ct = default)
    {
        var zeilen = await _db.Meldungen
            .AsNoTracking()
            .Include(m => m.Lage)
            .Include(m => m.Eigentuemer)
            .Include(m => m.Hinweise)
            .Include(m => m.Uebermittlungen)
            .OrderByDescending(m => m.ZuletztGeaendertAm)
            .ToListAsync(ct);

        return zeilen.Select(MeldungMapper.ZuUebersicht).ToList();
    }

    public async Task<GrundsteuerMeldungDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await VolleMeldung(id).FirstOrDefaultAsync(ct);
        return entity is null ? null : MeldungMapper.ZuDto(entity);
    }

    private IQueryable<GrundsteuerMeldungEntity> VolleMeldung(Guid id) =>
        _db.Meldungen
            .Include(m => m.Lage)
            .Include(m => m.Berechnung)
            .Include(m => m.Flurstuecke)
            .Include(m => m.Eigentuemer)
            .Include(m => m.Hinweise)
            .Where(m => m.Id == id);

    // -----------------------------------------------------------------------------------------
    //  Schreiben
    // -----------------------------------------------------------------------------------------
    public async Task<ApiResponse> SpeichernAsync(GrundsteuerMeldungDto dto, CancellationToken ct = default)
    {
        // Nebenläufigkeitsprüfung vor dem Schreiben: wer mit einem veralteten RowVersion speichert,
        // würde sonst Änderungen aus einem anderen Tab überschreiben.
        if (dto.Id != Guid.Empty && dto.RowVersion is { Length: > 0 })
        {
            var aktuell = await _db.Meldungen
                .AsNoTracking()
                .Where(m => m.Id == dto.Id)
                .Select(m => m.RowVersion)
                .FirstOrDefaultAsync(ct);

            if (aktuell is not null && !aktuell.SequenceEqual(dto.RowVersion))
            {
                return ApiResponse.Fehler(
                    "Die Meldung wurde zwischenzeitlich in einem anderen Fenster geändert. "
                    + "Bitte neu laden, damit keine Eingabe verloren geht.",
                    "NEBENLAEUFIGKEIT");
            }
        }

        var istNeu = dto.Id == Guid.Empty;
        GrundsteuerMeldungEntity entity;

        if (istNeu)
        {
            entity = new GrundsteuerMeldungEntity { Id = Guid.NewGuid(), Lage = new LageAdresse() };
            _db.Meldungen.Add(entity);
        }
        else
        {
            entity = await VolleMeldung(dto.Id).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Meldung {dto.Id} existiert nicht.");
        }

        var vorherStatus = entity.Status;
        MeldungMapper.Uebernehme(dto, entity);

        entity.ZuletztGeaendertAm = DateTime.UtcNow;
        entity.RowVersion = Guid.NewGuid().ToByteArray();

        if (istNeu)
        {
            entity.ErstelltAm = DateTime.UtcNow;
            _db.StatusVerlauf.Add(new StatusVerlaufEntity
            {
                GrundsteuerMeldungId = entity.Id,
                Status = entity.Status,
                Ausloeser = "Entwurf angelegt"
            });
        }
        else if (vorherStatus != entity.Status)
        {
            _db.StatusVerlauf.Add(new StatusVerlaufEntity
            {
                GrundsteuerMeldungId = entity.Id,
                Status = entity.Status,
                Ausloeser = "Status geändert beim Speichern",
                Bemerkung = $"vorher: {vorherStatus.AnzeigeName()}"
            });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Meldung {Id} gespeichert (Status {Status})", entity.Id, entity.Status);

        var antwort = new ApiResponse
        {
            Erfolg = true,
            Id = entity.Id,
            RowVersion = entity.RowVersion,
            Meldung = istNeu ? "Entwurf angelegt." : "Entwurf gespeichert.",
            Berechnung = MeldungMapper.ZuDto(entity.Berechnung),
            Hinweise = entity.Hinweise
                .Select(h => new ValidierungsHinweisDto
                {
                    Schwere = h.Schwere,
                    Feld = h.Feld,
                    Meldung = h.Meldung,
                    Rechtsgrundlage = h.Rechtsgrundlage,
                    Vorschlag = h.Vorschlag
                })
                .ToList()
        };

        // Tracking loslassen (siehe SetzeStatusAsync): der Kontext lebt so lange wie der
        // Blazor-Circuit, veraltete Originalwerte würden spätere Schreibvorgänge blockieren.
        _db.ChangeTracker.Clear();
        return antwort;
    }

    public async Task<bool> LoeschenAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Meldungen.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (entity is null) return false;

        // Fachregel: nur unbestätigte Entwürfe dürfen verschwinden. Ein übermittelter Vorgang
        // muss storniert werden, damit die Spur beim Finanzamt konsistent bleibt.
        if (entity.Status is not (MeldungStatus.Entwurf or MeldungStatus.Validierungsfehler))
        {
            _logger.LogWarning("Löschen von {Id} abgelehnt - Status {Status}", id, entity.Status);
            return false;
        }

        _db.Meldungen.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task SetzeStatusAsync(Guid id, MeldungStatus status, string ausloeser,
        string? bemerkung = null, CancellationToken ct = default)
    {
        var entity = await _db.Meldungen.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (entity is null) return;

        var vorher = entity.Status;
        entity.Status = status;
        entity.ZuletztGeaendertAm = DateTime.UtcNow;
        entity.RowVersion = Guid.NewGuid().ToByteArray();

        // WICHTIG: neue Kind-Entitäten explizit als "Added" anmelden.
        // Ihre Id ist über den Feldinitialisierer bereits gesetzt; EF schließt daraus sonst auf
        // "existiert schon" und erzeugt ein UPDATE statt INSERT - das trifft 0 Zeilen und endet in
        // einer DbUpdateConcurrencyException, obwohl niemand sonst geschrieben hat.
        _db.StatusVerlauf.Add(new StatusVerlaufEntity
        {
            GrundsteuerMeldungId = id,
            Status = status,
            Ausloeser = ausloeser,
            Bemerkung = bemerkung ?? (vorher == status ? null : $"vorher: {vorher.AnzeigeName()}")
        });

        await _db.SaveChangesAsync(ct);

        // Getrackte Entitäten loslassen. Ohne das hält der (scoped) Kontext nach dem Speichern
        // veraltete Originalwerte für RowVersion, und der nächste Schreibvorgang scheitert mit
        // DbUpdateConcurrencyException, obwohl niemand sonst geschrieben hat.
        _db.ChangeTracker.Clear();
    }

    public async Task<int> ProtokolliereUebermittlungAsync(UebermittlungsProtokoll p, CancellationToken ct = default)
    {
        var entity = await _db.Meldungen
            .FirstOrDefaultAsync(m => m.Id == p.MeldungId, ct);

        if (entity is null) return 0;

        // Auch hier: explizit als "Added" anmelden, sonst UPDATE-statt-INSERT (siehe SetzeStatusAsync).
        _db.Uebermittlungen.Add(new ElsterUebermittlungEntity
        {
            GrundsteuerMeldungId = p.MeldungId,
            Erfolgreich = p.Erfolgreich,
            Referenz = p.Referenz,
            FehlerCode = p.FehlerCode,
            Fehlertext = p.Fehlertext,
            Berechnung = MeldungMapper.ZuEntity(p.Berechnung),
            AnzahlFehler = p.AnzahlFehler,
            AnzahlWarnungen = p.AnzahlWarnungen
        });

        if (p.Erfolgreich)
        {
            entity.UebermitteltAm = DateTime.UtcNow;
            entity.UebermittlungsReferenz = p.Referenz;
            entity.Status = MeldungStatus.Uebermittelt;
            entity.FestgestellterMessbetrag ??= p.Berechnung?.Steuermessbetrag;
        }

        entity.ZuletztGeaendertAm = DateTime.UtcNow;
        entity.RowVersion = Guid.NewGuid().ToByteArray();

        await _db.SaveChangesAsync(ct);

        // Den Messbetrag aus dem Übermittlungsergebnis auch am Aggregat nachtragen - sonst stünde
        // er nur im Protokolldatensatz und die Übersicht zeigte nach der Übermittlung nichts an.
        var nachtrag = await _db.Meldungen.FirstOrDefaultAsync(m => m.Id == p.MeldungId, ct);
        if (nachtrag is not null && p.Berechnung is not null)
        {
            nachtrag.Berechnung ??= MeldungMapper.ZuEntity(p.Berechnung);
            nachtrag.FestgestellterMessbetrag ??= p.Berechnung.Steuermessbetrag;
            await _db.SaveChangesAsync(ct);
        }

        var anzahl = await _db.Uebermittlungen.CountAsync(u => u.GrundsteuerMeldungId == p.MeldungId, ct);
        _db.ChangeTracker.Clear();
        return anzahl;
    }

    public async Task<List<VerlaufsEintrag>> GetVerlaufAsync(Guid id, CancellationToken ct = default) =>
        await _db.StatusVerlauf
            .AsNoTracking()
            .Where(v => v.GrundsteuerMeldungId == id)
            .OrderBy(v => v.ZeitpunktUtc)
            .Select(v => new VerlaufsEintrag
            {
                Status = v.Status,
                ZeitpunktUtc = v.ZeitpunktUtc,
                Ausloeser = v.Ausloeser,
                Bemerkung = v.Bemerkung
            })
            .ToListAsync(ct);

    // -----------------------------------------------------------------------------------------
    //  Stammdaten
    // -----------------------------------------------------------------------------------------
    public async Task<List<FinanzamtDto>> GetFinanzaemterAsync(Bundesland land, CancellationToken ct = default) =>
        await _db.Finanzaemter
            .AsNoTracking()
            .Where(f => f.Bundesland == land)
            .OrderBy(f => f.Name)
            .Select(f => new FinanzamtDto
            {
                Bundesfinanzamtsnummer = f.Bundesfinanzamtsnummer,
                Name = f.Name,
                Bundesland = f.Bundesland,
                Ort = f.Ort
            })
            .ToListAsync(ct);

    public async Task AktualisiereFinanzaemterAsync(IEnumerable<FinanzamtDto> finanzaemter,
        CancellationToken ct = default)
    {
        foreach (var dto in finanzaemter)
        {
            if (string.IsNullOrWhiteSpace(dto.Bundesfinanzamtsnummer)) continue;

            var vorhanden = await _db.Finanzaemter
                .FirstOrDefaultAsync(f => f.Bundesfinanzamtsnummer == dto.Bundesfinanzamtsnummer, ct);

            if (vorhanden is null)
            {
                _db.Finanzaemter.Add(new FinanzamtEntity
                {
                    Bundesfinanzamtsnummer = dto.Bundesfinanzamtsnummer,
                    Name = dto.Name,
                    Bundesland = dto.Bundesland,
                    Ort = dto.Ort
                });
            }
            else
            {
                vorhanden.Name = dto.Name;
                vorhanden.Bundesland = dto.Bundesland;
                vorhanden.Ort = dto.Ort;
                vorhanden.ZuletztAktualisiertAmUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<PlzZuordnungDto?> GetPlzAsync(string postleitzahl, CancellationToken ct = default)
    {
        var plz = postleitzahl?.Trim() ?? string.Empty;
        if (plz.Length == 0) return null;

        return await _db.PlzZuordnungen
            .AsNoTracking()
            .Where(p => p.Postleitzahl == plz)
            .Select(p => new PlzZuordnungDto
            {
                Postleitzahl = p.Postleitzahl,
                Ort = p.Ort,
                Bundesland = p.Bundesland
            })
            .FirstOrDefaultAsync(ct);
    }

    // -----------------------------------------------------------------------------------------
    //  Initialisierung
    // -----------------------------------------------------------------------------------------
    public async Task InitialisierenAsync(CancellationToken ct = default)
    {
        // EnsureCreated statt Migrate: für eine Einzelplatz-SQLite-Datei ist das ausreichend und
        // erspart die Migrationskette. Sobald sich das Schema ändert, ist auf Migrate umzustellen;
        // siehe Persistence/README.md.
        var angelegt = await _db.Database.EnsureCreatedAsync(ct);
        if (angelegt)
        {
            _logger.LogInformation("SQLite-Datenbank neu angelegt und Grunddaten eingespielt.");
        }

        if (await _db.PlzZuordnungen.AnyAsync(ct)) return;

        _logger.LogInformation("PLZ-Grunddaten werden eingespielt.");
        _db.PlzZuordnungen.AddRange(PlzGrunddaten());
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Minimale PLZ-Stichprobe als Startbefüllung. Bewusst klein: die echten Daten kommen aus der
    /// WebAPI bzw. einer amtlichen Quelle und werden über <c>AktualisiereFinanzaemterAsync</c>
    /// nachgezogen. Hier stehen nur Orte aus dem Umfeld des Entwicklungsrechners, damit die
    /// Autovervollständigung im Wizard überhaupt etwas anzeigt.
    /// </summary>
    private static IEnumerable<PlzZuordnungEntity> PlzGrunddaten() => new[]
    {
        new PlzZuordnungEntity { Postleitzahl = "61209", Ort = "Echzell", Bundesland = Bundesland.Hessen },
        new PlzZuordnungEntity { Postleitzahl = "61200", Ort = "Wölfersheim", Bundesland = Bundesland.Hessen },
        new PlzZuordnungEntity { Postleitzahl = "61169", Ort = "Friedberg (Hessen)", Bundesland = Bundesland.Hessen },
        new PlzZuordnungEntity { Postleitzahl = "60311", Ort = "Frankfurt am Main", Bundesland = Bundesland.Hessen },
        new PlzZuordnungEntity { Postleitzahl = "80331", Ort = "München", Bundesland = Bundesland.Bayern },
        new PlzZuordnungEntity { Postleitzahl = "70173", Ort = "Stuttgart", Bundesland = Bundesland.BadenWuerttemberg },
        new PlzZuordnungEntity { Postleitzahl = "20095", Ort = "Hamburg", Bundesland = Bundesland.Hamburg },
        new PlzZuordnungEntity { Postleitzahl = "10115", Ort = "Berlin", Bundesland = Bundesland.Berlin }
    };
}
