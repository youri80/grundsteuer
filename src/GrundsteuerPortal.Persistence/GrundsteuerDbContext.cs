using GrundsteuerPortal.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GrundsteuerPortal.Persistence;

/// <summary>
/// EF-Core-Kontext auf eine lokale SQLite-Datei.
///
/// Konfigurationsgrundsätze:
///  - <b>Decimal</b>: SQLite kennt kein echtes DECIMAL. Alle Geld- und Flächenwerte werden als
///    TEXT gespeichert (Round-Trip über <c>string</c>), sonst entstehen Rundungsfehler bei
///    Steuermessbeträgen. Die Umrechnung ist zentral in <c>SqliteWertKonverter</c> hinterlegt.
///  - <b>Enums</b> werden als <c>int</c> gespeichert, nicht als String: die Zahlen sind über
///    <c>Enums.cs</c> stabil vergeben und ein Umbenennen im Code darf die DB nicht brechen.
///  - <b>Nebenläufigkeit</b>: SQLite hat kein <c>rowversion</c>. EF wird so konfiguriert, dass es
///    das <see cref="GrundsteuerMeldungEntity.RowVersion"/>-Byte-Array als Nebenläufigkeitstoken
///    behandelt (IsConcurrencyToken).
/// </summary>
public class GrundsteuerDbContext : DbContext
{
    public GrundsteuerDbContext(DbContextOptions<GrundsteuerDbContext> options) : base(options)
    {
    }

