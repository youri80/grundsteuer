using GrundsteuerPortal.Core.Domain;

namespace GrundsteuerPortal.Persistence.Entities;

/// <summary>
/// Eine natürliche oder juristische Person als wiederverwendbarer Master.
///
/// Eine Person kann Eigentümer mehrerer Wirtschaftseinheiten (auch in verschiedenen Bundesländern)
/// sein und als meldende Stelle beliebiger Meldungen auftreten - auch ohne je Eigentümer zu sein
/// (z. B. eine Steuerberatungs-GmbH). Deshalb ist sie eine eigene Tabelle, keine Liste an der
/// Meldung oder Einheit.
/// </summary>
public class PersonEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nebenläufigkeitstoken (wie bei der Meldung, da SQLite kein rowversion kennt).</summary>
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();

    public PersonStatus Status { get; set; } = PersonStatus.Aktiv;

    public EigentuemerArt Art { get; set; } = EigentuemerArt.NatuerlichePerson;
    public Anrede Anrede { get; set; } = Anrede.Keine;

    /// <summary>Nachname bei natürlichen Personen, sonst vollständiger Firmenname.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Vorname { get; set; }

    /// <summary>Steueridentifikationsnummer (§ 139 AO) - nur als Hash + letzte drei Stellen.</summary>
    public string? IdNrHash { get; set; }

    public string? IdNrLetzteDrei { get; set; }

    public string? Steuernummer { get; set; }
    public string Strasse { get; set; } = string.Empty;
    public string Hausnummer { get; set; } = string.Empty;
    public string Postleitzahl { get; set; } = string.Empty;
    public string Ort { get; set; } = string.Empty;
    public string? Land { get; set; } = "Deutschland";
    public DateTime? Geburtsdatum { get; set; }

    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public DateTime ZuletztGeaendertAm { get; set; } = DateTime.UtcNow;

    /// <summary>Anzeigename - nicht persistiert.</summary>
    public string AnzeigeName => Art == EigentuemerArt.NatuerlichePerson
        ? string.Join(' ', new[] { Vorname, Name }.Where(t => !string.IsNullOrWhiteSpace(t)))
        : Name;
}

/// <summary>
/// Eine Wirtschaftseinheit: der stabile Grundstücks-Bestand. Aus ihr werden bei Bedarf Meldungen
/// erzeugt (Snapshot). Die Meldung behält einen Herkunfts-Fremdschlüssel auf diese Einheit.
/// </summary>
public class WirtschaftseinheitEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();

    public EinheitStatus Status { get; set; } = EinheitStatus.Aktiv;

    public Bundesland Bundesland { get; set; }
    public GrundsteuerModell Modell { get; set; }

    /// <summary>Bundesfinanzamtsnummer des Lage-Finanzamts.</summary>
    public string? Bundesfinanzamtsnummer { get; set; }

    public string? FinanzamtName { get; set; }

    public string Gemarkung { get; set; } = string.Empty;
    public string? Gemarkungsnummer { get; set; }
    public string? Flur { get; set; }
    public string? FlurstueckZaehler { get; set; }
    public string? FlurstueckNenner { get; set; }
    public string? Grundbuchblatt { get; set; }
    public Grundstuecksart Grundstuecksart { get; set; }
    public decimal? Grundstuecksflaeche { get; set; }
    public decimal? Wohnflaeche { get; set; }
    public decimal? Nutzflaeche { get; set; }
    public int? Baujahr { get; set; }
    public decimal? Bodenrichtwert { get; set; }
    public decimal? DurchschnittlicherBodenrichtwert { get; set; }
    public Wohnlage? Wohnlage { get; set; }
    public bool IstDenkmalgeschuetzt { get; set; }
    public bool IstSozialerWohnungsbau { get; set; }

    /// <summary>Lageadresse des Grundstücks (Owned Type, gleiche Tabelle).</summary>
    public LageAdresse Lage { get; set; } = new();

    /// <summary>Flurstücke der wirtschaftlichen Einheit (1:n).</summary>
    public List<EinheitFlurstueckEntity> Flurstuecke { get; set; } = new();

    /// <summary>Eigentümer-Zuordnung (n:m zu Person über <see cref="EinheitEigentuemerEntity"/>).</summary>
    public List<EinheitEigentuemerEntity> Eigentuemer { get; set; } = new();

    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public DateTime ZuletztGeaendertAm { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Ein Flurstück der wirtschaftlichen Einheit. Eigene Tabelle, weil eine Einheit aus mehreren
/// Flurstücken bestehen kann.
/// </summary>
public class EinheitFlurstueckEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WirtschaftseinheitId { get; set; }
    public WirtschaftseinheitEntity? Wirtschaftseinheit { get; set; }

    public int Reihenfolge { get; set; }

    public string Gemarkung { get; set; } = string.Empty;
    public string? Gemarkungsnummer { get; set; }
    public string? Flur { get; set; }
    public string? Zaehler { get; set; }
    public string? Nenner { get; set; }

    /// <summary>Amtliche Fläche des Flurstücks in m².</summary>
    public decimal? Flaeche { get; set; }

    /// <summary>Anteil, zu dem das Flurstück zur Einheit gehört (1 = ganz).</summary>
    public decimal Anteil { get; set; } = 1m;
}

/// <summary>
/// Zuordnung einer Person als Eigentümer zu einer Wirtschaftseinheit, mit Eigentumsanteil.
/// Die Summe aller Anteile einer Einheit muss 1,0 ergeben.
/// </summary>
public class EinheitEigentuemerEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WirtschaftseinheitId { get; set; }
    public WirtschaftseinheitEntity? Wirtschaftseinheit { get; set; }

    public Guid PersonId { get; set; }
    public PersonEntity? Person { get; set; }

    public int Reihenfolge { get; set; }

    /// <summary>Eigentumsanteil (1 = allein, 0,5 = hälftig).</summary>
    public decimal Anteil { get; set; } = 1m;
}
