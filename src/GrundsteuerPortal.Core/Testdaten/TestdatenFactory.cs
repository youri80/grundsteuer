using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Core.Validation;

namespace GrundsteuerPortal.Core.Testdaten;

/// <summary>
/// Ein vollständiger Testdatensatz samt Beschreibung, damit in der Oberfläche erkennbar ist,
/// wofür der Datensatz gedacht ist.
/// </summary>
public sealed record Testdatensatz(string Bezeichnung, string Zweck, GrundsteuerMeldungDto Meldung);

/// <summary>
/// Erzeugt vollständige, gültige Beispielmeldungen zum Durchspielen des Wizard.
///
/// <b>Leitgedanke: die Prüfziffern werden NICHT von Hand gesetzt.</b> Jede Nummer entsteht in drei
/// Schritten: einen Grundteil bilden, die Prüfziffer mit <b>derselben Funktion berechnen, die der
/// Validator verwendet</b>, und das Ergebnis anschließend mit dem Validator gegenprüfen. Dadurch
/// kann die Erzeugung nicht von der Prüfung abweichen - ein zweiter, abgeschriebener Algorithmus
/// wäre genau die Stelle, an der Testdaten still ungültig werden.
///
/// Die Datensätze decken alle fünf Berechnungsmodelle und beide Ordnungskriterien ab
/// (Aktenzeichen und Steuernummer), damit jeder Weg durch den Wizard einmal begangen wird.
/// </summary>
public static class TestdatenFactory
{
    // -----------------------------------------------------------------------------------------
    //  Öffentliche Erzeugung
    // -----------------------------------------------------------------------------------------

    /// <summary>Alle Beispielmeldungen, jeweils im Status Entwurf und noch nicht gespeichert.</summary>
    public static IReadOnlyList<Testdatensatz> Alle() => new List<Testdatensatz>
    {
        HessenEinfamilienhaus(),
        HamburgMietwohnung(),
        BayernZweifamilienhaus(),
        BadenWuerttembergUnbebaut(),
        NiedersachsenGeschaeftshaus(),
        NordrheinWestfalenMehrfamilienhaus(),
        BerlinEigentumswohnung(),
        SachsenMiteigentum()
    };

    // -----------------------------------------------------------------------------------------
    //  Hessen - Flächen-Faktor-Verfahren, Ordnungskriterium Aktenzeichen
    // -----------------------------------------------------------------------------------------
    private static Testdatensatz HessenEinfamilienhaus()
    {
        const Bundesland land = Bundesland.Hessen;
        const string bufa = "2601";                    // Hessisches Finanzamt, zugelassen (2601-2647)

        var m = Basis(land, bufa, "Hessen – Finanzamt Friedberg");

        m.Aktenzeichen = Aktenzeichen(land, faAnteil: "01");
        m.AktenzeichenElster = m.Aktenzeichen;

        m.Gemarkung = "Echzell";
        m.Gemarkungsnummer = "006312";
        m.Flur = "7";
        m.FlurstueckZaehler = "123";
        m.FlurstueckNenner = "45";
        m.Grundbuchblatt = "Blatt 4711";
        m.Grundstuecksart = Grundstuecksart.Einfamilienhaus;
        m.Grundstuecksflaeche = 520m;
        m.Wohnflaeche = 145m;
        m.Nutzflaeche = 0m;
        m.Baujahr = 1998;
        m.Bodenrichtwert = 180m;
        m.DurchschnittlicherBodenrichtwert = 165m;      // Lagefaktor rund 1,02
        m.Lage = new AdresseDto
        {
            Strasse = "Wetterauer Straße",
            Hausnummer = "17",
            Postleitzahl = "61209",
            Ort = "Echzell"
        };

        m.Flurstuecke = new List<FlurstueckDto>
        {
            new() { Gemarkung = "Echzell", Gemarkungsnummer = "006312", Flur = "7",
                    Zaehler = "123", Nenner = "45", Flaeche = 520m, Anteil = 1m }
        };

        m.Eigentuemer = new List<EigentuemerDto>
        {
            Person(Anrede.Frau, "Beispiel", "Anna", "Wilhelm-Leuschner-Straße", "4", "61209", "Echzell")
        };

        return new Testdatensatz(
            "Hessen – Einfamilienhaus",
            "Flächen-Faktor-Verfahren mit Lagefaktor; Ordnungskriterium ist das Aktenzeichen.",
            m);
    }

