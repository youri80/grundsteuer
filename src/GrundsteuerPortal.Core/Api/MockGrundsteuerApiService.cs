using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Core.Validation;

namespace GrundsteuerPortal.Core.Api;

/// <summary>
/// Vollständiger In-Memory-Mock des API-Service. Damit ist das Frontend ohne laufende WebAPI
/// bedienbar (Konfiguration "Api:UseMock": true). Der Mock verhält sich bewusst wie die echte API:
/// er validiert, vergibt Ids und Referenzen, setzt Status und liefert Fehlerantworten.
/// </summary>
public sealed class MockGrundsteuerApiService : IGrundsteuerApiService
{
    private readonly Dictionary<Guid, GrundsteuerMeldungDto> _speicher = new();
    private readonly object _sperre = new();
    private int _laufendeNummer = 1000;

    public MockGrundsteuerApiService()
    {
        Seed();
    }

    public Task<List<GrundsteuerUebersichtDto>> GetMeldungenAsync(CancellationToken ct = default)
    {
        lock (_sperre)
        {
            var liste = _speicher.Values
                .OrderByDescending(m => m.ZuletztGeaendertAm ?? m.ErstelltAm ?? DateTime.MinValue)
                .Select(ZuUebersicht)
                .ToList();
            return Task.FromResult(liste);
        }
    }

    public Task<GrundsteuerMeldungDto?> GetMeldungByIdAsync(Guid id, CancellationToken ct = default)
    {
        lock (_sperre)
        {
            return Task.FromResult(_speicher.TryGetValue(id, out var m) ? Klone(m) : null);
        }
    }

    public Task<ApiResponse> SaveDraftAsync(GrundsteuerMeldungDto dto, CancellationToken ct = default)
    {
        var hinweise = MeldungsValidator.PruefeAlles(dto);
        var fehler = hinweise.Where(h => h.Schwere == HinweisSchwere.Fehler).ToList();

        lock (_sperre)
        {
            if (dto.Id == Guid.Empty) dto.Id = Guid.NewGuid();
            if (!_speicher.ContainsKey(dto.Id)) dto.ErstelltAm = DateTime.Now;

            dto.ZuletztGeaendertAm = DateTime.Now;
            dto.Hinweise = hinweise.ToList();
            dto.Berechnung = MessbetragRechner.Berechne(dto);

            // Ein Entwurf darf unfertig sein - gespeichert wird er trotzdem, nur der Status
            // macht sichtbar, dass noch etwas fehlt. Genau das erwartet ein Nutzer vom "Entwurf speichern".
            dto.Status = fehler.Count > 0 ? MeldungStatus.Validierungsfehler : MeldungStatus.Entwurf;
            dto.RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            _speicher[dto.Id] = Klone(dto);

            var antwort = fehler.Count > 0
                ? ApiResponse.Fehler($"{fehler.Count} Angabe(n) sind noch nicht übermittlungsfähig.", "VALIDIERUNG", hinweise.ToList())
                : ApiResponse.Ok("Entwurf gespeichert.", dto.Id);
            antwort.RowVersion = dto.RowVersion;
            antwort.Berechnung = dto.Berechnung;
            return Task.FromResult(antwort);
        }
    }

    public Task<ApiResponse> SubmitToElsterAsync(Guid id, CancellationToken ct = default)
    {
        lock (_sperre)
        {
            if (!_speicher.TryGetValue(id, out var m))
                return Task.FromResult(ApiResponse.Fehler("Die Meldung existiert nicht.", "NOT_FOUND"));

            var fehler = MeldungsValidator.PruefeAlles(m).Where(h => h.Schwere == HinweisSchwere.Fehler).ToList();
            if (fehler.Count > 0)
            {
                return Task.FromResult(ApiResponse.Fehler(
                    "Die Meldung ist noch nicht übermittlungsfähig - bitte die Fehler beheben.", "VALIDIERUNG", fehler));
            }

            _laufendeNummer++;
            m.Status = MeldungStatus.Uebermittelt;
            m.UebermitteltAm = DateTime.Now;
            m.UebermittlungsReferenz = $"ELSTER-{DateTime.Now:yyyy}-{_laufendeNummer}";
            m.Berechnung = MessbetragRechner.Berechne(m);
            m.ZuletztGeaendertAm = DateTime.Now;
            _speicher[id] = Klone(m);

            return Task.FromResult(ApiResponse.Ok(
                $"Übermittelt. Referenz: {m.UebermittlungsReferenz}", id));
        }
    }

