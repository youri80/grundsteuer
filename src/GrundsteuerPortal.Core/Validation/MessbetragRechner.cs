using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;

namespace GrundsteuerPortal.Core.Validation;

/// <summary>
/// Berechnet den Steuermessbetrag nach dem Modell des jeweiligen Bundeslandes - als Vorschau im
/// Wizard (Schritt 4), damit der Nutzer vor dem Absenden sieht, was das Finanzamt rechnen wird.
///
/// WICHTIG: Das ist eine Plausibilitätsvorschau, keine Steuerfestsetzung. Verbindlich ist der
/// Messbescheid des Finanzamts. Für das Bundesmodell wird bewusst NICHTS gerechnet: der
/// Grundsteuerwert entsteht dort aus dem Ertrags-/Sachwertverfahren, das nur die Verwaltung
/// durchführt - hier einen Wert zu erfinden wäre eine falsche Auskunft.
/// </summary>
public static class MessbetragRechner
{
    public static GrundsteuerBerechnungDto Berechne(GrundsteuerMeldungDto m)
    {
        var info = BundeslandKatalog.Fuer(m.Bundesland);
        var ergebnis = new GrundsteuerBerechnungDto { Modell = info.Modell };

        var flaeche = m.Grundstuecksflaeche ?? 0m;
        var wohnflaeche = m.Wohnflaeche ?? 0m;
        var nutzflaeche = m.Nutzflaeche ?? 0m;
        var wohnnutzung = m.Grundstuecksart is not (Grundstuecksart.Geschaeftsgrundstueck
            or Grundstuecksart.Teileigentum or Grundstuecksart.SonstigesBebautesGrundstueck);

        switch (info.Modell)
        {
            case GrundsteuerModell.ModifiziertesBodenwertmodell:
            {
                // Baden-Württemberg: Grundsteuerwert = Fläche × Bodenrichtwert, Messzahl einheitlich 1,3 ‰,
                // bei überwiegender Wohnnutzung 30 % Abschlag (§ 38 LGrStG, § 39 Abs. 1 LGrStG).
                if (flaeche > 0 && m.Bodenrichtwert is > 0)
                {
                    ergebnis.Grundsteuerwert = flaeche * m.Bodenrichtwert.Value;
                    ergebnis.Steuermesszahl = wohnnutzung ? info.MesszahlWohnen * 0.7m : info.MesszahlWohnen;
                    ergebnis.Steuermessbetrag = Runde2(ergebnis.Grundsteuerwert.Value * ergebnis.Steuermesszahl.Value);
                    ergebnis.Berechnungsweg =
                        $"{flaeche:0.##} m² × {m.Bodenrichtwert:0.##} €/m² = {ergebnis.Grundsteuerwert:0.00} € "
                        + $"× {ergebnis.Steuermesszahl * 1000m:0.####} ‰ = {ergebnis.Steuermessbetrag:0.00} €";
                }
                break;
            }

            case GrundsteuerModell.WertunabhaengigesFlaechenmodell:
            {
                // Bayern: Äquivalenzzahlen 0,04 €/m² (Boden) und 0,50 €/m² (Gebäude),
                // Messzahl 1,0, für Wohnflächen 0,7 (30 % Abschlag).
                var boden = flaeche * BundeslandKatalog.AequiGrundUndBoden * info.MesszahlSonstige;
                var wohnen = wohnflaeche * BundeslandKatalog.AequeGebaeude * info.MesszahlWohnen;
                var nichtwohnen = nutzflaeche * BundeslandKatalog.AequeGebaeude * info.MesszahlSonstige;
                ergebnis.AequivalenzbetragBoden = Runde2(boden);
                ergebnis.AequivalenzbetragGebaeude = Runde2(wohnen + nichtwohnen);
                ergebnis.Steuermessbetrag = Runde2(boden + wohnen + nichtwohnen);
                ergebnis.Berechnungsweg =
                    $"Boden {flaeche:0.##} m² × 0,04 €/m² = {boden:0.00} €; "
                    + $"Wohnen {wohnflaeche:0.##} m² × 0,50 €/m² × 0,7 = {wohnen:0.00} €; "
                    + $"Nichtwohnen {nutzflaeche:0.##} m² × 0,50 €/m² = {nichtwohnen:0.00} €";
                break;
            }

            case GrundsteuerModell.Wohnlagenmodell:
            {
                // Hamburg: gleiche Äquivalenzzahlen, Messzahl 100 %, Wohnflächen 70 %, Nutzflächen 87 %,
                // zusätzlich 25 % Ermäßigung der Wohnflächen-Messzahl bei normaler Wohnlage (§ 4 HmbGrStG).
                var wohnMesszahl = info.MesszahlWohnen;
                if (m.Wohnlage == Domain.Wohnlage.Normal) wohnMesszahl *= 0.75m;

                var boden = flaeche * BundeslandKatalog.AequiGrundUndBoden;
                var wohnen = wohnflaeche * BundeslandKatalog.AequeGebaeude * wohnMesszahl;
                var nutzen = nutzflaeche * BundeslandKatalog.AequeGebaeude * info.MesszahlSonstige;
                ergebnis.AequivalenzbetragBoden = Runde2(boden);
                ergebnis.AequivalenzbetragGebaeude = Runde2(wohnen + nutzen);
                ergebnis.Steuermesszahl = wohnMesszahl;
                ergebnis.Steuermessbetrag = Runde2(boden + wohnen + nutzen);
                ergebnis.Berechnungsweg =
                    $"Boden {flaeche:0.##} m² × 0,04 €/m² = {boden:0.00} €; "
                    + $"Wohnen {wohnflaeche:0.##} m² × 0,50 €/m² × {wohnMesszahl:0.####} = {wohnen:0.00} €; "
                    + $"Nutzflächen {nutzflaeche:0.##} m² × 0,50 €/m² × {info.MesszahlSonstige:0.####} = {nutzen:0.00} €";
                break;
            }

            case GrundsteuerModell.FlaechenFaktorVerfahren:
            {
                // Hessen: Flächenbeträge × Steuermesszahlen = Ausgangsbetrag, dann × Lagefaktor,
                // Abrundung des Messbetrags auf volle Euro (§ 4 Abs. 1 HGrStG).
                var boden = flaeche * BundeslandKatalog.AequiGrundUndBoden * info.MesszahlSonstige;
                var wohnen = wohnflaeche * BundeslandKatalog.AequeGebaeude * info.MesszahlWohnen;
                var nichtwohnen = nutzflaeche * BundeslandKatalog.AequeGebaeude * info.MesszahlSonstige;
                ergebnis.Flaechenbetrag = Runde2(flaeche * BundeslandKatalog.AequiGrundUndBoden
                                                 + (wohnflaeche + nutzflaeche) * BundeslandKatalog.AequeGebaeude);
                ergebnis.Ausgangsbetrag = Runde2(boden + wohnen + nichtwohnen);

                var faktor = Lagefaktor(m.Bodenrichtwert, m.DurchschnittlicherBodenrichtwert);
                ergebnis.LageFaktor = faktor;
                if (faktor.HasValue)
                {
                    ergebnis.Steuermessbetrag = Math.Floor(ergebnis.Ausgangsbetrag!.Value * faktor.Value);
                    ergebnis.Berechnungsweg =
                        $"Ausgangsbetrag {ergebnis.Ausgangsbetrag:0.00} € × Lagefaktor {faktor:0.00} "
                        + $"= {ergebnis.Steuermessbetrag:0} € (abgerundet auf volle Euro)";
                }
                break;
            }

            case GrundsteuerModell.FlaechenLageModell:
            {
                // Niedersachsen: Äquivalenzbeträge × Steuermesszahl, Summe × Lagefaktor.
                var boden = flaeche * BundeslandKatalog.AequiGrundUndBoden * info.MesszahlSonstige;
                var wohnen = wohnflaeche * BundeslandKatalog.AequeGebaeude * info.MesszahlWohnen;
                var nichtwohnen = nutzflaeche * BundeslandKatalog.AequeGebaeude * info.MesszahlSonstige;
                ergebnis.AequivalenzbetragBoden = Runde2(flaeche * BundeslandKatalog.AequiGrundUndBoden);
                ergebnis.AequivalenzbetragGebaeude = Runde2((wohnflaeche + nutzflaeche) * BundeslandKatalog.AequeGebaeude);
                ergebnis.Ausgangsbetrag = Runde2(boden + wohnen + nichtwohnen);

                var faktor = Lagefaktor(m.Bodenrichtwert, m.DurchschnittlicherBodenrichtwert);
                ergebnis.LageFaktor = faktor;
                if (faktor.HasValue)
                {
                    ergebnis.Steuermessbetrag = Runde2(ergebnis.Ausgangsbetrag!.Value * faktor.Value);
                    ergebnis.Berechnungsweg =
                        $"Äquivalenzbeträge × Messzahl = {ergebnis.Ausgangsbetrag:0.00} € "
                        + $"× Lagefaktor {faktor:0.00} = {ergebnis.Steuermessbetrag:0.00} €";
                }
                break;
            }

            default:
            {
                // Bundesmodell: kein rechnerischer Weg im Frontend (Ertrags-/Sachwertverfahren ist
                // Aufgabe der Finanzverwaltung). Nur der festgestellte Messbetrag wird angezeigt.
                ergebnis.Berechnungsweg =
                    "Bundesmodell: Der Grundsteuerwert wird vom Finanzamt im Ertrags- oder Sachwertverfahren "
                    + "ermittelt. Der Steuermessbetrag wird in diesem Portal nur angezeigt, wenn er vorliegt.";
                ergebnis.Steuermesszahl = wohnnutzung ? info.MesszahlWohnen : info.MesszahlSonstige;
                break;
            }
        }

        // Steuermesszahl, wo sie noch nicht gesetzt wurde, zur Anzeige nachtragen.
        ergebnis.Steuermesszahl ??= wohnnutzung ? info.MesszahlWohnen : info.MesszahlSonstige;
        return ergebnis;
    }

