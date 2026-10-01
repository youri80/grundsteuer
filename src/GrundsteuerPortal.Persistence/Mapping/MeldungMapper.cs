using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Persistence.Entities;
using GrundsteuerPortal.Persistence.Security;

namespace GrundsteuerPortal.Persistence.Mapping;

/// <summary>
/// Übersetzt zwischen den API-DTOs (die die UI kennt) und den EF-Entities (die die DB kennt).
/// Reine Funktionen, keine Seiteneffekte - dadurch einzeln testbar.
///
/// Ein Mapper ist notwendig, weil DTO und Entity bewusst auseinanderlaufen: das DTO führt die
/// IdNr im Klartext (das Formular braucht sie), die Entity nur Hash + letzte drei Stellen.
/// </summary>
public static class MeldungMapper
{
    // -----------------------------------------------------------------------------------------
    //  Entity -> DTO
    // -----------------------------------------------------------------------------------------
    public static GrundsteuerMeldungDto ZuDto(GrundsteuerMeldungEntity e)
    {
        var dto = new GrundsteuerMeldungDto
        {
            Id = e.Id,
            RowVersion = e.RowVersion,
            Status = e.Status,
            Bundesland = e.Bundesland,
            Erklaerungsart = e.Erklaerungsart,
            Hauptfeststellungszeitpunkt = e.Hauptfeststellungszeitpunkt,
            Bundesfinanzamtsnummer = e.Bundesfinanzamtsnummer,
            FinanzamtName = e.FinanzamtName,
            Aktenzeichen = e.Aktenzeichen,
            AktenzeichenElster = e.AktenzeichenElster,
            Steuernummer = e.Steuernummer,
            WirtschaftseinheitId = e.WirtschaftseinheitId,
            MeldendePersonId = e.MeldendePersonId,
            MeldendePersonName = e.MeldendePersonName,

            Gemarkung = e.Gemarkung,
            Gemarkungsnummer = e.Gemarkungsnummer,
            Flur = e.Flur,
            FlurstueckZaehler = e.FlurstueckZaehler,
            FlurstueckNenner = e.FlurstueckNenner,
            Grundbuchblatt = e.Grundbuchblatt,
            Grundstuecksart = e.Grundstuecksart,
            Grundstuecksflaeche = e.Grundstuecksflaeche,
            Wohnflaeche = e.Wohnflaeche,
            Nutzflaeche = e.Nutzflaeche,
            Baujahr = e.Baujahr,
            Bodenrichtwert = e.Bodenrichtwert,
            DurchschnittlicherBodenrichtwert = e.DurchschnittlicherBodenrichtwert,
            Wohnlage = e.Wohnlage,
            IstDenkmalgeschuetzt = e.IstDenkmalgeschuetzt,
            IstSozialerWohnungsbau = e.IstSozialerWohnungsbau,

            Lage = new AdresseDto
            {
                Strasse = e.Lage.Strasse,
                Hausnummer = e.Lage.Hausnummer,
                HausnummerZusatz = e.Lage.HausnummerZusatz,
                Postleitzahl = e.Lage.Postleitzahl,
                Ort = e.Lage.Ort,
                Ortsteil = e.Lage.Ortsteil,
                Land = e.Lage.Land
            },

            BevollmaechtigterName = e.BevollmaechtigterName,
            BevollmaechtigterIdNr = e.BevollmaechtigterIdNr,

            UebermittlungsReferenz = e.UebermittlungsReferenz,
            UebermitteltAm = e.UebermitteltAm,
            ZuletztGeaendertAm = e.ZuletztGeaendertAm,
            ErstelltAm = e.ErstelltAm,
            Berechnung = ZuDto(e.Berechnung)
        };

        dto.Flurstuecke.AddRange(e.Flurstuecke
            .OrderBy(f => f.Reihenfolge)
            .Select(f => new FlurstueckDto
            {
                Id = f.Id,
                Gemarkung = f.Gemarkung,
                Gemarkungsnummer = f.Gemarkungsnummer,
                Flur = f.Flur,
                Zaehler = f.Zaehler,
                Nenner = f.Nenner,
                Flaeche = f.Flaeche,
                Anteil = f.Anteil
            }));

        dto.Eigentuemer.AddRange(e.Eigentuemer
            .OrderBy(x => x.Reihenfolge)
            .Select(x => new EigentuemerDto
            {
                Id = x.Id,
                Art = x.Art,
                Anrede = x.Anrede,
                Name = x.Name,
                Vorname = x.Vorname,

                // Die IdNr liegt nur als Hash vor. Für die Anzeige wird der Platzhalter gesetzt;
                // das Formular überschreibt ihn, sobald der Nutzer die Nummer erneut eingibt.
                IdNummer = string.IsNullOrWhiteSpace(x.IdNrLetzteDrei)
                    ? null
                    : $"…{x.IdNrLetzteDrei}",

                Steuernummer = x.Steuernummer,
                Strasse = x.Strasse,
                Hausnummer = x.Hausnummer,
                Postleitzahl = x.Postleitzahl,
                Ort = x.Ort,
                Land = x.Land,
                Geburtsdatum = x.Geburtsdatum,
                Anteil = x.Anteil,
                IstBevollmaechtigt = x.IstBevollmaechtigt
            }));

        dto.Hinweise.AddRange(e.Hinweise
            .OrderBy(h => h.Schritt)
            .ThenBy(h => h.Schwere)
            .Select(h => new ValidierungsHinweisDto
            {
                Schwere = h.Schwere,
                Feld = h.Feld,
                Meldung = h.Meldung,
                Rechtsgrundlage = h.Rechtsgrundlage,
                Vorschlag = h.Vorschlag
            }));

        return dto;
    }

