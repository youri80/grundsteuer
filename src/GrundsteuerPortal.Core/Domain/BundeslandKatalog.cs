namespace GrundsteuerPortal.Core.Domain;

/// <summary>
/// Länderkatalog: welches Bundesland rechnet nach welchem Modell, mit welchen Sätzen und welchen
/// Ordnungskriterien. Eine Änderung in der Finanzverwaltung ist damit EINE Datenänderung, keine
/// Codeänderung in den Komponenten.
/// </summary>
public static class BundeslandKatalog
{
    /// <summary>Äquivalenzzahl je m² Grund und Boden in den reinen Flächen-/Äquivalenzmodellen.</summary>
    public const decimal AequiGrundUndBoden = 0.04m;

    /// <summary>Äquivalenzzahl je m² Gebäudefläche (Wohnen und Nichtwohnen) in den Äquivalenzmodellen.</summary>
    public const decimal AequeGebaeude = 0.50m;

    /// <summary>Abschlag auf die Steuermesszahl bei Wohnnutzung im Flächenmodell Bayern (30 %).</summary>
    public const decimal WohnAbschlagFlaechenmodell = 0.70m;

    /// <summary>Exponent des Lagefaktors (Hessen § 7 HGrStG, Niedersachsen) - dämpft Bodenwertunterschiede.</summary>
    public const decimal LageFaktorExponent = 0.3m;

