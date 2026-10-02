namespace GrundsteuerPortal.Persistence.Abstractions;

/// <summary>
/// Zugriff auf die lokale Datenhaltung (SQLite via EF Core).
///
/// Diese Schnittstelle ist absichtlich schmal und arbeitet mit Core-DTOs, nicht mit Entities:
/// die Web-Schicht darf die Persistenz nicht kennen. Damit bleibt es möglich, die Ablage später
/// gegen eine andere Datenbank oder einen echten Service zu tauschen, ohne die UI anzufassen.
/// </summary>
public interface IGrundsteuerRepository
{
    /// <summary>Übersichtsliste für das Dashboard, neueste Änderung zuerst.</summary>
    Task<List<Core.Api.GrundsteuerUebersichtDto>> GetUebersichtAsync(CancellationToken ct = default);

    /// <summary>Eine Meldung vollständig laden (mit allen Kinddaten).</summary>
    Task<Core.Api.GrundsteuerMeldungDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Entwurf anlegen oder aktualisieren. Liefert die Id und das neue RowVersion-Token.</summary>
    Task<Core.Api.ApiResponse> SpeichernAsync(Core.Api.GrundsteuerMeldungDto dto, CancellationToken ct = default);

    /// <summary>Meldung löschen. Nur im Status Entwurf/Validierungsfehler erlaubt.</summary>
    Task<bool> LoeschenAsync(Guid id, CancellationToken ct = default);

    /// <summary>Statuswechsel setzen und im Verlauf protokollieren.</summary>
    Task SetzeStatusAsync(Guid id, Core.Domain.MeldungStatus status, string ausloeser,
        string? bemerkung = null, CancellationToken ct = default);

    /// <summary>Übermittlungsversuch protokollieren (auch Fehlversuche). Liefert Anzahl Versuche.</summary>
    Task<int> ProtokolliereUebermittlungAsync(UebermittlungsProtokoll protokoll,
        CancellationToken ct = default);

    /// <summary>Statusverlauf einer Meldung, älteste zuerst.</summary>
    Task<List<VerlaufsEintrag>> GetVerlaufAsync(Guid id, CancellationToken ct = default);

    /// <summary>Finanzamt-Stammdaten aus dem Cache.</summary>
    Task<List<Core.Api.FinanzamtDto>> GetFinanzaemterAsync(Core.Domain.Bundesland land,
        CancellationToken ct = default);

    /// <summary>Finanzamt-Stammdaten aktualisieren (aus der WebAPI).</summary>
    Task AktualisiereFinanzaemterAsync(IEnumerable<Core.Api.FinanzamtDto> finanzaemter,
        CancellationToken ct = default);

    /// <summary>PLZ-Vorschlag aus dem Cache.</summary>
    Task<Core.Api.PlzZuordnungDto?> GetPlzAsync(string postleitzahl, CancellationToken ct = default);

    /// <summary>Datenbank anlegen und Grunddaten einspielen (idempotent).</summary>
    Task InitialisierenAsync(CancellationToken ct = default);

    // -----------------------------------------------------------------------------------------
    //  Wirtschaftseinheit-zentrisches Modell: Person (Master) + Wirtschaftseinheit (Bestand)
    // -----------------------------------------------------------------------------------------

    /// <summary>Übersicht aller Personen, aktive zuerst.</summary>
    Task<List<Core.Api.PersonUebersichtDto>> GetPersonenAsync(CancellationToken ct = default);

    /// <summary>Eine Person vollständig laden.</summary>
    Task<Core.Api.PersonDto?> GetPersonAsync(Guid id, CancellationToken ct = default);

    /// <summary>Person anlegen oder aktualisieren. Liefert die Id.</summary>
    Task<Core.Api.ApiResponse> SpeicherePersonAsync(Core.Api.PersonDto dto, CancellationToken ct = default);

    /// <summary>Übersicht aller Wirtschaftseinheiten, zuletzt geändert zuerst.</summary>
    Task<List<Core.Api.WirtschaftseinheitUebersichtDto>> GetWirtschaftseinheitenAsync(
        CancellationToken ct = default);

    /// <summary>Eine Wirtschaftseinheit vollständig laden (inkl. Eigentümer-Personen).</summary>
    Task<Core.Api.WirtschaftseinheitDto?> GetWirtschaftseinheitAsync(Guid id, CancellationToken ct = default);

    /// <summary>Wirtschaftseinheit anlegen oder aktualisieren. Liefert die Id.</summary>
    Task<Core.Api.ApiResponse> SpeichereWirtschaftseinheitAsync(Core.Api.WirtschaftseinheitDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Gibt es zu einer Einheit aktuell eine aktive (noch nicht abgeschlossene) Meldung?
    /// Aktiv = Entwurf, Validierungsfehler, Übermittelt, In Prüfung, Fehlgeschlagen.
    /// </summary>
    Task<bool> HatAktiveMeldungAsync(Guid wirtschaftseinheitId, CancellationToken ct = default);
}

/// <summary>Ein zu protokollierender ELSTER-Übermittlungsversuch.</summary>
public sealed record UebermittlungsProtokoll
{
    public required Guid MeldungId { get; init; }
    public bool Erfolgreich { get; init; }
    public string? Referenz { get; init; }
    public string? FehlerCode { get; init; }
    public string? Fehlertext { get; init; }
    public Core.Api.GrundsteuerBerechnungDto? Berechnung { get; init; }
    public int AnzahlFehler { get; init; }
    public int AnzahlWarnungen { get; init; }
}

/// <summary>Ein Eintrag des Statusverlaufs (Leseansicht).</summary>
public sealed record VerlaufsEintrag
{
    public required Core.Domain.MeldungStatus Status { get; init; }
    public required DateTime ZeitpunktUtc { get; init; }
    public required string Ausloeser { get; init; }
    public string? Bemerkung { get; init; }

    /// <summary>Ortszeit für die Anzeige - die DB hält UTC.</summary>
    public DateTime ZeitpunktLokal => ZeitpunktUtc.ToLocalTime();
}
