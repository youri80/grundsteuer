using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Testdaten;
using Microsoft.Extensions.Logging;

namespace GrundsteuerPortal.Web.Services;

/// <summary>
/// Spielt die Beispielmeldungen aus <see cref="TestdatenFactory"/> in die lokale Ablage ein.
///
/// Bewusst in der Web-Schicht und nicht im Core: der Core kennt nur die Erzeugung, nicht den
/// Speicherweg. Und bewusst als eigener Dienst statt als Methode in der Seite, damit die
/// Dublettenerkennung testbar bleibt - eine Seite lässt sich nicht ohne UI prüfen.
/// </summary>
public static class TestdatensatzDienst
{
    /// <summary>
    /// Kennzeichnet die eingespielten Datensätze. Die Finanzamt-Namen tragen diesen Zusatz,
    /// damit in der Oberfläche sofort erkennbar ist, was Testdaten sind.
    /// </summary>
    public const string Kennzeichen = "Testdaten";

    /// <summary>Ist mindestens ein Testdatensatz bereits vorhanden?</summary>
    public static bool IstBereitsEingespielt(IEnumerable<GrundsteuerUebersichtDto> vorhandene) =>
        vorhandene.Any(v => v.HauptEigentuemer?.Contains("Beispiel", StringComparison.Ordinal) == true
                            || v.Grundstuecksbezeichnung.Contains(Kennzeichen, StringComparison.Ordinal));

    /// <summary>
    /// Legt die Datensätze an, die noch fehlen. Vorhandene werden nicht angefasst und nicht
    /// dupliziert: erkannt wird ein Datensatz am Aktenzeichen bzw. an der Steuernummer.
    /// </summary>
    public static async Task<string> EinspielenAsync(IGrundsteuerApiService api,
        ILogger? logger = null, CancellationToken ct = default)
    {
        var vorhandene = await api.GetMeldungenAsync(ct);
        var bekannteNummern = vorhandene
            .Select(v => v.Aktenzeichen ?? v.Steuernummer)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.Ordinal);

        var angelegt = 0;
        var uebersprungen = 0;

        // Die Entwürfe zum Durchspielen UND die Datensätze mit Endzustand: ohne die letzteren
        // blieben die Statusfilter „Übermittelt" und „Festgestellt" im Navigationsbereich leer.
        foreach (var satz in TestdatenFactory.Alle().Concat(TestdatenFactory.MitEndzustaenden()))
        {
            var nummer = satz.Meldung.Aktenzeichen ?? satz.Meldung.Steuernummer;
            if (!string.IsNullOrWhiteSpace(nummer) && bekannteNummern.Contains(nummer!))
            {
                uebersprungen++;
                continue;
            }

            // Der Speicherweg der Oberfläche, nicht ein direkter Datenbankzugriff: damit prüft
            // dieser Aufruf gleichzeitig, dass das Speichern selbst funktioniert.
            var antwort = await api.SaveDraftAsync(satz.Meldung, ct);
            if (antwort.Erfolg)
            {
                angelegt++;
                if (!string.IsNullOrWhiteSpace(nummer)) bekannteNummern.Add(nummer!);
            }
            else
            {
                logger?.LogWarning("Testdatensatz '{Bezeichnung}' nicht gespeichert: {Grund}",
                    satz.Bezeichnung, antwort.Meldung);
            }
        }

        var text = angelegt == 0
            ? $"Alle {uebersprungen} Testdatensätze waren bereits vorhanden."
            : $"{angelegt} Testdatensätze angelegt"
              + (uebersprungen > 0 ? $", {uebersprungen} waren bereits vorhanden." : ".");

        logger?.LogInformation("Testdaten eingespielt: {Text}", text);
        return text;
    }
}
