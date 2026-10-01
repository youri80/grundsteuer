using GrundsteuerPortal.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrundsteuerPortal.Persistence;

/// <summary>
/// Registrierung der Persistenz in der DI. Absichtlich als Erweiterungsmethode, damit die
/// Web-Schicht keine EF-Typen kennen muss.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registriert den SQLite-Kontext und das Repository.
    ///
    /// <paramref name="datenbankPfad"/> ist der Pfad zur SQLite-Datei. Ein <c>:memory:</c>-Wert
    /// wird für Tests unterstützt (siehe Testprojekt).
    /// </summary>
    public static IServiceCollection AddGrundsteuerPersistenz(
        this IServiceCollection services, string datenbankPfad)
    {
        var istInMemory = datenbankPfad.Contains(":memory:", StringComparison.OrdinalIgnoreCase)
                          || datenbankPfad.StartsWith("DataSource=:memory:", StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<GrundsteuerDbContext>((sp, optionen) =>
        {
            if (istInMemory)
            {
                // Geteilte In-Memory-DB: die Verbindung muss offen bleiben, sonst ist die DB weg,
                // sobald der erste Kontext geschlossen wird.
                var verbindung = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
                verbindung.Open();
                optionen.UseSqlite(verbindung);
                return;
            }

            var ziel = datenbankPfad;
            if (!ziel.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                var verzeichnis = Path.GetDirectoryName(Path.GetFullPath(ziel));
                if (!string.IsNullOrWhiteSpace(verzeichnis))
                    Directory.CreateDirectory(verzeichnis);
                ziel = $"Data Source={ziel}";
            }

            optionen.UseSqlite(ziel);
        }, ServiceLifetime.Scoped);

        // Scoped: je Blazor-Circuit eine Instanz (siehe Klassenkommentar des Repositories).
        services.AddScoped<IGrundsteuerRepository, GrundsteuerRepository>();

        return services;
    }
}