    // -----------------------------------------------------------------------------------------
    //  Hamburg - Wohnlagenmodell, Ordnungskriterium Steuernummer
    // -----------------------------------------------------------------------------------------
    private static Testdatensatz HamburgMietwohnung()
    {
        const Bundesland land = Bundesland.Hamburg;
        const string bufa = "2216";                    // Hamburg: ausschließlich FA 16

        var m = Basis(land, bufa, "Hamburg – Finanzamt 16");

        m.Steuernummer = Steuernummer(land, bufa, bezirk: "041", laufend: "0123");
        m.Aktenzeichen = null;
        m.Erklaerungsart = Erklaerungsart.Erstmalig;

        m.Gemarkung = "Hamburg";
        m.Gemarkungsnummer = "002101";
        m.Flur = "3";
        m.FlurstueckZaehler = "2418";
        m.FlurstueckNenner = null;
        m.Grundbuchblatt = "Blatt 1208";
        m.Grundstuecksart = Grundstuecksart.Mietwohngrundstueck;
        m.Grundstuecksflaeche = 640m;
        m.Wohnflaeche = 410m;
        m.Nutzflaeche = 0m;
        m.Baujahr = 1962;
        m.Wohnlage = Wohnlage.Normal;                  // in Hamburg Pflichtangabe
        m.Lage = new AdresseDto
        {
            Strasse = "Osterstraße",
            Hausnummer = "112",
            Postleitzahl = "20259",
            Ort = "Hamburg"
        };

        m.Flurstuecke = new List<FlurstueckDto>
        {
            new() { Gemarkung = "Hamburg", Gemarkungsnummer = "002101", Flur = "3",
                    Zaehler = "2418", Flaeche = 640m, Anteil = 1m }
        };

        m.Eigentuemer = new List<EigentuemerDto>
        {
            Person(Anrede.Herr, "Beispiel", "Bernd", "Osterstraße", "9", "20259", "Hamburg")
        };

        return new Testdatensatz(
            "Hamburg – Mietwohngrundstück",
            "Wohnlagenmodell mit 25 % Ermäßigung bei normaler Wohnlage; Ordnungskriterium ist die Steuernummer.",
            m);
    }

    // -----------------------------------------------------------------------------------------
    //  Bayern - wertunabhängiges Flächenmodell, Aktenzeichen (17-stellig)
    // -----------------------------------------------------------------------------------------
    private static Testdatensatz BayernZweifamilienhaus()
    {
        const Bundesland land = Bundesland.Bayern;
        const string bufa = "9102";                    // München, zugelassen (9102-9115 ...)

        var m = Basis(land, bufa, "Bayern – Finanzamt München");

        m.Aktenzeichen = Aktenzeichen(land, faAnteil: "102");
        m.AktenzeichenElster = m.Aktenzeichen;

        m.Gemarkung = "München";
        m.Gemarkungsnummer = "009051";
        m.Flur = "12";
        m.FlurstueckZaehler = "887";
        m.FlurstueckNenner = "3";
        m.Grundbuchblatt = "Blatt 22001";
        m.Grundstuecksart = Grundstuecksart.Zweifamilienhaus;
        m.Grundstuecksflaeche = 480m;
        m.Wohnflaeche = 210m;
        m.Nutzflaeche = 0m;
        m.Baujahr = 1975;
        m.Lage = new AdresseDto
        {
            Strasse = "Tumblingerstraße",
            Hausnummer = "31",
            Postleitzahl = "80331",
            Ort = "München"
        };

        m.Flurstuecke = new List<FlurstueckDto>
        {
            new() { Gemarkung = "München", Gemarkungsnummer = "009051", Flur = "12",
                    Zaehler = "887", Nenner = "3", Flaeche = 480m, Anteil = 1m }
        };

        m.Eigentuemer = new List<EigentuemerDto>
        {
            Person(Anrede.Herr, "Beispiel", "Christian", "Tumblingerstraße", "8", "80331", "München")
        };

        return new Testdatensatz(
            "Bayern – Zweifamilienhaus",
            "Wertunabhängiges Flächenmodell (Boden 0,04 €/m², Gebäude 0,50 €/m², Wohnen 30 % günstiger).",
            m);
    }

