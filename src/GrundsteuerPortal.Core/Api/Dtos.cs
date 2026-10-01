using System.ComponentModel.DataAnnotations;
using GrundsteuerPortal.Core.Domain;

namespace GrundsteuerPortal.Core.Api;

// ---------------------------------------------------------------------------------------------
//  Übertragungsmodelle zur vorhandenen ELSTER-WebAPI.
//  Grundsatz: Diese DTOs sind die EINZIGE Schnittstelle zum Backend. Die UI kennt keine anderen
//  Typen. Für die Anzeige (Tabellen, Zusammenfassung) werden bewusst berechnete Eigenschaften
//  mitgeliefert, damit das Frontend keine Fachlogik duplizieren muss.
// ---------------------------------------------------------------------------------------------

/// <summary>Zeile in der Dashboard-Übersicht.</summary>
public sealed class GrundsteuerUebersichtDto
{
    public Guid Id { get; set; }
    public string? Aktenzeichen { get; set; }
    public string Steuernummer { get; set; } = string.Empty;
    public Bundesland Bundesland { get; set; }
    public GrundsteuerModell Modell { get; set; }
    public MeldungStatus Status { get; set; }
    public string Grundstuecksbezeichnung { get; set; } = string.Empty;
    public string Strasse { get; set; } = string.Empty;
    public string Hausnummer { get; set; } = string.Empty;
    public string Postleitzahl { get; set; } = string.Empty;
    public string Ort { get; set; } = string.Empty;
    public string HauptEigentuemer { get; set; } = string.Empty;
    public decimal? FestgestellterMessbetrag { get; set; }
    public DateTime ErstelltAm { get; set; }
    public DateTime ZuletztGeaendertAm { get; set; }
    public DateTime? UebermitteltAm { get; set; }
    public DateTime? MessbescheidAm { get; set; }
    public string? UebermittlungsReferenz { get; set; }
    public string? LetzteFehlermeldung { get; set; }
    public int AnzahlHinweise { get; set; }

    public string Anschrift => string.Join(' ',
        new[] { Strasse, Hausnummer }.Where(t => !string.IsNullOrWhiteSpace(t)))
        .Trim();

    public string OrtZeile => string.Join(' ', new[] { Postleitzahl, Ort }.Where(t => !string.IsNullOrWhiteSpace(t)));

    public string AktenzeichenAnzeige => string.IsNullOrWhiteSpace(Aktenzeichen)
        ? (string.IsNullOrWhiteSpace(Steuernummer) ? "—" : SteuernummerAnzeige)
        : AkteZeichenMitLand;

    private string SteuernummerAnzeige => Steuernummer;
    private string AkteZeichenMitLand => $"{Aktenzeichen} ({Bundesland.KurzName()})";

    public string ModellAnzeige => Modell.AnzeigeName();
    public string StatusAnzeige => Status.AnzeigeName();
}

/// <summary>Vollständige Grundsteuermeldung - Ein-/Ausgabemodell des Wizards.</summary>
public sealed class GrundsteuerMeldungDto
{
    public Guid Id { get; set; }

    /// <summary>Optimistische Nebenläufigkeit: kommt von der API, wird beim Speichern zurückgegeben.</summary>
    public byte[]? RowVersion { get; set; }

    public MeldungStatus Status { get; set; } = MeldungStatus.Entwurf;

    // ---- Schritt 1: Allgemeine Angaben -------------------------------------------------------
    public Bundesland Bundesland { get; set; } = Domain.Bundesland.Hessen;
    public Erklaerungsart Erklaerungsart { get; set; } = Erklaerungsart.Erstmalig;
    public int? Hauptfeststellungszeitpunkt { get; set; } = 2022;

    /// <summary>Bundesfinanzamtsnummer (4-stellig) des Lage-Finanzamts - nicht das Wohnsitz-Finanzamt!</summary>
    [Required(ErrorMessage = "Bitte das Lage-Finanzamt auswählen.")]
    public string? Bundesfinanzamtsnummer { get; set; }

    public string? FinanzamtName { get; set; }

    /// <summary>Das Ordnungskriterium: Aktenzeichen (11 Länder) ODER Steuernummer (BE, HB, HH, SH).</summary>
    public string? Aktenzeichen { get; set; }

