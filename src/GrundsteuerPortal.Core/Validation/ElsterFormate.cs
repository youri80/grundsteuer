using System.Globalization;
using System.Text;
using GrundsteuerPortal.Core.Domain;

namespace GrundsteuerPortal.Core.Validation;

/// <summary>Ergebnis einer Formatprüfung - feldgenau, damit die UI den Hinweis am richtigen Feld zeigt.</summary>
public sealed record FormatPruefung(bool IstGueltig, string? Meldung = null, string? Vorschlag = null,
    string? Normalisiert = null)
{
    public static readonly FormatPruefung Gueltig = new(true);
    public static FormatPruefung Ungueltig(string meldung, string? vorschlag = null) => new(false, meldung, vorschlag);
}

/// <summary>
/// Autoritative Format- und Prüfziffernprüfung für deutsche Steuer-Ordnungskriterien.
/// Quelle: "Prüfung der Steuer- und Steueridentifikationsnummer sowie der Ordnungskriterien bei der
/// Grundsteuer" (Bayerisches Landesamt für Steuern, Stand 02.09.2026) sowie § 139 AO.
///
/// Bewusst reine, statische Klasse ohne Abhängigkeiten: damit ist jede Regel ohne UI und ohne
/// laufende Anwendung testbar (siehe GrundsteuerPortal.Tests). Die UI ruft ausschließlich hier hinein
/// und leitet Button-Freigaben daraus ab - niemals aus MudForm.IsValid.
/// </summary>
public static class ElsterFormate
{
    // -----------------------------------------------------------------------------------------
    // Steueridentifikationsnummer (11-stellig, § 139 AO)
    // -----------------------------------------------------------------------------------------

    /// <summary>Prüft die Steueridentifikationsnummer inklusive Struktur- und Prüfziffernkriterien.</summary>
    public static FormatPruefung PruefeIdNr(string? idNr)
    {
        if (string.IsNullOrWhiteSpace(idNr))
            return FormatPruefung.Ungueltig("Die Identifikationsnummer fehlt.");

        var ziffern = NurZiffern(idNr);
        if (ziffern.Length != 11)
            return FormatPruefung.Ungueltig($"Die Identifikationsnummer muss 11 Ziffern haben (eingegeben: {ziffern.Length}).");

        var ersteZehn = ziffern[..10];
        var pruefziffer = ziffern[10] - '0';

        // Kriterium: genau eine Ziffer kommt doppelt oder dreifach vor.
        var gruppen = ersteZehn.GroupBy(c => c).Select(g => g.Count()).ToList();
        var doppeltOderDreifach = gruppen.Count(g => g == 2 || g == 3);
        if (doppeltOderDreifach != 1 || gruppen.Any(g => g > 3))
            return FormatPruefung.Ungueltig(
                "In den ersten 10 Stellen muss genau eine Ziffer doppelt oder dreifach vorkommen.");

        // Kriterium: drei gleiche Ziffern dürfen nicht direkt aufeinander folgen.
        for (var i = 0; i + 2 < ersteZehn.Length; i++)
        {
            if (ersteZehn[i] == ersteZehn[i + 1] && ersteZehn[i] == ersteZehn[i + 2])
                return FormatPruefung.Ungueltig("Drei gleiche Ziffern dürfen nicht aufeinander folgen.");
        }

        var berechnet = BerechneIdNrPruefziffer(ersteZehn);
        if (berechnet != pruefziffer)
            return FormatPruefung.Ungueltig("Die Prüfziffer der Identifikationsnummer stimmt nicht.");

        return new FormatPruefung(true, Normalisiert: ziffern);
    }

    /// <summary>Prüfziffer der IdNr nach dem Verfahren des ITZ Bund (n = 11, m = 10).</summary>
    public static int BerechneIdNrPruefziffer(string ersteZehnZiffern)
    {
        const int n = 11, m = 10;
        var produkt = m;
        foreach (var c in ersteZehnZiffern)
        {
            var summe = ((c - '0') + produkt) % m;
            if (summe == 0) summe = m;
            produkt = (2 * summe) % n;
        }
        var pruefziffer = n - produkt;
        return pruefziffer == 10 ? 0 : pruefziffer;
    }