    // -----------------------------------------------------------------------------------------
    //  Baden-Württemberg - modifiziertes Bodenwertmodell, unbebautes Grundstück
    // -----------------------------------------------------------------------------------------
    private static Testdatensatz BadenWuerttembergUnbebaut()
    {
        const Bundesland land = Bundesland.BadenWuerttemberg;
        const string bufa = "2801";                    // zugelassen (2801-2899 in Bereichen)

        var m = Basis(land, bufa, "Baden-Württemberg – Finanzamt Stuttgart");

        m.Aktenzeichen = Aktenzeichen(land, faAnteil: "01");
        m.AktenzeichenElster = m.Aktenzeichen;

        m.Gemarkung = "Stuttgart";
        m.Gemarkungsnummer = "008101";
        m.Flur = "4";
        m.FlurstueckZaehler = "512";
        m.FlurstueckNenner = null;
        m.Grundbuchblatt = "Blatt 908";
        m.Grundstuecksart = Grundstuecksart.Unbebaut;   // Bewertung ohne Bebauung
        m.Grundstuecksflaeche = 890m;
        m.Wohnflaeche = null;
        m.Nutzflaeche = null;
        m.Baujahr = null;
        m.Bodenrichtwert = 620m;                        // Pflichtangabe in BW
        m.Lage = new AdresseDto
        {
            Strasse = "Werastraße",
            Hausnummer = "46",
            Postleitzahl = "70173",
            Ort = "Stuttgart"
        };

        m.Flurstuecke = new List<FlurstueckDto>
        {
            new() { Gemarkung = "Stuttgart", Gemarkungsnummer = "008101", Flur = "4",
                    Zaehler = "512", Flaeche = 890m, Anteil = 1m }
        };

        m.Eigentuemer = new List<EigentuemerDto>
        {
            JuristischePerson("Beispiel Grundbesitz GmbH", "Werastraße", "46", "70173", "Stuttgart")
        };

        return new Testdatensatz(
            "Baden-Württemberg – unbebautes Grundstück",
            "Modifiziertes Bodenwertmodell: Wert = Fläche × Bodenrichtwert, Bebauung ohne Bedeutung. " +
            "Eigentümer ist eine juristische Person (Steuernummer statt Identifikationsnummer).",
            m);
    }

    // -----------------------------------------------------------------------------------------
    //  Niedersachsen - Flächen-Lage-Modell
    // -----------------------------------------------------------------------------------------
    private static Testdatensatz NiedersachsenGeschaeftshaus()
    {
        const Bundesland land = Bundesland.Niedersachsen;
        const string bufa = "2313";                    // zugelassen (2313-2323 ...)

        var m = Basis(land, bufa, "Niedersachsen – Finanzamt Hannover");

        m.Aktenzeichen = Aktenzeichen(land, faAnteil: "13");
        m.AktenzeichenElster = m.Aktenzeichen;

        m.Gemarkung = "Hannover";
        m.Gemarkungsnummer = "003204";
        m.Flur = "9";
        m.FlurstueckZaehler = "77";
        m.FlurstueckNenner = "12";
        m.Grundbuchblatt = "Blatt 5510";
        m.Grundstuecksart = Grundstuecksart.Geschaeftsgrundstueck;
        m.Grundstuecksflaeche = 1250m;
        m.Wohnflaeche = 0m;
        m.Nutzflaeche = 780m;
        m.Baujahr = 2004;
        m.Bodenrichtwert = 240m;
        m.DurchschnittlicherBodenrichtwert = 210m;
        m.Lage = new AdresseDto
        {
            Strasse = "Georgstraße",
            Hausnummer = "22",
            Postleitzahl = "30159",
            Ort = "Hannover"
        };

        m.Flurstuecke = new List<FlurstueckDto>
        {
            new() { Gemarkung = "Hannover", Gemarkungsnummer = "003204", Flur = "9",
                    Zaehler = "77", Nenner = "12", Flaeche = 1250m, Anteil = 1m }
        };

        m.Eigentuemer = new List<EigentuemerDto>
        {
            JuristischePerson("Beispiel Handel GmbH & Co. KG", "Georgstraße", "22", "30159", "Hannover")
        };

        return new Testdatensatz(
            "Niedersachsen – Geschäftsgrundstück",
            "Flächen-Lage-Modell: Äquivalenzbeträge × Messzahl × Lagefaktor. Reine Gewerbenutzung.",
            m);
    }

