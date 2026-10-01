using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Web.Services;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Core.Validation;
using GrundsteuerPortal.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GrundsteuerPortal.Web.Components.Pages;

/// <summary>
/// Code-Behind des Wizards. Die Komponente selbst enthält nur Markup; der Ablauf
/// (Laden, Schrittwechsel, Speichern, Übermitteln) liegt hier und delegiert die Fachlogik an Core.
/// </summary>
public partial class GrundsteuerWizard : ComponentBase, IDisposable
{
    [Parameter] public Guid? Id { get; set; }

    [Inject] private GrundsteuerFormularSitzung Sitzung { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private MudStepper? _stepper;
    private string? _ladefehler;
    private bool _istNeu = true;
    private List<BreadcrumbItem> _brotkrumen = new();

    protected override async Task OnParametersSetAsync()
    {
        _istNeu = Id is null;

        // Beim Wechsel der Route (neu <-> bestehende Id) muss die Sitzung neu aufgebaut werden,
        // sonst zeigt der Wizard Daten des vorigen Vorgangs.
        if (Id is null)
        {
            if (Sitzung.Meldung.Id != Guid.Empty) Sitzung.Zuruecksetzen();
        }
        else if (Sitzung.Meldung.Id != Id.Value)
        {
            var geladen = await Sitzung.LadenAsync(Id.Value);
            if (!geladen)
                _ladefehler = "Die Meldung wurde nicht gefunden oder konnte nicht geladen werden.";
        }

        BrotkrumenAufbauen();

        if (_stepper is not null && Id is not null)
        {
            // Beim Bearbeiten sofort beim Schritt mit Fehlern einsteigen - spart Klickarbeit.
            var fehler = Sitzung.FehlerJeSchritt;
            if (fehler.Count > 0 && fehler.Keys.Min() is var erster)
                Sitzung.AktiverSchritt = (int)erster;
        }
    }

    protected override void OnInitialized() => Sitzung.Geaendert += SitzungHatSichGeaendert;

    public void Dispose() => Sitzung.Geaendert -= SitzungHatSichGeaendert;

    private void SitzungHatSichGeaendert() => InvokeAsync(StateHasChanged);

    private void BrotkrumenAufbauen() => _brotkrumen = new List<BreadcrumbItem>
    {
        new("Übersicht", "/"),
        new(_istNeu ? "Neue Meldung" : "Meldung bearbeiten", null, _istNeu)
    };

    /// <summary>Fehlerindikator am MudStep - aus derselben Quelle wie die Validierung.</summary>
    private bool HatFehler(WizardSchritt schritt) =>
        Sitzung.FehlerJeSchritt.TryGetValue(schritt, out var anzahl) && anzahl > 0;

    private string FehlerSchritteText() => string.Join(", ",
        Sitzung.FehlerJeSchritt.Keys.OrderBy(k => k).Select(k => k switch
        {
            WizardSchritt.AllgemeineAngaben => "Allgemeine Angaben",
            WizardSchritt.Grundstuecksdaten => "Grundstücksdaten",
            WizardSchritt.Eigentuemer => "Eigentümer",
            _ => "Zusammenfassung"
        }));

    /// <summary>
    /// Kontrollierte Navigation: der Stepper darf nur vorwärts, wenn der aktuelle Schritt fehlerfrei ist.
    /// Die Prüfung läuft über Core (WizardSchritt), nicht über MudForm.IsValid - IsValid wäre erst nach
    /// einer Validierung gesetzt und würde den Nutzer stumm blockieren.
    /// </summary>
    private async Task VorschauInteraktionAsync(StepperInteractionEventArgs args)
    {
        if (args.Action == StepAction.Complete)
        {
            var schritt = (WizardSchritt)Math.Clamp(args.StepIndex, 0, 3);
            Sitzung.NeuValidieren();

            if (!MeldungsValidator.IstFertig(Sitzung.Meldung, schritt))
            {
                args.Cancel = true;
                var fehler = MeldungsValidator.PruefeSchritt(Sitzung.Meldung, schritt)
                    .Where(h => h.Schwere == HinweisSchwere.Fehler).ToList();

                Snackbar.Add(
                    $"{fehler.Count} Angabe(n) in diesem Schritt sind noch nicht vollständig: "
                    + string.Join(" · ", fehler.Take(2).Select(f => f.Meldung)),
                    Severity.Error);
            }
            else
            {
                // Zwischenstand automatisch sichern, damit nichts verloren geht.
                await Sitzung.SpeichernAsync();
            }
        }
        else if (args.Action == StepAction.Activate)
        {
            // Sprung vorwärts per Klick auf eine Schrittüberschrift nur, wenn alle Vorschritte fertig sind.
            var ziel = (WizardSchritt)Math.Clamp(args.StepIndex, 0, 3);
            Sitzung.NeuValidieren();

            for (var s = 0; s < (int)ziel; s++)
            {
                if (!MeldungsValidator.IstFertig(Sitzung.Meldung, (WizardSchritt)s))
                {
                    args.Cancel = true;
                    Snackbar.Add("Bitte zuerst die vorherigen Schritte vollständig ausfüllen.", Severity.Warning);
                    return;
                }
            }
        }

        await Task.CompletedTask;
    }

    private async Task WeiterAsync()
    {
        if (_stepper is null) return;
        await _stepper.NextStepAsync();
    }

    private async Task ZurueckAsync()
    {
        if (_stepper is null) return;
        await _stepper.PreviousStepAsync();
    }

    private void ZumAbschluss() => Sitzung.AktiverSchritt = 3;

    private async Task EntwurfSpeichernAsync()
    {
        var antwort = await Sitzung.SpeichernAsync();
        if (antwort.Erfolg)
        {
            Snackbar.Add("Entwurf gespeichert.", Severity.Success);

            // Nach dem ersten Speichern hat die Meldung eine Id: Route nachziehen, damit
            // ein Neuladen im Browser die Meldung wiederfindet.
            if (_istNeu && Sitzung.Meldung.Id != Guid.Empty)
                Navigation.NavigateTo($"/grundsteuer/{Sitzung.Meldung.Id}", replace: true);
        }
        else
        {
            Snackbar.Add(antwort.Meldung ?? "Der Entwurf konnte nicht gespeichert werden.", Severity.Error);
        }
    }

    private async Task UebermittelnAsync()
    {
        var bestaetigt = await DialogService.ShowMessageBoxAsync(
            "Erklärung verbindlich übermitteln",
            "Die Erklärung wird damit verbindlich an die Finanzverwaltung übermittelt. "
            + "Nach der Übermittlung ist keine inhaltliche Änderung mehr möglich – "
            + "Änderungen erfordern eine berichtigte Erklärung. Fortfahren?",
            yesText: "Verbindlich übermitteln", cancelText: "Abbrechen");

        if (bestaetigt != true) return;

        var antwort = await Sitzung.UebermittelnAsync();
        if (antwort.Erfolg)
        {
            Snackbar.Add(antwort.Meldung ?? "Die Erklärung wurde übermittelt.", Severity.Success);
            Navigation.NavigateTo("/");
        }
        else
        {
            Snackbar.Add(antwort.Meldung ?? "Die Übermittlung ist fehlgeschlagen.", Severity.Error);
        }
    }

    private async Task AbbrechenAsync()
    {
        if (Sitzung.IstNeuanlage && !string.IsNullOrWhiteSpace(Sitzung.Meldung.Gemarkung))
        {
            var verwerfen = await DialogService.ShowMessageBoxAsync(
                "Erfassung schließen",
                "Nicht gespeicherte Eingaben gehen verloren. Entwurf vorher speichern?",
                yesText: "Speichern und schließen", noText: "Ohne Speichern schließen", cancelText: "Weiter erfassen");

            if (verwerfen is null) return;
            if (verwerfen == true)
            {
                var antwort = await Sitzung.SpeichernAsync();
                if (!antwort.Erfolg)
                {
                    Snackbar.Add("Der Entwurf konnte nicht gespeichert werden.", Severity.Error);
                    return;
                }
            }
        }

        Navigation.NavigateTo("/");
    }
}
