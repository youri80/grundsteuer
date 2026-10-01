using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Core.Validation;
using GrundsteuerPortal.Persistence.Abstractions;
using GrundsteuerPortal.Persistence.Security;

namespace GrundsteuerPortal.Web.Services;

/// <summary>
/// Die produktive Fassade für die UI: sie verbindet die <b>lokale SQLite-Ablage</b> (Entwürfe,
/// Sitzungsdaten, Verlauf) mit der <b>vorhandenen ELSTER-WebAPI</b> (Übermittlung, Statusprüfung,
/// PDF, Vorschauberechnung).
///
/// Aufgabenteilung - bewusst so und nicht anders:
///  - <b>Lokal</b> gespeichert wird alles, was der Nutzer erfasst oder was den Bearbeitungsstand
///    beschreibt. Dadurch ist der Wizard jederzeit benutzbar, auch ohne Verbindung zur Finanz-
///    verwaltung, und ein Absturz kostet keine Eingabe.
///  - <b>Über die API</b> läuft ausschließlich, was zwingend die Finanzverwaltung braucht:
///    die ELSTER-Übermittlung, die Statusabfrage zum Messbescheid, der amtliche PDF-Export und
///    (falls vorhanden) die ERiC-Vorprüfung. Diese Aufrufe werden im lokalen Verlauf protokolliert.
///
/// Damit ist die UI identisch bedienbar, unabhängig davon, ob eine API erreichbar ist - nur die
/// ELSTER-Aktionen schlagen dann mit klarer Meldung fehl.
/// </summary>
public sealed class GrundsteuerService : IGrundsteuerApiService
{
    private readonly IGrundsteuerRepository _repo;
    private readonly IGrundsteuerApiService? _elster;
    private readonly ILogger<GrundsteuerService> _logger;

    public GrundsteuerService(
        IGrundsteuerRepository repo,
        ILogger<GrundsteuerService> logger,
        IGrundsteuerApiService? elster = null)
    {
        _repo = repo;
        _logger = logger;
        _elster = elster;
    }

    /// <summary>Ist die ELSTER-WebAPI konfiguriert? Steuert die Hinweise in der Oberfläche.</summary>
    public bool ElsterVerfuegbar => _elster is not null;

    // -----------------------------------------------------------------------------------------
    //  Lokale Operationen
    // -----------------------------------------------------------------------------------------
    public Task<List<GrundsteuerUebersichtDto>> GetMeldungenAsync(CancellationToken ct = default) =>
        _repo.GetUebersichtAsync(ct);

    public Task<GrundsteuerMeldungDto?> GetMeldungByIdAsync(Guid id, CancellationToken ct = default) =>
        _repo.GetAsync(id, ct);

    public async Task<ApiResponse> SaveDraftAsync(GrundsteuerMeldungDto dto, CancellationToken ct = default)
    {
        var antwort = await _repo.SpeichernAsync(dto, ct);

        if (!antwort.Erfolg)
        {
            _logger.LogWarning("Entwurf nicht gespeichert: {Grund}", antwort.FehlerCode);
            return antwort;
        }

        // Ergebnis der letzten Prüfung mitschreiben, damit das Dashboard die Hinweiszahl zeigen kann.
        return antwort;
    }

