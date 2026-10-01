using GrundsteuerPortal.Core.Domain;

namespace GrundsteuerPortal.Core.Api;

/// <summary>
/// Baut aus einer Wirtschaftseinheit und der meldenden Person eine neue Meldung als Snapshot.
///
/// Die Meldung übernimmt alle fachlichen Felder (Gemarkung, Flächen, Lage, Flurstücke) als Kopie
/// und ist danach eigenständig - eine abgegebene Erklärung muss den Stand zum Erklärungszeitpunkt
/// festhalten, auch wenn sich Einheit oder Person später ändern.
/// </summary>
public static class MeldungAusEinheit
{
    /// <summary>Snapshot-Meldung erzeugen (Id bleibt leer - das Repository erkennt daran einen neuen Entwurf).</summary>
    public static GrundsteuerMeldungDto Erzeuge(WirtschaftseinheitDto einheit, PersonDto meldendePerson)
    {
        var meldung = new GrundsteuerMeldungDto
        {
            Id = Guid.Empty,
            Status = MeldungStatus.Entwurf,
            WirtschaftseinheitId = einheit.Id,
            MeldendePersonId = meldendePerson.Id,
            MeldendePersonName = meldendePerson.AnzeigeName,

            Bundesland = einheit.Bundesland,
            Erklaerungsart = Erklaerungsart.Erstmalig,
            Hauptfeststellungszeitpunkt = 2022,
            Bundesfinanzamtsnummer = einheit.Bundesfinanzamtsnummer,
            FinanzamtName = einheit.FinanzamtName,

            Gemarkung = einheit.Gemarkung,
            Gemarkungsnummer = einheit.Gemarkungsnummer,
            Flur = einheit.Flur,
            FlurstueckZaehler = einheit.FlurstueckZaehler,
            FlurstueckNenner = einheit.FlurstueckNenner,
            Grundbuchblatt = einheit.Grundbuchblatt,
            Grundstuecksart = einheit.Grundstuecksart,
            Grundstuecksflaeche = einheit.Grundstuecksflaeche,
            Wohnflaeche = einheit.Wohnflaeche,
            Nutzflaeche = einheit.Nutzflaeche,
            Baujahr = einheit.Baujahr,
            Bodenrichtwert = einheit.Bodenrichtwert,
            DurchschnittlicherBodenrichtwert = einheit.DurchschnittlicherBodenrichtwert,
            Wohnlage = einheit.Wohnlage,
            IstDenkmalgeschuetzt = einheit.IstDenkmalgeschuetzt,
            IstSozialerWohnungsbau = einheit.IstSozialerWohnungsbau,

            Lage = new AdresseDto
            {
                Strasse = einheit.Lage.Strasse,
                Hausnummer = einheit.Lage.Hausnummer,
                HausnummerZusatz = einheit.Lage.HausnummerZusatz,
                Postleitzahl = einheit.Lage.Postleitzahl,
                Ort = einheit.Lage.Ort,
                Ortsteil = einheit.Lage.Ortsteil,
                Land = einheit.Lage.Land
            }
        };

        meldung.Flurstuecke.AddRange(einheit.Flurstuecke.Select(f => new FlurstueckDto
        {
            Id = Guid.Empty,
            Gemarkung = f.Gemarkung,
            Gemarkungsnummer = f.Gemarkungsnummer,
            Flur = f.Flur,
            Zaehler = f.Zaehler,
            Nenner = f.Nenner,
            Flaeche = f.Flaeche,
            Anteil = f.Anteil
        }));

        return meldung;
    }
}