    private static readonly Dictionary<Bundesland, BundeslandInfo> _katalog = new()
    {
        [Bundesland.BadenWuerttemberg] = new()
        {
            Bundesland = Bundesland.BadenWuerttemberg,
            Landesnummer = 28,
            Datenart = "GrundsteuerBW",
            Modell = GrundsteuerModell.ModifiziertesBodenwertmodell,
            Ordnungskriterium = Ordnungskriterium.Aktenzeichen,
            AktenzeichenFormat = AktenzeichenFormat.BadenWuerttemberg,
            MesszahlWohnen = 0.0013m,
            MesszahlSonstige = 0.0013m,
            WohnAbschlagProzent = 30,
            FinanzamtNummern = "801, 804-812, 814-816, 818-823, 830-859, 861-865, 869-871, 874, 876-891, 899",
            LaenderHinweis = "Modifiziertes Bodenwertmodell: Grundsteuerwert = Grundstücksfläche × Bodenrichtwert. "
                           + "Die Bebauung ist für die Bewertung unerheblich (§ 38 LGrStG)."
        },
        [Bundesland.Bayern] = new()
        {
            Bundesland = Bundesland.Bayern,
            Landesnummer = 9,
            Datenart = "GrundsteuerBY",
            Modell = GrundsteuerModell.WertunabhaengigesFlaechenmodell,
            Ordnungskriterium = Ordnungskriterium.Aktenzeichen,
            AktenzeichenFormat = AktenzeichenFormat.BayernVerbund,
            MesszahlWohnen = 0.001m * WohnAbschlagFlaechenmodell,
            MesszahlSonstige = 0.001m,
            WohnAbschlagProzent = 30,
            FinanzamtNummern = "102-115, 117, 119, 121, 123-127, 131-132, 134, 138-142, 151-154, 156-157, 159, 161-163, 168-171, 201-208, 211-212, 216-218, 220-223, 227-231, 235, 241, 244, 247-249, 252, 254-255, 257-259",
            LaenderHinweis = "Reines Flächenmodell: Grund und Boden 0,04 €/m², Gebäudefläche 0,50 €/m². "
                           + "Steuermesszahl 1,0, für Wohnflächen 30 % günstiger (0,7). Bodennutzung spielt keine Rolle."
        },
        [Bundesland.Berlin] = new()
        {
            Bundesland = Bundesland.Berlin,
            Landesnummer = 11,
            Datenart = "Grundsteuerwert",
            Modell = GrundsteuerModell.Bundesmodell,
            Ordnungskriterium = Ordnungskriterium.Steuernummer,
            AktenzeichenFormat = AktenzeichenFormat.SteuernummerLand,
            MesszahlWohnen = 0.00031m,
            MesszahlSonstige = 0.00034m,
            FinanzamtNummern = "alle FÄ außer 15, 28, 38 und 91",
            LaenderHinweis = "Bundesmodell. Abweichend: Ordnungskriterium ist die Steuernummer, nicht das Aktenzeichen."
        },
        [Bundesland.Brandenburg] = new()
        {
            Bundesland = Bundesland.Brandenburg,
            Landesnummer = 30,
            Datenart = "Grundsteuerwert",
            Modell = GrundsteuerModell.Bundesmodell,
            Ordnungskriterium = Ordnungskriterium.Aktenzeichen,
            AktenzeichenFormat = AktenzeichenFormat.BayernVerbund,
            MesszahlWohnen = 0.00031m,
            MesszahlSonstige = 0.00034m,
            FinanzamtNummern = "046, 048-053, 056, 057, 061, 062, 064, 065",
            LaenderHinweis = "Bundesmodell (Ertragswert-/Sachwertverfahren)."
        },
        [Bundesland.Bremen] = new()
        {
            Bundesland = Bundesland.Bremen,
            Landesnummer = 24,
            Datenart = "Grundsteuerwert",
            Modell = GrundsteuerModell.Bundesmodell,
            Ordnungskriterium = Ordnungskriterium.Steuernummer,
            AktenzeichenFormat = AktenzeichenFormat.SteuernummerLand,
            MesszahlWohnen = 0.00031m,
            MesszahlSonstige = 0.00034m,
            FinanzamtNummern = "nur FÄ 57 und 77 (BUFA 2457, 2477)",
            LaenderHinweis = "Bundesmodell. Ordnungskriterium ist die Steuernummer."
        },
        [Bundesland.Hamburg] = new()
        {
            Bundesland = Bundesland.Hamburg,
            Landesnummer = 22,
            Datenart = "GrundsteuerHH",
            Modell = GrundsteuerModell.Wohnlagenmodell,
            Ordnungskriterium = Ordnungskriterium.Steuernummer,
            AktenzeichenFormat = AktenzeichenFormat.SteuernummerLand,
            MesszahlWohnen = 0.001m * 0.70m,
            MesszahlSonstige = 0.001m * 0.87m,
            WohnAbschlagProzent = 25,
            FinanzamtNummern = "nur FA 16 (BUFA 2216)",
            LaenderHinweis = "Wohnlagenmodell: Grund und Boden 0,04 €/m², Gebäudefläche 0,50 €/m². "
                           + "Messzahl 100 %, Wohnflächen 70 %, Nutzflächen 87 %; zusätzlich 25 % Ermäßigung "
                           + "bei normaler Wohnlage (§ 4 Abs. 2 HmbGrStG)."
        },
        [Bundesland.Hessen] = new()
        {
            Bundesland = Bundesland.Hessen,
            Landesnummer = 26,
            Datenart = "GrundsteuerHE",
            Modell = GrundsteuerModell.FlaechenFaktorVerfahren,
            Ordnungskriterium = Ordnungskriterium.Aktenzeichen,
            AktenzeichenFormat = AktenzeichenFormat.Hessen,
            MesszahlWohnen = 0.70m,
            MesszahlSonstige = 1.00m,
            FinanzamtNummern = "nur FÄ 01 bis 47 (BUFA 2601-2647)",
            LaenderHinweis = "Flächen-Faktor-Verfahren: Flächenbeträge (Boden 0,04 €/m², Wohnen 0,50 €/m²) "
                           + "× Steuermesszahl (Boden/Nichtwohnen 100 %, Wohnen 70 %) = Ausgangsbetrag, "
                           + "dann × Lagefaktor = (Bodenrichtwert / durchschnittlicher Bodenrichtwert der Gemeinde)^0,3."
        },
        [Bundesland.MecklenburgVorpommern] = new()
        {
            Bundesland = Bundesland.MecklenburgVorpommern,
            Landesnummer = 40,
            Datenart = "Grundsteuerwert",
            Modell = GrundsteuerModell.Bundesmodell,
            Ordnungskriterium = Ordnungskriterium.Aktenzeichen,
            AktenzeichenFormat = AktenzeichenFormat.BayernVerbund,
            MesszahlWohnen = 0.00031m,
            MesszahlSonstige = 0.00034m,
            FinanzamtNummern = "072, 075, 079-082, 084, 086, 087, 090",
            LaenderHinweis = "Bundesmodell."
        },
        [Bundesland.Niedersachsen] = new()
        {
            Bundesland = Bundesland.Niedersachsen,
            Landesnummer = 23,
            Datenart = "GrundsteuerNI",
            Modell = GrundsteuerModell.FlaechenLageModell,
            Ordnungskriterium = Ordnungskriterium.Aktenzeichen,
            AktenzeichenFormat = AktenzeichenFormat.Niedersachsen,
            MesszahlWohnen = 0.70m,
            MesszahlSonstige = 1.00m,
            FinanzamtNummern = "313-323, 326-331, 333-336, 338, 340-341, 343-361, 363-370, 375, 376, 378, 379, 388",
            LaenderHinweis = "Flächen-Lage-Modell: Äquivalenzbeträge (Boden 0,04 €/m², Gebäude 0,50 €/m²) "
                           + "× Steuermesszahl (Boden/Nichtwohnen 1,0, Wohnen 0,7) × Lagefaktor "
                           + "= (Bodenrichtwert / durchschnittlicher Bodenrichtwert)^0,3."
        },
        [Bundesland.NordrheinWestfalen] = new()
        {
            Bundesland = Bundesland.NordrheinWestfalen,
            Landesnummer = 5,
            Datenart = "Grundsteuerwert",
            Modell = GrundsteuerModell.Bundesmodell,
            Ordnungskriterium = Ordnungskriterium.Aktenzeichen,
            AktenzeichenFormat = AktenzeichenFormat.NordrheinWestfalen,
            MesszahlWohnen = 0.00031m,
            MesszahlSonstige = 0.00034m,
            FinanzamtNummern = "FÄ 01-83 (OFD Düsseldorf/Köln), FÄ 01-84 (OFD Münster)",
            LaenderHinweis = "Bundesmodell. NRW nutzt als einziges Land das 4-stellige Bezirksnummernformat."
        },
        [Bundesland.RheinlandPfalz] = new()
        {
            Bundesland = Bundesland.RheinlandPfalz,
            Landesnummer = 27,
            Datenart = "Grundsteuerwert",
            Modell = GrundsteuerModell.Bundesmodell,
            Ordnungskriterium = Ordnungskriterium.Aktenzeichen,
            AktenzeichenFormat = AktenzeichenFormat.BayernVerbund,
            MesszahlWohnen = 0.00031m,
            MesszahlSonstige = 0.00034m,
            FinanzamtNummern = "701-702, 706, 708-710, 719, 722-724, 726-727, 729-732, 735, 740-744",
            LaenderHinweis = "Bundesmodell."
        },
        [Bundesland.Saarland] = new()
        {
            Bundesland = Bundesland.Saarland,
            Landesnummer = 10,
            Datenart = "Grundsteuerwert",
            Modell = GrundsteuerModell.Bundesmodell,
            Ordnungskriterium = Ordnungskriterium.Aktenzeichen,
            AktenzeichenFormat = AktenzeichenFormat.BayernVerbund,
            MesszahlWohnen = 0.00034m,
            MesszahlSonstige = 0.00064m,
            FinanzamtNummern = "010, 055, 060 (BUFA 1010, 1055, 1060)",
            LaenderHinweis = "Bundesmodell mit abweichenden Steuermesszahlen: Wohnen 0,34 ‰, "
                           + "unbebaut/Gewerbe 0,64 ‰."
        },
        [Bundesland.Sachsen] = new()
        {
            Bundesland = Bundesland.Sachsen,
            Landesnummer = 32,
            Datenart = "Grundsteuerwert",
            Modell = GrundsteuerModell.Bundesmodell,
            Ordnungskriterium = Ordnungskriterium.Aktenzeichen,
            AktenzeichenFormat = AktenzeichenFormat.BayernVerbund,
            MesszahlWohnen = 0.00036m,
            MesszahlSonstige = 0.00072m,
            FinanzamtNummern = "202, 204, 207-210, 213-214, 217-218, 220, 222-224, 227-228, 232, 236-239",
            LaenderHinweis = "Nutzungsartmodell: Wohnen 0,36 ‰, Gewerbe 0,72 ‰, unbebaut 0,36 ‰ - "
                           + "sonst Bundesmodell."
        },
        [Bundesland.SachsenAnhalt] = new()
        {
            Bundesland = Bundesland.SachsenAnhalt,
            Landesnummer = 31,
            Datenart = "Grundsteuerwert",
            Modell = GrundsteuerModell.Bundesmodell,
            Ordnungskriterium = Ordnungskriterium.Aktenzeichen,
            AktenzeichenFormat = AktenzeichenFormat.BayernVerbund,
            MesszahlWohnen = 0.00031m,
            MesszahlSonstige = 0.00034m,
            FinanzamtNummern = "102-103, 105-108, 110, 112, 114-119",
            LaenderHinweis = "Bundesmodell."
        },
        [Bundesland.SchleswigHolstein] = new()
        {
            Bundesland = Bundesland.SchleswigHolstein,
            Landesnummer = 21,
            Datenart = "Grundsteuerwert",
            Modell = GrundsteuerModell.Bundesmodell,
            Ordnungskriterium = Ordnungskriterium.Steuernummer,
            AktenzeichenFormat = AktenzeichenFormat.SteuernummerLand,
            MesszahlWohnen = 0.00031m,
            MesszahlSonstige = 0.00034m,
            FinanzamtNummern = "nur FÄ 71 bis 92 (BUFA 2171-2192)",
            LaenderHinweis = "Bundesmodell. Ordnungskriterium ist die Steuernummer."
        },
        [Bundesland.Thueringen] = new()
        {
            Bundesland = Bundesland.Thueringen,
            Landesnummer = 41,
            Datenart = "Grundsteuerwert",
            Modell = GrundsteuerModell.Bundesmodell,
            Ordnungskriterium = Ordnungskriterium.Aktenzeichen,
            AktenzeichenFormat = AktenzeichenFormat.BayernVerbund,
            MesszahlWohnen = 0.00031m,
            MesszahlSonstige = 0.00034m,
            FinanzamtNummern = "151, 154-157, 159, 161-162, 165, 166, 171",
            LaenderHinweis = "Bundesmodell."
        }
    };