    /// <summary>Aktenzeichen im 13-stelligen ELSTER-Format (nur für Anzeige/Übermittlung der API).</summary>
    public string? AktenzeichenElster { get; set; }

    public string? Steuernummer { get; set; }

    // ---- Herkunft (wirtschaftseinheit-zentrisches Modell) -----------------------------------
    /// <summary>Herkunfts-Einheit, aus der diese Meldung erzeugt wurde (Snapshot).</summary>
    public Guid? WirtschaftseinheitId { get; set; }

    /// <summary>Meldende Stelle (freier Verweis auf eine Person, auch Nicht-Eigentümer).</summary>
    public Guid? MeldendePersonId { get; set; }

    /// <summary>Snapshot des Anzeigenamens der meldenden Stelle.</summary>
    public string? MeldendePersonName { get; set; }

    // ---- Schritt 2: Grundstücksdaten ---------------------------------------------------------
    public string Gemarkung { get; set; } = string.Empty;
    public string? Gemarkungsnummer { get; set; }
    public string? Flur { get; set; }
    public string? FlurstueckZaehler { get; set; }
    public string? FlurstueckNenner { get; set; }
    public string? Grundbuchblatt { get; set; }
    public Grundstuecksart Grundstuecksart { get; set; } = Grundstuecksart.Einfamilienhaus;
    public decimal? Grundstuecksflaeche { get; set; }
    public decimal? Wohnflaeche { get; set; }
    public decimal? Nutzflaeche { get; set; }
    public int? Baujahr { get; set; }
    public decimal? Bodenrichtwert { get; set; }
    public decimal? DurchschnittlicherBodenrichtwert { get; set; }
    public Wohnlage? Wohnlage { get; set; }
    public bool IstDenkmalgeschuetzt { get; set; }
    public bool IstSozialerWohnungsbau { get; set; }

    /// <summary>Mehrere Flurstücke je wirtschaftlicher Einheit (§ 5 Abs. 1 HGrStG, Zeilen 9-21 GW-1).</summary>
    public List<FlurstueckDto> Flurstuecke { get; set; } = new();

    public AdresseDto Lage { get; set; } = new();

    // ---- Schritt 3: Eigentümer ---------------------------------------------------------------
    public List<EigentuemerDto> Eigentuemer { get; set; } = new();

    /// <summary>Identifikationsnummer des Vollmachtgebers, falls ein Bevollmächtigter übermittelt.</summary>
    public string? BevollmaechtigterName { get; set; }
    public string? BevollmaechtigterIdNr { get; set; }

    // ---- Ergebnis (nur lesend, von API/ERiC gefüllt) ----------------------------------------
    public GrundsteuerBerechnungDto? Berechnung { get; set; }
    public List<ValidierungsHinweisDto> Hinweise { get; set; } = new();
    public string? UebermittlungsReferenz { get; set; }
    public DateTime? UebermitteltAm { get; set; }
    public DateTime? ZuletztGeaendertAm { get; set; }
    public DateTime? ErstelltAm { get; set; }

    /// <summary>Fachliche Kenndaten des gewählten Bundeslandes (Modell, Messzahlen, Ordnungskriterium, Pflichtfelder).</summary>
    public BundeslandInfo BundeslandInfo => BundeslandKatalog.Fuer(Bundesland);

    /// <summary>Reine Anzeige-Hilfe: der Faktor, den das Landesschema vorsieht.</summary>
    public string ModellAnzeige => BundeslandInfo.Modell.AnzeigeName();
}

/// <summary>Ein Flurstück innerhalb der wirtschaftlichen Einheit.</summary>
public sealed class FlurstueckDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Gemarkung { get; set; } = string.Empty;
    public string? Gemarkungsnummer { get; set; }
    public string? Flur { get; set; }
    public string? Zaehler { get; set; }
    public string? Nenner { get; set; }

    /// <summary>Amtliche Fläche des Flurstücks in m².</summary>
    public decimal? Flaeche { get; set; }

    /// <summary>Anteil, zu dem das Flurstück zur wirtschaftlichen Einheit gehört (1 = ganz, 0,5 = zur Hälfte).</summary>
    public decimal Anteil { get; set; } = 1m;

    public string Bezeichnung
    {
        get
        {
            var flurstueck = string.IsNullOrWhiteSpace(Nenner) ? Zaehler : $"{Zaehler}/{Nenner}";
            var flur = string.IsNullOrWhiteSpace(Flur) ? null : $"Flur {Flur}";
            return string.Join(", ", new[] { Gemarkung, flur, $"Flurstück {flurstueck}" }
                .Where(t => !string.IsNullOrWhiteSpace(t)));
        }
    }
}