    // -----------------------------------------------------------------------------------------
    // Steuernummer im ELSTER-Steuernummerformat
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Prüft eine Steuernummer im 13-stelligen ELSTER-Format (FFFF0BBBUUUUP bzw. NRW FFFF0BBBBUUUP).
    /// Zusätzlich wird das landesspezifische Prüfziffernverfahren angewandt.
    /// </summary>
    public static FormatPruefung PruefeSteuernummerElster(string? steuernummer, Bundesland land)
    {
        if (string.IsNullOrWhiteSpace(steuernummer))
            return FormatPruefung.Ungueltig("Die Steuernummer fehlt.");

        var ziffern = NurZiffern(steuernummer);
        if (ziffern.Length != 13)
            return FormatPruefung.Ungueltig(
                $"Die Steuernummer muss im ELSTER-Format 13 Ziffern haben (eingegeben: {ziffern.Length}).");

        if (ziffern[4] != '0')
            return FormatPruefung.Ungueltig("Die 5. Stelle des ELSTER-Steuernummerformats muss immer 0 sein.");

        var bezirk = land == Bundesland.NordrheinWestfalen
            ? ziffern.Substring(5, 4)
            : ziffern.Substring(5, 3);

        if (land == Bundesland.NordrheinWestfalen)
        {
            if (bezirk is "0000" or "0998" or "0999")
                return FormatPruefung.Ungueltig("Die Bezirksnummer darf in NRW nicht 0000, 0998 oder 0999 sein.");
        }
        else if (bezirk is "000" or "998" or "999")
        {
            return FormatPruefung.Ungueltig("Die Bezirksnummer darf nicht 000, 998 oder 999 sein.");
        }

        if (VerbundlandMitBezirkAb100(land) && int.Parse(bezirk[..3], CultureInfo.InvariantCulture) < 100)
            return FormatPruefung.Ungueltig("Die Bezirksnummer muss in diesem Bundesland mindestens 100 sein.");

        var bufa = ziffern[..4];
        if (!Bundesfinanzamtsnummern.IstZulaessig(bufa, land))
            return FormatPruefung.Ungueltig(
                $"Die Bundesfinanzamtsnummer {bufa} ist für {land.AnzeigeName()} im ELSTER-Verfahren nicht zugelassen.");

        var kandidaten = BerechneSteuernummerPruefziffern(ziffern[..12], land);
        if (!kandidaten.Contains(ziffern[12] - '0'))
            return FormatPruefung.Ungueltig(
                "Die Prüfziffer der Steuernummer stimmt nicht.",
                $"Erwartet wäre {string.Join(" oder ", kandidaten)}.");

        return new FormatPruefung(true, Normalisiert: ziffern);
    }

    private static bool VerbundlandMitBezirkAb100(Bundesland land) => land is
        Bundesland.Bayern or Bundesland.Brandenburg or Bundesland.MecklenburgVorpommern or
        Bundesland.Saarland or Bundesland.Sachsen or Bundesland.SachsenAnhalt or Bundesland.Thueringen;

    /// <summary>
    /// Liefert alle Prüfziffern, die für die ersten 12 Stellen gültig sind. Eine Menge (nicht ein Wert),
    /// weil Berlin zwei Verfahren (Berlin-A/Berlin-B) kennt und die Ausnahmezuordnung von der
    /// Bezirksnummer abhängt - so entstehen keine falschen Negativmeldungen.
    /// </summary>
    public static IReadOnlyCollection<int> BerechneSteuernummerPruefziffern(string zwolfZiffern, Bundesland land)
    {
        if (zwolfZiffern.Length != 12) return Array.Empty<int>();
        var d = zwolfZiffern.Select(c => c - '0').ToArray();

        return land switch
        {
            Bundesland.BadenWuerttemberg or Bundesland.Hessen or Bundesland.SchleswigHolstein =>
                new[] { ZweierVerfahren(d) },
            Bundesland.RheinlandPfalz =>
                new[] { ModifiziertesElferVerfahren(d) },
            Bundesland.Berlin =>
                new[] { ElferVerfahren(d, BerlinA), ElferVerfahren(d, BerlinB) }.Distinct().ToArray(),
            Bundesland.NordrheinWestfalen =>
                new[] { ElferVerfahren(d, Nrw) },
            _ => new[] { ElferVerfahren(d, Verbund) }
        };
    }

