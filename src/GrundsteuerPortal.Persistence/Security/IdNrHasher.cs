using System.Security.Cryptography;
using System.Text;

namespace GrundsteuerPortal.Persistence.Security;

/// <summary>
/// Pseudonymisiert die Steueridentifikationsnummer (§ 139 AO) für die lokale Ablage.
///
/// Warum überhaupt? Die IdNr ist ein Identifikationsmerkmal und darf nicht im Klartext in einer
/// unverschlüsselten SQLite-Datei liegen. Die Datenbank braucht sie auch nicht im Klartext: für die
/// ELSTER-Übermittlung wird sie im Formular eingegeben bzw. aus der API geladen.
///
/// Gespeichert werden zwei Dinge:
///  1. ein SHA-256-Hash über (IdNr + Salt) - erlaubt den Vergleich "schon erfasst?", ohne die
///     Nummer zu kennen, und ist wegen des Zufalls-Salts nicht rückrechenbar;
///  2. die letzten drei Stellen im Klartext - damit der Nutzer in der Oberfläche erkennt,
///     welche Nummer hinterlegt ist ("…471"), was die Wiedererkennung praktikabel macht.
///
/// Die Prüfziffernprüfung der IdNr (nach ELSTER-Vorgabe, siehe <c>ElsterFormate</c>) bleibt
/// unberührt: sie arbeitet auf dem Eingabewert, nicht auf dem Hash.
/// </summary>
public static class IdNrHasher
{
    /// <summary>Anwendungsweiter Salt. Konfigurierbar, damit ein Datenbankdurchzug nicht
    /// versehentlich portable Hashes erzeugt.</summary>
    public static string Salt { get; set; } = "GrundsteuerPortal/IdNr/v1";

    /// <summary>SHA-256-Hex-Hash der normalisierten IdNr.</summary>
    public static string? Hashe(string? idNr)
    {
        if (string.IsNullOrWhiteSpace(idNr)) return null;

        var normalisiert = Normalisiere(idNr);
        if (normalisiert.Length == 0) return null;

        var bytes = Encoding.UTF8.GetBytes(Mischen(normalisiert));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    /// <summary>Letzte drei Stellen der normalisierten IdNr, für die Wiedererkennung in der UI.</summary>
    public static string? LetzteDrei(string? idNr)
    {
        var normalisiert = Normalisiere(idNr);
        return normalisiert.Length >= 3 ? normalisiert[^3..] : null;
    }

    private static string Normalisiere(string? eingabe) =>
        string.IsNullOrWhiteSpace(eingabe)
            ? string.Empty
            : new string(eingabe.Where(char.IsDigit).ToArray());

    // Salt und Wert verschränkt mischen (Zickzack), damit das Salt nicht als Präfix im
    // Eingabeblock steht - verhindert triviale Längen-Rückschlüsse und Rainbow-Table-Splitting.
    private static string Mischen(string wert)
    {
        var sb = new StringBuilder(wert.Length + Salt.Length);
        var laenge = Math.Max(wert.Length, Salt.Length);
        for (var i = 0; i < laenge; i++)
        {
            if (i < Salt.Length) sb.Append(Salt[i]);
            if (i < wert.Length) sb.Append(wert[i]);
        }
        return sb.ToString();
    }
}
