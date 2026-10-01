using GrundsteuerPortal.Core.Domain;

namespace GrundsteuerPortal.Persistence.Entities;

/// <summary>
/// Persistiertes Aggregat einer Grundsteuermeldung (die "Erklärung").
///
/// Bewusste Entwurfsentscheidungen:
///  - <see cref="Id"/> ist ein clientseitig erzeugter <see cref="Guid"/>. Damit kann der Wizard
///    eine Meldung mehrfach speichern, bevor die API antwortet, und Entwürfe bleiben offline gültig.
///  - <see cref="RowVersion"/> ersetzt das fehlende SQLite-<c>rowversion</c>: ein 16-Byte-Token,
///    das bei jeder Änderung neu gesetzt wird und als Nebenläufigkeitstoken dient.
///  - Die Lage-Adresse und das Berechnungsergebnis sind <b>Owned Types</b>: sie gehören exklusiv
///    zu dieser Meldung und werden in derselben Tabelle gehalten (kein Join nötig).
///  - Flurstücke, Eigentümer und Hinweise sind eigene Tabellen (1:n), weil sie Listen mit
///    fachlicher Bedeutung sind und einzeln abgefragt werden können müssen.
/// </summary>
public class GrundsteuerMeldungEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nebenläufigkeitstoken (siehe Klassenkommentar).</summary>
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();

    public MeldungStatus Status { get; set; } = MeldungStatus.Entwurf;

    // ---- Schritt 1: Allgemeine Angaben ------------------------------------------------------
    public Bundesland Bundesland { get; set; }
    public GrundsteuerModell Modell { get; set; }
    public Erklaerungsart Erklaerungsart { get; set; }
    public int? Hauptfeststellungszeitpunkt { get; set; }

    /// <summary>4-stellige Bundesfinanzamtsnummer des Lage-Finanzamts.</summary>
    public string? Bundesfinanzamtsnummer { get; set; }

    public string? FinanzamtName { get; set; }

    /// <summary>Ordnungskriterium in 11 Ländern (nicht BE, HB, HH, SH).</summary>
    public string? Aktenzeichen { get; set; }

    public string? AktenzeichenElster { get; set; }

    /// <summary>Ordnungskriterium in BE, HB, HH, SH.</summary>
    public string? Steuernummer { get; set; }

    // ---- Schritt 2: Grundstücksdaten --------------------------------------------------------
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

    /// <summary>Lageadresse des Grundstücks (Owned Type, Same-Table).</summary>
    public LageAdresse Lage { get; set; } = new();

    /// <summary>Weitere Flurstücke der wirtschaftlichen Einheit (1:n).</summary>
    public List<FlurstueckEntity> Flurstuecke { get; set; } = new();

    // ---- Schritt 3: Eigentümer --------------------------------------------------------------
    public List<EigentuemerEntity> Eigentuemer { get; set; } = new();

    public string? BevollmaechtigterName { get; set; }
    public string? BevollmaechtigterIdNr { get; set; }

    // ---- Ergebnis und Verlauf ---------------------------------------------------------------
    /// <summary>Zuletzt berechneter Steuermessbetrag (Owned Type, Same-Table).</summary>
    public Messbetrag? Berechnung { get; set; }

    /// <summary>Hinweise der letzten Prüfung (1:n, ersetzt bei jeder Prüfung).</summary>
    public List<ValidierungsHinweisEntity> Hinweise { get; set; } = new();

    /// <summary>Lückenloses Protokoll aller Statuswechsel (1:n, append-only).</summary>
    public List<StatusVerlaufEntity> Verlauf { get; set; } = new();

    /// <summary>Alle Übermittlungsversuche an ELSTER (1:n, append-only).</summary>
    public List<ElsterUebermittlungEntity> Uebermittlungen { get; set; } = new();

    public string? UebermittlungsReferenz { get; set; }
    public DateTime? UebermitteltAm { get; set; }
    public DateTime? MessbescheidAm { get; set; }
    public decimal? FestgestellterMessbetrag { get; set; }

    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public DateTime ZuletztGeaendertAm { get; set; } = DateTime.UtcNow;

    // ---- Anzeige-Hilfen (nicht persistiert) -------------------------------------------------
    /// <summary>Zeile für die Dashboard-Übersicht - erspart dem Frontend den Join.</summary>
    public string AnschriftAnzeige => string.Join(' ',
        new[] { Lage.Strasse, Lage.Hausnummer }.Where(t => !string.IsNullOrWhiteSpace(t))).Trim();

    public string AnzeigeName => string.Join(", ", Eigentuemer
        .Where(e => !string.IsNullOrWhiteSpace(e.AnzeigeName))
        .Select(e => e.AnzeigeName));
}