    public Task<StatusPruefungDto?> PruefeStatusAsync(Guid id, CancellationToken ct = default)
    {
        lock (_sperre)
        {
            if (!_speicher.TryGetValue(id, out var m)) return Task.FromResult<StatusPruefungDto?>(null);

            // Der Mock simuliert: eine übermittelte Meldung ist nach kurzer Zeit "in Prüfung",
            // ältere Meldungen sind bereits festgestellt (Messbescheid vorhanden).
            if (m.Status == MeldungStatus.Uebermittelt)
            {
                var alter = DateTime.Now - (m.UebermitteltAm ?? DateTime.Now);
                if (alter > TimeSpan.FromSeconds(30))
                {
                    m.Status = MeldungStatus.InPruefung;
                    m.ZuletztGeaendertAm = DateTime.Now;
                    _speicher[id] = Klone(m);
                }
            }

            return Task.FromResult<StatusPruefungDto?>(new StatusPruefungDto
            {
                Id = id,
                Status = m.Status,
                FestgestellterMessbetrag = m.Berechnung?.Steuermessbetrag,
                MessbescheidAm = m.Status == MeldungStatus.Festgestellt ? DateTime.Now.Date.AddDays(-7) : null,
                Aktenzeichen = m.Aktenzeichen,
                Nachricht = m.Status switch
                {
                    MeldungStatus.Entwurf => "Noch nicht übermittelt.",
                    MeldungStatus.Validierungsfehler => "Es sind noch Fehler zu beheben.",
                    MeldungStatus.Uebermittelt => "Eingang beim Finanzamt bestätigt, wird dort bearbeitet.",
                    MeldungStatus.InPruefung => "Die Erklärung liegt beim Finanzamt zur Prüfung.",
                    MeldungStatus.Festgestellt => "Der Messbescheid liegt vor.",
                    MeldungStatus.Fehlgeschlagen => "Die Übermittlung ist fehlgeschlagen - bitte erneut senden.",
                    _ => "Kein Status verfügbar."
                }
            });
        }
    }

    public Task<ApiResponse> StorniereAsync(Guid id, CancellationToken ct = default)
    {
        lock (_sperre)
        {
            if (!_speicher.TryGetValue(id, out var m))
                return Task.FromResult(ApiResponse.Fehler("Die Meldung existiert nicht.", "NOT_FOUND"));

            if (m.Status is MeldungStatus.InPruefung or MeldungStatus.Festgestellt)
            {
                return Task.FromResult(ApiResponse.Fehler(
                    "Eine bereits beim Finanzamt bearbeitete Erklärung kann nicht storniert werden - "
                    + "hier hilft nur eine berichtigte Erklärung.", "NICHT_MOEGLICH"));
            }

            m.Status = MeldungStatus.Storniert;
            m.ZuletztGeaendertAm = DateTime.Now;
            _speicher[id] = Klone(m);
            return Task.FromResult(ApiResponse.Ok("Die Meldung wurde storniert.", id));
        }
    }