    public static GrundsteuerBerechnungDto? ZuDto(Messbetrag? m) => m is null
        ? null
        : new GrundsteuerBerechnungDto
        {
            Modell = m.Modell,
            AequivalenzbetragBoden = m.AequivalenzbetragBoden,
            AequivalenzbetragGebaeude = m.AequivalenzbetragGebaeude,
            Flaechenbetrag = m.Flaechenbetrag,
            Ausgangsbetrag = m.Ausgangsbetrag,
            LageFaktor = m.LageFaktor,
            Grundsteuerwert = m.Grundsteuerwert,
            Steuermesszahl = m.Steuermesszahl,
            Steuermessbetrag = m.Steuermessbetrag,
            Berechnungsweg = m.Berechnungsweg
        };

    public static Messbetrag? ZuEntity(GrundsteuerBerechnungDto? d) => d is null
        ? null
        : new Messbetrag
        {
            Modell = d.Modell,
            AequivalenzbetragBoden = d.AequivalenzbetragBoden,
            AequivalenzbetragGebaeude = d.AequivalenzbetragGebaeude,
            Flaechenbetrag = d.Flaechenbetrag,
            Ausgangsbetrag = d.Ausgangsbetrag,
            LageFaktor = d.LageFaktor,
            Grundsteuerwert = d.Grundsteuerwert,
            Steuermesszahl = d.Steuermesszahl,
            Steuermessbetrag = d.Steuermessbetrag,
            Berechnungsweg = d.Berechnungsweg
        };