    public async Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var erfolg = await _repo.LoeschenAsync(id, ct);
        return erfolg
            ? ApiResponse.Ok("Der Entwurf wurde gelöscht.", id)
            : ApiResponse.Fehler(
                "Nur Entwürfe können gelöscht werden. Eine bereits übermittelte Meldung muss storniert werden.",
                "STATUS");
    }

    /// <summary>Statusverlauf aus der lokalen Ablage - für die Detailansicht.</summary>
    public Task<List<VerlaufsEintrag>> GetVerlaufAsync(Guid id, CancellationToken ct = default) =>
        _repo.GetVerlaufAsync(id, ct);

    // -----------------------------------------------------------------------------------------
    //  ELSTER-Operationen (über die vorhandene WebAPI)
    // -----------------------------------------------------------------------------------------
    public async Task<ApiResponse> SubmitToElsterAsync(Guid id, CancellationToken ct = default)
    {
        if (_elster is null)
            return ApiResponse.Fehler(
                "Die ELSTER-WebAPI ist nicht konfiguriert. Der Entwurf wurde lokal gespeichert.",
                "API_NICHT_KONFIGURIERT");

        var antwort = await _elster.SubmitToElsterAsync(id, ct);

        // Jeder Versuch wird protokolliert - auch der fehlgeschlagene. Erst das macht im
        // Steuerverfahren nachvollziehbar, wann was mit welchem Ergebnis gesendet wurde.
        await _repo.ProtokolliereUebermittlungAsync(new UebermittlungsProtokoll
        {
            MeldungId = id,
            Erfolgreich = antwort.Erfolg,
            Referenz = antwort.Erfolg ? antwort.Meldung : null,
            FehlerCode = antwort.FehlerCode,
            Fehlertext = antwort.Erfolg ? null : antwort.Meldung,
            Berechnung = antwort.Berechnung,
            AnzahlFehler = antwort.Hinweise.Count(h => h.Schwere == HinweisSchwere.Fehler),
            AnzahlWarnungen = antwort.Hinweise.Count(h => h.Schwere == HinweisSchwere.Warnung)
        }, ct);

        return antwort;
    }

    public async Task<StatusPruefungDto?> PruefeStatusAsync(Guid id, CancellationToken ct = default)
    {
        if (_elster is null) return null;

        var status = await _elster.PruefeStatusAsync(id, ct);
        if (status is null) return null;

        // Den vom Finanzamt gemeldeten Stand lokal nachziehen, damit das Dashboard aktuell bleibt.
        await _repo.SetzeStatusAsync(id, status.Status, "Status beim Finanzamt geprüft", status.Nachricht, ct);
        return status;
    }

    public async Task<ApiResponse> StorniereAsync(Guid id, CancellationToken ct = default)
    {
        if (_elster is not null)
        {
            var antwort = await _elster.StorniereAsync(id, ct);
            if (!antwort.Erfolg) return antwort;

            await _repo.SetzeStatusAsync(id, MeldungStatus.Storniert, "Bei ELSTER storniert", ct: ct);
            return antwort;
        }

        // Ohne API kann lokal storniert werden - der Versand ist dann nie erfolgt.
        await _repo.SetzeStatusAsync(id, MeldungStatus.Storniert, "Lokal storniert (keine API konfiguriert)", ct: ct);
        return ApiResponse.Ok("Die Meldung wurde lokal storniert.");
    }

    public async Task<byte[]?> ExportPdfAsync(Guid id, CancellationToken ct = default)
    {
        if (_elster is null)
        {
            _logger.LogInformation("PDF-Export für {Id} angefragt, aber keine ELSTER-WebAPI konfiguriert.", id);
            return null;
        }

        return await _elster.ExportPdfAsync(id, ct);
    }

    public async Task<GrundsteuerBerechnungDto?> BerechneVorschauAsync(GrundsteuerMeldungDto dto,
        CancellationToken ct = default)
    {
        if (_elster is null) return null;

        // Die amtliche Vorprüfung hat Vorrang vor der lokalen Näherung.
        var ergebnis = await _elster.BerechneVorschauAsync(dto, ct);
        return ergebnis ?? Core.Validation.MessbetragRechner.Berechne(dto);
    }

    // -----------------------------------------------------------------------------------------
    //  Stammdaten: Cache zuerst, API als Quelle
    // -----------------------------------------------------------------------------------------
    public async Task<List<FinanzamtDto>> GetFinanzaemterAsync(Bundesland land, CancellationToken ct = default)
    {
        var gecacht = await _repo.GetFinanzaemterAsync(land, ct);
        if (gecacht.Count > 0) return gecacht;

        if (_elster is not null)
        {
            // Beim ersten Zugriff auf ein Land den Cache aus der API füllen.
            var ausApi = await _elster.GetFinanzaemterAsync(land, ct);
            if (ausApi.Count > 0)
            {
                // Nur zulässige Nummern übernehmen: eine Nummer, die die Vorprüfung ablehnt, darf
                // gar nicht erst in die Auswahlliste - sonst wählt der Nutzer sie und scheitert
                // erst im nächsten Schritt an der Validierung.
                var zulaessig = ausApi
                    .Where(f => Bundesfinanzamtsnummern.IstZulaessig(f.Bundesfinanzamtsnummer, land))
                    .ToList();

                if (zulaessig.Count > 0)
                {
                    await _repo.AktualisiereFinanzaemterAsync(zulaessig, ct);
                    _logger.LogInformation("{Anzahl} Finanzämter für {Land} aus der WebAPI übernommen.",
                        zulaessig.Count, land.AnzeigeName());
                }
                return zulaessig;
            }
        }

        // Kein Cache und keine API (oder sie liefert nichts): die zulässigen Finanzämter aus dem
        // hinterlegten Bereichskatalog ableiten. Ohne das bliebe die Auswahlliste in Schritt 1
        // leer und der Wizard wäre nicht bedienbar, obwohl die Nummern bekannt sind.
        //
        // Das Ergebnis wird in den Cache geschrieben: sonst bliebe die Tabelle Finanzaemter dauerhaft
        // leer und die Liste würde bei jedem Aufruf neu aufgebaut. Der Aufruf ist ein Upsert und
        // damit beliebig oft wiederholbar.
        var ausKatalog = FinanzamtKatalog.Erzeuge(land);
        if (ausKatalog.Count > 0)
        {
            await _repo.AktualisiereFinanzaemterAsync(ausKatalog, ct);
            _logger.LogInformation("{Anzahl} Finanzämter für {Land} aus dem Bereichskatalog übernommen.",
                ausKatalog.Count, land.AnzeigeName());
        }

        return ausKatalog;
    }

    /// <summary>PLZ-Vorschlag aus dem lokalen Stammdaten-Cache.</summary>
    public Task<PlzZuordnungDto?> GetPlzVorschlagAsync(string postleitzahl, CancellationToken ct = default) =>
        _repo.GetPlzAsync(postleitzahl, ct);

    /// <summary>
    /// Setzt den anwendungsweiten IdNr-Salt. Muss beim Start aus der Konfiguration gesetzt werden,
    /// damit ein Wechsel des Salts nicht unbemerkt zu unvergleichbaren Hashes führt.
    /// </summary>
    public static void SetzeIdNrSalt(string? salt)
    {
        if (!string.IsNullOrWhiteSpace(salt)) IdNrHasher.Salt = salt;
    }
}
