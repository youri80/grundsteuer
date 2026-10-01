using Microsoft.AspNetCore.DataProtection;

namespace GrundsteuerPortal.Web.Health;

/// <summary>
/// Persistiert die DataProtection-Schlüssel, wenn ein Verzeichnis konfiguriert ist.
///
/// <b>Warum das im Container nötig ist:</b> Blazor Server und Antiforgery verschlüsseln ihre
/// Cookies mit DataProtection. Ohne persistierte Schlüssel erzeugt jeder neue Container einen
/// frischen Schlüsselbund - alle bestehenden Cookies werden ungültig, der Circuit bricht ab und
/// der Nutzer landet ohne verständliche Meldung wieder auf der Anmeldung. Das ist besonders
/// tückisch, weil ein laufender Container einwandfrei funktioniert und der Fehler erst beim
/// nächsten Deployment auftritt.
///
/// Ist kein Pfad konfiguriert, bleibt das Standardverhalten (Schlüssel im Benutzerprofil) - dann
/// funktioniert es weiterhin, aber ohne Neustartfestigkeit.
/// </summary>
public static class DataProtectionSetup
{
    /// <summary>
    /// Aktiviert die Persistenz, falls <c>Datenbank:DataProtectionPfad</c> gesetzt ist.
    /// Gibt zurück, ob persistiert wird (für die Logausgabe beim Start).
    /// </summary>
    public static bool Konfiguriere(WebApplicationBuilder builder)
    {
        var pfad = builder.Configuration["Datenbank:DataProtectionPfad"];
        if (string.IsNullOrWhiteSpace(pfad))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(pfad);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Kein harter Abbruch: die App läuft weiter, nur ohne Neustartfestigkeit. Das muss
            // aber sichtbar sein - sonst sucht man den Cookie-Fehler später im falschen Bauteil.
            builder.Logging.AddConsole();
            Console.Error.WriteLine(
                $"WARNUNG: DataProtection-Verzeichnis '{pfad}' ist nicht nutzbar ({ex.Message}). "
                + "Schlüssel werden nicht persistiert - alle Cookies werden bei einem Neustart ungültig.");
            return false;
        }

        var schluessel = builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(pfad));

        // Stabiler Anwendungsname: ohne ihn leitet DataProtection den Namen aus dem
        // Content-Root-Pfad ab, der im Container wechseln kann.
        var anwendungsname = builder.Configuration["Datenbank:DataProtectionApplicationName"];
        if (!string.IsNullOrWhiteSpace(anwendungsname))
        {
            schluessel.SetApplicationName(anwendungsname);
        }

        return true;
    }
}
