namespace GrundsteuerPortal.Persistence.Entities;

/// <summary>
/// Ein Flurstück der wirtschaftlichen Einheit. Als eigene Tabelle, weil ein Grundstück aus mehreren
/// Flurstücken bestehen kann (§ 5 Abs. 1 HGrStG, Zeilen 9-21 der Anlage GW-1) und jedes Flurstück
/// einen eigenen Anteil an der Einheit hat.
/// </summary>
public class FlurstueckEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GrundsteuerMeldungId { get; set; }
    public GrundsteuerMeldungEntity? GrundsteuerMeldung { get; set; }

    public int Reihenfolge { get; set; }

    public string Gemarkung { get; set; } = string.Empty;
    public string? Gemarkungsnummer { get; set; }
    public string? Flur { get; set; }
    public string? Zaehler { get; set; }
    public string? Nenner { get; set; }

    /// <summary>Amtliche Fläche des Flurstücks in m².</summary>
    public decimal? Flaeche { get; set; }

    /// <summary>Anteil, zu dem das Flurstück zur wirtschaftlichen Einheit gehört (1 = ganz).</summary>
    public decimal Anteil { get; set; } = 1m;
}

/// <summary>
/// Eigentümer oder Miteigentümer mit Eigentumsanteil. Die Summe aller Anteile einer Meldung muss
/// 1,0 ergeben (Validierungsregel in <c>MeldungsValidator</c>).
/// </summary>
public class EigentuemerEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GrundsteuerMeldungId { get; set; }
    public GrundsteuerMeldungEntity? GrundsteuerMeldung { get; set; }

    public int Reihenfolge { get; set; }

    public Core.Domain.EigentuemerArt Art { get; set; }
    public Core.Domain.Anrede Anrede { get; set; }

    /// <summary>Nachname bei natürlichen Personen, sonst vollständiger Firmenname.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Vorname { get; set; }

    /// <summary>
    /// Steueridentifikationsnummer (§ 139 AO). Wird NICHT im Klartext gespeichert, sondern als
    /// SHA-256-Hash plus letzte drei Stellen - siehe <c>IdNrHasher</c>. Damit erfüllt das Portal
    /// die Anforderung, kein unverschlüsseltes Identifikationsmerkmal in einer Datei-DB zu halten.
    /// </summary>
    public string? IdNrHash { get; set; }

    /// <summary>Die letzten drei Stellen zur Wiedererkennung in der UI (z. B. "…471").</summary>
    public string? IdNrLetzteDrei { get; set; }

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

    /// <summary>Anzeigename - nicht persistiert, sondern aus der Zeile abgeleitet.</summary>
    public string AnzeigeName => Art == Core.Domain.EigentuemerArt.NatuerlichePerson
        ? string.Join(' ', new[] { Vorname, Name }.Where(t => !string.IsNullOrWhiteSpace(t)))
        : Name;
}

/// <summary>
/// Ein Prüfhinweis der letzten Validierung. Wird bei jeder Prüfung ersetzt (kein Verlauf), damit
/// die Tabelle klein bleibt; die Historie der Übermittlungen liegt in
/// <see cref="ElsterUebermittlungEntity"/>.
/// </summary>
public class ValidierungsHinweisEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GrundsteuerMeldungId { get; set; }
    public GrundsteuerMeldungEntity? GrundsteuerMeldung { get; set; }

    /// <summary>Wizard-Schritt, dem der Hinweis zugeordnet ist (0-3).</summary>
    public int Schritt { get; set; }

    public Core.Domain.HinweisSchwere Schwere { get; set; }

    /// <summary>Technischer Feldname, damit die UI direkt markieren kann.</summary>
    public string Feld { get; set; } = string.Empty;

    public string Meldung { get; set; } = string.Empty;
    public string? Rechtsgrundlage { get; set; }
    public string? Vorschlag { get; set; }
}

/// <summary>
/// Ein Statuswechsel der Meldung. Append-only: jede Änderung wird protokolliert, weil im
/// Steuerverfahren nachvollziehbar sein muss, wann welcher Stand erreicht wurde.
/// </summary>
public class StatusVerlaufEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GrundsteuerMeldungId { get; set; }
    public GrundsteuerMeldungEntity? GrundsteuerMeldung { get; set; }

    public Core.Domain.MeldungStatus Status { get; set; }
    public DateTime ZeitpunktUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Auslöser des Wechsels, z. B. "Entwurf gespeichert", "An ELSTER übermittelt".</summary>
    public string Ausloeser { get; set; } = string.Empty;

    public string? Bemerkung { get; set; }
}

/// <summary>
/// Ein Übermittlungsversuch an ELSTER. Bewusst ein eigener Datensatz je Versuch (auch bei
/// Fehlschlag): ELSTER-Referenzen und Fehlerantworten sind der Nachweis gegenüber dem Finanzamt.
/// </summary>
public class ElsterUebermittlungEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GrundsteuerMeldungId { get; set; }
    public GrundsteuerMeldungEntity? GrundsteuerMeldung { get; set; }

    public DateTime VersuchtAmUtc { get; set; } = DateTime.UtcNow;
    public bool Erfolgreich { get; set; }

    /// <summary>Referenz-/Sendekennzeichen aus der ELSTER-Antwort.</summary>
    public string? Referenz { get; set; }

    public string? FehlerCode { get; set; }
    public string? Fehlertext { get; set; }

    /// <summary>Der zum Zeitpunkt der Übermittlung erzeugte Steuermessbetrag (Owned Type).</summary>
    public Messbetrag? Berechnung { get; set; }

    /// <summary>Anzahl der Fehler/Warnungen der Übermittlung - für die Übersichtsliste.</summary>
    public int AnzahlFehler { get; set; }
    public int AnzahlWarnungen { get; set; }
}