    // -----------------------------------------------------------------------------------------
    //  Nordrhein-Westfalen - Bundesmodell, 13-stelliges Aktenzeichen (Sonderformat)
    // -----------------------------------------------------------------------------------------
    private static Testdatensatz NordrheinWestfalenMehrfamilienhaus()
    {
        const Bundesland land = Bundesland.NordrheinWestfalen;
        const string bufa = "5101";                    // zugelassen (5101-5183, 5201-5283, 5301-5384)

        var m = Basis(land, bufa, "Nordrhein-Westfalen – Finanzamt Düsseldorf");

        m.Aktenzeichen = Aktenzeichen(land, faAnteil: "101");
        m.AktenzeichenElster = m.Aktenzeichen;

        m.Gemarkung = "Düsseldorf";
        m.Gemarkungsnummer = "005101";
        m.Flur = "18";
        m.FlurstueckZaehler = "1456";
        m.FlurstueckNenner = "7";
        m.Grundbuchblatt = "Blatt 33012";
        m.Grundstuecksart = Grundstuecksart.Mietwohngrundstueck;
        m.Grundstuecksflaeche = 780m;
        m.Wohnflaeche = 520m;
        m.Nutzflaeche = 90m;
        m.Baujahr = 1911;
        m.IstDenkmalgeschuetzt = true;
        m.Lage = new AdresseDto
        {
            Strasse = "Bilker Allee",
            Hausnummer = "58",
            Postleitzahl = "40217",
            Ort = "Düsseldorf"
        };

        m.Flurstuecke = new List<FlurstueckDto>
        {
            new() { Gemarkung = "Düsseldorf", Gemarkungsnummer = "005101", Flur = "18",
                    Zaehler = "1456", Nenner = "7", Flaeche = 780m, Anteil = 1m }
        };

        m.Eigentuemer = new List<EigentuemerDto>
        {
            Person(Anrede.Frau, "Beispiel", "Doris", "Bilker Allee", "58", "40217", "Düsseldorf")
        };

        return new Testdatensatz(
            "Nordrhein-Westfalen – Mietwohngrundstück",
            "Bundesmodell (Grundsteuerwert kommt vom Finanzamt). NRW nutzt als einziges Land das " +
            "13-stellige Aktenzeichenformat mit 4-stelliger Bezirksnummer.",
            m);
    }

    // -----------------------------------------------------------------------------------------
    //  Berlin - Bundesmodell, Ordnungskriterium Steuernummer
    // -----------------------------------------------------------------------------------------
    private static Testdatensatz BerlinEigentumswohnung()
    {
        const Bundesland land = Bundesland.Berlin;
        const string bufa = "1100";                    // Berlin: 1100-1199

        var m = Basis(land, bufa, "Berlin – Finanzamt Mitte");

        m.Steuernummer = Steuernummer(land, bufa, bezirk: "125", laufend: "0477");
        m.Aktenzeichen = null;

        m.Gemarkung = "Berlin";
        m.Gemarkungsnummer = "001001";
        m.Flur = "5";
        m.FlurstueckZaehler = "312";
        m.FlurstueckNenner = "100";
        m.Grundbuchblatt = "Blatt 7742";
        m.Grundstuecksart = Grundstuecksart.Wohnungseigentum;
        m.Grundstuecksflaeche = 85m;
        m.Wohnflaeche = 78m;
        m.Nutzflaeche = 0m;
        m.Baujahr = 1910;
        m.Lage = new AdresseDto
        {
            Strasse = "Chausseestraße",
            Hausnummer = "64",
            Postleitzahl = "10115",
            Ort = "Berlin"
        };

        m.Flurstuecke = new List<FlurstueckDto>
        {
            new() { Gemarkung = "Berlin", Gemarkungsnummer = "001001", Flur = "5",
                    Zaehler = "312", Nenner = "100", Flaeche = 85m, Anteil = 1m }
        };

        m.Eigentuemer = new List<EigentuemerDto>
        {
            Person(Anrede.Frau, "Beispiel", "Eva", "Chausseestraße", "64", "10115", "Berlin")
        };

        return new Testdatensatz(
            "Berlin – Wohnungseigentum",
            "Bundesmodell; Ordnungskriterium ist die Steuernummer im 13-stelligen ELSTER-Format.",
            m);
    }