    // -----------------------------------------------------------------------------------------
    //  DTO -> Entity (bestehende Entity wird aktualisiert, Kindlisten werden ersetzt)
    // -----------------------------------------------------------------------------------------
    public static void Uebernehme(GrundsteuerMeldungDto dto, GrundsteuerMeldungEntity e)
    {
        e.Status = dto.Status;
        e.Bundesland = dto.Bundesland;
        e.Modell = dto.BundeslandInfo.Modell;
        e.Erklaerungsart = dto.Erklaerungsart;
        e.Hauptfeststellungszeitpunkt = dto.Hauptfeststellungszeitpunkt;
        e.Bundesfinanzamtsnummer = dto.Bundesfinanzamtsnummer;
        e.FinanzamtName = dto.FinanzamtName;
        e.Aktenzeichen = dto.Aktenzeichen;
        e.AktenzeichenElster = dto.AktenzeichenElster;
        e.Steuernummer = dto.Steuernummer;
        e.WirtschaftseinheitId = dto.WirtschaftseinheitId;
        e.MeldendePersonId = dto.MeldendePersonId;
        e.MeldendePersonName = dto.MeldendePersonName;

        e.Gemarkung = dto.Gemarkung;
        e.Gemarkungsnummer = dto.Gemarkungsnummer;
        e.Flur = dto.Flur;
        e.FlurstueckZaehler = dto.FlurstueckZaehler;
        e.FlurstueckNenner = dto.FlurstueckNenner;
        e.Grundbuchblatt = dto.Grundbuchblatt;
        e.Grundstuecksart = dto.Grundstuecksart;
        e.Grundstuecksflaeche = dto.Grundstuecksflaeche;
        e.Wohnflaeche = dto.Wohnflaeche;
        e.Nutzflaeche = dto.Nutzflaeche;
        e.Baujahr = dto.Baujahr;
        e.Bodenrichtwert = dto.Bodenrichtwert;
        e.DurchschnittlicherBodenrichtwert = dto.DurchschnittlicherBodenrichtwert;
        e.Wohnlage = dto.Wohnlage;
        e.IstDenkmalgeschuetzt = dto.IstDenkmalgeschuetzt;
        e.IstSozialerWohnungsbau = dto.IstSozialerWohnungsbau;

        e.Lage.Strasse = dto.Lage.Strasse;
        e.Lage.Hausnummer = dto.Lage.Hausnummer;
        e.Lage.HausnummerZusatz = dto.Lage.HausnummerZusatz;
        e.Lage.Postleitzahl = dto.Lage.Postleitzahl;
        e.Lage.Ort = dto.Lage.Ort;
        e.Lage.Ortsteil = dto.Lage.Ortsteil;
        e.Lage.Land = dto.Lage.Land;

        e.BevollmaechtigterName = dto.BevollmaechtigterName;
        e.BevollmaechtigterIdNr = dto.BevollmaechtigterIdNr;

        // Uebermittlungs- und Bescheidangaben: sie kommen nicht aus dem Formular, sondern aus der
        // API. Ein Datensatz kann sie aber bereits tragen (z.B. ein eingespielter Beispieldatensatz
        // mit Status "Uebermittelt" oder "Festgestellt"). Ohne diese Uebernahme zeigte die
        // Übersicht den Status ohne Uebermittlungsdatum bzw. ohne Messbescheid.
        e.UebermitteltAm = dto.UebermitteltAm;
        e.UebermittlungsReferenz = dto.UebermittlungsReferenz;

        e.Berechnung = ZuEntity(dto.Berechnung);

        // Kindlisten werden vollständig neu aufgebaut: der Wizard liefert immer den kompletten
        // Stand, und ein Diff wäre teurer als das Ersetzen. Reihenfolge wird fixiert, damit die
        // Anzeige reproduzierbar bleibt.
        e.Flurstuecke.Clear();
        var fIndex = 0;
        foreach (var f in dto.Flurstuecke)
        {
            e.Flurstuecke.Add(new FlurstueckEntity
            {
                Id = f.Id == Guid.Empty ? Guid.NewGuid() : f.Id,
                Reihenfolge = fIndex++,
                Gemarkung = f.Gemarkung,
                Gemarkungsnummer = f.Gemarkungsnummer,
                Flur = f.Flur,
                Zaehler = f.Zaehler,
                Nenner = f.Nenner,
                Flaeche = f.Flaeche,
                Anteil = f.Anteil
            });
        }

        e.Eigentuemer.Clear();
        var eIndex = 0;
        foreach (var x in dto.Eigentuemer)
        {
            var vorhandenerHash = IdNrHasher.Hashe(x.IdNummer);
            var istPlatzhalter = x.IdNummer?.StartsWith('…') == true;

            e.Eigentuemer.Add(new EigentuemerEntity
            {
                Id = x.Id == Guid.Empty ? Guid.NewGuid() : x.Id,
                Reihenfolge = eIndex++,
                Art = x.Art,
                Anrede = x.Anrede,
                Name = x.Name,
                Vorname = x.Vorname,
                // Ein Platzhalter ("…471", aus dem Laden) darf den echten Hash nicht überschreiben.
                IdNrHash = istPlatzhalter ? null : vorhandenerHash,
                IdNrLetzteDrei = istPlatzhalter ? x.IdNummer![1..] : IdNrHasher.LetzteDrei(x.IdNummer),
                Steuernummer = x.Steuernummer,
                Strasse = x.Strasse,
                Hausnummer = x.Hausnummer,
                Postleitzahl = x.Postleitzahl,
                Ort = x.Ort,
                Land = x.Land,
                Geburtsdatum = x.Geburtsdatum,
                Anteil = x.Anteil,
                IstBevollmaechtigt = x.IstBevollmaechtigt
            });
        }

        e.Hinweise.Clear();
        var hIndex = 0;
        foreach (var h in dto.Hinweise)
        {
            // Zuordnung zum Wizard-Schritt ist in der UI bekannt; wir leiten sie grob aus dem
            // berechneten Stand ab und speichern sie für die Badge-Anzeige.
            e.Hinweise.Add(new ValidierungsHinweisEntity
            {
                Feld = h.Feld,
                Meldung = h.Meldung,
                Schwere = h.Schwere,
                Rechtsgrundlage = h.Rechtsgrundlage,
                Vorschlag = h.Vorschlag,
                Schritt = SchrittFuerFeld(h.Feld, hIndex++)
            });
        }
    }