    public static IReadOnlyDictionary<Bundesland, BundeslandInfo> Alle => _katalog;

    public static BundeslandInfo Fuer(Bundesland land) => _katalog[land];

    /// <summary>Alle Bundesländer, die ein eigenes Landesmodell fahren (Länderöffnungsklausel).</summary>
    public static IEnumerable<BundeslandInfo> Landesmodelle =>
        _katalog.Values.Where(i => i.Modell != GrundsteuerModell.Bundesmodell);
}

/// <summary>Ordnungskriterium, das ELSTER je Land erwartet.</summary>
public enum Ordnungskriterium
{
    Aktenzeichen = 0,
    Steuernummer = 1
}

/// <summary>Das landesspezifische Aktenzeichenformat (Bestimmt Aufbau und Prüfziffernverfahren).</summary>
public enum AktenzeichenFormat
{
    /// <summary>FFF/BBB/UUUUP bzw. FF/BBB/UUUUP - 13-stelliges ELSTER-Steuernummerformat.</summary>
    SteuernummerLand = 0,

    /// <summary>17-stellig: xxx/xxx/xxxx/xxx/xxx/x, Prüfziffer = Einerstelle der gewichteten Summe (5,4,3,2,7,6...).</summary>
    BayernVerbund = 1,

    /// <summary>16-stellig: xx/xxx/xxxx/xxx/xxx/x (bebaut) bzw. xx/xxx/9/xxxxxx/xxx/x (unbebaut).</summary>
    BadenWuerttemberg = 2,

