using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;

namespace GrundsteuerPortal.Core.Validation;

/// <summary>Die Wizard-Schritte, damit Hinweise dem richtigen Schritt zugeordnet werden können.</summary>
public enum WizardSchritt
{
    AllgemeineAngaben = 0,
    Grundstuecksdaten = 1,
    Eigentuemer = 2,
    Zusammenfassung = 3
}

/// <summary>Ein Hinweis, der einem Wizard-Schritt zugeordnet ist (für die Badge-Anzeige im Stepper).</summary>
public sealed record SchrittHinweis(WizardSchritt Schritt, ValidierungsHinweisDto Hinweis);

/// <summary>
/// Zentrale Validierung der Grundsteuermeldung. Reine Funktionen - kein Zugriff auf die API, keine UI.
/// Damit ist jede Regel testbar, und der Wizard kann daraus sowohl die Button-Freigabe als auch die
/// Fehleranzeige im MudStepper ableiten (HasError je MudStep).
/// </summary>
public static class MeldungsValidator
{
    /// <summary>Prüft einen einzelnen Schritt und liefert alle Hinweise dazu.</summary>
    public static IReadOnlyList<ValidierungsHinweisDto> PruefeSchritt(GrundsteuerMeldungDto m, WizardSchritt schritt) =>
        schritt switch
        {
            WizardSchritt.AllgemeineAngaben => PruefeAllgemeineAngaben(m),
            WizardSchritt.Grundstuecksdaten => PruefeGrundstuecksdaten(m),
            WizardSchritt.Eigentuemer => PruefeEigentuemer(m),
            WizardSchritt.Zusammenfassung => PruefeAlles(m),
            _ => Array.Empty<ValidierungsHinweisDto>()
        };

    /// <summary>Prüft alle Schritte außer "Zusammenfassung" und liefert sie mit Schrittzuordnung zurück.</summary>
    public static IReadOnlyList<SchrittHinweis> PruefeAlleSchritte(GrundsteuerMeldungDto m) =>
        new[]
        {
            WizardSchritt.AllgemeineAngaben,
            WizardSchritt.Grundstuecksdaten,
            WizardSchritt.Eigentuemer
        }.SelectMany(s => PruefeSchritt(m, s).Select(h => new SchrittHinweis(s, h)))
         .ToList();

    /// <summary>Ist der Schritt fehlerfrei (Fehler zählen, Warnungen nicht)?</summary>
    public static bool IstFertig(GrundsteuerMeldungDto m, WizardSchritt schritt) =>
        !PruefeSchritt(m, schritt).Any(h => h.Schwere == HinweisSchwere.Fehler);

    /// <summary>Zählt Fehler je Schritt - Grundlage für HasError am MudStep.</summary>
    public static IReadOnlyDictionary<WizardSchritt, int> FehlerJeSchritt(GrundsteuerMeldungDto m) =>
        PruefeAlleSchritte(m)
            .Where(h => h.Hinweis.Schwere == HinweisSchwere.Fehler)
            .GroupBy(h => h.Schritt)
            .ToDictionary(g => g.Key, g => g.Count());

    public static IReadOnlyList<ValidierungsHinweisDto> PruefeAlles(GrundsteuerMeldungDto m) =>
        new[]
        {
            PruefeAllgemeineAngaben(m), PruefeGrundstuecksdaten(m), PruefeEigentuemer(m)
        }.SelectMany(x => x).ToList();

