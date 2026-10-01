using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Persistence;
using GrundsteuerPortal.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrundsteuerPortal.Tests;

/// <summary>
/// Testbasis für die Persistenz. Jeder Test bekommt eine <b>eigene SQLite-Datei</b> in einem
/// temporären Verzeichnis. Bewusst keine In-Memory-Datenbank: nur eine echte SQLite-Datei deckt
/// die Eigenheiten auf, die uns interessieren (Decimal-als-TEXT, Enum-als-INT, Owned Types in
/// derselben Tabelle, Nebenläufigkeitstoken).
/// </summary>
public abstract class PersistenzTestBasis : IDisposable
{
    private readonly string _verzeichnis;
    protected readonly GrundsteuerDbContext Db;
    protected readonly IGrundsteuerRepository Repo;

    protected PersistenzTestBasis()
    {
        _verzeichnis = Path.Combine(Path.GetTempPath(), "gst-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_verzeichnis);

        var optionen = new DbContextOptionsBuilder<GrundsteuerDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_verzeichnis, "test.db")}")
            .Options;

        Db = new GrundsteuerDbContext(optionen);
        Repo = new GrundsteuerRepository(Db, NullLogger<GrundsteuerRepository>.Instance);
    }

    /// <summary>Ein vollständig gefüllter, valider Meldungsentwurf.</summary>
    protected static GrundsteuerMeldungDto BeispielMeldung() => BeispielMeldungOeffentlich();

    /// <summary>Für Diagnose-Tests, die die Basisklasse nicht erben.</summary>
    internal static Core.Api.GrundsteuerMeldungDto BeispielMeldungOeffentlich() => new()
    {
        Id = Guid.Empty,
        Status = MeldungStatus.Entwurf,
        Bundesland = Bundesland.Hessen,
        Erklaerungsart = Erklaerungsart.Erstmalig,
        Hauptfeststellungszeitpunkt = 2022,
        Bundesfinanzamtsnummer = "2601",
        FinanzamtName = "Finanzamt Friedberg",
        Aktenzeichen = "60 001 0001 001 005 1",
        Gemarkung = "Echzell",
        Flur = "7",
        FlurstueckZaehler = "123",
        FlurstueckNenner = "45",
        Grundstuecksart = Grundstuecksart.Einfamilienhaus,
        Grundstuecksflaeche = 512.75m,
        Wohnflaeche = 142.5m,
        Baujahr = 1978,
        Bodenrichtwert = 245.50m,
        DurchschnittlicherBodenrichtwert = 210.00m,
        IstDenkmalgeschuetzt = false,
        Lage = new AdresseDto
        {
            Strasse = "Hauptstraße",
            Hausnummer = "12",
            Postleitzahl = "61209",
            Ort = "Echzell"
        },
        Eigentuemer =
        {
            new EigentuemerDto
            {
                Art = EigentuemerArt.NatuerlichePerson,
                Anrede = Anrede.Frau,
                Name = "Beispiel",
                Vorname = "Lyra",
                IdNummer = "12345678901",
                Strasse = "Hauptstraße",
                Hausnummer = "12",
                Postleitzahl = "61209",
                Ort = "Echzell",
                Anteil = 1m
            }
        },
        Flurstuecke =
        {
            new FlurstueckDto
            {
                Gemarkung = "Echzell",
                Flur = "7",
                Zaehler = "123",
                Nenner = "45",
                Flaeche = 512.75m,
                Anteil = 1m
            }
        }
    };

    public void Dispose()
    {
        Db.Dispose();
        try
        {
            Directory.Delete(_verzeichnis, recursive: true);
        }
        catch (IOException)
        {
            // Temporäres Verzeichnis darf liegen bleiben - der Test ist trotzdem gültig.
        }
        GC.SuppressFinalize(this);
    }
}