    // -----------------------------------------------------------------------------------------
    //  Sachsen - Bundesmodell mit Miteigentum (zwei Eigentümer, Anteile 1/2 + 1/2)
    // -----------------------------------------------------------------------------------------
    private static Testdatensatz SachsenMiteigentum()
    {
        const Bundesland land = Bundesland.Sachsen;
        const string bufa = "3202";                    // zugelassen (3202, 3204, 3207-3210 ...)

        var m = Basis(land, bufa, "Sachsen – Finanzamt Dresden");

        m.Aktenzeichen = Aktenzeichen(land, faAnteil: "202");
        m.AktenzeichenElster = m.Aktenzeichen;

        m.Gemarkung = "Dresden";
        m.Gemarkungsnummer = "004502";
        m.Flur = "11";
        m.FlurstueckZaehler = "909";
        m.FlurstueckNenner = "2";
        m.Grundbuchblatt = "Blatt 18840";
        m.Grundstuecksart = Grundstuecksart.Einfamilienhaus;
        m.Grundstuecksflaeche = 610m;
        m.Wohnflaeche = 160m;
        m.Nutzflaeche = 0m;
        m.Baujahr = 1936;
        m.Lage = new AdresseDto
        {
            Strasse = "Bautzner Straße",
            Hausnummer = "77",
            Postleitzahl = "01099",
            Ort = "Dresden"
        };

        m.Flurstuecke = new List<FlurstueckDto>
        {
            new() { Gemarkung = "Dresden", Gemarkungsnummer = "004502", Flur = "11",
                    Zaehler = "909", Nenner = "2", Flaeche = 610m, Anteil = 1m }
        };

        // Miteigentum: die Anteile müssen zusammen genau 1 ergeben.
        var erster = Person(Anrede.Herr, "Beispiel", "Frank", "Bautzner Straße", "77", "01099", "Dresden");
        erster.Anteil = 0.5m;

        var zweiter = Person(Anrede.Frau, "Beispiel", "Gisela", "Bautzner Straße", "77", "01099", "Dresden");
        zweiter.Anteil = 0.5m;

        m.Eigentuemer = new List<EigentuemerDto> { erster, zweiter };

        return new Testdatensatz(
            "Sachsen – Einfamilienhaus in Miteigentum",
            "Bundesmodell mit zwei Eigentümern zu je 1/2. Prüft die Anteilsprüfung (Summe der Anteile = 100 %).",
            m);
    }

    // -----------------------------------------------------------------------------------------
    //  Gemeinsamer Rahmen
    // -----------------------------------------------------------------------------------------
    private static GrundsteuerMeldungDto Basis(Bundesland land, string bufa, string finanzamtName)
    {
        return new GrundsteuerMeldungDto
        {
            // Id bleibt LEER: das Repository erkennt daran einen neuen Entwurf
            // (`istNeu = dto.Id == Guid.Empty`) und legt ihn an. Mit gesetzter Id würde es
            // einen bestehenden Datensatz erwarten und mit "Meldung existiert nicht" abbrechen.
            Id = Guid.Empty,
            Status = MeldungStatus.Entwurf,
            Bundesland = land,

            Hauptfeststellungszeitpunkt = 2022,
            Bundesfinanzamtsnummer = bufa,
            FinanzamtName = $"{finanzamtName} ({bufa})",
        };
    }

    // -----------------------------------------------------------------------------------------
    //  Personen
    // -----------------------------------------------------------------------------------------
    private static EigentuemerDto Person(Anrede anrede, string nachname, string vorname,
        string strasse, string hausnummer, string plz, string ort)
    {
        return new EigentuemerDto
        {
            Art = EigentuemerArt.NatuerlichePerson,
            Anrede = anrede,
            Name = nachname,
            Vorname = vorname,
            // Der Name als Variante: stabil über Läufe, verschieden je Person.
            IdNummer = IdNr($"{nachname}-{vorname}"),
            Strasse = strasse,
            Hausnummer = hausnummer,
            Postleitzahl = plz,
            Ort = ort,
            Anteil = 1m
        };
    }

