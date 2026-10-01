using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Web.Services;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Core.Validation;
using Microsoft.AspNetCore.Components;

namespace GrundsteuerPortal.Web.Components.Wizard;

/// <summary>
/// Schritt 4: Zusammenfassung, Prüfergebnis und Übermittlung.
/// </summary>
public partial class GrundsteuerWizardSchritt4 : ComponentBase, IDisposable
{
    /// <summary>Die eigentliche Übermittlung führt die Wizard-Seite aus (dort sitzt Dialog und Navigation).</summary>
    [Parameter] public EventCallback OnUebermitteln { get; set; }

    [Inject] private GrundsteuerFormularSitzung Sitzung { get; set; } = default!;

    private bool _bestaetigtErforderlich;

    protected override void OnInitialized() => Sitzung.Geaendert += NeuRendern;

    public void Dispose() => Sitzung.Geaendert -= NeuRendern;

    private void NeuRendern() => InvokeAsync(StateHasChanged);

    private void BestaetigungGeaendert(bool wert) => _bestaetigtErforderlich = wert;

    /// <summary>
    /// Freigabe der Übermittlung: dieselben Bedingungen, die auch das Absenden prüft -
    /// Fehlerfreiheit UND die Bestätigung des Nutzers. Ein Klick, der nur scheinbar möglich ist,
    /// führt sonst zu einem Fehlschlag direkt nach dem Absenden.
    /// </summary>
    private bool UebermittlungGesperrt()
    {
        if (Sitzung.Beschaeftigt) return true;

        // Ohne Bestätigung nicht senden; bereits übermittelt ebenfalls nicht.
        if (!_bestaetigtErforderlich) return true;
        if (Sitzung.Meldung.Status is MeldungStatus.Uebermittelt or MeldungStatus.InPruefung
            or MeldungStatus.Festgestellt) return true;

        return Sitzung.Fehler.Count > 0;
    }

    private async Task UebermittelnAsync()
    {
        if (UebermittlungGesperrt()) return;
        await OnUebermitteln.InvokeAsync();
    }

    /// <summary>Zeigt nur die letzten vier Stellen der IdNr - die vollständige Nummer muss nicht auf dem Bildschirm stehen.</summary>
    private static string MaskiereIdNr(string? idNr)
    {
        var ziffern = ElsterFormate.NurZiffern(idNr);
        return ziffern.Length <= 4 ? "····" : $"····{ziffern[^4..]}";
    }
}