    private static readonly int[] ZweierSummand = { 0, 0, 9, 8, 0, 7, 6, 5, 4, 3, 2, 1 };
    private static readonly int[] ZweierFaktor = { 0, 0, 512, 256, 0, 128, 64, 32, 16, 8, 4, 2 };

    /// <summary>2er-Verfahren (Baden-Württemberg, Hessen, Schleswig-Holstein).</summary>
    public static int ZweierVerfahren(IReadOnlyList<int> d)
    {
        var summe = 0;
        for (var i = 0; i < 12; i++)
        {
            var s = (d[i] + ZweierSummand[i]) % 10;
            var produkt = s * ZweierFaktor[i];
            summe += QuersummeEinstellig(produkt);
        }
        return summe % 10 == 0 ? 0 : 10 - (summe % 10);
    }

    // Faktoren des 11er-Verfahrens, Stelle 1-12 (F1-F4, M=0, B2-B4, U1-U4)
    private static readonly int[] Verbund = { 0, 5, 4, 3, 0, 2, 7, 6, 5, 4, 3, 2 };
    private static readonly int[] BerlinA = { 0, 0, 0, 0, 0, 7, 6, 5, 8, 4, 3, 2 };
    private static readonly int[] BerlinB = { 0, 0, 2, 9, 0, 8, 7, 6, 5, 4, 3, 2 };
    private static readonly int[] Nrw = { 0, 3, 2, 1, 0, 7, 6, 5, 4, 3, 2, 1 };

    /// <summary>11er-Verfahren. NRW rechnet abweichend (Rest statt Differenz zur nächsten 11er-Zahl).</summary>
    public static int ElferVerfahren(IReadOnlyList<int> d, int[] faktoren)
    {
        var summe = 0;
        for (var i = 0; i < 12; i++) summe += d[i] * faktoren[i];

        if (ReferenceEquals(faktoren, Nrw))
            return summe % 11;

        if (summe % 11 == 0) return 0;
        return 11 - (summe % 11);
    }

    private static readonly int[] RpFaktor = { 0, 0, 1, 2, 0, 1, 2, 1, 2, 1, 2, 1 };

    /// <summary>Modifiziertes 11er-Verfahren (Rheinland-Pfalz).</summary>
    public static int ModifiziertesElferVerfahren(IReadOnlyList<int> d)
    {
        var summe = 0;
        for (var i = 0; i < 12; i++)
        {
            var produkt = d[i] * RpFaktor[i];
            summe += produkt < 10 ? produkt : (produkt % 10) + 1;
        }
        return summe % 10 == 0 ? 0 : 10 - (summe % 10);
    }

    // -----------------------------------------------------------------------------------------
    // Grundsteuer-Aktenzeichen (17-stellig, 16-stellig, 13-stellig NRW)
    // -----------------------------------------------------------------------------------------

