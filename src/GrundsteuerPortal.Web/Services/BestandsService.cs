using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Persistence.Abstractions;

namespace GrundsteuerPortal.Web.Services;

/// <summary>
/// Fassade für die wirtschaftseinheit-zentrischen Bestandsdaten: Personen (Master) und
/// Wirtschaftseinheiten (Bestand) samt der Erstellung einer Meldung aus einer Einheit.
///
/// Bewusst getrennt vom <see cref="GrundsteuerService"/>: dieser hier arbeitet ausschließlich
/// lokal (keine ELSTER-WebAPI). Die Meldung selbst bleibt im <see cref="GrundsteuerService"/>.
/// </summary>
public sealed class BestandsService
{
    private readonly IGrundsteuerRepository _repo;
    private readonly ILogger<BestandsService> _logger;

    public BestandsService(IGrundsteuerRepository repo, ILogger<BestandsService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // -----------------------------------------------------------------------------------------
    //  Personen
    // -----------------------------------------------------------------------------------------
    public Task<List<PersonUebersichtDto>> GetPersonenAsync(CancellationToken ct = default) =>
        _repo.GetPersonenAsync(ct);

    public Task<PersonDto?> GetPersonAsync(Guid id, CancellationToken ct = default) =>
        _repo.GetPersonAsync(id, ct);

    public Task<ApiResponse> SpeicherePersonAsync(PersonDto dto, CancellationToken ct = default) =>
        _repo.SpeicherePersonAsync(dto, ct);

    // -----------------------------------------------------------------------------------------
    //  Wirtschaftseinheiten
    // -----------------------------------------------------------------------------------------
    public Task<List<WirtschaftseinheitUebersichtDto>> GetWirtschaftseinheitenAsync(
        CancellationToken ct = default) => _repo.GetWirtschaftseinheitenAsync(ct);

    public Task<WirtschaftseinheitDto?> GetWirtschaftseinheitAsync(Guid id,
        CancellationToken ct = default) => _repo.GetWirtschaftseinheitAsync(id, ct);

    public Task<ApiResponse> SpeichereWirtschaftseinheitAsync(WirtschaftseinheitDto dto,
        CancellationToken ct = default) => _repo.SpeichereWirtschaftseinheitAsync(dto, ct);

    // -----------------------------------------------------------------------------------------
    //  Meldung aus einer Wirtschaftseinheit erzeugen (Snapshot)
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Erzeugt eine neue Meldung als Snapshot aus einer Wirtschaftseinheit und speichert sie als
    /// Entwurf. Die meldende Stelle ist ein freier Personen-Verweis (auch Nicht-Eigentümer).
    /// </summary>
    /// <remarks>
    /// Regeln: eine archivierte Einheit darf nicht gemeldet werden; es darf nur eine aktive
    /// Meldung je Einheit geben. Die Meldung übernimmt alle fachlichen Felder als Kopie und ist
    /// danach eigenständig.
    /// </remarks>
    public async Task<ApiResponse> ErstelleMeldungAsync(Guid einheitId, Guid meldendePersonId,
        CancellationToken ct = default)
    {
        var einheit = await _repo.GetWirtschaftseinheitAsync(einheitId, ct);
        if (einheit is null)
            return ApiResponse.Fehler("Die Wirtschaftseinheit wurde nicht gefunden.", "NICHT_GEFUNDEN");

        if (einheit.Status == EinheitStatus.Archiviert)
            return ApiResponse.Fehler(
                "Eine archivierte Wirtschaftseinheit kann nicht gemeldet werden. "
                + "Bitte zuerst reaktivieren.", "ARCHIVIERT");

        if (await _repo.HatAktiveMeldungAsync(einheitId, ct))
            return ApiResponse.Fehler(
                "Zu dieser Wirtschaftseinheit existiert bereits eine aktive Meldung. "
                + "Erst nach deren Abschluss ist eine weitere Erklärung möglich.", "AKTIVE_MELDUNG");

        var meldendePerson = await _repo.GetPersonAsync(meldendePersonId, ct);
        if (meldendePerson is null)
            return ApiResponse.Fehler("Die meldende Stelle wurde nicht gefunden.", "NICHT_GEFUNDEN");

        // Snapshot: Einheit-Felder + Flurstücke übernehmen.
        var meldung = MeldungAusEinheit.Erzeuge(einheit, meldendePerson);

        // Eigentümer aus den zugeordneten Personen übernehmen (Kopie; IdNr als Platzhalter,
        // weil die Person nur als Hash vorliegt).
        foreach (var zuordnung in einheit.Eigentuemer)
        {
            var person = await _repo.GetPersonAsync(zuordnung.PersonId, ct);
            if (person is null) continue;

            meldung.Eigentuemer.Add(new EigentuemerDto
            {
                Art = person.Art,
                Anrede = person.Anrede,
                Name = person.Name,
                Vorname = person.Vorname,
                IdNummer = person.IdNummer,
                Steuernummer = person.Steuernummer,
                Strasse = person.Strasse,
                Hausnummer = person.Hausnummer,
                Postleitzahl = person.Postleitzahl,
                Ort = person.Ort,
                Land = person.Land,
                Geburtsdatum = person.Geburtsdatum,
                Anteil = zuordnung.Anteil
            });
        }

        if (meldung.Eigentuemer.Count == 0)
            return ApiResponse.Fehler(
                "Die Wirtschaftseinheit hat keinen Eigentümer. Bitte zuerst einen Eigentümer zuordnen.",
                "KEINE_EIGENTUEMER");

        var antwort = await _repo.SpeichernAsync(meldung, ct);
        if (antwort.Erfolg)
        {
            _logger.LogInformation("Meldung {Id} aus Wirtschaftseinheit {Einheit} erzeugt.",
                antwort.Id, einheitId);
        }

        return antwort;
    }
}
