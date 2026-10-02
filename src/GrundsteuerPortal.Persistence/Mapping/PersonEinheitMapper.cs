using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Persistence.Entities;
using GrundsteuerPortal.Persistence.Security;

namespace GrundsteuerPortal.Persistence.Mapping;

/// <summary>
/// Übersetzt zwischen den DTOs für Person und Wirtschaftseinheit und den EF-Entities.
/// Gleiche Grenze wie im <see cref="MeldungMapper"/>: die IdNr liegt im DTO als Klartext vor,
/// in der Entity nur als Hash + letzte drei Stellen.
/// </summary>
public static class PersonEinheitMapper
{
    // -----------------------------------------------------------------------------------------
    //  Person: Entity -> DTO
    // -----------------------------------------------------------------------------------------
    public static PersonDto ZuDto(PersonEntity e) => new()
    {
        Id = e.Id,
        RowVersion = e.RowVersion,
        Status = e.Status,
        Art = e.Art,
        Anrede = e.Anrede,
        Name = e.Name,
        Vorname = e.Vorname,
        IdNummer = string.IsNullOrWhiteSpace(e.IdNrLetzteDrei) ? null : $"…{e.IdNrLetzteDrei}",
        Steuernummer = e.Steuernummer,
        Strasse = e.Strasse,
        Hausnummer = e.Hausnummer,
        Postleitzahl = e.Postleitzahl,
        Ort = e.Ort,
        Land = e.Land,
        Geburtsdatum = e.Geburtsdatum
    };

    public static PersonUebersichtDto ZuUebersicht(PersonEntity e) => new()
    {
        Id = e.Id,
        Status = e.Status,
        Art = e.Art,
        Name = e.Name,
        Vorname = e.Vorname,
        Ort = e.Ort
    };

    // -----------------------------------------------------------------------------------------
    //  Person: DTO -> Entity
    // -----------------------------------------------------------------------------------------
    public static void Uebernehme(PersonDto dto, PersonEntity e)
    {
        e.Status = dto.Status;
        e.Art = dto.Art;
        e.Anrede = dto.Anrede;
        e.Name = dto.Name;
        e.Vorname = dto.Vorname;
        e.Steuernummer = dto.Steuernummer;
        e.Strasse = dto.Strasse;
        e.Hausnummer = dto.Hausnummer;
        e.Postleitzahl = dto.Postleitzahl;
        e.Ort = dto.Ort;
        e.Land = dto.Land;
        e.Geburtsdatum = dto.Geburtsdatum;

        var istPlatzhalter = dto.IdNummer?.StartsWith('…') == true;
        e.IdNrHash = istPlatzhalter ? e.IdNrHash : IdNrHasher.Hashe(dto.IdNummer);
        e.IdNrLetzteDrei = istPlatzhalter
            ? (e.IdNrLetzteDrei ?? dto.IdNummer?[1..])
            : IdNrHasher.LetzteDrei(dto.IdNummer);
    }

    // -----------------------------------------------------------------------------------------
    //  Wirtschaftseinheit: Entity -> DTO
    // -----------------------------------------------------------------------------------------
    public static WirtschaftseinheitDto ZuDto(WirtschaftseinheitEntity e, bool hatAktiveMeldung)
    {
        var dto = new WirtschaftseinheitDto
        {
            Id = e.Id,
            RowVersion = e.RowVersion,
            Status = e.Status,
            Bundesland = e.Bundesland,
            Bundesfinanzamtsnummer = e.Bundesfinanzamtsnummer,
            FinanzamtName = e.FinanzamtName,
            Gemarkung = e.Gemarkung,
            Gemarkungsnummer = e.Gemarkungsnummer,
            Flur = e.Flur,
            FlurstueckZaehler = e.FlurstueckZaehler,
            FlurstueckNenner = e.FlurstueckNenner,
            Grundbuchblatt = e.Grundbuchblatt,
            Grundstuecksart = e.Grundstuecksart,
            Grundstuecksflaeche = e.Grundstuecksflaeche,
            Wohnflaeche = e.Wohnflaeche,
            Nutzflaeche = e.Nutzflaeche,
            Baujahr = e.Baujahr,
            Bodenrichtwert = e.Bodenrichtwert,
            DurchschnittlicherBodenrichtwert = e.DurchschnittlicherBodenrichtwert,
            Wohnlage = e.Wohnlage,
            IstDenkmalgeschuetzt = e.IstDenkmalgeschuetzt,
            IstSozialerWohnungsbau = e.IstSozialerWohnungsbau,
            HatAktiveMeldung = hatAktiveMeldung,
            Lage = new AdresseDto
            {
                Strasse = e.Lage.Strasse,
                Hausnummer = e.Lage.Hausnummer,
                HausnummerZusatz = e.Lage.HausnummerZusatz,
                Postleitzahl = e.Lage.Postleitzahl,
                Ort = e.Lage.Ort,
                Ortsteil = e.Lage.Ortsteil,
                Land = e.Lage.Land
            }
        };

        dto.Flurstuecke.AddRange(e.Flurstuecke
            .OrderBy(f => f.Reihenfolge)
            .Select(f => new FlurstueckDto
            {
                Id = f.Id,
                Gemarkung = f.Gemarkung,
                Gemarkungsnummer = f.Gemarkungsnummer,
                Flur = f.Flur,
                Zaehler = f.Zaehler,
                Nenner = f.Nenner,
                Flaeche = f.Flaeche,
                Anteil = f.Anteil
            }));

        dto.Eigentuemer.AddRange(e.Eigentuemer
            .OrderBy(x => x.Reihenfolge)
            .Select(x => new EinheitEigentuemerDto
            {
                Id = x.Id,
                PersonId = x.PersonId,
                Anteil = x.Anteil,
                AnzeigeName = x.Person?.AnzeigeName ?? string.Empty,
                Art = x.Person?.Art ?? EigentuemerArt.NatuerlichePerson
            }));

        return dto;
    }