    private static EigentuemerDto JuristischePerson(string firma, string strasse, string hausnummer,
        string plz, string ort)
    {
        return new EigentuemerDto
        {
            Art = EigentuemerArt.JuristischePerson,
            Anrede = Anrede.Firma,
            Name = firma,
            // Für juristische Personen genügt die Steuernummer (statt der Identifikationsnummer).
            Steuernummer = "26/815/08154",
            Strasse = strasse,
            Hausnummer = hausnummer,
            Postleitzahl = plz,
            Ort = ort,
            Anteil = 1m
        };
    }

    // -----------------------------------------------------------------------------------------
    //  Nummernzeugung: bilden, Prüfziffer rechnen lassen, gegenprüfen, notfalls wiederholen
    //
    //  Die Wiederholungsschleife ist der Kern: sie prüft das erzeugte Ergebnis mit DERSELBEN
    //  Funktion, die später der Wizard verwendet. Schlägt sie fehl, wird ein neuer Grundteil
    //  gebildet - es kann also kein ungültiger Datensatz in die Oberfläche gelangen.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Elfstellige Steueridentifikationsnummer nach § 139 AO.
    ///
    /// <paramref name="variante"/> geht in den Startwert ein und muss je Person verschieden sein:
    /// ein <c>Random</c> mit gleichem Startwert liefert dieselbe Zahlenfolge, sonst bekämen alle
    /// Eigentümer dieselbe Nummer.
    /// </summary>
    public static string IdNr(string variante)
    {
        var zufall = new Random(StabilerStartwert($"idnr-{variante}"));
        for (var versuch = 0; versuch < 20_000; versuch++)
        {
            var ersteZehn = new char[10];
            for (var i = 0; i < 10; i++) ersteZehn[i] = (char)('0' + zufall.Next(0, 10));
            var text = new string(ersteZehn);

            // Vorgriffe auf die Strukturkriterien, damit die Schleife schnell konvergiert:
            // genau eine Ziffer doppelt oder dreifach, keine drei gleichen in Folge.
            var gruppen = text.GroupBy(c => c).Select(g => g.Count()).ToList();
            if (gruppen.Count(g => g is 2 or 3) != 1 || gruppen.Any(g => g > 3)) continue;
            if (HatDreifachFolge(text)) continue;

            var pruefziffer = ElsterFormate.BerechneIdNrPruefziffer(text);
            var vollstaendig = text + (char)('0' + pruefziffer);

            // Gegenprüfung mit dem Validator - er ist die Autorität, nicht diese Schleife.
            if (ElsterFormate.PruefeIdNr(vollstaendig).IstGueltig) return vollstaendig;
        }

        throw new InvalidOperationException("Es konnte keine gültige Identifikationsnummer erzeugt werden.");
    }

    /// <summary>
    /// 13-stellige Steuernummer im ELSTER-Format: FFFF0BBBUUUU P (bzw. NRW FFFF0BBBBUUUP).
    /// Nur für die Länder, deren Ordnungskriterium die Steuernummer ist.
    /// </summary>
    public static string Steuernummer(Bundesland land, string bufa, string bezirk, string laufend)
    {
        var zufall = new Random(StabilerStartwert($"stnr-{land}-{bufa}"));
        var istNrw = land == Bundesland.NordrheinWestfalen;
        var bezirkLaenge = istNrw ? 4 : 3;
        var laufendLaenge = istNrw ? 3 : 4;

        for (var versuch = 0; versuch < 20_000; versuch++)
        {
            var b = versuch == 0 ? bezirk : ZufallsZiffern(zufall, bezirkLaenge);

            // Bezirksnummer: in NRW nicht 0000/0998/0999, sonst nicht 000/998/999;
            // in den Verbundländern mit Bezirk ab 100 darf sie nicht unter 100 liegen.
            if (istNrw)
            {
                if (b is "0000" or "0998" or "0999") continue;
            }
            else
            {
                if (b is "000" or "998" or "999") continue;
                if (VerbundlandMitBezirkAb100(land) && int.Parse(b) < 100) continue;
            }

            var u = versuch == 0 ? laufend : ZufallsZiffern(zufall, laufendLaenge);
            var grundteil = bufa + "0" + b + u;          // 12 Stellen

            var kandidaten = ElsterFormate.BerechneSteuernummerPruefziffern(grundteil, land);
            if (kandidaten.Count == 0) continue;

            // Berlin kennt zwei Verfahren - der erste gültige Kandidat genügt.
            foreach (var pruefziffer in kandidaten)
            {
                var vollstaendig = grundteil + pruefziffer;
                if (ElsterFormate.PruefeSteuernummerElster(vollstaendig, land).IstGueltig) return vollstaendig;
            }
        }

        throw new InvalidOperationException($"Es konnte keine gültige Steuernummer für {land} erzeugt werden.");
    }