    // -----------------------------------------------------------------------------------------
    // Schritt 1
    // -----------------------------------------------------------------------------------------
    public static IReadOnlyList<ValidierungsHinweisDto> PruefeAllgemeineAngaben(GrundsteuerMeldungDto m)
    {
        var hinweise = new List<ValidierungsHinweisDto>();
        var info = BundeslandKatalog.Fuer(m.Bundesland);

        if (m.Hauptfeststellungszeitpunkt is null or < 2022 or > 2030)
        {
            hinweise.Add(Fehler(nameof(m.Hauptfeststellungszeitpunkt),
                "Der Hauptfeststellungszeitpunkt ist anzugeben (regulär 01.01.2022).",
                "§ 8 GrStG"));
        }

        if (string.IsNullOrWhiteSpace(m.Bundesfinanzamtsnummer))
        {
            hinweise.Add(Fehler(nameof(m.Bundesfinanzamtsnummer),
                "Bitte das Lage-Finanzamt des Grundstücks wählen (nicht das Wohnsitz-Finanzamt).",
                "ELSTER-Hinweis zum Ordnungskriterium"));
        }
        else if (!Bundesfinanzamtsnummern.IstZulaessig(m.Bundesfinanzamtsnummer, m.Bundesland))
        {
            hinweise.Add(Fehler(nameof(m.Bundesfinanzamtsnummer),
                $"Die Bundesfinanzamtsnummer {m.Bundesfinanzamtsnummer} ist für {m.Bundesland.AnzeigeName()} "
                + "im Grundsteuerverfahren nicht zugelassen.",
                "Abschnitt 8.5 der ELSTER-Schnittstellendokumentation",
                $"Zulässig sind z. B.: {ZulaessigeBufaKurz(m.Bundesland)}"));
        }

        if (info.Ordnungskriterium == Ordnungskriterium.Aktenzeichen)
        {
            var pruefung = ElsterFormate.PruefeAktenzeichen(m.Aktenzeichen, m.Bundesland);
            if (!pruefung.IstGueltig)
            {
                hinweise.Add(Fehler(nameof(m.Aktenzeichen), pruefung.Meldung!,
                    "Abschnitte 8.2/8.3 der ELSTER-Schnittstellendokumentation", pruefung.Vorschlag));
            }
        }
        else
        {
            var pruefung = ElsterFormate.PruefeSteuernummerElster(m.Steuernummer, m.Bundesland);
            if (!pruefung.IstGueltig)
            {
                hinweise.Add(Fehler(nameof(m.Steuernummer), pruefung.Meldung!,
                    "Abschnitt 8.3.1 der ELSTER-Schnittstellendokumentation", pruefung.Vorschlag));
            }
        }

        if (m.Bundesland == Bundesland.Hamburg && !string.Equals(m.Bundesfinanzamtsnummer, "2216", StringComparison.Ordinal))
        {
            hinweise.Add(Warnung(nameof(m.Bundesfinanzamtsnummer),
                "In Hamburg ist für die Bewertung ausschließlich das Finanzamt 16 (BUFA 2216) zuständig.",
                "Abschnitt 8.5"));
        }

        return hinweise;
    }

    private static string ZulaessigeBufaKurz(Bundesland land)
    {
        var bereiche = Bundesfinanzamtsnummern.Bereiche(land);
        if (bereiche.Count == 0) return "keine Angabe verfügbar";
        var erste = bereiche.Take(4).Select(b => b.Von == b.Bis ? $"{b.Von}" : $"{b.Von}–{b.Bis}");
        var rest = bereiche.Count > 4 ? " …" : string.Empty;
        return string.Join(", ", erste) + rest;
    }

