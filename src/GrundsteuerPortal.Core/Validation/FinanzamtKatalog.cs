using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;

namespace GrundsteuerPortal.Core.Validation;

/// <summary>
/// Erzeugt die Auswahlliste der Finanzämter aus dem hinterlegten Bereichskatalog.
///
/// <b>Warum es diese Klasse gibt:</b> Die Bundesfinanzamtsnummern sind in
/// <see cref="Bundesfinanzamtsnummern"/> als Bereiche hinterlegt - die Prüfung kannte sie also,
/// die Auswahlliste aber nicht. Ohne eine API blieb die Finanzamt-Suche in Schritt 1 deshalb leer
/// und der Wizard war nicht bedienbar, obwohl die zulässigen Nummern bekannt sind.
///
/// Die Liste enthält ausschließlich Nummern, die <see cref="Bundesfinanzamtsnummern.IstZulaessig"/>
/// akzeptiert: eine Nummer, die die Prüfung später ablehnt, darf gar nicht erst zur Auswahl stehen.
/// In der echten Umgebung füllt die ELSTER-WebAPI diesen Katalog mit amtlichen Namen; hier stehen
/// neutrale Bezeichnungen mit dem Hinweis auf den Testbetrieb.
/// </summary>
public static class FinanzamtKatalog
{
    /// <summary>Obergrenze je Land - verhindert, dass die Auswahlliste unübersichtlich wird.</summary>
    private const int MaxJeLand = 400;

    /// <summary>Alle zulässigen Finanzämter eines Bundeslandes, aufsteigend nach Nummer.</summary>
    public static List<FinanzamtDto> Erzeuge(Bundesland land)
    {
        var liste = new List<FinanzamtDto>();
        var info = BundeslandKatalog.Fuer(land);

        foreach (var (von, bis) in Bundesfinanzamtsnummern.Bereiche(land))
        {
            for (var nummer = von; nummer <= bis && liste.Count < MaxJeLand; nummer++)
            {
                var bufa = nummer.ToString("D4");

                // Doppelte Prüfung: die Quelle darf nichts liefern, was die Prüfung ablehnt.
                if (!Bundesfinanzamtsnummern.IstZulaessig(bufa, land)) continue;

                liste.Add(new FinanzamtDto
                {
                    Bundesfinanzamtsnummer = bufa,
                    Name = $"{land.AnzeigeName()} – Finanzamt {bufa}",
                    Bundesland = land,
                    Ort = info.FinanzamtNummern.Length > 0 ? null : null
                });
            }
        }

        // Bei bekannten Einzelfällen die amtliche Bezeichnung voranstellen, damit die Liste im
        // Testbetrieb einen sichtbaren Wiedererkennungspunkt hat.
        var bekannte = BekannteFinanzaemter(land);
        foreach (var eintrag in bekannte.OrderByDescending(e => e.Name.StartsWith("Finanzamt", StringComparison.Ordinal)))
        {
            var vorhanden = liste.FirstOrDefault(f => f.Bundesfinanzamtsnummer == eintrag.Bundesfinanzamtsnummer);
            if (vorhanden is not null)
            {
                vorhanden.Name = eintrag.Name;
                vorhanden.Ort = eintrag.Ort;
            }
        }

        return liste.OrderBy(f => f.Bundesfinanzamtsnummer, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Finanzämter mit bekanntem Sitz, passend zum jeweiligen Bundesland. Bewusst kurz gehalten:
    /// es sind die Orte, die zu den hinterlegten Postleitzahl-Stichproben passen, damit die
    /// Auswahl in der Oberfläche plausibel wirkt.
    /// </summary>
    private static IEnumerable<FinanzamtDto> BekannteFinanzaemter(Bundesland land) => land switch
    {
        Bundesland.Hessen => new[]
        {
            Amt("2601", "Finanzamt Friedberg", land, "Friedberg (Hessen)"),
            Amt("2602", "Finanzamt Bad Nauheim", land, "Bad Nauheim"),
            Amt("2610", "Finanzamt Gießen", land, "Gießen"),
            Amt("2616", "Finanzamt Frankfurt am Main I", land, "Frankfurt am Main")
        },
        Bundesland.Hamburg => new[]
        {
            Amt("2216", "Finanzamt Hamburg 16 (Bewertung)", land, "Hamburg")
        },
        Bundesland.Bayern => new[]
        {
            Amt("9102", "Finanzamt München", land, "München"),
            Amt("9104", "Finanzamt München-Abteilung Grundsteuer", land, "München")
        },
        Bundesland.BadenWuerttemberg => new[]
        {
            Amt("2801", "Finanzamt Stuttgart", land, "Stuttgart"),
            Amt("2804", "Finanzamt Stuttgart-Körperschaften", land, "Stuttgart")
        },
        Bundesland.Niedersachsen => new[]
        {
            Amt("2313", "Finanzamt Hannover-Mitte", land, "Hannover"),
            Amt("2314", "Finanzamt Hannover-Nord", land, "Hannover")
        },
        Bundesland.NordrheinWestfalen => new[]
        {
            Amt("5101", "Finanzamt Düsseldorf-Mitte", land, "Düsseldorf"),
            Amt("5102", "Finanzamt Düsseldorf-Süd", land, "Düsseldorf")
        },
        Bundesland.Berlin => new[]
        {
            Amt("1100", "Finanzamt Berlin Mitte/Tiergarten", land, "Berlin"),
            Amt("1101", "Finanzamt Berlin Wedding", land, "Berlin")
        },
        Bundesland.Sachsen => new[]
        {
            Amt("3202", "Finanzamt Dresden-Süd", land, "Dresden"),
            Amt("3204", "Finanzamt Dresden-Nord", land, "Dresden")
        },
        _ => Array.Empty<FinanzamtDto>()
    };

    private static FinanzamtDto Amt(string bufa, string name, Bundesland land, string ort) => new()
    {
        Bundesfinanzamtsnummer = bufa,
        Name = name,
        Bundesland = land,
        Ort = ort
    };
}
