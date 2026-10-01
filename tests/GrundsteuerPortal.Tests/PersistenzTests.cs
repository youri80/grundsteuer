using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Persistence;
using GrundsteuerPortal.Persistence.Abstractions;
using GrundsteuerPortal.Persistence.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GrundsteuerPortal.Tests;

/// <summary>
/// Prüft das Speichern und Laden gegen eine echte SQLite-Datei. Diese Tests sind der Nachweis,
/// dass die Persistenz tatsächlich funktioniert - nicht nur, dass sie kompiliert.
/// </summary>
public sealed class PersistenzTests : PersistenzTestBasis
{
    [Fact]
    public async Task Entwurf_speichern_und_wieder_laden_erhaelt_alle_werte()
    {
        await Repo.InitialisierenAsync();

        var dto = BeispielMeldung();
        var antwort = await Repo.SpeichernAsync(dto);

        Assert.True(antwort.Erfolg, antwort.Meldung);
        Assert.NotNull(antwort.Id);
        Assert.NotEqual(Guid.Empty, antwort.Id!.Value);
        Assert.NotNull(antwort.RowVersion);

        var geladen = await Repo.GetAsync(antwort.Id.Value);

        Assert.NotNull(geladen);
        Assert.Equal(Bundesland.Hessen, geladen!.Bundesland);
        Assert.Equal(GrundsteuerModell.FlaechenFaktorVerfahren, geladen.BundeslandInfo.Modell);
        Assert.Equal("2601", geladen.Bundesfinanzamtsnummer);
        Assert.Equal("60 001 0001 001 005 1", geladen.Aktenzeichen);
        Assert.Equal("Echzell", geladen.Gemarkung);
        Assert.Equal("123", geladen.FlurstueckZaehler);
        Assert.Equal(Grundstuecksart.Einfamilienhaus, geladen.Grundstuecksart);
        Assert.Equal("Hauptstraße", geladen.Lage.Strasse);
        Assert.Equal("61209", geladen.Lage.Postleitzahl);
    }

    [Fact]
    public async Task Decimal_werte_ueberleben_den_sqlite_roundtrip_unveraendert()
    {
        await Repo.InitialisierenAsync();
        var antwort = await Repo.SpeichernAsync(BeispielMeldung());
        var geladen = await Repo.GetAsync(antwort.Id!.Value);

        // Das ist der Kern der TEXT-Spaltentypen: SQLite würde als REAL die Nachkommastellen
        // verlieren und ein Steuermessbetrag wäre dann falsch.
        Assert.Equal(512.75m, geladen!.Grundstuecksflaeche);
        Assert.Equal(142.5m, geladen.Wohnflaeche);
        Assert.Equal(245.50m, geladen.Bodenrichtwert);
        Assert.Equal(210.00m, geladen.DurchschnittlicherBodenrichtwert);
        Assert.Equal(1m, geladen.Eigentuemer[0].Anteil);
    }

    [Fact]
    public async Task Eigentuemer_und_flurstuecke_werden_mitgespeichert()
    {
        await Repo.InitialisierenAsync();
        var dto = BeispielMeldung();
        dto.Eigentuemer.Add(new Core.Api.EigentuemerDto
        {
            Art = EigentuemerArt.NatuerlichePerson,
            Name = "Zweiter",
            Vorname = "Max",
            Anteil = 0m, // wird unten korrigiert
            Postleitzahl = "61209",
            Ort = "Echzell"
        });
        dto.Eigentuemer[0].Anteil = 0.5m;
        dto.Eigentuemer[1].Anteil = 0.5m;

        dto.Flurstuecke.Add(new Core.Api.FlurstueckDto
        {
            Gemarkung = "Echzell", Flur = "7", Zaehler = "124", Flaeche = 310.25m, Anteil = 0.5m
        });

        var antwort = await Repo.SpeichernAsync(dto);
        var geladen = await Repo.GetAsync(antwort.Id!.Value);

        Assert.Equal(2, geladen!.Eigentuemer.Count);
        Assert.Equal(2, geladen.Flurstuecke.Count);
        Assert.Equal(0.5m, geladen.Eigentuemer[0].Anteil);
        Assert.Equal("Lyra Beispiel", geladen.Eigentuemer[0].AnzeigeName);
        Assert.Equal(310.25m, geladen.Flurstuecke[1].Flaeche);
    }