    public static WirtschaftseinheitUebersichtDto ZuUebersicht(WirtschaftseinheitEntity e) => new()
    {
        Id = e.Id,
        Status = e.Status,
        Bundesland = e.Bundesland,
        Gemarkung = e.Gemarkung,
        Flur = e.Flur,
        Strasse = e.Lage.Strasse,
        Hausnummer = e.Lage.Hausnummer,
        Ort = e.Lage.Ort,
        HauptEigentuemer = e.Eigentuemer
            .OrderBy(x => x.Reihenfolge)
            .Select(x => x.Person?.AnzeigeName ?? string.Empty)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? string.Empty,
        AnzahlEigentuemer = e.Eigentuemer.Count,
        AnzahlFlurstuecke = e.Flurstuecke.Count,
        ZuletztGeaendertAm = e.ZuletztGeaendertAm
    };

    // -----------------------------------------------------------------------------------------
    //  Wirtschaftseinheit: DTO -> Entity
    // -----------------------------------------------------------------------------------------
    public static void Uebernehme(WirtschaftseinheitDto dto, WirtschaftseinheitEntity e)
    {
        e.Status = dto.Status;
        e.Bundesland = dto.Bundesland;
        e.Modell = dto.BundeslandInfo.Modell;
        e.Bundesfinanzamtsnummer = dto.Bundesfinanzamtsnummer;
        e.FinanzamtName = dto.FinanzamtName;
        e.Gemarkung = dto.Gemarkung;
        e.Gemarkungsnummer = dto.Gemarkungsnummer;
        e.Flur = dto.Flur;
        e.FlurstueckZaehler = dto.FlurstueckZaehler;
        e.FlurstueckNenner = dto.FlurstueckNenner;
        e.Grundbuchblatt = dto.Grundbuchblatt;
        e.Grundstuecksart = dto.Grundstuecksart;
        e.Grundstuecksflaeche = dto.Grundstuecksflaeche;
        e.Wohnflaeche = dto.Wohnflaeche;
        e.Nutzflaeche = dto.Nutzflaeche;
        e.Baujahr = dto.Baujahr;
        e.Bodenrichtwert = dto.Bodenrichtwert;
        e.DurchschnittlicherBodenrichtwert = dto.DurchschnittlicherBodenrichtwert;
        e.Wohnlage = dto.Wohnlage;
        e.IstDenkmalgeschuetzt = dto.IstDenkmalgeschuetzt;
        e.IstSozialerWohnungsbau = dto.IstSozialerWohnungsbau;

        e.Lage.Strasse = dto.Lage.Strasse;
        e.Lage.Hausnummer = dto.Lage.Hausnummer;
        e.Lage.HausnummerZusatz = dto.Lage.HausnummerZusatz;
        e.Lage.Postleitzahl = dto.Lage.Postleitzahl;
        e.Lage.Ort = dto.Lage.Ort;
        e.Lage.Ortsteil = dto.Lage.Ortsteil;
        e.Lage.Land = dto.Lage.Land;

        // Flurstücke: vollständig neu aufbauen (der Editor liefert immer den kompletten Stand).
        // Wichtig: Id explizit Guid.Empty setzen - die Entity hat einen Guid.NewGuid()-Default.
        // Nur mit leerer Id erkennt EF die neue Zeile als "Added" (INSERT); eine vorbelegte Id
        // führt zu einem UPDATE, das 0 Zeilen trifft und als Concurrency-Exception scheitert.
        e.Flurstuecke.Clear();
        var fIndex = 0;
        foreach (var f in dto.Flurstuecke)
        {
            e.Flurstuecke.Add(new EinheitFlurstueckEntity
            {
                Id = Guid.Empty,
                Reihenfolge = fIndex++,
                Gemarkung = f.Gemarkung,
                Gemarkungsnummer = f.Gemarkungsnummer,
                Flur = f.Flur,
                Zaehler = f.Zaehler,
                Nenner = f.Nenner,
                Flaeche = f.Flaeche,
                Anteil = f.Anteil
            });
        }

        // Eigentümer-Zuordnung: neu aufbauen. Die PersonId bleibt erhalten (kein erneutes Anlegen
        // der Person selbst - das passiert in der Personen-Maske). Id ebenfalls Guid.Empty (s.o.).
        e.Eigentuemer.Clear();
        var eIndex = 0;
        foreach (var x in dto.Eigentuemer)
        {
            e.Eigentuemer.Add(new EinheitEigentuemerEntity
            {
                Id = Guid.Empty,
                Reihenfolge = eIndex++,
                PersonId = x.PersonId,
                Anteil = x.Anteil
            });
        }
    }
}
