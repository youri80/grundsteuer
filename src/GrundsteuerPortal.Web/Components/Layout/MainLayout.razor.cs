using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;

namespace GrundsteuerPortal.Web.Components.Layout;

public partial class MainLayout : LayoutComponentBase
{
    private bool _drawerOffen = true;
    private bool _istDunkel;

    /// <summary>
    /// Ist die ELSTER-WebAPI nicht verbunden? Dann zeigt die AppBar das offen an - der Nutzer soll
    /// wissen, dass Entwürfe lokal gespeichert werden und nur die Übermittlung nicht möglich ist.
    /// </summary>
    private bool MockAktiv => !Konfiguration.GetValue("Api:IstKonfiguriert", false);

    private string _version => typeof(MainLayout).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private void DrawerToggle() => _drawerOffen = !_drawerOffen;

    private void ThemeUmschalten() => _istDunkel = !_istDunkel;
}