    public Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        lock (_sperre)
        {
            if (!_speicher.TryGetValue(id, out var m))
                return Task.FromResult(ApiResponse.Fehler("Die Meldung existiert nicht.", "NOT_FOUND"));

            if (m.Status != MeldungStatus.Entwurf)
            {
                return Task.FromResult(ApiResponse.Fehler(
                    "Nur Entwürfe können gelöscht werden. Übermittelte Erklärungen bleiben revisionssicher erhalten.",
                    "NICHT_MOEGLICH"));
            }

            _speicher.Remove(id);
            return Task.FromResult(ApiResponse.Ok("Entwurf gelöscht.", id));
        }
    }

    public Task<byte[]?> ExportPdfAsync(Guid id, CancellationToken ct = default)
    {
        lock (_sperre)
        {
            if (!_speicher.TryGetValue(id, out var m)) return Task.FromResult<byte[]?>(null);

            // Der Mock erzeugt ein minimales, gültiges PDF mit den Kerndaten.
            // Der echte Export kommt als PDF von der WebAPI (Vorlage GW-1).
            var inhalt = PdfExport.ErzeugeUebersichtPdf(m);
            return Task.FromResult<byte[]?>(inhalt);
        }
    }

    public Task<List<FinanzamtDto>> GetFinanzaemterAsync(Bundesland land, CancellationToken ct = default)
    {
        var bereiche = Bundesfinanzamtsnummern.Bereiche(land);
        var liste = new List<FinanzamtDto>();
        foreach (var (von, bis) in bereiche)
        {
            for (var n = von; n <= bis && liste.Count < 200; n++)
            {
                liste.Add(new FinanzamtDto
                {
                    Bundesfinanzamtsnummer = n.ToString("D4"),
                    Name = $"{land.AnzeigeName()} – Finanzamt {n:D4}",
                    Bundesland = land
                });
            }
        }

        if (land == Bundesland.Hessen)
        {
            liste.Insert(0, new FinanzamtDto
            {
                Bundesfinanzamtsnummer = "2660",
                Name = "HE_Testfinanzamt -2660- (Testdaten)",
                Bundesland = land,
                Ort = "Testbetrieb"
            });
        }

        return Task.FromResult(liste);
    }

    public Task<GrundsteuerBerechnungDto?> BerechneVorschauAsync(GrundsteuerMeldungDto dto, CancellationToken ct = default) =>
        Task.FromResult<GrundsteuerBerechnungDto?>(MessbetragRechner.Berechne(dto));

    // -----------------------------------------------------------------------------------------
    private static GrundsteuerUebersichtDto ZuUebersicht(GrundsteuerMeldungDto m) => new()
    {
        Id = m.Id,
        Aktenzeichen = m.Aktenzeichen,
        Steuernummer = m.Steuernummer ?? string.Empty,
        Bundesland = m.Bundesland,
        Modell = m.BundeslandInfo.Modell,
        Status = m.Status,
        Grundstuecksbezeichnung = string.Join(", ", new[]
        {
            m.Gemarkung,
            string.IsNullOrWhiteSpace(m.Flur) ? null : $"Flur {m.Flur}",
            m.FlurstueckZaehler is null ? null : $"Flurstück {m.FlurstueckZaehler}"
                + (string.IsNullOrWhiteSpace(m.FlurstueckNenner) ? string.Empty : $"/{m.FlurstueckNenner}")
        }.Where(t => !string.IsNullOrWhiteSpace(t))),
        Strasse = m.Lage.Strasse,
        Hausnummer = m.Lage.Hausnummer,
        Postleitzahl = m.Lage.Postleitzahl,
        Ort = m.Lage.Ort,
        HauptEigentuemer = m.Eigentuemer.FirstOrDefault()?.AnzeigeName ?? "—",
        FestgestellterMessbetrag = m.Status == MeldungStatus.Festgestellt ? m.Berechnung?.Steuermessbetrag : null,
        ErstelltAm = m.ErstelltAm ?? DateTime.Now,
        ZuletztGeaendertAm = m.ZuletztGeaendertAm ?? DateTime.Now,
        UebermitteltAm = m.UebermitteltAm,
        UebermittlungsReferenz = m.UebermittlungsReferenz,
        AnzahlHinweise = m.Hinweise.Count(h => h.Schwere != HinweisSchwere.Hinweis)
    };

    /// <summary>Deep-Copy, damit der Mock nicht dieselbe Instanz an Aufrufer und Speicher gibt.</summary>
    private static GrundsteuerMeldungDto Klone(GrundsteuerMeldungDto m) => new()
    {
        Id = m.Id,
        RowVersion = m.RowVersion,
        Status = m.Status,
        Bundesland = m.Bundesland,
        Erklaerungsart = m.Erklaerungsart,
        Hauptfeststellungszeitpunkt = m.Hauptfeststellungszeitpunkt,
        Bundesfinanzamtsnummer = m.Bundesfinanzamtsnummer,
        FinanzamtName = m.FinanzamtName,
        Aktenzeichen = m.Aktenzeichen,
        AktenzeichenElster = m.AktenzeichenElster,
        Steuernummer = m.Steuernummer,
        Gemarkung = m.Gemarkung,
        Gemarkungsnummer = m.Gemarkungsnummer,
        Flur = m.Flur,
        FlurstueckZaehler = m.FlurstueckZaehler,
        FlurstueckNenner = m.FlurstueckNenner,
        Grundbuchblatt = m.Grundbuchblatt,
        Grundstuecksart = m.Grundstuecksart,
        Grundstuecksflaeche = m.Grundstuecksflaeche,
        Wohnflaeche = m.Wohnflaeche,
        Nutzflaeche = m.Nutzflaeche,
        Baujahr = m.Baujahr,
        Bodenrichtwert = m.Bodenrichtwert,
        DurchschnittlicherBodenrichtwert = m.DurchschnittlicherBodenrichtwert,
        Wohnlage = m.Wohnlage,
        IstDenkmalgeschuetzt = m.IstDenkmalgeschuetzt,
        IstSozialerWohnungsbau = m.IstSozialerWohnungsbau,
        Flurstuecke = m.Flurstuecke.Select(f => new FlurstueckDto
        {
            Id = f.Id,
            Gemarkung = f.Gemarkung,
            Gemarkungsnummer = f.Gemarkungsnummer,
            Flur = f.Flur,
            Zaehler = f.Zaehler,
            Nenner = f.Nenner,
            Flaeche = f.Flaeche,
            Anteil = f.Anteil
        }).ToList(),
        Lage = new AdresseDto
        {
            Strasse = m.Lage.Strasse,
            Hausnummer = m.Lage.Hausnummer,
            HausnummerZusatz = m.Lage.HausnummerZusatz,
            Postleitzahl = m.Lage.Postleitzahl,
            Ort = m.Lage.Ort,
            Ortsteil = m.Lage.Ortsteil,
            Land = m.Lage.Land
        },
        Eigentuemer = m.Eigentuemer.Select(e => new EigentuemerDto
        {
            Id = e.Id,
            Art = e.Art,
            Anrede = e.Anrede,
            Name = e.Name,
            Vorname = e.Vorname,
            IdNummer = e.IdNummer,
            Steuernummer = e.Steuernummer,
            Strasse = e.Strasse,
            Hausnummer = e.Hausnummer,
            Postleitzahl = e.Postleitzahl,
            Ort = e.Ort,
            Land = e.Land,
            Geburtsdatum = e.Geburtsdatum,
            Anteil = e.Anteil,
            IstBevollmaechtigt = e.IstBevollmaechtigt
        }).ToList(),
        BevollmaechtigterName = m.BevollmaechtigterName,
        BevollmaechtigterIdNr = m.BevollmaechtigterIdNr,
        Berechnung = m.Berechnung,
        Hinweise = m.Hinweise.ToList(),
        UebermittlungsReferenz = m.UebermittlungsReferenz,
        UebermitteltAm = m.UebermitteltAm,
        ZuletztGeaendertAm = m.ZuletztGeaendertAm,
        ErstelltAm = m.ErstelltAm
    };

    // -----------------------------------------------------------------------------------------
    private void Seed()
    {
        var jetzt = DateTime.Now;

        // 1) Hessen, übermittelt - Flächen-Faktor-Verfahren
        var hessen = new GrundsteuerMeldungDto
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Bundesland = Bundesland.Hessen,
            Erklaerungsart = Erklaerungsart.Erstmalig,
            Hauptfeststellungszeitpunkt = 2022,
            Bundesfinanzamtsnummer = "2660",
            FinanzamtName = "HE_Testfinanzamt -2660-",
            Aktenzeichen = "2660400100010010051",
            Gemarkung = "Echzell",
            Gemarkungsnummer = "000123",
            Flur = "12",
            FlurstueckZaehler = "145",
            FlurstueckNenner = "3",
            Grundbuchblatt = "GB 412",
            Grundstuecksart = Grundstuecksart.Einfamilienhaus,
            Grundstuecksflaeche = 620m,
            Wohnflaeche = 148m,
            Baujahr = 1978,
            Bodenrichtwert = 165m,
            DurchschnittlicherBodenrichtwert = 150m,
            Lage = new AdresseDto
            {
                Strasse = "Wiesenweg", Hausnummer = "12", Postleitzahl = "61209", Ort = "Echzell"
            },
            Eigentuemer =
            {
                new EigentuemerDto
                {
                    Anrede = Anrede.Frau, Vorname = "Lyra", Name = "Beispiel",
                    IdNummer = "02476291358", Strasse = "Wiesenweg", Hausnummer = "12",
                    Postleitzahl = "61209", Ort = "Echzell", Anteil = 1m
                }
            },
            ErstelltAm = jetzt.AddDays(-31),
            ZuletztGeaendertAm = jetzt.AddDays(-30),
            UebermitteltAm = jetzt.AddDays(-30),
            Status = MeldungStatus.Festgestellt,
            UebermittlungsReferenz = "ELSTER-2026-1001"
        };
        hessen.Berechnung = MessbetragRechner.Berechne(hessen);
        hessen.Hinweise = MeldungsValidator.PruefeAlles(hessen).ToList();
        _speicher[hessen.Id] = hessen;

        // 2) Bayern, Entwurf - reines Flächenmodell
        var bayern = new GrundsteuerMeldungDto
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Bundesland = Bundesland.Bayern,
            Bundesfinanzamtsnummer = "9198",
            FinanzamtName = "Testfinanzamt OF-Bereich München",
            Aktenzeichen = "9198469040000000012",
            Gemarkung = "München",
            Flur = "5",
            FlurstueckZaehler = "400",
            Grundstuecksart = Grundstuecksart.Einfamilienhaus,
            Grundstuecksflaeche = 480m,
            Wohnflaeche = 132m,
            Baujahr = 1995,
            Lage = new AdresseDto
            {
                Strasse = "Am Hang", Hausnummer = "7", Postleitzahl = "80331", Ort = "München"
            },
            Eigentuemer =
            {
                new EigentuemerDto
                {
                    Art = EigentuemerArt.NatuerlichePerson, Anrede = Anrede.Herr,
                    Vorname = "Max", Name = "Mustermann", IdNummer = "86095742719",
                    Strasse = "Am Hang", Hausnummer = "7", Postleitzahl = "80331", Ort = "München", Anteil = 1m
                }
            },
            ErstelltAm = jetzt.AddDays(-4),
            ZuletztGeaendertAm = jetzt.AddDays(-2),
            Status = MeldungStatus.Entwurf
        };
        bayern.Berechnung = MessbetragRechner.Berechne(bayern);
        bayern.Hinweise = MeldungsValidator.PruefeAlles(bayern).ToList();
        _speicher[bayern.Id] = bayern;

        // 3) Baden-Württemberg, übermittelt - modifiziertes Bodenwertmodell
        var bw = new GrundsteuerMeldungDto
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Bundesland = Bundesland.BadenWuerttemberg,
            Bundesfinanzamtsnummer = "2831",
            FinanzamtName = "Finanzamt Ettlingen",
            Aktenzeichen = "2831400500690160017",
            Gemarkung = "Ettlingen",
            Flur = "1",
            FlurstueckZaehler = "69",
            FlurstueckNenner = "16",
            Grundstuecksart = Grundstuecksart.Wohnungseigentum,
            Grundstuecksflaeche = 310m,
            Wohnflaeche = 94m,
            Baujahr = 1910,
            Bodenrichtwert = 420m,
            Lage = new AdresseDto
            {
                Strasse = "Marktplatz", Hausnummer = "3", Postleitzahl = "76275", Ort = "Ettlingen"
            },
            Eigentuemer =
            {
                new EigentuemerDto
                {
                    Anrede = Anrede.Frau, Vorname = "Anna", Name = "Keller",
                    IdNummer = "47036892816", Strasse = "Marktplatz", Hausnummer = "3",
                    Postleitzahl = "76275", Ort = "Ettlingen", Anteil = 1m
                }
            },
            ErstelltAm = jetzt.AddDays(-60),
            ZuletztGeaendertAm = jetzt.AddDays(-59),
            UebermitteltAm = jetzt.AddDays(-59),
            Status = MeldungStatus.InPruefung,
            UebermittlungsReferenz = "ELSTER-2026-1002"
        };
        bw.Berechnung = MessbetragRechner.Berechne(bw);
        bw.Hinweise = MeldungsValidator.PruefeAlles(bw).ToList();
        _speicher[bw.Id] = bw;

        // 4) Hamburg, Fehlerfall - Wohnlagenmodell ohne Wohnlage (zeigt die Validierungsoberfläche)
        var hh = new GrundsteuerMeldungDto
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Bundesland = Bundesland.Hamburg,
            Bundesfinanzamtsnummer = "2216",
            FinanzamtName = "Finanzamt Hamburg für Verkehrsteuern und Grundbesitz",
            Steuernummer = "2216005432634",
            Gemarkung = "Hamburg",
            FlurstueckZaehler = "1234",
            Grundstuecksart = Grundstuecksart.Mietwohngrundstueck,
            Grundstuecksflaeche = 540m,
            Wohnflaeche = 380m,
            Nutzflaeche = 60m,
            Baujahr = 1961,
            Lage = new AdresseDto
            {
                Strasse = "Elbchaussee", Hausnummer = "150", Postleitzahl = "22605", Ort = "Hamburg"
            },
            Eigentuemer =
            {
                new EigentuemerDto
                {
                    Art = EigentuemerArt.JuristischePerson, Anrede = Anrede.Firma,
                    Name = "Hanseatic Wohnbau GmbH", Steuernummer = "22/123/45678",
                    Strasse = "Ballindamm", Hausnummer = "1", Postleitzahl = "20095", Ort = "Hamburg",
                    Anteil = 1m
                }
            },
            ErstelltAm = jetzt.AddDays(-8),
            ZuletztGeaendertAm = jetzt.AddDays(-1),
            Status = MeldungStatus.Validierungsfehler
        };
        hh.Berechnung = MessbetragRechner.Berechne(hh);
        hh.Hinweise = MeldungsValidator.PruefeAlles(hh).ToList();
        _speicher[hh.Id] = hh;

        // 5) Niedersachsen, halbfertiger Entwurf - Flächen-Lage-Modell
        var ni = new GrundsteuerMeldungDto
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Bundesland = Bundesland.Niedersachsen,
            Bundesfinanzamtsnummer = "2379",
            Aktenzeichen = "2379468000600010009",
            Grundstuecksart = Grundstuecksart.Zweifamilienhaus,
            Grundstuecksflaeche = 700m,
            Wohnflaeche = 180m,
            Bodenrichtwert = 240m,
            Lage = new AdresseDto { Strasse = "Lindenallee", Hausnummer = "22", Ort = "Bad Eilsen" },
            ErstelltAm = jetzt.AddDays(-2),
            ZuletztGeaendertAm = jetzt.AddHours(-6),
            Status = MeldungStatus.Validierungsfehler
        };
        ni.Berechnung = MessbetragRechner.Berechne(ni);
        ni.Hinweise = MeldungsValidator.PruefeAlles(ni).ToList();
        _speicher[ni.Id] = ni;
    }
}

