using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Web.Services;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Core.Validation;
using GrundsteuerPortal.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GrundsteuerPortal.Web.Components.Wizard;

/// <summary>Schritt 1: Allgemeine Angaben. Code-Behind getrennt vom Markup.</summary>
public partial class GrundsteuerWizardSchritt1 : ComponentBase, IDisposable
{
    [Inject] private GrundsteuerFormularSitzung Sitzung { get; set; } = default!;
    [Inject] private IGrundsteuerApiService Api { get; set; } = default!;

    private FinanzamtDto? _gewaehltesFinanzamt;
    private bool _aktenzeichenFehler;
    private string? _aktenzeichenFehlertext;
    private bool _steuernummerFehler;
    private string? _steuernummerFehlertext;
    private List<FinanzamtDto> _finanzaemter = new();

    protected override async Task OnInitializedAsync()
    {
        Sitzung.Geaendert += NeuRendern;
        await FinanzaemterLadenAsync();
        FinanzamtAusVorbelegungSetzen();
    }

    public void Dispose() => Sitzung.Geaendert -= NeuRendern;

    private void NeuRendern() => InvokeAsync(StateHasChanged);

    private async Task FinanzaemterLadenAsync() =>
        _finanzaemter = await Api.GetFinanzaemterAsync(Sitzung.Meldung.Bundesland);

    private void FinanzamtAusVorbelegungSetzen()
    {
        if (string.IsNullOrWhiteSpace(Sitzung.Meldung.Bundesfinanzamtsnummer)) return;
        _gewaehltesFinanzamt = _finanzaemter.FirstOrDefault(f =>
                                    f.Bundesfinanzamtsnummer == Sitzung.Meldung.Bundesfinanzamtsnummer)
                                ?? new FinanzamtDto
                                {
                                    Bundesfinanzamtsnummer = Sitzung.Meldung.Bundesfinanzamtsnummer,
                                    Name = Sitzung.Meldung.FinanzamtName ?? "bestehende Auswahl",
                                    Bundesland = Sitzung.Meldung.Bundesland
                                };
    }

    private async Task BundeslandGeaendert(Bundesland land)
    {
        Sitzung.BundeslandGeaendert(land);
        _gewaehltesFinanzamt = null;
        Sitzung.Meldung.Bundesfinanzamtsnummer = null;
        Sitzung.Meldung.FinanzamtName = null;
        _aktenzeichenFehler = false;
        _steuernummerFehler = false;
        await FinanzaemterLadenAsync();
    }

    private void ErklaerungsartGeaendert(Erklaerungsart art)
    {
        Sitzung.Meldung.Erklaerungsart = art;
        Sitzung.NeuValidieren();
    }

    private void HauptfeststellungGeaendert(int? jahr)
    {
        Sitzung.Meldung.Hauptfeststellungszeitpunkt = jahr;
        Sitzung.NeuValidieren();
    }

    private void FinanzamtGeaendert(FinanzamtDto? finanzamt)
    {
        _gewaehltesFinanzamt = finanzamt;
        Sitzung.Meldung.Bundesfinanzamtsnummer = finanzamt?.Bundesfinanzamtsnummer;
        Sitzung.Meldung.FinanzamtName = finanzamt?.Name;
        Sitzung.NeuValidieren();
    }

    /// <summary>Suche im Nummernkreis des Landes; die echte Liste kommt von der API.</summary>
    private async Task<IEnumerable<FinanzamtDto>> FinanzamtSuche(string? suche, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(suche)) return _finanzaemter.Take(20);

        await Task.CompletedTask;
        return _finanzaemter
            .Where(f => f.Anzeige.Contains(suche, StringComparison.OrdinalIgnoreCase))
            .Take(30);
    }

    private void AktenzeichenGeaendert(string? wert)
    {
        Sitzung.Meldung.Aktenzeichen = wert;

        if (string.IsNullOrWhiteSpace(wert))
        {
            _aktenzeichenFehler = false;
            _aktenzeichenFehlertext = null;
        }
        else
        {
            var pruefung = ElsterFormate.PruefeAktenzeichen(wert, Sitzung.Meldung.Bundesland);
            _aktenzeichenFehler = !pruefung.IstGueltig;
            _aktenzeichenFehlertext = pruefung.IstGueltig
                ? null
                : $"{pruefung.Meldung} {(pruefung.Vorschlag ?? string.Empty)}".Trim();
        }

        Sitzung.NeuValidieren();
    }

    private void SteuernummerGeaendert(string? wert)
    {
        Sitzung.Meldung.Steuernummer = wert;

        if (string.IsNullOrWhiteSpace(wert))
        {
            _steuernummerFehler = false;
            _steuernummerFehlertext = null;
        }
        else
        {
            var pruefung = ElsterFormate.PruefeSteuernummerElster(wert, Sitzung.Meldung.Bundesland);
            _steuernummerFehler = !pruefung.IstGueltig;
            _steuernummerFehlertext = pruefung.IstGueltig
                ? null
                : $"{pruefung.Meldung} {(pruefung.Vorschlag ?? string.Empty)}".Trim();
        }

        Sitzung.NeuValidieren();
    }

    private string _aktenzeichenFormatiert =>
        ElsterFormate.FormatiereAktenzeichen(Sitzung.Meldung.Aktenzeichen, Sitzung.Meldung.Bundesland);

    private string AktenzeichenHilfe() => Sitzung.Meldung.BundeslandInfo.AktenzeichenFormat switch
    {
        AktenzeichenFormat.BayernVerbund => "17 Stellen, z. B. 198/690/4000/000/001/2.",
        AktenzeichenFormat.Hessen => "16 Stellen, z. B. 60 001 0001 001 005 1.",
        AktenzeichenFormat.BadenWuerttemberg => "16 Stellen, z. B. 31/005/0069/016/001/7.",
        AktenzeichenFormat.Niedersachsen => "16 Stellen, z. B. 79/680/0060/001/000/9.",
        AktenzeichenFormat.NordrheinWestfalen => "13 Stellen, z. B. 600/035-3-01285.1.",
        _ => "Aktenzeichen wie auf dem Bescheid."
    };

    private string BufaHinweis()
    {
        var info = BundeslandKatalog.Fuer(Sitzung.Meldung.Bundesland);
        return info.FinanzamtNummern;
    }

    private List<ValidierungsHinweisDto> HinweiseFuerSchritt() =>
        Sitzung.Meldung.Hinweise.Where(h => h.Schwere != HinweisSchwere.Hinweis
            && SchrittFelder.Contains(h.Feld)).ToList();

    private static readonly HashSet<string> SchrittFelder = new(StringComparer.Ordinal)
    {
        "Bundesfinanzamtsnummer", "Aktenzeichen", "Steuernummer", "Hauptfeststellungszeitpunkt"
    };

    private static Severity Schwere(HinweisSchwere schwere) => schwere switch
    {
        HinweisSchwere.Fehler => Severity.Error,
        HinweisSchwere.Warnung => Severity.Warning,
        _ => Severity.Info
    };
}