    /// <summary>
    /// Ordnet einen Hinweis einem Wizard-Schritt zu. Die Feldnamen stammen aus
    /// <c>MeldungsValidator</c>; unbekannte Felder landen in der Zusammenfassung.
    /// </summary>
    private static int SchrittFuerFeld(string feld, int fallbackIndex) => feld switch
    {
        nameof(GrundsteuerMeldungDto.Bundesland) => 0,
        nameof(GrundsteuerMeldungDto.Bundesfinanzamtsnummer) => 0,
        nameof(GrundsteuerMeldungDto.Aktenzeichen) => 0,
        nameof(GrundsteuerMeldungDto.Steuernummer) => 0,
        nameof(GrundsteuerMeldungDto.Erklaerungsart) => 0,
        nameof(GrundsteuerMeldungDto.Gemarkung) => 1,
        nameof(GrundsteuerMeldungDto.Flur) => 1,
        nameof(GrundsteuerMeldungDto.Flurstuecke) => 1,
        nameof(GrundsteuerMeldungDto.Grundstuecksflaeche) => 1,
        nameof(GrundsteuerMeldungDto.Bodenrichtwert) => 1,
        nameof(GrundsteuerMeldungDto.Wohnflaeche) => 1,
        nameof(GrundsteuerMeldungDto.Nutzflaeche) => 1,
        nameof(GrundsteuerMeldungDto.Baujahr) => 1,
        nameof(GrundsteuerMeldungDto.Eigentuemer) => 2,
        _ => fallbackIndex < 0 ? 3 : 3
    };

    // -----------------------------------------------------------------------------------------
    //  Übersichtszeile für das Dashboard
    // -----------------------------------------------------------------------------------------
    public static GrundsteuerUebersichtDto ZuUebersicht(GrundsteuerMeldungEntity e) => new()
    {
        Id = e.Id,
        Aktenzeichen = e.Aktenzeichen,
        Steuernummer = e.Steuernummer ?? string.Empty,
        Bundesland = e.Bundesland,
        Modell = e.Modell,
        Status = e.Status,
        Grundstuecksbezeichnung = string.Join(", ",
            new[] { e.Gemarkung, string.IsNullOrWhiteSpace(e.Flur) ? null : $"Flur {e.Flur}" }
                .Where(t => !string.IsNullOrWhiteSpace(t))),
        Strasse = e.Lage.Strasse,
        Hausnummer = e.Lage.Hausnummer,
        Postleitzahl = e.Lage.Postleitzahl,
        Ort = e.Lage.Ort,
        HauptEigentuemer = e.Eigentuemer
            .OrderBy(x => x.Reihenfolge)
            .Select(x => x.AnzeigeName)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? string.Empty,
        FestgestellterMessbetrag = e.FestgestellterMessbetrag,
        ErstelltAm = e.ErstelltAm,
        ZuletztGeaendertAm = e.ZuletztGeaendertAm,
        UebermitteltAm = e.UebermitteltAm,
        MessbescheidAm = e.MessbescheidAm,
        UebermittlungsReferenz = e.UebermittlungsReferenz,
        LetzteFehlermeldung = e.Uebermittlungen
            .OrderByDescending(u => u.VersuchtAmUtc)
            .Select(u => u.Fehlertext)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)),
        AnzahlHinweise = e.Hinweise.Count
    };
}