    [Fact]
    public async Task IdNr_wird_nicht_im_klartext_gespeichert()
    {
        await Repo.InitialisierenAsync();
        var dto = BeispielMeldung();
        dto.Eigentuemer[0].IdNummer = "12345678901";

        var antwort = await Repo.SpeichernAsync(dto);

        // Der eigentliche Nachweis: in der Tabelle darf die Nummer nicht stehen.
        // Rohe SQL-Abfrage über den Kontext, weil die Spalte absichtlich nicht im Modell sichtbar
        // gemappt ist - wir prüfen den tatsächlichen DB-Inhalt.
        // Wichtig: EF Core legt Guid-Werte in SQLite als 16-Byte-BLOB ab, nicht als Text -
        // ein Vergleich gegen die String-Form liefert sonst stillschweigend null Zeilen.
        var verbindung = Db.Database.GetDbConnection();
        await verbindung.OpenAsync();
        var roh = new List<string>();
        await using (var befehl = verbindung.CreateCommand())
        {
            befehl.CommandText =
                "SELECT IdNrHash FROM Eigentuemer WHERE GrundsteuerMeldungId = $id";
            var p = befehl.CreateParameter();
            p.ParameterName = "$id";
            // EF Core legt Guid-Werte in SQLite als TEXT in Großbuchstaben ab (nicht als BLOB).
            p.Value = antwort.Id!.Value.ToString().ToUpperInvariant();
            befehl.Parameters.Add(p);

            await using var leser = await befehl.ExecuteReaderAsync();
            while (await leser.ReadAsync())
            {
                roh.Add(leser.GetString(0));
            }
        }

        Assert.Single(roh);
        Assert.DoesNotContain("12345678901", roh[0]);
        Assert.Equal(64, roh[0].Length); // SHA-256 als Hex

        var geladen = await Repo.GetAsync(antwort.Id.Value);
        Assert.Equal("…901", geladen!.Eigentuemer[0].IdNummer);
    }

    [Fact]
    public async Task Speichern_erzeugt_verlaufseintrag_und_neues_rowversion()
    {
        await Repo.InitialisierenAsync();
        var antwort = await Repo.SpeichernAsync(BeispielMeldung());
        var ersteVersion = antwort.RowVersion!;

        var verlauf = await Repo.GetVerlaufAsync(antwort.Id!.Value);
        Assert.Single(verlauf);
        Assert.Equal(MeldungStatus.Entwurf, verlauf[0].Status);
        Assert.Equal("Entwurf angelegt", verlauf[0].Ausloeser);

        // Zweites Speichern: Version muss sich ändern, sonst greift die Nebenläufigkeitsprüfung nicht.
        var geladen = await Repo.GetAsync(antwort.Id.Value);
        geladen!.Gemarkung = "Wölfersheim";
        var zweite = await Repo.SpeichernAsync(geladen);

        Assert.True(zweite.Erfolg, zweite.Meldung);
        Assert.NotEqual(ersteVersion, zweite.RowVersion);
    }

    [Fact]
    public async Task Veraltetes_rowversion_wird_abgelehnt()
    {
        await Repo.InitialisierenAsync();
        var angelegt = await Repo.SpeichernAsync(BeispielMeldung());
        var id = angelegt.Id!.Value;

        // Simuliert zwei Browser-Tabs: beide laden denselben Stand.
        var tabA = await Repo.GetAsync(id);
        var tabB = await Repo.GetAsync(id);

        tabA!.Lage.Ort = "A";
        var erstes = await Repo.SpeichernAsync(tabA);
        Assert.True(erstes.Erfolg, erstes.Meldung);

        tabB!.Lage.Ort = "B";
        var zweites = await Repo.SpeichernAsync(tabB!);

        Assert.False(zweites.Erfolg);
        Assert.Equal("NEBENLAEUFIGKEIT", zweites.FehlerCode);
    }

    [Fact]
    public async Task Uebermittlung_wird_protokolliert_auch_bei_fehlschlag()
    {
        await Repo.InitialisierenAsync();
        var antwort = await Repo.SpeichernAsync(BeispielMeldung());
        var id = antwort.Id!.Value;

        var fehlversuch = await Repo.ProtokolliereUebermittlungAsync(new
            GrundsteuerPortal.Persistence.Abstractions.UebermittlungsProtokoll
        {
            MeldungId = id,
            Erfolgreich = false,
            FehlerCode = "ERiC-610001002",
            Fehlertext = "Die ELSTER-WebAPI ist nicht erreichbar.",
            AnzahlFehler = 1,
            AnzahlWarnungen = 0
        });

        Assert.Equal(1, fehlversuch);

        var uebersicht = await Repo.GetUebersichtAsync();
        Assert.Single(uebersicht);
        // Der Fehlertext muss im Dashboard sichtbar sein.
        Assert.Contains("nicht erreichbar", uebersicht[0].LetzteFehlermeldung);

        // Und der Zustand darf NICHT auf "Übermittelt" springen.
        var geladen = await Repo.GetAsync(id);
        Assert.Equal(MeldungStatus.Entwurf, geladen!.Status);
        Assert.Null(geladen.UebermitteltAm);
    }

    [Fact]
    public async Task Erfolgreiche_uebermittlung_setzt_status_und_referenz()
    {
        await Repo.InitialisierenAsync();
        var antwort = await Repo.SpeichernAsync(BeispielMeldung());
        var id = antwort.Id!.Value;

        await Repo.ProtokolliereUebermittlungAsync(new
            GrundsteuerPortal.Persistence.Abstractions.UebermittlungsProtokoll
        {
            MeldungId = id,
            Erfolgreich = true,
            Referenz = "ELSTER-2026-0004711",
            Berechnung = new Core.Api.GrundsteuerBerechnungDto { Steuermessbetrag = 123.45m }
        });

        var geladen = await Repo.GetAsync(id);
        Assert.Equal(MeldungStatus.Uebermittelt, geladen!.Status);
        Assert.Equal("ELSTER-2026-0004711", geladen.UebermittlungsReferenz);
        Assert.NotNull(geladen.UebermitteltAm);
        Assert.Equal(123.45m, geladen.Berechnung?.Steuermessbetrag);
    }