    // -----------------------------------------------------------------------------------------
    // Schritt 2
    // -----------------------------------------------------------------------------------------
    public static IReadOnlyList<ValidierungsHinweisDto> PruefeGrundstuecksdaten(GrundsteuerMeldungDto m)
    {
        var hinweise = new List<ValidierungsHinweisDto>();
        var info = BundeslandKatalog.Fuer(m.Bundesland);

        if (string.IsNullOrWhiteSpace(m.Gemarkung))
            hinweise.Add(Fehler(nameof(m.Gemarkung), "Die Gemarkung fehlt.", "GW-1 Zeile 9"));

        var gemarkungsnr = ElsterFormate.PruefeGemarkungsnummer(m.Gemarkungsnummer);
        if (!gemarkungsnr.IstGueltig)
            hinweise.Add(Fehler(nameof(m.Gemarkungsnummer), gemarkungsnr.Meldung!, "GW-1 Zeile 10"));

        if (m.Flurstuecke.Count == 0)
        {
            var flurstueck = ElsterFormate.PruefeFlurstueck(m.FlurstueckZaehler, m.FlurstueckNenner);
            if (!flurstueck.IstGueltig)
                hinweise.Add(Fehler(nameof(m.FlurstueckZaehler), flurstueck.Meldung!, "GW-1 Zeile 10"));
        }
        else
        {
            foreach (var (f, index) in m.Flurstuecke.Select((f, i) => (f, i)))
            {
                var pruefung = ElsterFormate.PruefeFlurstueck(f.Zaehler, f.Nenner);
                if (!pruefung.IstGueltig)
                    hinweise.Add(Fehler($"Flurstuecke[{index}].Zaehler",
                        $"Flurstück {index + 1}: {pruefung.Meldung}", "GW-1 Zeile 10"));
                if (f.Anteil is <= 0m or > 1m)
                    hinweise.Add(Fehler($"Flurstuecke[{index}].Anteil",
                        $"Flurstück {index + 1}: Der Anteil muss zwischen 0 und 1 liegen.", "GW-1 Zeile 11"));
            }
        }

        var flaeche = ElsterFormate.PruefeFlaeche(m.Grundstuecksflaeche, "Die Grundstücksfläche");
        if (!flaeche.IstGueltig)
            hinweise.Add(Fehler(nameof(m.Grundstuecksflaeche), flaeche.Meldung!, "§ 5 Abs. 1 HGrStG / Äquivalenzzahl"));

        if (info.BrauchtBodenrichtwert)
        {
            var brw = ElsterFormate.PruefeBodenrichtwert(m.Bodenrichtwert, "Der Bodenrichtwert");
            if (!brw.IstGueltig)
                hinweise.Add(Fehler(nameof(m.Bodenrichtwert), brw.Meldung!,
                    "§ 196 BauGB / § 7 HGrStG"));

            if (info.Modell is GrundsteuerModell.FlaechenFaktorVerfahren or GrundsteuerModell.FlaechenLageModell)
            {
                if (m.DurchschnittlicherBodenrichtwert is null or <= 0)
                    hinweise.Add(Warnung(nameof(m.DurchschnittlicherBodenrichtwert),
                        "Ohne den durchschnittlichen Bodenrichtwert der Gemeinde lässt sich der Lagefaktor "
                        + "nicht bilden - der Messbetrag bleibt dann unbestimmt.",
                        "§ 7 Abs. 3 HGrStG"));

                var faktor = MessbetragRechner.Lagefaktor(m.Bodenrichtwert, m.DurchschnittlicherBodenrichtwert);
                if (faktor.HasValue && !MessbetragRechner.IstLagefaktorPlausibel(faktor))
                    hinweise.Add(Warnung(nameof(m.Bodenrichtwert),
                        $"Der Lagefaktor ergibt sich zu {faktor:0.00} und liegt damit ungewöhnlich weit vom "
                        + "Durchschnitt der Gemeinde entfernt. Bitte Bodenrichtwert und Durchschnittswert prüfen.",
                        "§ 7 HGrStG"));
            }
        }

        var istBebaut = m.Grundstuecksart != Grundstuecksart.Unbebaut
                        && m.Grundstuecksart != Grundstuecksart.LandForstwirtschaft;

        if (info.BrauchtGebaeudeflaechen && istBebaut)
        {
            var wohn = m.Wohnflaeche ?? 0m;
            var nutzen = m.Nutzflaeche ?? 0m;
            if (wohn <= 0 && nutzen <= 0)
            {
                hinweise.Add(Fehler(nameof(m.Wohnflaeche),
                    "Bei einem bebauten Grundstück ist mindestens die Wohn- oder Nutzfläche anzugeben.",
                    "§ 2 HmbGrStG / § 5 Abs. 2-3 HGrStG"));
            }
            if (wohn > (m.Grundstuecksflaeche ?? 0) * 20m && m.Grundstuecksflaeche > 0)
            {
                hinweise.Add(Warnung(nameof(m.Wohnflaeche),
                    "Die Wohnfläche ist sehr viel größer als die Grundstücksfläche - bitte Eingabe prüfen."));
            }
        }

        var baujahr = ElsterFormate.PruefeBaujahr(m.Baujahr);
        if (!baujahr.IstGueltig)
            hinweise.Add(Fehler(nameof(m.Baujahr), baujahr.Meldung!, "§ 249 BewG"));

        if (info.BrauchtWohnlage && m.Wohnlage is null)
            hinweise.Add(Fehler(nameof(m.Wohnlage),
                "In Hamburg ist die Wohnlage anzugeben (maßgeblich für 25 % Ermäßigung bei normaler Wohnlage).",
                "§ 4 Abs. 2 HmbGrStG"));

        var adresse = m.Lage;
        if (string.IsNullOrWhiteSpace(adresse.Strasse) || string.IsNullOrWhiteSpace(adresse.Hausnummer))
            hinweise.Add(Warnung(nameof(m.Lage), "Die Straße und Hausnummer des Grundstücks sind unvollständig."));

        var plz = ElsterFormate.PruefePostleitzahl(adresse.Postleitzahl);
        if (!plz.IstGueltig) hinweise.Add(Fehler("Lage.Postleitzahl", plz.Meldung!, "Adressangabe"));

        if (string.IsNullOrWhiteSpace(adresse.Ort))
            hinweise.Add(Fehler("Lage.Ort", "Der Ort des Grundstücks fehlt.", "Adressangabe"));

        if (string.IsNullOrWhiteSpace(m.Grundbuchblatt))
            hinweise.Add(Hinweis(nameof(m.Grundbuchblatt),
                "Die Nummer des Grundbuchblatts sollte angegeben werden (nicht zwingend überall erforderlich).",
                "GW-1 Zeile 10"));

        return hinweise;
    }

