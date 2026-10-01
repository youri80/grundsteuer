using GrundsteuerPortal.Core.Domain;

namespace GrundsteuerPortal.Core.Api;

/// <summary>
/// Der einzige Zugang des Frontends zur ELSTER-WebAPI. Die UI kennt nur dieses Interface -
/// Mock- und HTTP-Implementierung sind austauschbar (Konfiguration: "Api:UseMock").
/// </summary>
public interface IGrundsteuerApiService
{
    /// <summary>Alle Meldungen des angemeldeten Nutzers für die Dashboard-Übersicht.</summary>
    Task<List<GrundsteuerUebersichtDto>> GetMeldungenAsync(CancellationToken ct = default);

    /// <summary>Eine Meldung vollständig laden (Wizard-Bearbeitung).</summary>
    Task<GrundsteuerMeldungDto?> GetMeldungByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Entwurf anlegen oder aktualisieren. Liefert die vergebene Id zurück.</summary>
    Task<ApiResponse> SaveDraftAsync(GrundsteuerMeldungDto dto, CancellationToken ct = default);

    /// <summary>Meldung endgültig an ELSTER übermitteln (unwiderruflich - Bestätigung in der UI).</summary>
    Task<ApiResponse> SubmitToElsterAsync(Guid id, CancellationToken ct = default);

    /// <summary>Status beim Finanzamt prüfen (Messbescheid / Eingangsbestätigung).</summary>
    Task<StatusPruefungDto?> PruefeStatusAsync(Guid id, CancellationToken ct = default);

    /// <summary>Meldung stornieren/zurückziehen, solange sie noch nicht verarbeitet ist.</summary>
    Task<ApiResponse> StorniereAsync(Guid id, CancellationToken ct = default);

    /// <summary>Meldung löschen (nur Entwürfe).</summary>
    Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>PDF-Export des ausgefüllten Formulars (Vorlage GW-1) für die eigenen Unterlagen.</summary>
    Task<byte[]?> ExportPdfAsync(Guid id, CancellationToken ct = default);

    /// <summary>Zulässige Finanzämter des Bundeslandes für die Auswahlliste in Schritt 1.</summary>
    Task<List<FinanzamtDto>> GetFinanzaemterAsync(Bundesland land, CancellationToken ct = default);

    /// <summary>Vorschau des Steuermessbetrags vor dem Absenden (nur Bundesländer mit Flächenmodell).</summary>
    Task<GrundsteuerBerechnungDto?> BerechneVorschauAsync(GrundsteuerMeldungDto dto, CancellationToken ct = default);
}
