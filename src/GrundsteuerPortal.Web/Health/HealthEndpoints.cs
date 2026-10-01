using GrundsteuerPortal.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GrundsteuerPortal.Web.Health;

/// <summary>
/// Health-Endpunkte für Container-Betrieb.
///
/// <b>Warum Liveness und Readiness getrennt sind:</b> Ein Datenbankausfall darf den Container
/// nicht neu starten lassen - ein Neustart löst das Problem nicht und macht es schlimmer, weil
/// der Dienst dann in einer Schleife hängt. Der <c>HEALTHCHECK</c> im Dockerfile fragt deshalb
/// <c>/health/live</c> ab (nur "läuft der Prozess"), während ein Orchestrator für die
/// Verkehrsfreigabe <c>/health/ready</c> verwendet.
///
/// Beide Routen sind bewusst ohne Authentifizierung: hinter <c>[Authorize]</c> käme ein 302
/// zurück und der Check würde immer fehlschlagen.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new
        {
            status = "healthy",
            pruefung = "liveness",
            zeitpunktUtc = DateTime.UtcNow
        }));

        app.MapGet("/health/ready", async (
            GrundsteuerDbContext db,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Health.Readiness");
            try
            {
                // Echter Datenbankzugriff: eine Readiness-Prüfung, die nur den Prozess ansieht,
                // meldet "bereit", während jede Anfrage an der DB scheitert.
                var erreichbar = await db.Database.CanConnectAsync(ct);
                if (!erreichbar)
                {
                    logger.LogError("Readiness fehlgeschlagen: SQLite-Datenbank nicht erreichbar.");
                    return Results.Json(new
                    {
                        status = "unhealthy",
                        pruefung = "readiness",
                        grund = "SQLite-Datenbank nicht erreichbar",
                        zeitpunktUtc = DateTime.UtcNow
                    }, statusCode: StatusCodes.Status503ServiceUnavailable);
                }

                var anzahl = await db.Meldungen.CountAsync(ct);

                return Results.Ok(new
                {
                    status = "healthy",
                    pruefung = "readiness",
                    datenbank = "erreichbar",
                    meldungen = anzahl,
                    zeitpunktUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Readiness fehlgeschlagen: {Meldung}", ex.Message);
                return Results.Json(new
                {
                    status = "unhealthy",
                    pruefung = "readiness",
                    grund = ex.Message,
                    zeitpunktUtc = DateTime.UtcNow
                }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });
    }
}