    /// <summary>Prüft ein Aktenzeichen gegen das Format des jeweiligen Bundeslandes.</summary>
    public static FormatPruefung PruefeAktenzeichen(string? aktenzeichen, Bundesland land)
    {
        var info = BundeslandKatalog.Fuer(land);
        if (info.Ordnungskriterium == Ordnungskriterium.Steuernummer)
            return FormatPruefung.Ungueltig(
                $"{land.AnzeigeName()} verwendet als Ordnungskriterium die Steuernummer, kein Aktenzeichen.");

        if (string.IsNullOrWhiteSpace(aktenzeichen))
            return FormatPruefung.Ungueltig("Das Aktenzeichen fehlt.");

        var ziffern = NurZiffern(aktenzeichen);
        var erwarteteLaenge = info.AktenzeichenFormat == AktenzeichenFormat.BayernVerbund ? 17 : 16;
        if (info.AktenzeichenFormat == AktenzeichenFormat.NordrheinWestfalen) erwarteteLaenge = 13;

        if (ziffern.Length != erwarteteLaenge)
            return FormatPruefung.Ungueltig(
                $"Das Aktenzeichen muss {erwarteteLaenge} Ziffern haben (eingegeben: {ziffern.Length}).");

        var d = ziffern.Select(c => c - '0').ToArray();
        var erwartet = info.AktenzeichenFormat switch
        {
            AktenzeichenFormat.BayernVerbund => AktenzeichenBayernVerbund(d),
            AktenzeichenFormat.Hessen => AktenzeichenVerdopplungsverfahren(d),
            AktenzeichenFormat.BadenWuerttemberg => AktenzeichenVerdopplungsverfahren(d),
            AktenzeichenFormat.Niedersachsen => AktenzeichenNiedersachsen(d),
            AktenzeichenFormat.NordrheinWestfalen => AktenzeichenNrw(d),
            _ => -1
        };

        if (erwartet < 0) return FormatPruefung.Gueltig;

        if (erwartet != d[^1])
            return FormatPruefung.Ungueltig("Die Prüfziffer des Aktenzeichens stimmt nicht.",
                $"Erwartet wäre {erwartet}.");

        return new FormatPruefung(true, Normalisiert: ziffern);
    }

