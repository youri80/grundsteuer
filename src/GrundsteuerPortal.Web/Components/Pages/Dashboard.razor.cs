using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Web.Services;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Core.Validation;
using GrundsteuerPortal.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace GrundsteuerPortal.Web.Components.Pages;

/// <summary>
/// Code-Behind des Dashboards. Enthält keine Darstellung und keine Fachlogik -
/// nur Laden, Filtern und die Aktionen (Status prüfen, PDF, Löschen, Stornieren, Kopieren).
/// </summary>
public partial class Dashboard : ComponentBase, IDisposable
{
    [Inject] private MeldungsUebersichtState State { get; set; } = default!;
    [Inject] private IGrundsteuerApiService Api { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private ILogger<Dashboard> _logger { get; set; } = default!;

    /// <summary>
    /// Der Statusfilter aus der Adresse (<c>/?status=4</c>). Die Navigationsleiste verlinkt die
    /// Status direkt; damit ist die Adresse die eine Quelle für diesen Filter.
    /// </summary>
    /// <remarks>
    /// Der Wert muss als Parameter gelesen werden, nicht einmalig aus <c>Navigation.Uri</c>:
    /// ein Klick im Navigationsbereich ist eine Navigation auf dieselbe Route. Die Komponente
    /// bleibt dabei bestehen, <c>OnInitializedAsync</c> läuft kein zweites Mal — ein dort
    /// gelesener Wert würde nur beim ersten Aufruf greifen und der Filter bliebe wirkungslos.
    /// Als Parameter gebunden wird die Komponente bei jeder Adressänderung neu versorgt.
    /// </remarks>
    [Parameter, SupplyParameterFromQuery(Name = "status")]
    public int? StatusAusAdresse { get; set; }

    protected override async Task OnInitializedAsync()
    {
        State.Geaendert += StateHatSichGeaendert;
        await State.LadenAsync();
    }

    protected override void OnParametersSet()
    {
        // Der Adresswert wird bei jedem Rendern abgeglichen, nicht nur beim ersten. Dadurch wirken
        // auch die Statuslinks im Navigationsbereich, die Zurück-Schaltfläche und ein Lesezeichen.
        var gewuenscht = StatusAusAdresse is int wert && Enum.IsDefined(typeof(MeldungStatus), wert)
            ? (MeldungStatus)wert
            : (MeldungStatus?)null;

        if (gewuenscht != State.FilterStatus)
        {
            State.FilterStatus = gewuenscht;
            State.GeaendertMelden();
        }
    }

    public void Dispose() => State.Geaendert -= StateHatSichGeaendert;

    private void StateHatSichGeaendert() => InvokeAsync(StateHasChanged);

    private async Task LadenAsync() => await State.LadenAsync();

    private void SucheGeaendert(string? wert)
    {
        State.Suche = wert ?? string.Empty;
        State.GeaendertMelden();
    }

    private void BundeslandGeaendert(Bundesland? land)
    {
        State.FilterBundesland = land;
        State.GeaendertMelden();
    }

    private void StatusGeaendert(MeldungStatus? status)
    {
        // Die Adresse ist die eine Quelle für diesen Filter - also wird sie hier mitgeführt.
        // Ohne das würde OnParametersSet die Auswahl beim nächsten Rendern auf den alten
        // Adresswert zurückstellen.
        Navigation.NavigateTo(status is null ? "/" : $"/?status={(int)status.Value}");
    }

    private void FilterZuruecksetzen()
    {
        State.FilterZuruecksetzen();

        // Steht der Status in der Adresse, muss sie mitgeführt werden - sonst setzt
        // OnParametersSet ihn beim nächsten Rendern wieder.
        if (StatusAusAdresse is not null) Navigation.NavigateTo("/");
    }

    /// <summary>
    /// Legt die Beispielmeldungen an, damit der Meldungsprozess in der Oberfläche vollständig
    /// durchgespielt werden kann. Bewusst mit Rückfrage: der Vorgang schreibt in die Datenbank.
    ///
    /// Ein bereits eingespielter Datensatz wird nicht erneut angelegt - erkennbar am Aktenzeichen
    /// bzw. an der Steuernummer. Sonst entstünden bei jedem Klick Dubletten.
    /// </summary>
    private async Task TestdatenEinspielenAsync()
    {
        var vorhandene = await Api.GetMeldungenAsync();
        var bereitsVorhanden = TestdatensatzDienst.IstBereitsEingespielt(vorhandene);

        var bestaetigt = await DialogService.ShowMessageBoxAsync(
            "Testdaten einspielen",
            bereitsVorhanden
                ? "Es sind bereits Testdaten vorhanden. Fehlende Datensätze werden ergänzt, "
                  + "bestehende bleiben unverändert."
                : "Es werden zwölf vollständige Beispielmeldungen angelegt: acht Entwürfe zum "
                  + "Durchspielen des Assistenten und vier mit Endzustand (übermittelt, "
                  + "festgestellt, in Prüfung, Validierungsfehler), damit auch die Statusfilter "
                  + "etwas anzeigen. Die Nummern sind rechnerisch gültig, aber fiktiv – für die "
                  + "Übermittlung an ein echtes Finanzamt sind sie nicht bestimmt.",
            yesText: "Einspielen", cancelText: "Abbrechen");

        if (bestaetigt != true) return;

        var ergebnis = await TestdatensatzDienst.EinspielenAsync(Api, _logger);
        Snackbar.Add(ergebnis, Severity.Success);
        await State.LadenAsync();
    }

    /// <summary>Räumt die Ladefehlermeldung weg, ohne die Seite neu aufzubauen.</summary>
    private void LadefehlerSchliessen()
    {
        State.LadefehlerLeeren();
        State.GeaendertMelden();
    }
    /// <summary>Hervorhebung abgeschlossener Vorgänge, ohne die Tabelle zu überladen.</summary>
    /// <remarks>MudDataGrid erwartet eine <c>Func&lt;T, int, string&gt;</c> — der Zeilenindex ist ungenutzt.</remarks>
    private static string ZeilenKlasse(GrundsteuerUebersichtDto zeile, int zeilenIndex) => zeile.Status switch
    {
        MeldungStatus.Validierungsfehler => "zeile-aufmerksamkeit",
        MeldungStatus.Festgestellt => "zeile-erledigt",
        _ => string.Empty
    };

    private static bool IstGesperrt(GrundsteuerUebersichtDto zeile) =>
        zeile.Status is MeldungStatus.InPruefung or MeldungStatus.Festgestellt;

    private static string BearbeitenText(GrundsteuerUebersichtDto zeile) => IstGesperrt(zeile)
        ? "Beim Finanzamt in Bearbeitung – nur ansehen (berichtigte Erklärung nötig)"
        : "Meldung bearbeiten";

    private static bool KannStorniertWerden(GrundsteuerUebersichtDto zeile) =>
        zeile.Status is MeldungStatus.Entwurf or MeldungStatus.Validierungsfehler
            or MeldungStatus.Uebermittelt or MeldungStatus.Fehlgeschlagen;

    private async Task StatusPruefenAsync(GrundsteuerUebersichtDto zeile)
    {
        var status = await Api.PruefeStatusAsync(zeile.Id);
        if (status is null)
        {
            Snackbar.Add("Der Status konnte nicht abgerufen werden – die API ist nicht erreichbar.",
                Severity.Warning);
            return;
        }

        Snackbar.Add($"{status.Status.AnzeigeName()}: {status.Nachricht}", Severity.Info);
        await State.LadenAsync();
    }

    private async Task PdfExportierenAsync(GrundsteuerUebersichtDto zeile)
    {
        var pdf = await Api.ExportPdfAsync(zeile.Id);
        if (pdf is null || pdf.Length == 0)
        {
            Snackbar.Add("Der PDF-Export ist derzeit nicht möglich.", Severity.Warning);
            return;
        }

        // Download über den Browser auslösen: Blazor Server kann keine Datei direkt "zurückschicken".
        var kennung = string.IsNullOrWhiteSpace(zeile.Aktenzeichen)
            ? zeile.Id.ToString()[..8]
            : zeile.Aktenzeichen!;
        var dateiname = $"Grundsteuer_{kennung}_{DateTime.Now:yyyyMMdd}.pdf";
        await JsRuntime.InvokeVoidAsync("portalDownload.pdf", dateiname, Convert.ToBase64String(pdf));
        Snackbar.Add($"PDF erzeugt: {dateiname}", Severity.Success);
    }

    private async Task LoeschenAsync(GrundsteuerUebersichtDto zeile)
    {
        var bestaetigt = await DialogService.ShowMessageBoxAsync(
            "Entwurf löschen",
            $"Soll der Entwurf für '{zeile.Grundstuecksbezeichnung}' ({zeile.Ort}) wirklich gelöscht werden? "
            + "Das kann nicht rückgängig gemacht werden.",
            yesText: "Löschen", cancelText: "Abbrechen");

        if (bestaetigt != true) return;

        var antwort = await Api.DeleteAsync(zeile.Id);
        Snackbar.Add(antwort.Meldung ?? (antwort.Erfolg ? "Gelöscht." : "Löschen fehlgeschlagen."),
            antwort.Erfolg ? Severity.Success : Severity.Error);
        if (antwort.Erfolg) await State.LadenAsync();
    }

    private async Task StornierenAsync(GrundsteuerUebersichtDto zeile)
    {
        var bestaetigt = await DialogService.ShowMessageBoxAsync(
            "Meldung stornieren",
            "Die Meldung wird zurückgezogen. Eine bereits vom Finanzamt bearbeitete Erklärung "
            + "kann nicht storniert werden – dafür ist eine berichtigte Erklärung nötig.",
            yesText: "Stornieren", cancelText: "Abbrechen");

        if (bestaetigt != true) return;

        var antwort = await Api.StorniereAsync(zeile.Id);
        Snackbar.Add(antwort.Meldung ?? "Storniert.", antwort.Erfolg ? Severity.Success : Severity.Error);
        if (antwort.Erfolg) await State.LadenAsync();
    }

    private async Task KopierenAsync(GrundsteuerUebersichtDto zeile)
    {
        var original = await Api.GetMeldungByIdAsync(zeile.Id);
        if (original is null)
        {
            Snackbar.Add("Die Meldung konnte nicht geladen werden.", Severity.Error);
            return;
        }

        // Kopie bewusst OHNE Aktenzeichen/Steuernummer und ohne Übermittlungsdaten:
        // eine Kopie ist ein neuer Vorgang, kein Duplikat derselben Erklärung.
        original.Id = Guid.Empty;
        original.Aktenzeichen = null;
        original.Steuernummer = null;
        original.AktenzeichenElster = null;
        original.UebermittlungsReferenz = null;
        original.UebermitteltAm = null;
        original.ErstelltAm = null;
        original.RowVersion = null;
        original.Status = MeldungStatus.Entwurf;

        var antwort = await Api.SaveDraftAsync(original);
        if (antwort.Erfolg && antwort.Id.HasValue)
        {
            Snackbar.Add("Kopie als neuer Entwurf angelegt.", Severity.Success);
            await State.LadenAsync();
            Navigation.NavigateTo($"/grundsteuer/{antwort.Id.Value}");
        }
        else
        {
            Snackbar.Add(antwort.Meldung ?? "Die Kopie konnte nicht angelegt werden.", Severity.Error);
        }
    }

    private async Task DetailsAnzeigenAsync(GrundsteuerUebersichtDto zeile)
    {
        var meldung = await Api.GetMeldungByIdAsync(zeile.Id);
        if (meldung is null)
        {
            Snackbar.Add("Die Meldung konnte nicht geladen werden.", Severity.Error);
            return;
        }

        var parameter = new DialogParameters<MeldungDetailsDialog>
        {
            { x => x.Meldung, meldung }
        };
        var dialog = await DialogService.ShowAsync<MeldungDetailsDialog>("Meldungsdetails", parameter);
        await dialog.Result;
    }
}
