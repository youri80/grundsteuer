using GrundsteuerPortal.Core.Domain;

namespace GrundsteuerPortal.Core.Validation;

/// <summary>
/// Zulässige Bundesfinanzamtsnummern (BUFA) je Bundesland im Grundsteuer-ELSTER-Verfahren.
/// Quelle: ELSTER-Schnittstellendokumentation "Prüfung der Steuer- und Steueridentifikationsnummer
/// sowie der Ordnungskriterien bei der Grundsteuer", Abschnitt 8.5 (Stand 02.09.2026).
///
/// Die Liste ist bewusst als Datenbereich hinterlegt, nicht als starre Werteliste: die aktuell
/// freigegebenen Finanzämter kommen zur Laufzeit von der WebAPI (IFinanzamtApi). Diese Prüfung ist
/// die Offline-/Vorprüfung, damit der Nutzer schon im Wizard und nicht erst bei ELSTER scheitert.
/// </summary>
public static class Bundesfinanzamtsnummern
{
    private sealed record Bereich(int Von, int Bis);

    private static readonly Dictionary<Bundesland, Bereich[]> _bereiche = new()
    {
        [Bundesland.BadenWuerttemberg] = new[]
        {
            new Bereich(2801, 2801), new Bereich(2804, 2812), new Bereich(2814, 2816),
            new Bereich(2818, 2823), new Bereich(2830, 2859), new Bereich(2861, 2865),
            new Bereich(2869, 2871), new Bereich(2874, 2874), new Bereich(2876, 2891),
            new Bereich(2899, 2899)
        },
        [Bundesland.Bayern] = new[]
        {
            new Bereich(9102, 9115), new Bereich(9117, 9117), new Bereich(9119, 9119),
            new Bereich(9121, 9121), new Bereich(9123, 9127), new Bereich(9131, 9132),
            new Bereich(9134, 9134), new Bereich(9138, 9142), new Bereich(9151, 9154),
            new Bereich(9156, 9157), new Bereich(9159, 9159), new Bereich(9161, 9163),
            new Bereich(9168, 9171), new Bereich(9201, 9208), new Bereich(9211, 9212),
            new Bereich(9216, 9218), new Bereich(9220, 9223), new Bereich(9227, 9231),
            new Bereich(9235, 9235), new Bereich(9241, 9241), new Bereich(9244, 9244),
            new Bereich(9247, 9249), new Bereich(9252, 9252), new Bereich(9254, 9255),
            new Bereich(9257, 9259)
        },
        [Bundesland.Berlin] = new[] { new Bereich(1100, 1199) },
        [Bundesland.Brandenburg] = new[]
        {
            new Bereich(3046, 3046), new Bereich(3048, 3053), new Bereich(3056, 3057),
            new Bereich(3061, 3062), new Bereich(3064, 3065)
        },
        [Bundesland.Bremen] = new[] { new Bereich(2457, 2457), new Bereich(2477, 2477) },
        [Bundesland.Hamburg] = new[] { new Bereich(2216, 2216) },
        [Bundesland.Hessen] = new[] { new Bereich(2601, 2647) },
        [Bundesland.MecklenburgVorpommern] = new[]
        {
            new Bereich(4072, 4072), new Bereich(4075, 4075), new Bereich(4079, 4082),
            new Bereich(4084, 4084), new Bereich(4086, 4087), new Bereich(4090, 4090)
        },
        [Bundesland.Niedersachsen] = new[]
        {
            new Bereich(2313, 2323), new Bereich(2326, 2331), new Bereich(2333, 2336),
            new Bereich(2338, 2338), new Bereich(2340, 2341), new Bereich(2343, 2361),
            new Bereich(2363, 2370), new Bereich(2375, 2376), new Bereich(2378, 2379),
            new Bereich(2388, 2388)
        },
        [Bundesland.NordrheinWestfalen] = new[]
        {
            new Bereich(5101, 5183), new Bereich(5201, 5283), new Bereich(5301, 5384)
        },
        [Bundesland.RheinlandPfalz] = new[]
        {
            new Bereich(2701, 2702), new Bereich(2706, 2706), new Bereich(2708, 2710),
            new Bereich(2719, 2719), new Bereich(2722, 2724), new Bereich(2726, 2727),
            new Bereich(2729, 2732), new Bereich(2735, 2735), new Bereich(2740, 2744)
        },
        [Bundesland.Saarland] = new[]
        {
            new Bereich(1010, 1010), new Bereich(1055, 1055), new Bereich(1060, 1060)
        },
        [Bundesland.Sachsen] = new[]
        {
            new Bereich(3202, 3202), new Bereich(3204, 3204), new Bereich(3207, 3210),
            new Bereich(3213, 3214), new Bereich(3217, 3218), new Bereich(3220, 3220),
            new Bereich(3222, 3224), new Bereich(3227, 3228), new Bereich(3232, 3232),
            new Bereich(3236, 3239)
        },
        [Bundesland.SachsenAnhalt] = new[]
        {
            new Bereich(3102, 3103), new Bereich(3105, 3108), new Bereich(3110, 3110),
            new Bereich(3112, 3112), new Bereich(3114, 3119)
        },
        [Bundesland.SchleswigHolstein] = new[] { new Bereich(2171, 2192) },
        [Bundesland.Thueringen] = new[]
        {
            new Bereich(4151, 4151), new Bereich(4154, 4157), new Bereich(4159, 4159),
            new Bereich(4161, 4162), new Bereich(4165, 4166), new Bereich(4171, 4171)
        }
    };

    /// <summary>Ist die 4-stellige Bundesfinanzamtsnummer für dieses Land im Verfahren zugelassen?</summary>
    public static bool IstZulaessig(string? bufa, Bundesland land)
    {
        if (string.IsNullOrWhiteSpace(bufa)) return false;
        if (!int.TryParse(bufa.Trim(), out var nummer)) return false;
        if (!_bereiche.TryGetValue(land, out var bereiche)) return false;
        return bereiche.Any(b => nummer >= b.Von && nummer <= b.Bis);
    }

    /// <summary>Menschenlesbare Kurzbeschreibung der zulässigen Nummern, für Hilfetexte im Formular.</summary>
    public static IReadOnlyList<(int Von, int Bis)> Bereiche(Bundesland land) =>
        _bereiche.TryGetValue(land, out var b)
            ? b.Select(x => (x.Von, x.Bis)).ToList()
            : new List<(int, int)>();

    /// <summary>Extrahiert die BUFA-Nummer aus einem elternfreien Aktenzeichen oder einer 13-stelligen Steuernummer.</summary>
    public static string? AusAktenzeichenOderSteuernummer(string? eingabe)
    {
        var z = ElsterFormate.NurZiffern(eingabe);
        return z.Length switch
        {
            13 => z[..4],
            _ => null
        };
    }
}