/// <summary>Lageadresse des Grundstücks.</summary>
public sealed class AdresseDto
{
    public string Strasse { get; set; } = string.Empty;
    public string Hausnummer { get; set; } = string.Empty;
    public string? HausnummerZusatz { get; set; }
    public string Postleitzahl { get; set; } = string.Empty;
    public string Ort { get; set; } = string.Empty;
    public string? Ortsteil { get; set; }
    public string? Land { get; set; } = "Deutschland";

    public string Einzeilig => string.Join(", ", new[]
    {
        string.Join(' ', new[] { Strasse, Hausnummer, HausnummerZusatz }.Where(t => !string.IsNullOrWhiteSpace(t))),
        string.Join(' ', new[] { Postleitzahl, Ort }.Where(t => !string.IsNullOrWhiteSpace(t)))
    }.Where(t => !string.IsNullOrWhiteSpace(t)));
}

/// <summary>Eigentümer oder Miteigentümer mit Anteil.</summary>
public sealed class EigentuemerDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public EigentuemerArt Art { get; set; } = EigentuemerArt.NatuerlichePerson;
    public Anrede Anrede { get; set; } = Anrede.Keine;

    /// <summary>Vor- und Nachname bzw. Firmenname.</summary>
    public string Name { get; set; } = string.Empty;
    public string? Vorname { get; set; }

    /// <summary>11-stellige Steueridentifikationsnummer (§ 139 AO) - Pflicht zur ELSTER-Übermittlung.</summary>
    public string? IdNummer { get; set; }

    public string? Steuernummer { get; set; }
    public string Strasse { get; set; } = string.Empty;
    public string Hausnummer { get; set; } = string.Empty;
    public string Postleitzahl { get; set; } = string.Empty;
    public string Ort { get; set; } = string.Empty;
    public string? Land { get; set; } = "Deutschland";
    public DateTime? Geburtsdatum { get; set; }

    /// <summary>Eigentumsanteil als Bruch (1 = Alleineigentum, 0,5 = hälftig).</summary>
    public decimal Anteil { get; set; } = 1m;

    public bool IstBevollmaechtigt { get; set; }

    public string AnzeigeName => Art == EigentuemerArt.NatuerlichePerson
        ? string.Join(' ', new[] { Vorname, Name }.Where(t => !string.IsNullOrWhiteSpace(t)))
        : Name;

    public string AnteilAnzeige => Anteil == decimal.Truncate(Anteil)
        ? Anteil.ToString("0")
        : Anteil.ToString("0.####");
}

/// <summary>Ergebnis der Steuermessbetragsberechnung, wie sie die API zurückliefert (ERiC-Vorprüfung).</summary>
public sealed class GrundsteuerBerechnungDto
{
    public GrundsteuerModell Modell { get; set; }
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

/// <summary>Ein Validierungshinweis, der aus der API/ERiC-Vorprüfung zurückkommt.</summary>
public sealed class ValidierungsHinweisDto
{
    public HinweisSchwere Schwere { get; set; }
    public string Feld { get; set; } = string.Empty;
    public string Meldung { get; set; } = string.Empty;
    public string? Rechtsgrundlage { get; set; }
    public string? Vorschlag { get; set; }

    public string SchwereAnzeige => Schwere.AnzeigeName();
}

/// <summary>Antwort einer schreibenden API-Operation.</summary>
public sealed class ApiResponse
{
    public bool Erfolg { get; set; }
    public string? Meldung { get; set; }
    public string? FehlerCode { get; set; }
    public Guid? Id { get; set; }
    public byte[]? RowVersion { get; set; }
    public List<ValidierungsHinweisDto> Hinweise { get; set; } = new();
    public GrundsteuerBerechnungDto? Berechnung { get; set; }

