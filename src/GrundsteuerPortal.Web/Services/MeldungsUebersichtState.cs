using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;

namespace GrundsteuerPortal.Web.Services;

/// <summary>
/// Zustand der Dashboard-Übersicht (Laden, Filtern, Sortieren). Ebenfalls scoped, damit die
/// Filterauswahl beim Navigieren nicht verloren geht und nach einer Aktion gezielt neu geladen
/// werden kann, statt die ganze Seite neu aufzubauen.
/// </summary>
public sealed class MeldungsUebersichtState
{
    private readonly IGrundsteuerApiService _api;
    private readonly ILogger<MeldungsUebersichtState> _logger;

    public MeldungsUebersichtState(IGrundsteuerApiService api, ILogger<MeldungsUebersichtState> logger)
    {
        _api = api;
        _logger = logger;
    }

    public List<GrundsteuerUebersichtDto> Meldungen { get; private set; } = new();
    public bool Laedt { get; private set; }
    public string? Ladefehler { get; private set; }

    // Filterzustand
    public string Suche { get; set; } = string.Empty;
    public Bundesland? FilterBundesland { get; set; }
    public MeldungStatus? FilterStatus { get; set; }

    public event Action? Geaendert;

    public async Task LadenAsync(CancellationToken ct = default)
    {
        Laedt = true;
        Ladefehler = null;
        Geaendert?.Invoke();
        try
        {
            Meldungen = await _api.GetMeldungenAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Übersicht konnte nicht geladen werden");
            Ladefehler = "Die Meldungen konnten nicht geladen werden. Bitte erneut versuchen.";
        }
        finally
        {
            Laedt = false;
            Geaendert?.Invoke();
        }
    }

    public IReadOnlyList<GrundsteuerUebersichtDto> Gefiltert
    {
        get
        {
            IEnumerable<GrundsteuerUebersichtDto> ergebnis = Meldungen;

            if (!string.IsNullOrWhiteSpace(Suche))
            {
                var s = Suche.Trim();
                ergebnis = ergebnis.Where(m =>
                    Enthaelt(m.Aktenzeichen, s) || Enthaelt(m.Steuernummer, s)
                    || Enthaelt(m.Ort, s) || Enthaelt(m.Strasse, s)
                    || Enthaelt(m.Grundstuecksbezeichnung, s) || Enthaelt(m.HauptEigentuemer, s));
            }

            if (FilterBundesland is not null)
                ergebnis = ergebnis.Where(m => m.Bundesland == FilterBundesland);

            if (FilterStatus is not null)
                ergebnis = ergebnis.Where(m => m.Status == FilterStatus);

            return ergebnis.OrderByDescending(m => m.ZuletztGeaendertAm).ToList();
        }
    }

    private static bool Enthaelt(string? quelle, string suche) =>
        !string.IsNullOrWhiteSpace(quelle)
        && quelle.Contains(suche, StringComparison.OrdinalIgnoreCase);

    public int AnzahlFilterAktiv
    {
        get
        {
            var anzahl = 0;
            if (FilterBundesland is not null) anzahl++;
            if (FilterStatus is not null) anzahl++;
            if (!string.IsNullOrWhiteSpace(Suche)) anzahl++;
            return anzahl;
        }
    }

    /// <summary>Löscht die Fehlermeldung des letzten Ladeversuchs (Aufruf aus der Dashboard-Alert).</summary>
    public void LadefehlerLeeren() => Ladefehler = null;

    public void FilterZuruecksetzen()
    {
        Suche = string.Empty;
        FilterBundesland = null;
        FilterStatus = null;
        Geaendert?.Invoke();
    }

    public void GeaendertMelden() => Geaendert?.Invoke();

    /// <summary>Kennzahlen für die Kacheln über der Tabelle.</summary>
    public (int Gesamt, int Entwuerfe, int Offen, int Festgestellt) Kennzahlen => (
        Meldungen.Count,
        Meldungen.Count(m => m.Status is MeldungStatus.Entwurf or MeldungStatus.Validierungsfehler),
        Meldungen.Count(m => m.Status is MeldungStatus.Uebermittelt or MeldungStatus.InPruefung),
        Meldungen.Count(m => m.Status == MeldungStatus.Festgestellt));
}
