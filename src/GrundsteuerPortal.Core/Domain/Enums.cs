using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace GrundsteuerPortal.Core.Domain;

/// <summary>Die 16 Bundesländer mit Stabiler Reihenfolge (alphabetisch wie im ELSTER-Portal).</summary>
public enum Bundesland
{
    [Display(Name = "Baden-Württemberg", ShortName = "BW")] BadenWuerttemberg = 1,
    [Display(Name = "Bayern", ShortName = "BY")] Bayern = 2,
    [Display(Name = "Berlin", ShortName = "BE")] Berlin = 3,
    [Display(Name = "Brandenburg", ShortName = "BB")] Brandenburg = 4,
    [Display(Name = "Bremen", ShortName = "HB")] Bremen = 5,
    [Display(Name = "Hamburg", ShortName = "HH")] Hamburg = 6,
    [Display(Name = "Hessen", ShortName = "HE")] Hessen = 7,
    [Display(Name = "Mecklenburg-Vorpommern", ShortName = "MV")] MecklenburgVorpommern = 8,
    [Display(Name = "Niedersachsen", ShortName = "NI")] Niedersachsen = 9,
    [Display(Name = "Nordrhein-Westfalen", ShortName = "NW")] NordrheinWestfalen = 10,
    [Display(Name = "Rheinland-Pfalz", ShortName = "RP")] RheinlandPfalz = 11,
    [Display(Name = "Saarland", ShortName = "SL")] Saarland = 12,
    [Display(Name = "Sachsen", ShortName = "SN")] Sachsen = 13,
    [Display(Name = "Sachsen-Anhalt", ShortName = "ST")] SachsenAnhalt = 14,
    [Display(Name = "Schleswig-Holstein", ShortName = "SH")] SchleswigHolstein = 15,
    [Display(Name = "Thüringen", ShortName = "TH")] Thueringen = 16
}

/// <summary>
/// Das Grundsteuermodell, nach dem das jeweilige Bundesland ab 2025 bewertet.
/// Grundlage: Länderöffnungsklausel (Art. 72 Abs. 3 GG) - 5 Länder haben ein eigenes Modell,
/// Saarland und Sachsen weichen nur bei den Steuermesszahlen vom Bundesmodell ab.
/// </summary>
public enum GrundsteuerModell
{
    [Display(Name = "Bundesmodell", ShortName = "Bund")]
    Bundesmodell = 0,

    [Display(Name = "Modifiziertes Bodenwertmodell", ShortName = "BW")]
    ModifiziertesBodenwertmodell = 1,

    [Display(Name = "Wertunabhängiges Flächenmodell", ShortName = "BY")]
    WertunabhaengigesFlaechenmodell = 2,

    [Display(Name = "Flächen-/Wohnlagenmodell", ShortName = "HH")]
    Wohnlagenmodell = 3,

    [Display(Name = "Flächen-Faktor-Verfahren", ShortName = "HE")]
    FlaechenFaktorVerfahren = 4,

    [Display(Name = "Flächen-Lage-Modell", ShortName = "NI")]
    FlaechenLageModell = 5
}

/// <summary>Art des Grundstücks / der wirtschaftlichen Einheit (bestimmt Steuermesszahl und Pflichtfelder).</summary>
public enum Grundstuecksart
{
    [Display(Name = "Unbebautes Grundstück")] Unbebaut = 0,
    [Display(Name = "Einfamilienhaus")] Einfamilienhaus = 1,
    [Display(Name = "Zweifamilienhaus")] Zweifamilienhaus = 2,
    [Display(Name = "Mietwohngrundstück")] Mietwohngrundstueck = 3,
    [Display(Name = "Wohnungseigentum")] Wohnungseigentum = 4,
    [Display(Name = "Teileigentum")] Teileigentum = 5,
    [Display(Name = "Geschäftsgrundstück")] Geschaeftsgrundstueck = 6,
    [Display(Name = "Gemischt genutztes Grundstück")] GemischtGenutzt = 7,
    [Display(Name = "Sonstiges bebautes Grundstück")] SonstigesBebautesGrundstueck = 8,
    [Display(Name = "Land- und forstwirtschaftliches Vermögen (Grundsteuer A)")] LandForstwirtschaft = 9
}

/// <summary>Erklärungsart - unterscheidet erstmalige Abgabe, Änderung und Berichtigung.</summary>
public enum Erklaerungsart
{
    [Display(Name = "Erstmalige Erklärung (Hauptfeststellung)")] Erstmalig = 0,
    [Display(Name = "Erklärung zur Änderung")] Aenderung = 1,
    [Display(Name = "Berichtigung einer Erklärung")] Berichtigung = 2
}

/// <summary>Rechtsform des Eigentümers.</summary>
public enum EigentuemerArt
{
    [Display(Name = "Natürliche Person")] NatuerlichePerson = 0,
    [Display(Name = "Juristische Person")] JuristischePerson = 1,
    [Display(Name = "Personengesellschaft")] Personengesellschaft = 2,
    [Display(Name = "Erbengemeinschaft")] Erbengemeinschaft = 3,
    [Display(Name = "Wohnungseigentümergemeinschaft (WEG)")] Weg = 4
}

/// <summary>Anrede / Rechtsformbezeichnung.</summary>
public enum Anrede
{
    [Display(Name = "Keine Angabe")] Keine = 0,
    [Display(Name = "Herr")] Herr = 1,
    [Display(Name = "Frau")] Frau = 2,
    [Display(Name = "Divers")] Divers = 3,
    [Display(Name = "Firma")] Firma = 4
}

/// <summary>Wohnlage - nur im Hamburger Wohnlagenmodell bewertungsrelevant (§ 4 Abs. 2 HmbGrStG).</summary>
public enum Wohnlage
{
    [Display(Name = "Normale Wohnlage")] Normal = 0,
    [Display(Name = "Gute Wohnlage")] Gut = 1
}

/// <summary>Lebenszyklus einer Grundsteuermeldung im Portal.</summary>
public enum MeldungStatus
{
    [Display(Name = "Entwurf")] Entwurf = 0,
    [Display(Name = "Validierungsfehler")] Validierungsfehler = 1,
    [Display(Name = "Übermittelt")] Uebermittelt = 2,
    [Display(Name = "In Prüfung beim Finanzamt")] InPruefung = 3,
    [Display(Name = "Festgestellt (Messbetrag liegt vor)")] Festgestellt = 4,
    [Display(Name = "Übermittlung fehlgeschlagen")] Fehlgeschlagen = 5,
    [Display(Name = "Storniert")] Storniert = 6
}

/// <summary>Grad eines Validierungshinweises.</summary>
public enum HinweisSchwere
{
    [Display(Name = "Hinweis")] Hinweis = 0,
    [Display(Name = "Warnung")] Warnung = 1,
    [Display(Name = "Fehler")] Fehler = 2
}

/// <summary>Liest die deutschen Anzeigenamen der Enums (zentral, damit UI und PDF identisch beschriften).</summary>
public static class EnumAnzeige
{
    public static string AnzeigeName(this Enum? value)
    {
        if (value is null) return string.Empty;
        var field = value.GetType().GetField(value.ToString());
        return field?.GetCustomAttribute<DisplayAttribute>()?.Name ?? value.ToString();
    }

    public static string KurzName(this Enum? value)
    {
        if (value is null) return string.Empty;
        var field = value.GetType().GetField(value.ToString());
        return field?.GetCustomAttribute<DisplayAttribute>()?.ShortName ?? value.ToString();
    }
}
