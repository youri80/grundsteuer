using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;

namespace GrundsteuerPortal.Web.Components.Layout;

public partial class MainLayout : LayoutComponentBase
{
    private bool _drawerOffen = true;
    private bool _istDunkel;

    /// <summary>Läuft das Portal gegen den Mock (kein erreichbares Backend)? Dann zeigt die AppBar das an.</summary>
    private bool MockAktiv => Konfiguration.GetValue("Api:UseMock", true);

    private string _version => typeof(MainLayout).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private void DrawerToggle() => _drawerOffen = !_drawerOffen;

    private void ThemeUmschalten() => _istDunkel = !_istDunkel;
}