    // -----------------------------------------------------------------------------------------
    // Schritt 3
    // -----------------------------------------------------------------------------------------
    public static IReadOnlyList<ValidierungsHinweisDto> PruefeEigentuemer(GrundsteuerMeldungDto m)
    {
        var hinweise = new List<ValidierungsHinweisDto>();

        if (m.Eigentuemer.Count == 0)
        {
            hinweise.Add(Fehler(nameof(m.Eigentuemer), "Es muss mindestens ein Eigentümer erfasst werden.", "§ 2 GrStG"));
            return hinweise;
        }

        foreach (var (e, index) in m.Eigentuemer.Select((e, i) => (e, i)))
        {
            var praefix = $"Eigentuemer[{index}]";

            if (string.IsNullOrWhiteSpace(e.AnzeigeName))
                hinweise.Add(Fehler($"{praefix}.Name", $"Eigentümer {index + 1}: Der Name fehlt."));

            if (e.Art == EigentuemerArt.NatuerlichePerson)
            {
                if (IstIdNrPlatzhalter(e.IdNummer))
                {
                    // Aus Datenschutzgründen wird die IdNr nicht gespeichert - beim Laden steht nur
                    // ein Platzhalter ("…471"). Er bedeutet "unverändert": der MeldungMapper erhält
                    // beim Speichern den bestehenden Hash (siehe dort, 'istPlatzhalter'). Ihn als
                    // Fehler zu behandeln würde JEDEN geladenen Entwurf als fehlerhaft anzeigen,
                    // obwohl nichts fehlt - und das Absenden blockieren.
                    hinweise.Add(Hinweis($"{praefix}.IdNummer",
                        $"Eigentümer {index + 1}: Die hinterlegte Identifikationsnummer "
                        + $"({e.IdNummer}) bleibt unverändert. Aus Sicherheitsgründen wird sie "
                        + "nicht im Klartext gespeichert.", "§ 139 AO"));
                }
                else
                {
                    var idNr = ElsterFormate.PruefeIdNr(e.IdNummer);
                    if (!idNr.IstGueltig)
                        hinweise.Add(Fehler($"{praefix}.IdNummer",
                            $"Eigentümer {index + 1}: {idNr.Meldung}", "§ 139 AO"));
                }
            }
            else if (string.IsNullOrWhiteSpace(e.Steuernummer) && string.IsNullOrWhiteSpace(e.IdNummer))
            {
                hinweise.Add(Fehler($"{praefix}.Steuernummer",
                    $"Eigentümer {index + 1}: Für juristische Personen ist die Steuernummer oder die "
                    + "wirtschafts-Identifikationsnummer anzugeben.", "§ 139a-c AO"));
            }

            if (e.Anteil is <= 0m or > 1m)
                hinweise.Add(Fehler($"{praefix}.Anteil",
                    $"Eigentümer {index + 1}: Der Eigentumsanteil muss zwischen 0 und 1 liegen (1 = Alleineigentum)."));

            var plz = ElsterFormate.PruefePostleitzahl(e.Postleitzahl);
            if (!plz.IstGueltig)
                hinweise.Add(Fehler($"{praefix}.Postleitzahl", $"Eigentümer {index + 1}: {plz.Meldung}"));

            if (string.IsNullOrWhiteSpace(e.Ort))
                hinweise.Add(Fehler($"{praefix}.Ort", $"Eigentümer {index + 1}: Der Ort fehlt."));

            if (e.IstBevollmaechtigt && string.IsNullOrWhiteSpace(e.IdNummer))
                hinweise.Add(Fehler($"{praefix}.IdNummer",
                    $"Eigentümer {index + 1}: Ein Bevollmächtigter muss sich mit seiner "
                    + "Identifikationsnummer ausweisen.", "§ 80 AO"));
        }

        var anteile = ElsterFormate.PruefeAnteile(m.Eigentuemer.Select(e => e.Anteil));
        if (!anteile.IstGueltig)
            hinweise.Add(Fehler(nameof(m.Eigentuemer), anteile.Meldung!, "§ 3 GrStG"));

        return hinweise;
    }

    /// <summary>Ist der Wert ein Platzhalter für eine gespeicherte, nicht im Klartext lesbare IdNr?</summary>
    public static bool IstIdNrPlatzhalter(string? idNr) =>
        !string.IsNullOrWhiteSpace(idNr) && idNr.TrimStart().StartsWith('…');

    // -----------------------------------------------------------------------------------------
    // Helfer
    // -----------------------------------------------------------------------------------------
    private static ValidierungsHinweisDto Fehler(string feld, string meldung, string? grundlage = null, string? vorschlag = null) =>
        new() { Feld = feld, Meldung = meldung, Schwere = HinweisSchwere.Fehler, Rechtsgrundlage = grundlage, Vorschlag = vorschlag };

    private static ValidierungsHinweisDto Warnung(string feld, string meldung, string? grundlage = null, string? vorschlag = null) =>
        new() { Feld = feld, Meldung = meldung, Schwere = HinweisSchwere.Warnung, Rechtsgrundlage = grundlage, Vorschlag = vorschlag };

    private static ValidierungsHinweisDto Hinweis(string feld, string meldung, string? grundlage = null, string? vorschlag = null) =>
        new() { Feld = feld, Meldung = meldung, Schwere = HinweisSchwere.Hinweis, Rechtsgrundlage = grundlage, Vorschlag = vorschlag };
}
