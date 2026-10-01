namespace GrundsteuerPortal.Persistence.Entities;

/// <summary>
/// Lageadresse des Grundstücks als Owned Type. Wird in derselben Tabelle wie die Meldung gehalten
/// (<c>OwnsOne</c>), weil sie ohne die Meldung keine Bedeutung hat.
/// </summary>
public class LageAdresse
{
    public string Strasse { get; set; } = string.Empty;
    public string Hausnummer { get; set; } = string.Empty;
    public string? HausnummerZusatz { get; set; }
    public string Postleitzahl { get; set; } = string.Empty;
    public string Ort { get; set; } = string.Empty;
    public string? Ortsteil { get; set; }
    public string? Land { get; set; } = "Deutschland";
}

/// <summary>
/// Berechnungsergebnis (Steuermessbetrag) als Owned Type. Es wird nur der zuletzt von ERiC bzw.
/// lokal ermittelte Stand gehalten - die Historie steckt in <see cref="ElsterUebermittlungEntity"/>
/// und <see cref="StatusVerlaufEntity"/>.
/// </summary>
public class Messbetrag
{
    public Core.Domain.GrundsteuerModell Modell { get; set; }
    public decimal? AequivalenzbetragBoden { get; set; }
    public decimal? AequivalenzbetragGebaeude { get; set; }
    public decimal? Flaechenbetrag { get; set; }
    public decimal? Ausgangsbetrag { get; set; }
    public decimal? LageFaktor { get; set; }
    public decimal? Grundsteuerwert { get; set; }
    public decimal? Steuermesszahl { get; set; }
    public decimal? Steuermessbetrag { get; set; }
    public string? Berechnungsweg { get; set; }
}