    /// <summary>Aktenzeichen Bayern, Brandenburg, MV, RP, Saarland, Sachsen, Sachsen-Anhalt, Thüringen (17 Stellen).</summary>
    public static int AktenzeichenBayernVerbund(IReadOnlyList<int> d)
    {
        int[] f = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };
        var summe = 0;
        for (var i = 0; i < 16; i++) summe += d[i] * f[i];
        return summe % 10;
    }

    /// <summary>Aktenzeichen Hessen und Baden-Württemberg (16 Stellen): ungerade Stellen verdoppeln.</summary>
    public static int AktenzeichenVerdopplungsverfahren(IReadOnlyList<int> d)
    {
        var summe = 0;
        for (var i = 0; i < 15; i++)
            summe += i % 2 == 0 ? QuersummeEinstellig(d[i] * 2) : d[i];
        return summe % 10 == 0 ? 0 : 10 - (summe % 10);
    }

    /// <summary>Aktenzeichen Niedersachsen (16 Stellen), Gewichte 1,3,7.</summary>
    public static int AktenzeichenNiedersachsen(IReadOnlyList<int> d)
    {
        int[] f = { 1, 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7 };
        var summe = 0;
        for (var i = 0; i < 15; i++) summe += d[i] * f[i];
        return summe % 10;
    }

    /// <summary>Aktenzeichen Nordrhein-Westfalen (13 Stellen), Gewichte 1,2,3,4,7,8,9,2,3,4,7,8.</summary>
    public static int AktenzeichenNrw(IReadOnlyList<int> d)
    {
        int[] f = { 1, 2, 3, 4, 7, 8, 9, 2, 3, 4, 7, 8 };
        var summe = 0;
        for (var i = 0; i < 12; i++) summe += d[i] * f[i];
        return summe % 10;
    }

    // -----------------------------------------------------------------------------------------
    // Weitere Feldformate
    // -----------------------------------------------------------------------------------------

    /// <summary>Flurstückzähler/-nenner: Zähler ist Pflicht, Nenner darf fehlen (nicht jedes Flurstück hat einen).</summary>
    public static FormatPruefung PruefeFlurstueck(string? zaehler, string? nenner)
    {
        if (string.IsNullOrWhiteSpace(zaehler))
            return FormatPruefung.Ungueltig("Der Flurstückzähler fehlt.");

        if (!NurZiffern(zaehler).Equals(zaehler.Trim(), StringComparison.Ordinal))
            return FormatPruefung.Ungueltig("Der Flurstückzähler darf nur Ziffern enthalten.");

        if (zaehler.Trim().TrimStart('0').Length is 0 or > 5)
            return FormatPruefung.Ungueltig("Der Flurstückzähler muss zwischen 1 und 5 Stellen haben.");

        if (!string.IsNullOrWhiteSpace(nenner))
        {
            if (!NurZiffern(nenner).Equals(nenner.Trim(), StringComparison.Ordinal))
                return FormatPruefung.Ungueltig("Der Flurstücknenner darf nur Ziffern enthalten.");
            if (nenner.Trim().TrimStart('0').Length > 5)
                return FormatPruefung.Ungueltig("Der Flurstücknenner darf höchstens 5 Stellen haben.");
        }

        return FormatPruefung.Gueltig;
    }

    /// <summary>Gemarkungsnummer ist 6-stellig (ELSTER Zeile 10/„Angaben zum Flurstück").</summary>
    public static FormatPruefung PruefeGemarkungsnummer(string? nummer)
    {
        if (string.IsNullOrWhiteSpace(nummer)) return FormatPruefung.Gueltig;
        var z = NurZiffern(nummer);
        return z.Length == 6
            ? new FormatPruefung(true, Normalisiert: z)
            : FormatPruefung.Ungueltig("Die Gemarkungsnummer muss 6-stellig sein.");
    }

    /// <summary>PLZ: fünf Ziffern, deutsche Bereiche 01067–99998.</summary>
    public static FormatPruefung PruefePostleitzahl(string? plz)
    {
        if (string.IsNullOrWhiteSpace(plz)) return FormatPruefung.Ungueltig("Die Postleitzahl fehlt.");
        var z = NurZiffern(plz);
        if (z.Length != 5) return FormatPruefung.Ungueltig("Die Postleitzahl muss 5-stellig sein.");
        var wert = int.Parse(z, CultureInfo.InvariantCulture);
        if (wert is < 1067 or > 99998)
            return FormatPruefung.Ungueltig("Die Postleitzahl liegt außerhalb des deutschen Zustellbereichs.");
        return new FormatPruefung(true, Normalisiert: z);
    }

    /// <summary>Grundstücksfläche: positiv, in der Praxis bis 1.000.000 m² plausibel.</summary>
    public static FormatPruefung PruefeFlaeche(decimal? flaeche, string feldname)
    {
        if (flaeche is null) return FormatPruefung.Ungueltig($"{feldname} fehlt.");
        if (flaeche <= 0) return FormatPruefung.Ungueltig($"{feldname} muss größer als 0 m² sein.");
        if (flaeche > 1_000_000m)
            return FormatPruefung.Ungueltig($"{feldname} erscheint unplausibel hoch (über 1.000.000 m²).");
        return FormatPruefung.Gueltig;
    }

    /// <summary>Baujahr: 1800 bis aktuelles Jahr (Werte vor 1800 gelten bewertungsrechtlich als 1800).</summary>
    public static FormatPruefung PruefeBaujahr(int? baujahr)
    {
        if (baujahr is null) return FormatPruefung.Gueltig;
        var max = DateTime.Today.Year + 1;
        if (baujahr is < 1800 or > 2100 || baujahr > max)
            return FormatPruefung.Ungueltig($"Das Baujahr muss zwischen 1800 und {max} liegen.");
        return FormatPruefung.Gueltig;
    }

    /// <summary>Bodenrichtwert in €/m²: positiv und plausibel (bis 100.000 €/m²).</summary>
    public static FormatPruefung PruefeBodenrichtwert(decimal? wert, string feldname)
    {
        if (wert is null) return FormatPruefung.Ungueltig($"{feldname} fehlt.");
        if (wert <= 0) return FormatPruefung.Ungueltig($"{feldname} muss größer als 0 €/m² sein.");
        if (wert > 100_000m) return FormatPruefung.Ungueltig($"{feldname} erscheint unplausibel hoch.");
        return FormatPruefung.Gueltig;
    }

    /// <summary>Eigentumsanteile müssen zusammen genau 1 ergeben (100 %).</summary>
    public static FormatPruefung PruefeAnteile(IEnumerable<decimal> anteile)
    {
        var summe = anteile.Sum();
        if (summe == 0m) return FormatPruefung.Ungueltig("Es muss mindestens ein Eigentümer mit Anteil erfasst sein.");
        if (summe != 1m)
            return FormatPruefung.Ungueltig(
                $"Die Eigentumsanteile müssen zusammen 100 % ergeben (aktuell: {summe * 100m:0.##} %).");
        return FormatPruefung.Gueltig;
    }

    // -----------------------------------------------------------------------------------------
    // Hilfsfunktionen
    // -----------------------------------------------------------------------------------------

    public static string NurZiffern(string? eingabe) =>
        new((eingabe ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>Quersumme, die garantiert einstellig ist.</summary>
    public static int QuersummeEinstellig(int wert)
    {
        while (wert > 9)
            wert = ZiffernSumme(wert);
        return wert;
    }

    private static int ZiffernSumme(int wert)
    {
        var summe = 0;
        while (wert > 0)
        {
            summe += wert % 10;
            wert /= 10;
        }
        return summe;
    }

    /// <summary>Formatiert ein Aktenzeichen in der auf Bescheiden üblichen Schreibweise mit Trennzeichen.</summary>
    public static string FormatiereAktenzeichen(string? aktenzeichen, Bundesland land)
    {
        var z = NurZiffern(aktenzeichen);
        if (z.Length == 0) return string.Empty;
        var format = BundeslandKatalog.Fuer(land).AktenzeichenFormat;

        return format switch
        {
            AktenzeichenFormat.SteuernummerLand when z.Length == 13 => $"{z[..4]} {z[5..8]} {z[8..12]}{z[12]}",
            AktenzeichenFormat.SteuernummerLand when z.Length == 10 => $"{z[..2]}/{z[2..5]}/{z[5..]}",
            AktenzeichenFormat.BayernVerbund when z.Length == 17 =>
                $"{z[..3]}/{z[3..6]}/{z[6..10]}/{z[10..13]}/{z[13..16]}/{z[16]}",
            AktenzeichenFormat.Hessen when z.Length == 16 =>
                $"{z[..2]} {z[2..5]} {z[5..9]} {z[9..12]} {z[12..15]} {z[15]}",
            AktenzeichenFormat.BadenWuerttemberg when z.Length == 16 => z[5] == '9' || z[5] == '7'
                ? $"{z[..2]}/{z[2..5]}/{z[5]}/{z[6..12]}/{z[12..15]}/{z[15]}"
                : $"{z[..2]}/{z[2..5]}/{z[5..9]}/{z[9..12]}/{z[12..15]}/{z[15]}",
            AktenzeichenFormat.Niedersachsen when z.Length == 16 =>
                $"{z[..2]}/{z[2..5]}/{z[5..9]}/{z[9..12]}/{z[12..15]}/{z[15]}",
            AktenzeichenFormat.NordrheinWestfalen when z.Length == 13 =>
                $"{z[..3]}/{z[3..6]}-{z[6]}-{z[7..12]}.{z[12]}",
            _ => z
        };
    }

    /// <summary>Nur zur Diagnose: zeigt den Rechenweg der Prüfziffer (hilft bei Support-Anfragen).</summary>
    public static string ErklaereSteuernummerPruefziffer(string? steuernummer, Bundesland land)
    {
        var z = NurZiffern(steuernummer);
        if (z.Length != 13) return "Kein ELSTER-Steuernummerformat (13 Ziffern erwartet).";
        var kandidaten = BerechneSteuernummerPruefziffern(z[..12], land);
        var sb = new StringBuilder();
        sb.Append($"{land.AnzeigeName()}: Prüfziffer kann {string.Join(" oder ", kandidaten)} sein; ");
        sb.Append($"eingegeben ist {z[12]}.");
        return sb.ToString();
    }
}