    [Fact]
    public async Task Uebermittelte_meldung_darf_nicht_geloescht_werden()
    {
        await Repo.InitialisierenAsync();
        var antwort = await Repo.SpeichernAsync(BeispielMeldung());
        var id = antwort.Id!.Value;

        await Repo.SetzeStatusAsync(id, MeldungStatus.Uebermittelt, "Test");

        Assert.False(await Repo.LoeschenAsync(id));
        Assert.NotNull(await Repo.GetAsync(id));

        // Ein Entwurf dagegen schon.
        await Repo.SetzeStatusAsync(id, MeldungStatus.Entwurf, "Zurückgesetzt");
        Assert.True(await Repo.LoeschenAsync(id));
        Assert.Null(await Repo.GetAsync(id));
    }

    [Fact]
    public async Task Statuswechsel_landen_vollstaendig_im_verlauf()
    {
        await Repo.InitialisierenAsync();
        var antwort = await Repo.SpeichernAsync(BeispielMeldung());
        var id = antwort.Id!.Value;

        await Repo.SetzeStatusAsync(id, MeldungStatus.Uebermittelt, "An ELSTER übermittelt");
        await Repo.SetzeStatusAsync(id, MeldungStatus.InPruefung, "Statusabfrage");
        await Repo.SetzeStatusAsync(id, MeldungStatus.Festgestellt, "Messbescheid erhalten",
            "Messbetrag 123,45 €");

        var verlauf = await Repo.GetVerlaufAsync(id);

        Assert.Equal(4, verlauf.Count);
        Assert.Equal(MeldungStatus.Entwurf, verlauf[0].Status);
        Assert.Equal(MeldungStatus.Festgestellt, verlauf[3].Status);
        Assert.Equal("Messbetrag 123,45 €", verlauf[3].Bemerkung);
    }

    [Fact]
    public async Task Dashboard_uebersicht_zeigt_anschrift_und_letzte_aenderung_zuerst()
    {
        await Repo.InitialisierenAsync();

        var erste = BeispielMeldung();
        erste.Gemarkung = "Älter";
        var a = await Repo.SpeichernAsync(erste);

        var zweite = BeispielMeldung();
        zweite.Gemarkung = "Neuer";
        await Repo.SpeichernAsync(zweite);

        var uebersicht = await Repo.GetUebersichtAsync();

        Assert.Equal(2, uebersicht.Count);
        // Die Bezeichnung setzt sich aus Gemarkung und Flur zusammen, sofern ein Flur vorliegt.
        Assert.Equal("Neuer, Flur 7", uebersicht[0].Grundstuecksbezeichnung);
        Assert.Equal("Hauptstraße 12", uebersicht[0].Anschrift);
        Assert.Equal("61209 Echzell", uebersicht[0].OrtZeile);
        Assert.Equal("Lyra Beispiel", uebersicht[0].HauptEigentuemer);
        Assert.Equal(GrundsteuerModell.FlaechenFaktorVerfahren, uebersicht[0].Modell);
        Assert.NotEqual(a.Id, uebersicht[0].Id);
    }

    [Fact]
    public async Task Finanzaemter_und_plz_werden_gecacht()
    {
        await Repo.InitialisierenAsync();

        var plz = await Repo.GetPlzAsync("61209");
        Assert.NotNull(plz);
        Assert.Equal("Echzell", plz!.Ort);
        Assert.Equal(Bundesland.Hessen, plz.Bundesland);

        await Repo.AktualisiereFinanzaemterAsync(new[]
        {
            new Core.Api.FinanzamtDto
            {
                Bundesfinanzamtsnummer = "2601", Name = "Finanzamt Friedberg",
                Bundesland = Bundesland.Hessen, Ort = "Friedberg"
            }
        });

        var aemter = await Repo.GetFinanzaemterAsync(Bundesland.Hessen);
        Assert.Single(aemter);
        Assert.Equal("2601 – Finanzamt Friedberg", aemter[0].Anzeige);

        // Zweites Aktualisieren darf keinen Doppeleintrag erzeugen.
        await Repo.AktualisiereFinanzaemterAsync(new[]
        {
            new Core.Api.FinanzamtDto
            {
                Bundesfinanzamtsnummer = "2601", Name = "Finanzamt Friedberg (neu)",
                Bundesland = Bundesland.Hessen
            }
        });

        var erneut = await Repo.GetFinanzaemterAsync(Bundesland.Hessen);
        Assert.Single(erneut);
        Assert.Contains("neu", erneut[0].Name);
    }
}
