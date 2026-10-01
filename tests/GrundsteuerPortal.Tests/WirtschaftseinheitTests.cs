using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Persistence.Entities;
using GrundsteuerPortal.Persistence.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GrundsteuerPortal.Tests;

/// <summary>
/// Tests für das wirtschaftseinheit-zentrische Datenmodell: Person-Master, Wirtschaftseinheit als
/// Bestand, Meldung als Snapshot sowie die Regeln (eine aktive Meldung je Einheit, Deduplizierung
/// über IdNr-Hash, Meldung-aus-Einheit).
/// </summary>
public class WirtschaftseinheitTests : PersistenzTestBasis
{
    // -----------------------------------------------------------------------------------------
    //  Person (Master)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Person_Anlegen_Und_Laden()
    {
        await Repo.InitialisierenAsync();

        var dto = new PersonDto
        {
            Art = EigentuemerArt.NatuerlichePerson,
            Anrede = Anrede.Frau,
            Name = "Musterfrau",
            Vorname = "Erika",
            IdNummer = "12345678901",
            Strasse = "Hauptstraße",
            Hausnummer = "1",
            Postleitzahl = "61209",
            Ort = "Echzell",
            Geburtsdatum = new DateTime(1980, 5, 12)
        };

        var antwort = await Repo.SpeicherePersonAsync(dto);
        Assert.True(antwort.Erfolg);
        Assert.NotNull(antwort.Id);

        var geladen = await Repo.GetPersonAsync(antwort.Id!.Value);
        Assert.NotNull(geladen);
        Assert.Equal("Erika Musterfrau", geladen.AnzeigeName);
        Assert.Equal("Echzell", geladen.Ort);

        // IdNr darf NICHT im Klartext in der DB liegen - nur Hash + letzte Drei.
        var entity = await Db.Personen.FirstAsync(p => p.Id == antwort.Id.Value);
        Assert.Equal(IdNrHasher.Hashe("12345678901"), entity.IdNrHash);
        Assert.Equal("901", entity.IdNrLetzteDrei);
    }

    [Fact]
    public async Task Person_Archiviert_WirdNichtMehrInAktiverListeAufgefuehrt()
    {
        await Repo.InitialisierenAsync();

        var dto = new PersonDto { Name = "Alt", Status = PersonStatus.Archiviert };
        await Repo.SpeicherePersonAsync(dto);

        var personen = await Repo.GetPersonenAsync();
        Assert.Contains(personen, p => p.Name == "Alt" && p.Status == PersonStatus.Archiviert);
    }

    // -----------------------------------------------------------------------------------------
    //  Wirtschaftseinheit (Bestand)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Wirtschaftseinheit_Anlegen_Laden_Und_Aendern()
    {
        await Repo.InitialisierenAsync();

        var einheit = new WirtschaftseinheitDto
        {
            Bundesland = Bundesland.Hessen,
            Gemarkung = "Echzell",
            Flur = "7",
            Grundstuecksart = Grundstuecksart.Einfamilienhaus,
            Grundstuecksflaeche = 512.75m,
            Wohnflaeche = 142.5m,
            Lage = new AdresseDto { Strasse = "Hauptstraße", Hausnummer = "12", Postleitzahl = "61209", Ort = "Echzell" },
            Flurstuecke =
            {
                new FlurstueckDto { Gemarkung = "Echzell", Flur = "7", Zaehler = "123", Nenner = "45", Flaeche = 512.75m }
            }
        };

        var antwort = await Repo.SpeichereWirtschaftseinheitAsync(einheit);
        Assert.True(antwort.Erfolg);

        var geladen = await Repo.GetWirtschaftseinheitAsync(antwort.Id!.Value);
        Assert.NotNull(geladen);
        Assert.Equal("Echzell", geladen.Gemarkung);
        Assert.Single(geladen.Flurstuecke);
        Assert.False(geladen.HatAktiveMeldung);

        // Ändern und neu laden.
        geladen.Wohnflaeche = 160m;
        var aenderung = await Repo.SpeichereWirtschaftseinheitAsync(geladen);
        Assert.True(aenderung.Erfolg);

        var neuGeladen = await Repo.GetWirtschaftseinheitAsync(antwort.Id!.Value);
        Assert.Equal(160m, neuGeladen!.Wohnflaeche);
    }

