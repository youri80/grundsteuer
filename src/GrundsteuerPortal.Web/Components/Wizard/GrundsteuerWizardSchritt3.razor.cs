using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Web.Services;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Core.Validation;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GrundsteuerPortal.Web.Components.Wizard;

/// <summary>Schritt 3: Eigentümer und Miteigentümer mit Anteilen (dynamische Liste).</summary>
public partial class GrundsteuerWizardSchritt3 : ComponentBase, IDisposable
{
    [Inject] private GrundsteuerFormularSitzung Sitzung { get; set; } = default!;

    // Feldgenaue Fehlertexte je Eigentümer-Id - so markiert MudBlazor genau das betroffene Feld.
    private readonly Dictionary<Guid, bool> _idNrFehler = new();
    private readonly Dictionary<Guid, string?> _idNrFehlertext = new();
    private readonly Dictionary<Guid, bool> _plzFehler = new();
    private readonly Dictionary<Guid, string?> _plzFehlertext = new();

    protected override void OnInitialized() => Sitzung.Geaendert += NeuRendern;

    public void Dispose() => Sitzung.Geaendert -= NeuRendern;

    private void NeuRendern() => InvokeAsync(StateHasChanged);

    private static string Titel(EigentuemerDto e, int index)
    {
        var name = string.IsNullOrWhiteSpace(e.AnzeigeName) ? $"Eigentümer {index + 1} (noch ohne Name)" : e.AnzeigeName;
        return name;
    }

    /// <summary>Symbol je Rechtsform - als Methode, damit das Markup einzeilig bleibt.</summary>
    private static string ArtIcon(EigentuemerDto e) =>
        e.Art == EigentuemerArt.NatuerlichePerson
            ? Icons.Material.Outlined.Person
            : Icons.Material.Outlined.Business;

    private void Hinzufuegen()
    {
        // Neuer Eigentümer startet mit Anteil 0: der Nutzer muss den Anteil bewusst setzen,
        // statt dass zwei Eigentümer unbemerkt je 100 % tragen.
        Sitzung.Meldung.Eigentuemer.Add(new EigentuemerDto
        {
            Art = EigentuemerArt.NatuerlichePerson,
            Anteil = 0m
        });
        Sitzung.NeuValidieren();
    }

    private void Entfernen(EigentuemerDto eigentuemer)
    {
        if (Sitzung.Meldung.Eigentuemer.Count <= 1) return;
        Sitzung.Meldung.Eigentuemer.Remove(eigentuemer);
        _idNrFehler.Remove(eigentuemer.Id);
        _idNrFehlertext.Remove(eigentuemer.Id);
        _plzFehler.Remove(eigentuemer.Id);
        _plzFehlertext.Remove(eigentuemer.Id);
        Sitzung.NeuValidieren();
    }

    private void FeldGeaendert(EigentuemerDto eigentuemer, Action<EigentuemerDto> aenderung)
    {
        aenderung(eigentuemer);
        Sitzung.NeuValidieren();
    }

    private void ArtGeaendert(EigentuemerDto eigentuemer, EigentuemerArt art)
    {
        eigentuemer.Art = art;
        // Rechtsformwechsel: das jeweils nicht passende Identifikationsfeld leeren,
        // damit keine unzutreffende Nummer stehen bleibt.
        if (art == EigentuemerArt.NatuerlichePerson) eigentuemer.Steuernummer = null;
        else eigentuemer.IdNummer = null;
        Sitzung.NeuValidieren();
    }

    private void AnredeGeaendert(EigentuemerDto eigentuemer, Anrede anrede)
    {
        eigentuemer.Anrede = anrede;
        Sitzung.NeuValidieren();
    }

    private void IdNrGeaendert(EigentuemerDto eigentuemer, string? wert)
    {
        eigentuemer.IdNummer = wert;
        if (string.IsNullOrWhiteSpace(wert))
        {
            _idNrFehler[eigentuemer.Id] = false;
            _idNrFehlertext[eigentuemer.Id] = null;
        }
        else
        {
            var pruefung = ElsterFormate.PruefeIdNr(wert);
            _idNrFehler[eigentuemer.Id] = !pruefung.IstGueltig;
            _idNrFehlertext[eigentuemer.Id] = pruefung.Meldung;
        }
        Sitzung.NeuValidieren();
    }

    private void PlzGeaendert(EigentuemerDto eigentuemer, string? wert)
    {
        eigentuemer.Postleitzahl = wert ?? string.Empty;
        var pruefung = ElsterFormate.PruefePostleitzahl(wert);
        _plzFehler[eigentuemer.Id] = !string.IsNullOrWhiteSpace(wert) && !pruefung.IstGueltig;
        _plzFehlertext[eigentuemer.Id] = pruefung.Meldung;
        Sitzung.NeuValidieren();
    }

    private void AnteilGeaendert(EigentuemerDto eigentuemer, decimal wert)
    {
        eigentuemer.Anteil = wert;
        Sitzung.NeuValidieren();
    }

    private void GeburtsdatumGeaendert(EigentuemerDto eigentuemer, DateTime? wert)
    {
        eigentuemer.Geburtsdatum = wert;
        Sitzung.NeuValidieren();
    }

    private decimal AnteilsProzent() =>
        Math.Round(Sitzung.Meldung.Eigentuemer.Sum(e => e.Anteil) * 100m, 2);

    private Color AnteilsFarbe() => AnteilsProzent() switch
    {
        100m => Color.Success,
        > 100m => Color.Error,
        0m => Color.Error,
        _ => Color.Warning
    };

    private string AnteilsFarbeName() => AnteilsProzent() switch
    {
        100m => "success",
        > 100m => "error",
        0m => "error",
        _ => "warning"
    };

    /// <summary>Fehlerzustand je Eigentümer - als Methode, damit die Bindung im Markup einfach bleibt.</summary>
    private bool IdNrFehler(EigentuemerDto eigentuemer) =>
        _idNrFehler.TryGetValue(eigentuemer.Id, out var fehler) && fehler;

    private string? IdNrFehlertext(EigentuemerDto eigentuemer) =>
        _idNrFehlertext.GetValueOrDefault(eigentuemer.Id);

    private bool PlzFehler(EigentuemerDto eigentuemer) =>
        _plzFehler.TryGetValue(eigentuemer.Id, out var fehler) && fehler;

    private string? PlzFehlertext(EigentuemerDto eigentuemer) =>
        _plzFehlertext.GetValueOrDefault(eigentuemer.Id);

    private List<ValidierungsHinweisDto> HinweiseFuerSchritt() =>
        Sitzung.Meldung.Hinweise
            .Where(h => h.Schwere != HinweisSchwere.Hinweis
                        && (h.Feld == "Eigentuemer" || h.Feld.StartsWith("Eigentuemer[", StringComparison.Ordinal)))
            .ToList();

    private static MudBlazor.Severity Schwere(HinweisSchwere schwere) => schwere switch
    {
        HinweisSchwere.Fehler => MudBlazor.Severity.Error,
        HinweisSchwere.Warnung => MudBlazor.Severity.Warning,
        _ => MudBlazor.Severity.Info
    };
}