    /// <summary>
    /// Grundsteuer-Aktenzeichen im Format des jeweiligen Landes. <paramref name="faAnteil"/> ist
    /// der vorangestellte Finanzamtsanteil, damit das Aktenzeichen zum Finanzamt passt.
    /// </summary>
    public static string Aktenzeichen(Bundesland land, string faAnteil)
    {
        var info = BundeslandKatalog.Fuer(land);
        var laenge = info.AktenzeichenFormat == AktenzeichenFormat.BayernVerbund ? 17
                   : info.AktenzeichenFormat == AktenzeichenFormat.NordrheinWestfalen ? 13
                   : 16;

        var zufall = new Random(StabilerStartwert($"az-{land}-{faAnteil}"));

        for (var versuch = 0; versuch < 20_000; versuch++)
        {
            // Die ersten Stellen sind der Finanzamtsanteil; die restlichen Füllstellen sind bis auf
            // die letzte (Prüfziffer) beliebig.
            var fuellung = laenge - faAnteil.Length - 1;
            if (fuellung < 1) continue;

            var grundteil = faAnteil + ZufallsZiffern(zufall, fuellung);
            var d = grundteil.Select(c => c - '0').ToArray();

            var pruefziffer = info.AktenzeichenFormat switch
            {
                AktenzeichenFormat.BayernVerbund => ElsterFormate.AktenzeichenBayernVerbund(d),
                AktenzeichenFormat.Hessen => ElsterFormate.AktenzeichenVerdopplungsverfahren(d),
                AktenzeichenFormat.BadenWuerttemberg => ElsterFormate.AktenzeichenVerdopplungsverfahren(d),
                AktenzeichenFormat.Niedersachsen => ElsterFormate.AktenzeichenNiedersachsen(d),
                AktenzeichenFormat.NordrheinWestfalen => ElsterFormate.AktenzeichenNrw(d),
                _ => -1
            };

            if (pruefziffer < 0) continue;

            var aktenzeichen = grundteil + pruefziffer;
            if (ElsterFormate.PruefeAktenzeichen(aktenzeichen, land).IstGueltig) return aktenzeichen;
        }

        throw new InvalidOperationException($"Es konnte kein gültiges Aktenzeichen für {land} erzeugt werden.");
    }

    // -----------------------------------------------------------------------------------------
    //  Hilfsfunktionen
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Stabile Startwerte je Zweck: die erzeugten Nummern sind damit bei jedem Lauf identisch.
    /// Das ist Absicht - ein Testdatensatz, der bei jedem Start anders lautet, macht jede
    /// Fehlersuche und jede Absprache unmöglich.
    /// </summary>
    private static int StabilerStartwert(string zweck)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in zweck) hash = hash * 31 + c;
            return Math.Abs(hash);
        }
    }

    private static string ZufallsZiffern(Random zufall, int laenge)
    {
        var zeichen = new char[laenge];
        for (var i = 0; i < laenge; i++) zeichen[i] = (char)('0' + zufall.Next(0, 10));
        return new string(zeichen);
    }

    private static bool HatDreifachFolge(string text)
    {
        for (var i = 0; i + 2 < text.Length; i++)
        {
            if (text[i] == text[i + 1] && text[i] == text[i + 2]) return true;
        }
        return false;
    }

    private static bool VerbundlandMitBezirkAb100(Bundesland land) => land is
        Bundesland.Bayern or Bundesland.Brandenburg or Bundesland.MecklenburgVorpommern or
        Bundesland.Saarland or Bundesland.Sachsen or Bundesland.SachsenAnhalt or Bundesland.Thueringen;
}