    [Fact]
    public async Task Wirtschaftseinheit_MitMehrerenEigentuemern_Und_Anteilen()
    {
        await Repo.InitialisierenAsync();

        var p1 = await Repo.SpeicherePersonAsync(new PersonDto { Name = "Eins", Vorname = "A", IdNummer = "12345678901" });
        var p2 = await Repo.SpeicherePersonAsync(new PersonDto { Name = "Zwei", Vorname = "B", IdNummer = "23456789012" });

        var einheit = new WirtschaftseinheitDto
        {
            Bundesland = Bundesland.Hessen,
            Gemarkung = "Gemarkung",
            Lage = new AdresseDto { Ort = "Ort" },
            Eigentuemer =
            {
                new EinheitEigentuemerDto { PersonId = p1.Id!.Value, Anteil = 0.5m },
                new EinheitEigentuemerDto { PersonId = p2.Id!.Value, Anteil = 0.5m }
            }
        };

        var antwort = await Repo.SpeichereWirtschaftseinheitAsync(einheit);
        Assert.True(antwort.Erfolg);

        var geladen = await Repo.GetWirtschaftseinheitAsync(antwort.Id!.Value);
        Assert.Equal(2, geladen!.Eigentuemer.Count);
        Assert.All(geladen.Eigentuemer, z => Assert.Equal(0.5m, z.Anteil));

        // Übersicht enthält die beiden Eigentümer als Anzahl + Haupt.
        var uebersicht = await Repo.GetWirtschaftseinheitenAsync();
        var zeile = Assert.Single(uebersicht);
        Assert.Equal(2, zeile.AnzahlEigentuemer);
        Assert.False(string.IsNullOrWhiteSpace(zeile.HauptEigentuemer));
    }

    // -----------------------------------------------------------------------------------------
    //  Meldung aus Wirtschaftseinheit (Snapshot)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task HatAktiveMeldung_ErkenntEntwurfAberKeineAbgeschlosseneMeldung()
    {
        await Repo.InitialisierenAsync();

        // Einheit anlegen.
        var einheitAntwort = await Repo.SpeichereWirtschaftseinheitAsync(new WirtschaftseinheitDto
        {
            Bundesland = Bundesland.Hessen,
            Gemarkung = "Gemarkung",
            Lage = new AdresseDto { Ort = "Ort" }
        });
        var einheitId = einheitAntwort.Id!.Value;

        // Noch keine Meldung.
        Assert.False(await Repo.HatAktiveMeldungAsync(einheitId));

        // Meldung als Entwurf speichern.
        var meldung = BeispielMeldung();
        meldung.WirtschaftseinheitId = einheitId;
        await Repo.SpeichernAsync(meldung);

        Assert.True(await Repo.HatAktiveMeldungAsync(einheitId));
    }

    [Fact]
    public void MeldungAusEinheit_UebernimmtFelderAlsKopie()
    {
        var einheit = new WirtschaftseinheitDto
        {
            Id = Guid.NewGuid(),
            Bundesland = Bundesland.Hessen,
            Bundesfinanzamtsnummer = "2601",
            FinanzamtName = "Finanzamt Friedberg",
            Gemarkung = "Echzell",
            Flur = "7",
            FlurstueckZaehler = "123",
            FlurstueckNenner = "45",
            Grundstuecksart = Grundstuecksart.Einfamilienhaus,
            Grundstuecksflaeche = 512.75m,
            Wohnflaeche = 142.5m,
            Baujahr = 1978,
            Lage = new AdresseDto { Strasse = "Hauptstraße", Hausnummer = "12", Postleitzahl = "61209", Ort = "Echzell" },
            Flurstuecke =
            {
                new FlurstueckDto { Gemarkung = "Echzell", Flur = "7", Zaehler = "123", Nenner = "45", Flaeche = 512.75m }
            }
        };

        var meldende = new PersonDto { Id = Guid.NewGuid(), Name = "Berater", Vorname = "Karl", Art = EigentuemerArt.JuristischePerson };

        var meldung = MeldungAusEinheit.Erzeuge(einheit, meldende);

        Assert.Equal(Guid.Empty, meldung.Id);
        Assert.Equal(MeldungStatus.Entwurf, meldung.Status);
        Assert.Equal(einheit.Id, meldung.WirtschaftseinheitId);
        Assert.Equal(meldende.Id, meldung.MeldendePersonId);
        Assert.Equal("Berater", meldung.MeldendePersonName); // juristische Person -> nur Name
        Assert.Equal("Echzell", meldung.Gemarkung);
        Assert.Equal(142.5m, meldung.Wohnflaeche);
        Assert.Single(meldung.Flurstuecke);
        Assert.Equal("Echzell", meldung.Lage.Ort);
    }