/// <summary>Erzeugt ein schlichtes, aber gültiges PDF für den Export im Mock-Betrieb.</summary>
internal static class PdfExport
{
    public static byte[] ErzeugeUebersichtPdf(GrundsteuerMeldungDto m)
    {
        var zeilen = new List<string>
        {
            "Grundsteuererklärung - Zusammenfassung",
            $"Aktenzeichen / Steuernummer: {m.Aktenzeichen ?? m.Steuernummer ?? "-"}",
            $"Bundesland: {m.Bundesland.AnzeigeName()} ({m.BundeslandInfo.Modell.AnzeigeName()})",
            $"Grundstück: {m.Lage.Einzeilig}",
            $"Gemarkung/Flur/Flurstück: {m.Gemarkung} {m.Flur} {m.FlurstueckZaehler}/{m.FlurstueckNenner}",
            $"Grundstücksfläche: {m.Grundstuecksflaeche:0.##} m²",
            $"Wohnfläche: {m.Wohnflaeche:0.##} m²",
            $"Eigentümer: {string.Join("; ", m.Eigentuemer.Select(e => $"{e.AnzeigeName} ({e.Anteil * 100m:0.##} %)"))}",
            $"Steuermessbetrag (Vorschau): {MessbetragRechner.Euro(m.Berechnung?.Steuermessbetrag)}",
            $"Stand: {DateTime.Now:dd.MM.yyyy HH:mm}"
        };

        var inhalt = new System.Text.StringBuilder();
        inhalt.Append("BT /F1 11 Tf 50 780 Td 16 TL\n");
        foreach (var zeile in zeilen)
            inhalt.Append('(').Append(Escape(zeile)).Append(") Tj T*\n");
        inhalt.Append("ET");
        var stream = System.Text.Encoding.ASCII.GetBytes(inhalt.ToString());

        var objekt = new System.Text.StringBuilder();
        objekt.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        void Add(string text)
        {
            offsets.Add(System.Text.Encoding.ASCII.GetByteCount(objekt.ToString()));
            objekt.Append(text);
        }

        Add("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Add("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        Add("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] "
            + "/Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>\nendobj\n");
        Add($"4 0 obj\n<< /Length {stream.Length} >>\nstream\n");
        var streamStart = System.Text.Encoding.ASCII.GetByteCount(objekt.ToString());
        objekt.Append(inhalt).Append("\nendstream\nendobj\n");
        Add("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        var xref = System.Text.Encoding.ASCII.GetByteCount(objekt.ToString());
        objekt.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var o in offsets.Take(5))
            objekt.Append(o.ToString("D10")).Append(" 00000 n \n");
        objekt.Append($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");

        _ = streamStart;
        return System.Text.Encoding.ASCII.GetBytes(objekt.ToString());
    }

    private static string Escape(string text)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in text)
        {
            if (c > 127) { sb.Append('?'); continue; }
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