    public static ApiResponse Ok(string? meldung = null, Guid? id = null) =>
        new() { Erfolg = true, Meldung = meldung, Id = id };

    public static ApiResponse Fehler(string meldung, string? code = null,
        List<ValidierungsHinweisDto>? hinweise = null) =>
        new() { Erfolg = false, Meldung = meldung, FehlerCode = code, Hinweise = hinweise ?? new() };
}

/// <summary>Ergebnis des Statusabrufs beim Finanzamt (Messbescheid prüfen).</summary>
public sealed class StatusPruefungDto
{
    public Guid Id { get; set; }
    public MeldungStatus Status { get; set; }
    public decimal? FestgestellterMessbetrag { get; set; }
    public DateTime? MessbescheidAm { get; set; }
    public string? Aktenzeichen { get; set; }
    public string? Nachricht { get; set; }
}

/// <summary>Finanzamt für die Auswahlliste (kommt von der API, Fallback im Mock).</summary>
public sealed class FinanzamtDto
{
    public string Bundesfinanzamtsnummer { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Bundesland Bundesland { get; set; }
    public string? Ort { get; set; }

    public string Anzeige => $"{Bundesfinanzamtsnummer} – {Name}";
}

/// <summary>Vorschlagswert für eine PLZ (nur Anzeige-Hilfe im Wizard).</summary>
public sealed class PlzZuordnungDto
{
    public string Postleitzahl { get; set; } = string.Empty;
    public string Ort { get; set; } = string.Empty;
    public Bundesland Bundesland { get; set; }
}

// =============================================================================================
//  Wirtschaftseinheit-zentrisches Modell: Person (Master) + Wirtschaftseinheit (Bestand).
//  Die Meldung bleibt ein Vorgang, der sich auf eine Einheit bezieht (Snapshot).
// =============================================================================================

/// <summary>
/// Ein Eigentümer-Anteil an einer Wirtschaftseinheit: verweist auf eine Person und trägt den
/// Anteil (Miteigentum 1/2 + 1/2). Die Person selbst ist ein wiederverwendbarer Master.
/// </summary>
public sealed class EinheitEigentuemerDto
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Verweis auf die Person (Master).</summary>
    public Guid PersonId { get; set; }

    /// <summary>Eigentumsanteil (1 = allein, 0,5 = hälftig).</summary>
    public decimal Anteil { get; set; } = 1m;

    // Anzeige-Hilfen werden beim Laden aus der Person befüllt (kein zusätzlicher Join in der UI).
    public string AnzeigeName { get; set; } = string.Empty;
    public EigentuemerArt Art { get; set; } = EigentuemerArt.NatuerlichePerson;
}

/// <summary>
/// Eine natürliche oder juristische Person als wiederverwendbarer Master. Eine Person kann
/// Eigentümer mehrerer Wirtschaftseinheiten (auch in verschiedenen Bundesländern) sein und als
/// meldende Stelle beliebiger Meldungen auftreten - auch ohne je Eigentümer zu sein
/// (z. B. eine Steuerberatungs-GmbH).
/// </summary>
public sealed class PersonDto
{
    public Guid Id { get; set; }
    public byte[]? RowVersion { get; set; }

    public PersonStatus Status { get; set; } = PersonStatus.Aktiv;

    public EigentuemerArt Art { get; set; } = EigentuemerArt.NatuerlichePerson;
    public Anrede Anrede { get; set; } = Anrede.Keine;

    /// <summary>Nachname bei natürlichen Personen, sonst vollständiger Firmenname.</summary>
    public string Name { get; set; } = string.Empty;
    public string? Vorname { get; set; }

    /// <summary>11-stellige Steueridentifikationsnummer (§ 139 AO) - wird nicht im Klartext gespeichert.</summary>
    public string? IdNummer { get; set; }

    public string? Steuernummer { get; set; }
    public string Strasse { get; set; } = string.Empty;
    public string Hausnummer { get; set; } = string.Empty;
    public string Postleitzahl { get; set; } = string.Empty;
    public string Ort { get; set; } = string.Empty;
    public string? Land { get; set; } = "Deutschland";
    public DateTime? Geburtsdatum { get; set; }

