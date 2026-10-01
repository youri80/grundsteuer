using GrundsteuerPortal.Core.Domain;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GrundsteuerPortal.Web.Components.Pages;

public partial class Laender : ComponentBase
{
    /// <summary>Gruppiert die Bundesländer nach Modell - die Übersicht folgt der Systematik, nicht dem Alphabet.</summary>
    private static Dictionary<GrundsteuerModell, List<Bundesland>> ModelleMitLaendern() =>
        BundeslandKatalog.Alle.Values
            .GroupBy(i => i.Modell)
            .OrderBy(g => (int)g.Key)
            .ToDictionary(g => g.Key, g => g.Select(i => i.Bundesland).OrderBy(b => b.AnzeigeName()).ToList());

    private static string ModellIcon(GrundsteuerModell modell) => modell switch
    {
        GrundsteuerModell.Bundesmodell => Icons.Material.Outlined.AccountBalance,
        GrundsteuerModell.ModifiziertesBodenwertmodell => Icons.Material.Outlined.Landscape,
        GrundsteuerModell.WertunabhaengigesFlaechenmodell => Icons.Material.Outlined.SquareFoot,
        GrundsteuerModell.Wohnlagenmodell => Icons.Material.Outlined.LocationCity,
        GrundsteuerModell.FlaechenFaktorVerfahren => Icons.Material.Outlined.Tune,
        GrundsteuerModell.FlaechenLageModell => Icons.Material.Outlined.Explore,
        _ => Icons.Material.Outlined.Info
    };

    private static string ModellBeschreibung(GrundsteuerModell modell) => modell switch
    {
        GrundsteuerModell.Bundesmodell =>
            "Bewertung über den Grundsteuerwert: bei Wohnimmobilien im Ertragswertverfahren, "
            + "bei Gewerbe und sonstigen Grundstücken im Sachwertverfahren. Faktoren sind Bodenrichtwert, "
            + "Grundstücksfläche, Gebäudeart, Baujahr, Wohnfläche und Mieteinnahmen. "
            + "Saarland (Wohnen 0,34 ‰ / sonst 0,64 ‰) und Sachsen (Wohnen 0,36 ‰ / Gewerbe 0,72 ‰) "
            + "weichen nur bei den Steuermesszahlen ab.",
        GrundsteuerModell.ModifiziertesBodenwertmodell =>
            "Grundstücksfläche × Bodenrichtwert = Grundsteuerwert. Einheitliche Steuermesszahl 1,3 ‰, "
            + "bei überwiegender Wohnnutzung 30 % Abschlag. Bebauung und Gebäudeflächen sind unerheblich.",
        GrundsteuerModell.WertunabhaengigesFlaechenmodell =>
            "Reines Flächenmodell: Boden 0,04 €/m², Gebäudefläche 0,50 €/m², multipliziert mit der "
            + "Steuermesszahl (1,0, Wohnflächen 0,7). Werte und Mieten spielen keine Rolle.",
        GrundsteuerModell.Wohnlagenmodell =>
            "Äquivalenzmodell wie Bayern, zusätzlich bewertungsrelevant: die Wohnlage aus dem "
            + "Mietenspiegelverzeichnis (normal/gut) mit 25 % Ermäßigung bei normaler Wohnlage. "
            + "Messzahl 100 %, Wohnflächen 70 %, Nutzflächen 87 %.",
        GrundsteuerModell.FlaechenFaktorVerfahren =>
            "Zweistufig: Flächenbeträge × Steuermesszahl (Boden/Nichtwohnen 100 %, Wohnen 70 %) ergeben "
            + "den Ausgangsbetrag, der mit einem Lagefaktor multipliziert wird. Der Faktor ist "
            + "(Bodenrichtwert ÷ durchschnittlicher Bodenrichtwert der Gemeinde)^0,3 und wird auf zwei "
            + "Nachkommastellen abgerundet; der Messbetrag wird auf volle Euro abgerundet.",
        GrundsteuerModell.FlaechenLageModell =>
            "Äquivalenzbeträge (Boden 0,04 €/m², Gebäude 0,50 €/m²) × Steuermesszahl (Boden und "
            + "Nichtwohnen 1,0, Wohnen 0,7), anschließend Multiplikation mit dem Lagefaktor "
            + "(Bodenrichtwert ÷ Gemeindedurchschnitt)^0,3.",
        _ => string.Empty
    };

    /// <summary>Messzahl lesbar: als Promillewert bei Wertmodellen, als Prozentwert bei den Flächenmodellen.</summary>
    private static string MesszahlText(decimal messzahl, BundeslandInfo info)
    {
        if (info.Modell == GrundsteuerModell.Bundesmodell
            || info.Modell == GrundsteuerModell.ModifiziertesBodenwertmodell)
        {
            return $"{messzahl * 1000m:0.####} ‰";
        }

        return $"{messzahl * 100m:0.####} %";
    }
}
