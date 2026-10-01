namespace GrundsteuerPortal.Web.Health;

/// <summary>
/// Selbstprüfung der statischen Assets beim Start.
///
/// <b>Warum das nötig ist:</b> Wenn beim Container-Build die Blazor-Framework-Assets fehlen
/// (typischerweise, weil ein früher <c>restore</c> nur gegen die csproj-Dateien lief und ein
/// <c>publish --no-restore</c> das unvollständige Asset-Manifest übernommen hat), fehlt
/// <c>wwwroot/_framework/blazor.web.js</c>. Folge: Der Browser lädt kein Blazor, der Circuit kommt
/// nie zustande, die Oberfläche ist totes HTML - <b>und jede Seite liefert trotzdem HTTP 200</b>.
///
/// Diese Prüfung macht den Fehler sichtbar:
///  - beim Start als <c>ERROR</c> im Log (in diesem Zustand ist die App unbenutzbar),
///  - über <c>/health/assets</c> als 503 mit Angabe, welche Datei fehlt.
///
/// Sie testet bewusst die <b>Datei auf der Platte</b>, nicht die HTTP-Route: Im Baukontext bedient
/// <c>MapStaticAssets</c> eine fehlende Datei notfalls aus dem Quellpfad und antwortet mit 200,
/// obwohl die Datei im Publish nicht existiert. Nur der Dateitest ist verlässlich.
/// </summary>
public static class AssetSelfCheck
{
    /// <summary>Dateien, deren Fehlen die App unbenutzbar macht.</summary>
    private static readonly string[] PflichtDateien =
    {
        "wwwroot/_framework/blazor.web.js",
        "wwwroot/app.css"
    };

    /// <summary>Prüft beim Start und protokolliert das Ergebnis.</summary>
    public static void LogResult(WebApplication app, ILogger logger)
    {
        var wurzel = app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var fehlend = FehlendeDateien(wurzel);

        if (fehlend.Count == 0)
        {
            logger.LogInformation("Asset-Selbstprüfung: alle {Anzahl} Pflichtdateien vorhanden.", PflichtDateien.Length);
            return;
        }

        logger.LogError(
            "Asset-Selbstprüfung FEHLGESCHLAGEN: {Anzahl} Datei(en) fehlen im Publish: {Fehlend}. "
            + "Die Oberfläche wird als totes HTML ausgeliefert (kein Blazor-Circuit), obwohl alle "
            + "Seiten HTTP 200 liefern. Ursache ist in der Regel ein 'publish --no-restore' nach "
            + "einem Restore, der nur die csproj-Dateien gesehen hat - siehe Dockerfile.",
            fehlend.Count, string.Join(", ", fehlend));
    }

    /// <summary>Registriert <c>GET /health/assets</c> (200 = alles da, 503 = etwas fehlt).</summary>
    public static void MapAssetCheck(this WebApplication app)
    {
        app.MapGet("/health/assets", (IWebHostEnvironment env) =>
        {
            var wurzel = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var fehlend = FehlendeDateien(wurzel);

            var geprueft = PflichtDateien.Select(p => new
            {
                pfad = "/" + p.Replace("wwwroot/", string.Empty).Replace('\\', '/'),
                imPublish = File.Exists(Path.Combine(wurzel, p.Replace("wwwroot/", string.Empty)))
            }).ToList();

            if (fehlend.Count == 0)
            {
                return Results.Ok(new { status = "healthy", geprueft });
            }

            return Results.Json(new
            {
                status = "unhealthy",
                grund = "Blazor-Framework-Assets fehlen im Publish - die Oberfläche ist totes HTML.",
                fehlend,
                geprueft
            }, statusCode: StatusCodes.Status503ServiceUnavailable);
        });
    }

    private static List<string> FehlendeDateien(string webWurzel) =>
        PflichtDateien
            .Where(p => !File.Exists(Path.Combine(webWurzel, p.Replace("wwwroot/", string.Empty))))
            .ToList();
}