    public DbSet<GrundsteuerMeldungEntity> Meldungen => Set<GrundsteuerMeldungEntity>();
    public DbSet<FlurstueckEntity> Flurstuecke => Set<FlurstueckEntity>();
    public DbSet<EigentuemerEntity> Eigentuemer => Set<EigentuemerEntity>();
    public DbSet<ValidierungsHinweisEntity> Hinweise => Set<ValidierungsHinweisEntity>();
    public DbSet<StatusVerlaufEntity> StatusVerlauf => Set<StatusVerlaufEntity>();
    public DbSet<ElsterUebermittlungEntity> Uebermittlungen => Set<ElsterUebermittlungEntity>();
    public DbSet<FinanzamtEntity> Finanzaemter => Set<FinanzamtEntity>();
    public DbSet<PlzZuordnungEntity> PlzZuordnungen => Set<PlzZuordnungEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // =====================================================================================
        //  Grundsteuermeldung - das Aggregat
        // =====================================================================================
        b.Entity<GrundsteuerMeldungEntity>(e =>
        {
            e.ToTable("Meldungen");
            e.HasKey(x => x.Id);

            e.Property(x => x.RowVersion)
                .IsConcurrencyToken()
                .HasMaxLength(16);

            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Bundesland).HasConversion<int>();
            e.Property(x => x.Modell).HasConversion<int>();
            e.Property(x => x.Erklaerungsart).HasConversion<int>();
            e.Property(x => x.Grundstuecksart).HasConversion<int>();
            e.Property(x => x.Wohnlage).HasConversion<int>();

            e.Property(x => x.Gemarkung).HasMaxLength(120);
            e.Property(x => x.Bundesfinanzamtsnummer).HasMaxLength(4);
            e.Property(x => x.Aktenzeichen).HasMaxLength(40);
            e.Property(x => x.AktenzeichenElster).HasMaxLength(20);
            e.Property(x => x.Steuernummer).HasMaxLength(20);

            // Geld- und Flächenwerte: TEXT, damit SQLite die Dezimalstellen nicht verliert.
            e.Property(x => x.Grundstuecksflaeche).HasColumnType("TEXT");
            e.Property(x => x.Wohnflaeche).HasColumnType("TEXT");
            e.Property(x => x.Nutzflaeche).HasColumnType("TEXT");
            e.Property(x => x.Bodenrichtwert).HasColumnType("TEXT");
            e.Property(x => x.DurchschnittlicherBodenrichtwert).HasColumnType("TEXT");
            e.Property(x => x.FestgestellterMessbetrag).HasColumnType("TEXT");

            // Lageadresse: gehört exklusiv zur Meldung -> Owned Type, gleiche Tabelle.
            e.OwnsOne(x => x.Lage, lage =>
            {
                lage.Property(p => p.Strasse).HasColumnName("Lage_Strasse").HasMaxLength(120).HasDefaultValue(string.Empty);
                lage.Property(p => p.Hausnummer).HasColumnName("Lage_Hausnummer").HasMaxLength(20).HasDefaultValue(string.Empty);
                lage.Property(p => p.HausnummerZusatz).HasColumnName("Lage_HausnummerZusatz").HasMaxLength(20);
                lage.Property(p => p.Postleitzahl).HasColumnName("Lage_Postleitzahl").HasMaxLength(10).HasDefaultValue(string.Empty);
                lage.Property(p => p.Ort).HasColumnName("Lage_Ort").HasMaxLength(120).HasDefaultValue(string.Empty);
                lage.Property(p => p.Ortsteil).HasColumnName("Lage_Ortsteil").HasMaxLength(120);
                lage.Property(p => p.Land).HasColumnName("Lage_Land").HasMaxLength(80);
            });

            // Berechnungsergebnis: ebenfalls Owned Type, wird als Ganzes ersetzt.
            e.OwnsOne(x => x.Berechnung, rechnung =>
            {
                rechnung.Property(p => p.Modell).HasColumnName("Berechnung_Modell").HasConversion<int>();
                rechnung.Property(p => p.AequivalenzbetragBoden).HasColumnName("Berechnung_AequivalenzBoden").HasColumnType("TEXT");
                rechnung.Property(p => p.AequivalenzbetragGebaeude).HasColumnName("Berechnung_AequivalenzGebaeude").HasColumnType("TEXT");
                rechnung.Property(p => p.Flaechenbetrag).HasColumnName("Berechnung_Flaechenbetrag").HasColumnType("TEXT");
                rechnung.Property(p => p.Ausgangsbetrag).HasColumnName("Berechnung_Ausgangsbetrag").HasColumnType("TEXT");
                rechnung.Property(p => p.LageFaktor).HasColumnName("Berechnung_LageFaktor").HasColumnType("TEXT");
                rechnung.Property(p => p.Grundsteuerwert).HasColumnName("Berechnung_Grundsteuerwert").HasColumnType("TEXT");
                rechnung.Property(p => p.Steuermesszahl).HasColumnName("Berechnung_Steuermesszahl").HasColumnType("TEXT");
                rechnung.Property(p => p.Steuermessbetrag).HasColumnName("Berechnung_Steuermessbetrag").HasColumnType("TEXT");
                rechnung.Property(p => p.Berechnungsweg).HasColumnName("Berechnung_Berechnungsweg").HasMaxLength(400);
            });

            e.HasMany(x => x.Flurstuecke)
                .WithOne(x => x.GrundsteuerMeldung!)
                .HasForeignKey(x => x.GrundsteuerMeldungId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Eigentuemer)
                .WithOne(x => x.GrundsteuerMeldung!)
                .HasForeignKey(x => x.GrundsteuerMeldungId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Hinweise)
                .WithOne(x => x.GrundsteuerMeldung!)
                .HasForeignKey(x => x.GrundsteuerMeldungId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Verlauf)
                .WithOne(x => x.GrundsteuerMeldung!)
                .HasForeignKey(x => x.GrundsteuerMeldungId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Uebermittlungen)
                .WithOne(x => x.GrundsteuerMeldung!)
                .HasForeignKey(x => x.GrundsteuerMeldungId)
                .OnDelete(DeleteBehavior.Cascade);

            // Das Dashboard filtert/sortiert nach diesen Feldern.
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ZuletztGeaendertAm);
            e.HasIndex(x => x.Aktenzeichen);
        });

        // =====================================================================================
        //  Kindtabellen
        // =====================================================================================
        b.Entity<FlurstueckEntity>(e =>
        {
            e.ToTable("Flurstuecke");
            e.HasKey(x => x.Id);
            e.Property(x => x.Gemarkung).HasMaxLength(120).HasDefaultValue(string.Empty);
            e.Property(x => x.Gemarkungsnummer).HasMaxLength(20);
            e.Property(x => x.Flur).HasMaxLength(20);
            e.Property(x => x.Zaehler).HasMaxLength(20);
            e.Property(x => x.Nenner).HasMaxLength(20);
            e.Property(x => x.Flaeche).HasColumnType("TEXT");
            e.Property(x => x.Anteil).HasColumnType("TEXT");
            e.HasIndex(x => new { x.GrundsteuerMeldungId, x.Reihenfolge });
        });

        b.Entity<EigentuemerEntity>(e =>
        {
            e.ToTable("Eigentuemer");
            e.HasKey(x => x.Id);
            e.Property(x => x.Art).HasConversion<int>();
            e.Property(x => x.Anrede).HasConversion<int>();
            e.Property(x => x.Name).HasMaxLength(160).HasDefaultValue(string.Empty);
            e.Property(x => x.Vorname).HasMaxLength(80);
            e.Property(x => x.IdNrHash).HasMaxLength(64);
            e.Property(x => x.IdNrLetzteDrei).HasMaxLength(3);
            e.Property(x => x.Steuernummer).HasMaxLength(20);
            e.Property(x => x.Strasse).HasMaxLength(120).HasDefaultValue(string.Empty);
            e.Property(x => x.Hausnummer).HasMaxLength(20).HasDefaultValue(string.Empty);
            e.Property(x => x.Postleitzahl).HasMaxLength(10).HasDefaultValue(string.Empty);
            e.Property(x => x.Ort).HasMaxLength(120).HasDefaultValue(string.Empty);
            e.Property(x => x.Land).HasMaxLength(80);
            e.Property(x => x.Anteil).HasColumnType("TEXT");
            e.HasIndex(x => new { x.GrundsteuerMeldungId, x.Reihenfolge });
        });

        b.Entity<ValidierungsHinweisEntity>(e =>
        {
            e.ToTable("Hinweise");
            e.HasKey(x => x.Id);
            e.Property(x => x.Schwere).HasConversion<int>();
            e.Property(x => x.Feld).HasMaxLength(120).HasDefaultValue(string.Empty);
            e.Property(x => x.Meldung).HasMaxLength(1000).HasDefaultValue(string.Empty);
            e.Property(x => x.Rechtsgrundlage).HasMaxLength(200);
            e.Property(x => x.Vorschlag).HasMaxLength(400);
        });

        b.Entity<StatusVerlaufEntity>(e =>
        {
            e.ToTable("StatusVerlauf");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Ausloeser).HasMaxLength(200).HasDefaultValue(string.Empty);
            e.Property(x => x.Bemerkung).HasMaxLength(1000);
            e.HasIndex(x => new { x.GrundsteuerMeldungId, x.ZeitpunktUtc });
        });

        b.Entity<ElsterUebermittlungEntity>(e =>
        {
            e.ToTable("Uebermittlungen");
            e.HasKey(x => x.Id);
            e.Property(x => x.Referenz).HasMaxLength(120);
            e.Property(x => x.FehlerCode).HasMaxLength(40);
            e.Property(x => x.Fehlertext).HasMaxLength(1000);
            e.OwnsOne(x => x.Berechnung, rechnung =>
            {
                rechnung.Property(p => p.Modell).HasColumnName("Berechnung_Modell").HasConversion<int>();
                rechnung.Property(p => p.Steuermessbetrag).HasColumnName("Berechnung_Steuermessbetrag").HasColumnType("TEXT");
                rechnung.Property(p => p.Grundsteuerwert).HasColumnName("Berechnung_Grundsteuerwert").HasColumnType("TEXT");
                rechnung.Property(p => p.LageFaktor).HasColumnName("Berechnung_LageFaktor").HasColumnType("TEXT");
            });
            e.HasIndex(x => new { x.GrundsteuerMeldungId, x.VersuchtAmUtc });
        });

        // =====================================================================================
        //  Stammdaten-Caches
        // =====================================================================================
        b.Entity<FinanzamtEntity>(e =>
        {
            e.ToTable("Finanzaemter");
            e.HasKey(x => x.Bundesfinanzamtsnummer);
            e.Property(x => x.Bundesfinanzamtsnummer).HasMaxLength(4);
            e.Property(x => x.Name).HasMaxLength(200).HasDefaultValue(string.Empty);
            e.Property(x => x.Bundesland).HasConversion<int>();
            e.Property(x => x.Ort).HasMaxLength(120);
            e.HasIndex(x => x.Bundesland);
        });

        b.Entity<PlzZuordnungEntity>(e =>
        {
            e.ToTable("PlzZuordnungen");
            e.HasKey(x => x.Postleitzahl);
            e.Property(x => x.Postleitzahl).HasMaxLength(10);
            e.Property(x => x.Ort).HasMaxLength(120).HasDefaultValue(string.Empty);
            e.Property(x => x.Bundesland).HasConversion<int>();
        });
    }
}