    public string AnzeigeName => Art == EigentuemerArt.NatuerlichePerson
        ? string.Join(' ', new[] { Vorname, Name }.Where(t => !string.IsNullOrWhiteSpace(t)))
        : Name;
}

/// <summary>Zeile in der Personen-Übersicht.</summary>
public sealed class PersonUebersichtDto
{
    public Guid Id { get; set; }
    public PersonStatus Status { get; set; }
    public EigentuemerArt Art { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Vorname { get; set; }
    public string Ort { get; set; } = string.Empty;

    public string AnzeigeName => Art == EigentuemerArt.NatuerlichePerson
        ? string.Join(' ', new[] { Vorname, Name }.Where(t => !string.IsNullOrWhiteSpace(t)))
        : Name;
}

/// <summary>
/// Eine Wirtschaftseinheit: der stabile Grundstücks-Bestand (Lage, Flurstücke, Eigentümer,
/// Flächen, zuständiges Finanzamt). Aus ihr werden bei Bedarf Meldungen erzeugt (Snapshot).
/// </summary>
public sealed class WirtschaftseinheitDto
{
    public Guid Id { get; set; }
    public byte[]? RowVersion { get; set; }

    public EinheitStatus Status { get; set; } = EinheitStatus.Aktiv;

    public Bundesland Bundesland { get; set; } = Bundesland.Hessen;

    /// <summary>Bundesfinanzamtsnummer des Lage-Finanzamts.</summary>
    public string? Bundesfinanzamtsnummer { get; set; }
    public string? FinanzamtName { get; set; }

    public string Gemarkung { get; set; } = string.Empty;
    public string? Gemarkungsnummer { get; set; }
    public string? Flur { get; set; }
    public string? FlurstueckZaehler { get; set; }
    public string? FlurstueckNenner { get; set; }
    public string? Grundbuchblatt { get; set; }
    public Grundstuecksart Grundstuecksart { get; set; } = Grundstuecksart.Einfamilienhaus;
    public decimal? Grundstuecksflaeche { get; set; }
    public decimal? Wohnflaeche { get; set; }
    public decimal? Nutzflaeche { get; set; }
    public int? Baujahr { get; set; }
    public decimal? Bodenrichtwert { get; set; }
    public decimal? DurchschnittlicherBodenrichtwert { get; set; }
    public Wohnlage? Wohnlage { get; set; }
    public bool IstDenkmalgeschuetzt { get; set; }
    public bool IstSozialerWohnungsbau { get; set; }

    public AdresseDto Lage { get; set; } = new();
    public List<FlurstueckDto> Flurstuecke { get; set; } = new();
    public List<EinheitEigentuemerDto> Eigentuemer { get; set; } = new();

    public BundeslandInfo BundeslandInfo => BundeslandKatalog.Fuer(Bundesland);

    /// <summary>Gibt es aktuell eine aktive (noch nicht abgeschlossene) Meldung zu dieser Einheit?</summary>
    public bool HatAktiveMeldung { get; set; }

    public string AnzeigeName => string.Join(", ",
        new[] { Gemarkung, string.IsNullOrWhiteSpace(Flur) ? null : $"Flur {Flur}" }
        .Where(t => !string.IsNullOrWhiteSpace(t)));
}

/// <summary>Zeile in der Wirtschaftseinheiten-Übersicht.</summary>
public sealed class WirtschaftseinheitUebersichtDto
{
    public Guid Id { get; set; }
    public EinheitStatus Status { get; set; }
    public Bundesland Bundesland { get; set; }
    public string Gemarkung { get; set; } = string.Empty;
    public string? Flur { get; set; }
    public string Strasse { get; set; } = string.Empty;
    public string Hausnummer { get; set; } = string.Empty;
    public string Ort { get; set; } = string.Empty;
    public string HauptEigentuemer { get; set; } = string.Empty;
    public int AnzahlEigentuemer { get; set; }
    public int AnzahlFlurstuecke { get; set; }
    public DateTime ZuletztGeaendertAm { get; set; }

    public string AnzeigeName => string.Join(", ",
        new[] { Gemarkung, string.IsNullOrWhiteSpace(Flur) ? null : $"Flur {Flur}" }
        .Where(t => !string.IsNullOrWhiteSpace(t)));
}
