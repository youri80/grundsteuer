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

    protected override async Task OnInitializedAsync()
    {
        State.Geaendert += StateHatSichGeaendert;

        // Der Statusfilter kann per Deep-Link aus dem Drawer gesetzt werden: /?status=2
        var query = Navigation.ToAbsoluteUri(Navigation.Uri).Query;
        foreach (var paar in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var teile = paar.Split('=', 2);
            if (teile.Length == 2 && teile[0] == "status" && int.TryParse(teile[1], out var statusWert)
                && Enum.IsDefined(typeof(MeldungStatus), statusWert))
            {
                State.FilterStatus = (MeldungStatus)statusWert;
            }
        }

        await State.LadenAsync();
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
        State.FilterStatus = status;
        State.GeaendertMelden();
    }

    private void FilterZuruecksetzen() => State.FilterZuruecksetzen();

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
                : "Es werden acht vollständige Beispielmeldungen angelegt (alle Berechnungsmodelle, "
                  + "beide Ordnungskriterien). Die Nummern sind rechnerisch gültig, aber fiktiv – "
                  + "für die Übermittlung an ein echtes Finanzamt sind sie nicht bestimmt.",
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