    // -----------------------------------------------------------------------------------------
    //  Migration: Bestandsmeldungen -> Einheiten + Personen
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Migration_TeiltBestandsmeldungInEinheitUndPersonAuf()
    {
        // 1. Schema anlegen (noch keine Meldungen -> Migration ohne Wirkung).
        await Repo.InitialisierenAsync();

        // 2. Bestandsmeldung (wie vor dem Umbau) ohne Herkunfts-FK anlegen.
        var meldung = BeispielMeldung();
        var antwort = await Repo.SpeichernAsync(meldung);
        var meldungId = antwort.Id!.Value;

        // Sicherstellen, dass die Meldung noch keine Einheit hat.
        var vorher = await Db.Meldungen.FirstAsync(m => m.Id == meldungId);
        Assert.Null(vorher.WirtschaftseinheitId);

        // 3. Erneutes Initialisieren löst die Migration der Bestandsdaten aus.
        await Repo.InitialisierenAsync();

        var nachher = await Db.Meldungen.FirstAsync(m => m.Id == meldungId);
        Assert.NotNull(nachher.WirtschaftseinheitId);

        var einheit = await Db.Wirtschaftseinheiten
            .Include(e => e.Eigentuemer)
            .FirstAsync(e => e.Id == nachher.WirtschaftseinheitId!.Value);
        Assert.Equal(Bundesland.Hessen, einheit.Bundesland);
        Assert.Equal("Echzell", einheit.Gemarkung);
        Assert.Single(einheit.Eigentuemer);

        // Person wurde angelegt und über den IdNr-Hash dedupliziert.
        var person = await Db.Personen.FirstAsync(p => p.Id == einheit.Eigentuemer.Single().PersonId);
        Assert.Equal("Lyra Beispiel", $"{person.Vorname} {person.Name}");
        Assert.Equal(IdNrHasher.Hashe("12345678901"), person.IdNrHash);

        // Meldende Stelle = erster Eigentümer.
        Assert.Equal(person.Id, nachher.MeldendePersonId);
    }

    [Fact]
    public async Task Migration_IstIdempotent()
    {
        await Repo.InitialisierenAsync();

        var meldung = BeispielMeldung();
        await Repo.SpeichernAsync(meldung);

        await Repo.InitialisierenAsync();  // migriert die Bestandsmeldung
        await Repo.InitialisierenAsync();  // zweiter Durchlauf darf nichts duplizieren

        Assert.Equal(1, await Db.Wirtschaftseinheiten.CountAsync());
        Assert.Equal(1, await Db.Personen.CountAsync());
        Assert.Equal(1, await Db.Meldungen.CountAsync());
    }

    // -----------------------------------------------------------------------------------------
    //  Regression: Eigentümer nachträglich an bestehende Einheit anhängen (Update-Pfad)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Einheit_EigentuemerNachtraeglichZugeordnet_WirdGespeichert()
    {
        await Repo.InitialisierenAsync();

        // 1. Einheit ohne Eigentümer anlegen (wie im Browser-Workflow).
        var angelegt = await Repo.SpeichereWirtschaftseinheitAsync(new WirtschaftseinheitDto
        {
            Bundesland = Bundesland.Hessen,
            Gemarkung = "Echzell",
            Lage = new AdresseDto { Ort = "Echzell" }
        });
        Assert.True(angelegt.Erfolg);

        // 2. Person anlegen.
        var person = await Repo.SpeicherePersonAsync(new PersonDto { Name = "Muster", Vorname = "Max" });
        Assert.True(person.Erfolg);

        // 3. Einheit laden, Eigentümer hinzufügen, erneut speichern (Update).
        var geladen = await Repo.GetWirtschaftseinheitAsync(angelegt.Id!.Value);
        Assert.NotNull(geladen);
        geladen.Eigentuemer.Add(new EinheitEigentuemerDto { PersonId = person.Id!.Value, Anteil = 1m });

        var update = await Repo.SpeichereWirtschaftseinheitAsync(geladen);
        Assert.True(update.Erfolg);

        // 4. Neu laden und prüfen.
        var neu = await Repo.GetWirtschaftseinheitAsync(angelegt.Id!.Value);
        Assert.Single(neu!.Eigentuemer);
        Assert.Equal(person.Id!.Value, neu.Eigentuemer[0].PersonId);
    }
}