    /// <summary>
    /// Lagefaktor = (Bodenrichtwert / durchschnittlicher Bodenrichtwert der Gemeinde)^0,3,
    /// abgerundet auf zwei Nachkommastellen (§ 7 Abs. 1 HGrStG). Ohne Durchschnittswert kein Faktor -
    /// dann wird bewusst kein Messbetrag ausgegeben statt ein falscher.
    /// </summary>
    public static decimal? Lagefaktor(decimal? bodenrichtwert, decimal? durchschnitt)
    {
        if (bodenrichtwert is not > 0 || durchschnitt is not > 0) return null;
        var verhaeltnis = (double)(bodenrichtwert!.Value / durchschnitt!.Value);
        var faktor = Math.Pow(verhaeltnis, (double)BundeslandKatalog.LageFaktorExponent);
        return Math.Floor((decimal)faktor * 100m) / 100m;
    }

    /// <summary>Plausibilitätsfenster für den Lagefaktor - außerhalb davon ist eine Eingabe verdächtig.</summary>
    public static bool IstLagefaktorPlausibel(decimal? faktor) => faktor is null or (>= 0.5m and <= 1.5m);

    private static decimal Runde2(decimal wert) => Math.Round(wert, 2, MidpointRounding.AwayFromZero);

    /// <summary>Formatiert einen Euro-Betrag in deutscher Schreibweise (1.234,56 €).</summary>
    public static string Euro(decimal? betrag, int nachkommastellen = 2)
    {
        var kultur = new System.Globalization.CultureInfo("de-DE");
        return betrag is null
            ? "—"
            : betrag.Value.ToString($"N{nachkommastellen}", kultur) + " €";
    }
}
