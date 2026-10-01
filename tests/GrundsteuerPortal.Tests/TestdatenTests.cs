using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Core.Testdaten;
using GrundsteuerPortal.Core.Validation;
using Xunit;

namespace GrundsteuerPortal.Tests;

/// <summary>
/// Prüft die erzeugten Testdaten gegen den echten Validator.
///
/// Das ist der Kern der Sache: die Testdaten sind nur brauchbar, wenn der Wizard sie akzeptiert.
/// Ein Datensatz, der die Prüfung nicht besteht, führt beim Durchspielen in eine Sackgasse - und
/// genau das fällt ohne diesen Test erst in der Oberfläche auf.
/// </summary>
public class TestdatenTests
{
    // -----------------------------------------------------------------------------------------
    //  Der Datensatz als Ganzes
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void AlleDatensaetze_HabenKeineFehler()
    {
        var alle = TestdatenFactory.Alle();
        Assert.NotEmpty(alle);

        var fehlerhaft = new List<string>();

        foreach (var satz in alle)
        {
            var hinweise = MeldungsValidator.PruefeAlles(satz.Meldung);
            foreach (var fehler in hinweise.Where(h => h.Schwere == HinweisSchwere.Fehler))
                fehlerhaft.Add($"{satz.Bezeichnung}: [{fehler.Feld}] {fehler.Meldung}");
        }

        Assert.True(fehlerhaft.Count == 0,
            "Die Testdaten enthalten Fehler, die den Wizard blockieren würden:\n  "
            + string.Join("\n  ", fehlerhaft));
    }

    [Fact]
    public void AlleDatensaetze_SindInJedemSchrittFertig()
    {
        foreach (var satz in TestdatenFactory.Alle())
        {
            foreach (var schritt in new[]
                     {
                         WizardSchritt.AllgemeineAngaben,
                         WizardSchritt.Grundstuecksdaten,
                         WizardSchritt.Eigentuemer
                     })
            {
                Assert.True(MeldungsValidator.IstFertig(satz.Meldung, schritt),
                    $"{satz.Bezeichnung}: Schritt {schritt} ist nicht fehlerfrei.");
            }
        }
    }

    [Fact]
    public void AlleDatensaetze_HabenEineBezeichnungUndEinenZweck()
    {
        foreach (var satz in TestdatenFactory.Alle())
        {
            Assert.False(string.IsNullOrWhiteSpace(satz.Bezeichnung));
            Assert.False(string.IsNullOrWhiteSpace(satz.Zweck));
        }
    }

    [Fact]
    public void AlleDatensaetze_SindEntwuerfeOhneUebermittlung()
    {
        // Ein Testdatensatz darf nie als übermittelt erscheinen - er war nie beim Finanzamt.
        foreach (var satz in TestdatenFactory.Alle())
        {
            Assert.Equal(MeldungStatus.Entwurf, satz.Meldung.Status);
            Assert.Null(satz.Meldung.UebermitteltAm);
            Assert.Null(satz.Meldung.UebermittlungsReferenz);
        }
    }

    // -----------------------------------------------------------------------------------------
    //  Abdeckung: jeder Weg einmal
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Datensaetze_DeckenBeideOrdnungskriterienAb()
    {
        var laender = TestdatenFactory.Alle().Select(s => s.Meldung.Bundesland).ToList();

        Assert.Contains(laender, l => BundeslandKatalog.Fuer(l).Ordnungskriterium == Ordnungskriterium.Aktenzeichen);
        Assert.Contains(laender, l => BundeslandKatalog.Fuer(l).Ordnungskriterium == Ordnungskriterium.Steuernummer);
    }

    [Fact]
    public void Datensaetze_DeckenAlleBerechnungsmodelleAb()
    {
        var modelle = TestdatenFactory.Alle()
            .Select(s => BundeslandKatalog.Fuer(s.Meldung.Bundesland).Modell)
            .Distinct()
            .ToList();

        foreach (var modell in Enum.GetValues<GrundsteuerModell>())
            Assert.Contains(modell, modelle);

        // Die fünf Landesmodelle müssen vertreten sein; das Bundesmodell zusätzlich.
        Assert.Equal(Enum.GetValues<GrundsteuerModell>().Length, modelle.Count);
    }

    [Fact]
    public void Datensaetze_DeckenBebautUndUnbebautAb()
    {
        var arten = TestdatenFactory.Alle().Select(s => s.Meldung.Grundstuecksart).ToList();
        Assert.Contains(Grundstuecksart.Unbebaut, arten);
        Assert.Contains(arten, a => a != Grundstuecksart.Unbebaut && a != Grundstuecksart.LandForstwirtschaft);
    }