    /// <summary>16-stellig, Stellen 1-15 abwechselnd verdoppelt, Prüfziffer = Aufrunden auf die nächste Zehnerzahl.</summary>
    Hessen = 3,

    /// <summary>16-stellig, Gewichte 1,3,7 wiederholt, Prüfziffer = Einerstelle der Summe.</summary>
    Niedersachsen = 4,

    /// <summary>13-stellig: xxx/xxx-G-xxxxx.p, Prüfziffer = Rest der Division durch 10.</summary>
    NordrheinWestfalen = 5
}

/// <summary>Fachliche Kenndaten eines Bundeslandes.</summary>
public sealed record BundeslandInfo
{
    public required Bundesland Bundesland { get; init; }
    public required int Landesnummer { get; init; }
    public required string Datenart { get; init; }
    public required GrundsteuerModell Modell { get; init; }
    public required Ordnungskriterium Ordnungskriterium { get; init; }
    public required AktenzeichenFormat AktenzeichenFormat { get; init; }
    public required decimal MesszahlWohnen { get; init; }
    public required decimal MesszahlSonstige { get; init; }
    public required string FinanzamtNummern { get; init; }
    public required string LaenderHinweis { get; init; }

    /// <summary>Zusätzlicher Abschlag in Prozent, der nur als Ermäßigung auf die Messzahl wirkt (Hamburg 25 %).</summary>
    public int WohnAbschlagProzent { get; init; }

    public bool HatEigenesLandesmodell => Modell != GrundsteuerModell.Bundesmodell;

    /// <summary>Erwartet das Land einen Bodenrichtwert als Pflichtangabe?</summary>
    public bool BrauchtBodenrichtwert =>
        Modell is GrundsteuerModell.ModifiziertesBodenwertmodell
               or GrundsteuerModell.FlaechenFaktorVerfahren
               or GrundsteuerModell.FlaechenLageModell;

    /// <summary>Erwartet das Land Gebäudeflächen (Wohn-/Nutzfläche)?</summary>
    public bool BrauchtGebaeudeflaechen =>
        Modell is not GrundsteuerModell.ModifiziertesBodenwertmodell;

    /// <summary>Ist die Wohnlage bewertungsrelevant (nur Hamburg)?</summary>
    public bool BrauchtWohnlage => Modell == GrundsteuerModell.Wohnlagenmodell;

    public string AnzeigeName => Bundesland.AnzeigeName();
}
