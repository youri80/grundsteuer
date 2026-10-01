using Microsoft.EntityFrameworkCore;

namespace GrundsteuerPortal.Persistence;

/// <summary>
/// Schema-Nachzug für eine bestehende SQLite-Datenbank, die ohne EF-Migrationen ausgeliefert wurde.
///
/// <see cref="GrundsteuerDbContext"/> nutzt <c>EnsureCreatedAsync</c> - das legt auf einer bereits
/// existierenden Datenbank KEINE neuen Tabellen an (siehe ef-core-runtime-pitfalls § 8). Für den
/// wirtschaftseinheit-zentrischen Umbau müssen deshalb drei neue Tabellen und drei neue Spalten an
/// <c>Meldungen</c> gezielt nachgezogen werden.
///
/// Alles hier ist idempotent: ein mehrfacher Start oder ein Abbruch mitten im Nachzug darf den
/// zweiten Lauf nicht scheitern lassen.
/// </summary>
internal static class SchemaNachzug
{
    /// <summary>Legt fehlende Tabellen des neuen Modells an und ergänzt fehlende Spalten.</summary>
    public static async Task ZieheNachAsync(GrundsteuerDbContext db, CancellationToken ct = default)
    {
        // Neue Tabellen: DDL aus dem Modell ziehen (nicht abschreiben), damit sie nicht von der
        // Entität abweichen kann.
        foreach (var tabelle in new[]
        {
            "Personen",
            "Wirtschaftseinheiten",
            "EinheitFlurstuecke",
            "EinheitEigentuemer"
        })
        {
            await StelleTabelleSicherAsync(db, tabelle, ct);
        }

        // Neue Spalten an der bestehenden Tabelle Meldungen.
        await StelleSpalteSicherAsync(db, "Meldungen", "WirtschaftseinheitId", "TEXT", ct);
        await StelleSpalteSicherAsync(db, "Meldungen", "MeldendePersonId", "TEXT", ct);
        await StelleSpalteSicherAsync(db, "Meldungen", "MeldendePersonName", "TEXT", ct);
    }

    private static async Task<bool> TabelleExistiertAsync(GrundsteuerDbContext db, string tabelle,
        CancellationToken ct)
    {
        var anzahl = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = {0}",
                tabelle)
            .SingleAsync(ct);
        return anzahl > 0;
    }

    private static async Task StelleTabelleSicherAsync(GrundsteuerDbContext db, string tabelle,
        CancellationToken ct)
    {
        if (await TabelleExistiertAsync(db, tabelle, ct)) return;

        // DDL aus dem Modell erzeugen und nur die Statements für diese Tabelle samt Indizes ziehen.
        var skript = db.Database.GenerateCreateScript();
        foreach (var stmt in AnweisungenFuer(skript, tabelle))
        {
            await db.Database.ExecuteSqlRawAsync(MachIdempotent(stmt), ct);
        }
    }

    private static async Task StelleSpalteSicherAsync(GrundsteuerDbContext db, string tabelle,
        string spalte, string typ, CancellationToken ct)
    {
        var vorhanden = await db.Database.SqlQueryRaw<string>(
                "SELECT name AS Value FROM pragma_table_info({0})", tabelle)
            .AnyAsync(n => n == spalte, ct);

        if (!vorhanden)
        {
            // Bezeichner stammen aus einer festen internen Whitelist (Aufrufer übergeben Literale),
            // nie aus Nutzereingaben. DDL-Bezeichner lassen sich in SQLite nicht parametrisieren
            // (sonst würde EF ein '@p0' einsetzen); die Interpolation ist daher hier sicher.
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE \"{tabelle}\" ADD COLUMN \"{spalte}\" {typ} NULL", ct);
#pragma warning restore EF1002
        }
    }

    /// <summary>Zieht aus dem GenerateCreateScript nur die CREATE-Statements, die zur Tabelle gehören.</summary>
    private static IEnumerable<string> AnweisungenFuer(string skript, string tabelle)
    {
        // Statements sind durch ';' getrennt; wir nehmen CREATE TABLE und CREATE INDEX auf die Tabelle.
        var name = $"\"{tabelle}\"";
        foreach (var roh in skript.Split(';'))
        {
            var stmt = roh.Trim();
            if (stmt.Length == 0) continue;

            if (stmt.Contains($"CREATE TABLE {name}", StringComparison.Ordinal)
                || (stmt.Contains("CREATE INDEX", StringComparison.Ordinal)
                    && stmt.Contains($" ON {name}", StringComparison.Ordinal)))
            {
                yield return stmt;
            }
        }
    }

    /// <summary>Macht CREATE-Statements wiederholbar (IF NOT EXISTS).</summary>
    private static string MachIdempotent(string stmt)
    {
        if (stmt.StartsWith("CREATE TABLE ", StringComparison.Ordinal)
            && !stmt.Contains("IF NOT EXISTS", StringComparison.Ordinal))
        {
            return stmt.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.Ordinal);
        }

        if (stmt.StartsWith("CREATE INDEX ", StringComparison.Ordinal)
            && !stmt.Contains("IF NOT EXISTS", StringComparison.Ordinal))
        {
            return stmt.Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ", StringComparison.Ordinal);
        }

        return stmt;
    }
}