    [Fact]
    public void Datensaetze_DeckenNatuerlicheUndJuristischePersonenAb()
    {
        var alle = TestdatenFactory.Alle().SelectMany(s => s.Meldung.Eigentuemer).ToList();
        Assert.Contains(alle, e => e.Art == EigentuemerArt.NatuerlichePerson);
        Assert.Contains(alle, e => e.Art == EigentuemerArt.JuristischePerson);
    }

    [Fact]
    public void Datensaetze_EnthaltenMiteigentumMitZweiEigentuemern()
    {
        Assert.Contains(TestdatenFactory.Alle(), s => s.Meldung.Eigentuemer.Count > 1);
    }

    // -----------------------------------------------------------------------------------------
    //  Die Nummern einzeln - jede gegen den Validator
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void AlleIdNummern_SindGueltig()
    {
        foreach (var satz in TestdatenFactory.Alle())
        {
            foreach (var e in satz.Meldung.Eigentuemer.Where(e => e.Art == EigentuemerArt.NatuerlichePerson))
            {
                var pruefung = ElsterFormate.PruefeIdNr(e.IdNummer);
                Assert.True(pruefung.IstGueltig,
                    $"{satz.Bezeichnung}, Eigentümer {e.AnzeigeName}: {pruefung.Meldung}");
            }
        }
    }

    [Fact]
    public void AlleSteuernummernUndAktenzeichen_SindGueltig()
    {
        foreach (var satz in TestdatenFactory.Alle())
        {
            var land = satz.Meldung.Bundesland;

            if (BundeslandKatalog.Fuer(land).Ordnungskriterium == Ordnungskriterium.Steuernummer)
            {
                var pruefung = ElsterFormate.PruefeSteuernummerElster(satz.Meldung.Steuernummer, land);
                Assert.True(pruefung.IstGueltig,
                    $"{satz.Bezeichnung}: Steuernummer ungültig - {pruefung.Meldung}");
            }
            else
            {
                var pruefung = ElsterFormate.PruefeAktenzeichen(satz.Meldung.Aktenzeichen, land);
                Assert.True(pruefung.IstGueltig,
                    $"{satz.Bezeichnung}: Aktenzeichen ungültig - {pruefung.Meldung} {pruefung.Vorschlag}");
            }
        }
    }

    [Fact]
    public void AlleBundesfinanzamtsnummern_SindZulaessig()
    {
        // Der häufigste Grund, warum eine sonst gültige Meldung abgelehnt wird: eine BUFA, die
        // für das Land im Grundsteuerverfahren nicht freigegeben ist.
        foreach (var satz in TestdatenFactory.Alle())
        {
            var land = satz.Meldung.Bundesland;
            var bufa = satz.Meldung.Bundesfinanzamtsnummer;

            Assert.True(Bundesfinanzamtsnummern.IstZulaessig(bufa, land),
                $"{satz.Bezeichnung}: BUFA {bufa} ist für {land.AnzeigeName()} nicht zugelassen.");
        }
    }

    [Fact]
    public void AlleFinanzamtAuswahlen_EnthaltenDenDatensatz()
    {
        // Der Datensatz muss in der Liste stehen, die der Wizard zur Auswahl anbietet - sonst
        // findet der Nutzer "sein" Finanzamt nicht und der Durchlauf gelingt nicht.
        foreach (var satz in TestdatenFactory.Alle())
        {
            var land = satz.Meldung.Bundesland;
            var liste = FinanzamtKatalog.Erzeuge(land);
            var bufa = satz.Meldung.Bundesfinanzamtsnummer;

            Assert.True(liste.Any(f => f.Bundesfinanzamtsnummer == bufa),
                $"{satz.Bezeichnung}: BUFA {bufa} fehlt in der Finanzamt-Auswahl für {land.AnzeigeName()}.");
        }
    }

    [Fact]
    public void FinanzamtKatalog_LiefertNurZulaessigeNummern()
    {
        // Die Auswahlliste darf nichts enthalten, was die Prüfung ablehnt - sonst führt jede
        // Auswahl in eine Sackgasse.
        foreach (var land in BundeslandKatalog.Alle.Keys)
        {
            var liste = FinanzamtKatalog.Erzeuge(land);
            Assert.NotEmpty(liste);

            foreach (var fa in liste)
                Assert.True(Bundesfinanzamtsnummern.IstZulaessig(fa.Bundesfinanzamtsnummer, land),
                    $"{land.AnzeigeName()}: unzulässige BUFA {fa.Bundesfinanzamtsnummer} im Katalog.");
        }
    }

