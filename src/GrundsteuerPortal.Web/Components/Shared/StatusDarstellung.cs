using GrundsteuerPortal.Core.Domain;
using MudBlazor;

namespace GrundsteuerPortal.Web.Components.Shared;

/// <summary>
/// Darstellungsregeln für den Meldungsstatus an EINER Stelle. Tabelle, Detailseite und
/// Zusammenfassung greifen darauf zu - sonst laufen Farbe, Symbol und Text auseinander.
/// </summary>
public static class StatusDarstellung
{
    public static string Text(MeldungStatus status) => status.AnzeigeName();

    public static Color Farbe(MeldungStatus status) => status switch
    {
        MeldungStatus.Entwurf => Color.Default,
        MeldungStatus.Validierungsfehler => Color.Warning,
        MeldungStatus.Uebermittelt => Color.Info,
        MeldungStatus.InPruefung => Color.Primary,
        MeldungStatus.Festgestellt => Color.Success,
        MeldungStatus.Fehlgeschlagen => Color.Error,
        MeldungStatus.Storniert => Color.Dark,
        _ => Color.Default
    };

    public static string Icon(MeldungStatus status) => status switch
    {
        MeldungStatus.Entwurf => Icons.Material.Outlined.EditNote,
        MeldungStatus.Validierungsfehler => Icons.Material.Outlined.ErrorOutline,
        MeldungStatus.Uebermittelt => Icons.Material.Outlined.Outbox,
        MeldungStatus.InPruefung => Icons.Material.Outlined.HourglassTop,
        MeldungStatus.Festgestellt => Icons.Material.Outlined.TaskAlt,
        MeldungStatus.Fehlgeschlagen => Icons.Material.Outlined.ReportGmailerrorred,
        MeldungStatus.Storniert => Icons.Material.Outlined.Block,
        _ => Icons.Material.Outlined.Info
    };

    public static Color FarbeFuerHinweis(HinweisSchwere schwere) => schwere switch
    {
        HinweisSchwere.Fehler => Color.Error,
        HinweisSchwere.Warnung => Color.Warning,
        _ => Color.Info
    };

    public static string IconFuerHinweis(HinweisSchwere schwere) => schwere switch
    {
        HinweisSchwere.Fehler => Icons.Material.Filled.Error,
        HinweisSchwere.Warnung => Icons.Material.Filled.WarningAmber,
        _ => Icons.Material.Filled.Info
    };
}
