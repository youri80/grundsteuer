using System.Net;
using System.Net.Http.Json;
using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;

namespace GrundsteuerPortal.Web.Services;

/// <summary>
/// Typed HttpClient auf die vorhandene ELSTER-WebAPI. Diese Klasse enthält KEINE Fachlogik -
/// sie übersetzt nur HTTP in ApiResponse und wirft keine Exceptions in die Oberfläche hinein:
/// ein Netzwerkfehler wird zu einer ApiResponse mit lesbarer deutscher Meldung.
/// </summary>
public sealed class GrundsteuerApiService : IGrundsteuerApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<GrundsteuerApiService> _logger;

    public GrundsteuerApiService(HttpClient http, ILogger<GrundsteuerApiService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<GrundsteuerUebersichtDto>> GetMeldungenAsync(CancellationToken ct = default)
    {
        var antwort = await SendeAsync<List<GrundsteuerUebersichtDto>>(
            () => _http.GetAsync("api/grundsteuer/meldungen", ct), "Meldungen laden", ct);
        return antwort ?? new List<GrundsteuerUebersichtDto>();
    }

    public async Task<GrundsteuerMeldungDto?> GetMeldungByIdAsync(Guid id, CancellationToken ct = default)
    {
        var antwort = await _http.GetAsync($"api/grundsteuer/meldungen/{id}", ct);
        if (antwort.StatusCode == HttpStatusCode.NotFound) return null;
        if (!antwort.IsSuccessStatusCode)
        {
            _logger.LogWarning("Meldung {Id} konnte nicht geladen werden: {Status}", id, antwort.StatusCode);
            return null;
        }
        return await antwort.Content.ReadFromJsonAsync<GrundsteuerMeldungDto>(ct);
    }

    public Task<ApiResponse> SaveDraftAsync(GrundsteuerMeldungDto dto, CancellationToken ct = default) =>
        PosteAsync(dto.Id == Guid.Empty ? "api/grundsteuer/meldungen" : $"api/grundsteuer/meldungen/{dto.Id}", dto,
            "Entwurf speichern", ct);

    public Task<ApiResponse> SubmitToElsterAsync(Guid id, CancellationToken ct = default) =>
        PosteAsync($"api/grundsteuer/meldungen/{id}/submit", new { }, "An ELSTER übermitteln", ct);

    public async Task<StatusPruefungDto?> PruefeStatusAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var antwort = await _http.GetAsync($"api/grundsteuer/meldungen/{id}/status", ct);
            if (!antwort.IsSuccessStatusCode) return null;
            return await antwort.Content.ReadFromJsonAsync<StatusPruefungDto>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Statusprüfung für {Id} fehlgeschlagen", id);
            return null;
        }
    }

    public Task<ApiResponse> StorniereAsync(Guid id, CancellationToken ct = default) =>
        PosteAsync($"api/grundsteuer/meldungen/{id}/storno", new { }, "Meldung stornieren", ct);

    public async Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var antwort = await _http.DeleteAsync($"api/grundsteuer/meldungen/{id}", ct);
            if (antwort.IsSuccessStatusCode)
                return await antwort.Content.ReadFromJsonAsync<ApiResponse>(ct) ?? ApiResponse.Ok("Gelöscht.", id);

            return ApiResponse.Fehler(await FehlertextAsync(antwort),
                ((int)antwort.StatusCode).ToString());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Löschen von {Id} fehlgeschlagen", id);
            return ApiResponse.Fehler("Die API ist nicht erreichbar. Bitte später erneut versuchen.", "NETZWERK");
        }
    }

    public async Task<byte[]?> ExportPdfAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var antwort = await _http.GetAsync($"api/grundsteuer/meldungen/{id}/pdf", ct);
            return antwort.IsSuccessStatusCode ? await antwort.Content.ReadAsByteArrayAsync(ct) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "PDF-Export für {Id} fehlgeschlagen", id);
            return null;
        }
    }

    public async Task<List<FinanzamtDto>> GetFinanzaemterAsync(Bundesland land, CancellationToken ct = default)
    {
        var antwort = await SendeAsync<List<FinanzamtDto>>(
            () => _http.GetAsync($"api/grundsteuer/finanzamt?land={(int)land}", ct), "Finanzämter laden", ct);
        return antwort ?? new List<FinanzamtDto>();
    }

    public async Task<GrundsteuerBerechnungDto?> BerechneVorschauAsync(GrundsteuerMeldungDto dto,
        CancellationToken ct = default)
    {
        try
        {
            var antwort = await _http.PostAsJsonAsync("api/grundsteuer/vorschau", dto, ct);
            if (!antwort.IsSuccessStatusCode) return null;
            return await antwort.Content.ReadFromJsonAsync<GrundsteuerBerechnungDto>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Vorschauberechnung fehlgeschlagen");
            return null;
        }
    }

    // -----------------------------------------------------------------------------------------
    private async Task<ApiResponse> PosteAsync<T>(string pfad, T inhalt, string aktion, CancellationToken ct)
    {
        try
        {
            var antwort = await _http.PostAsJsonAsync(pfad, inhalt, ct);
            var ergebnis = await antwort.Content.ReadFromJsonAsync<ApiResponse>(ct);
            if (ergebnis is not null) return ergebnis;

            return antwort.IsSuccessStatusCode
                ? ApiResponse.Ok($"{aktion}: erfolgreich.")
                : ApiResponse.Fehler($"{aktion} fehlgeschlagen ({(int)antwort.StatusCode}).");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "{Aktion} fehlgeschlagen", aktion);
            return ApiResponse.Fehler(
                $"{aktion} fehlgeschlagen: Die ELSTER-WebAPI ist nicht erreichbar. "
                + "Bitte Verbindung prüfen oder den Mock-Betrieb aktivieren (Api:UseMock).",
                "NETZWERK");
        }
    }

    private async Task<T?> SendeAsync<T>(Func<Task<HttpResponseMessage>> aufruf, string aktion, CancellationToken ct)
    {
        try
        {
            var antwort = await aufruf();
            if (!antwort.IsSuccessStatusCode)
            {
                _logger.LogWarning("{Aktion}: {Status}", aktion, antwort.StatusCode);
                return default;
            }
            return await antwort.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "{Aktion} fehlgeschlagen", aktion);
            return default;
        }
    }

    private static async Task<string> FehlertextAsync(HttpResponseMessage antwort)
    {
        try
        {
            var inhalt = await antwort.Content.ReadFromJsonAsync<ApiResponse>();
            if (!string.IsNullOrWhiteSpace(inhalt?.Meldung)) return inhalt!.Meldung!;
        }
        catch
        {
            // Kein JSON-Body - dann die Statusmeldung verwenden.
        }

        return antwort.StatusCode switch
        {
            HttpStatusCode.NotFound => "Die Meldung wurde nicht gefunden.",
            HttpStatusCode.Conflict => "Die Meldung wurde zwischenzeitlich geändert. Bitte neu laden.",
            HttpStatusCode.Forbidden => "Für diese Aktion fehlt die Berechtigung.",
            _ => $"Die API antwortete mit Status {(int)antwort.StatusCode}."
        };
    }
}