    [Fact]
    public void FinanzamtKatalog_LiefertKeineDubletten()
    {
        foreach (var land in BundeslandKatalog.Alle.Keys)
        {
            var liste = FinanzamtKatalog.Erzeuge(land);
            var nummern = liste.Select(f => f.Bundesfinanzamtsnummer).ToList();
            Assert.Equal(nummern.Count, nummern.Distinct().Count());
        }
    }

    // -----------------------------------------------------------------------------------------
    //  Berechenbarkeit: Schritt 4 soll etwas anzeigen
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Landesmodelle_LiefernEinenBerechenbarenMessbetrag()
    {
        // Bei den reinen Landesmodellen ist der Rechenweg im Frontend abbildbar - dort muss in
        // Schritt 4 ein Messbetrag erscheinen, sonst wirkt die Vorschau kaputt.
        foreach (var satz in TestdatenFactory.Alle())
        {
            var info = BundeslandKatalog.Fuer(satz.Meldung.Bundesland);
            if (info.Modell == GrundsteuerModell.Bundesmodell) continue;

            var ergebnis = MessbetragRechner.Berechne(satz.Meldung);

            Assert.NotNull(ergebnis.Berechnungsweg);
            Assert.True(ergebnis.Steuermessbetrag > 0,
                $"{satz.Bezeichnung}: kein Messbetrag berechnet (Modell {info.Modell}).");
        }
    }

    [Fact]
    public void HessenUndNiedersachsen_HabenEinenPlausiblenLagefaktor()
    {
        // Ein Lagefaktor außerhalb von 0,5 bis 1,5 erzeugt eine Warnung im Wizard. Die Testdaten
        // sollen den Normalfall zeigen, nicht den Warnfall.
        foreach (var satz in TestdatenFactory.Alle())
        {
            var land = satz.Meldung.Bundesland;
            if (land is not (Bundesland.Hessen or Bundesland.Niedersachsen)) continue;

            var faktor = MessbetragRechner.Lagefaktor(
                satz.Meldung.Bodenrichtwert, satz.Meldung.DurchschnittlicherBodenrichtwert);

            Assert.NotNull(faktor);
            Assert.True(MessbetragRechner.IstLagefaktorPlausibel(faktor),
                $"{satz.Bezeichnung}: Lagefaktor {faktor} liegt außerhalb des plausiblen Bereichs.");
        }
    }

    // -----------------------------------------------------------------------------------------
    //  Wiederholbarkeit
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ZweiLaeufe_ErzeugenDieselbenNummern()
    {
        // Die Nummern sind bewusst stabil: ein Testdatensatz, der bei jedem Start anders lautet,
        // macht jede Fehlersuche und jede Absprache unmöglich.
        var ersterLauf = TestdatenFactory.Alle()
            .Select(s => s.Meldung.Aktenzeichen ?? s.Meldung.Steuernummer).ToList();
        var zweiterLauf = TestdatenFactory.Alle()
            .Select(s => s.Meldung.Aktenzeichen ?? s.Meldung.Steuernummer).ToList();

        Assert.Equal(ersterLauf, zweiterLauf);
    }

    [Fact]
    public void AlleDatensaetze_SindNeueEntwuerfeMitLeererId()
    {
        // Das Repository erkennt einen neuen Entwurf an `Id == Guid.Empty`. Eine vorab gesetzte Id
        // lässt es einen bestehenden Datensatz erwarten und bricht mit "Meldung existiert nicht"
        // ab. Der Fehler wäre erst beim Speichern in der Oberfläche aufgetreten.
        foreach (var satz in TestdatenFactory.Alle())
        {
            Assert.Equal(Guid.Empty, satz.Meldung.Id);
            Assert.Null(satz.Meldung.RowVersion);
        }
    }

    [Fact]
    public void IdNummernSindUntereinanderVerschieden()
    {
        var nummern = TestdatenFactory.Alle()
            .SelectMany(s => s.Meldung.Eigentuemer)
            .Where(e => !string.IsNullOrWhiteSpace(e.IdNummer))
            .Select(e => e.IdNummer!)
            .ToList();

        Assert.Equal(nummern.Count, nummern.Distinct().Count());
    }
}
