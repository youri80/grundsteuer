using MudBlazor;

namespace GrundsteuerPortal.Web.Services;

/// <summary>
/// Zentrales Theme des Portals. Bewusst ein eigener Typ statt einer Instanz im Markup:
/// AppBar, Drawer, Dialoge und Snackbar beziehen dasselbe Theme aus der DI, sonst greift der
/// Umschalter auf Hell/Dunkel nur an einer Stelle.
/// </summary>
public sealed class GrundsteuerTheme : MudTheme
{
    public GrundsteuerTheme()
    {
        PaletteLight = new PaletteLight
        {
            // Ein ruhiges Verwaltungs-Blau: seriös, aber kein Behörden-Grau.
            Primary = "#0F4C81",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#2E7DAF",
            Tertiary = "#0B7A75",
            Background = "#F7F9FC",
            Surface = "#FFFFFF",
            AppbarBackground = "#0F4C81",
            AppbarText = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#1B2631",
            TextPrimary = "#1B2631",
            TextSecondary = "#4B5B6B",
            ActionDefault = "#4B5B6B",
            Success = "#1E7B4D",
            Info = "#0F4C81",
            Warning = "#B25E00",
            Error = "#B3261E",
            Divider = "#DDE3EA",
            LinesDefault = "#DDE3EA",
            TableLines = "#E6EAEF",
            TableStriped = "#F2F5F9",
            TableHover = "#E8F0F8"
        };

        PaletteDark = new PaletteDark
        {
            Primary = "#79B8F0",
            PrimaryContrastText = "#08243D",
            Secondary = "#8FCBF0",
            Tertiary = "#6FD3CC",
            Background = "#101820",
            Surface = "#16202A",
            AppbarBackground = "#0E1A24",
            AppbarText = "#E8EEF4",
            DrawerBackground = "#131E28",
            DrawerText = "#D6DEE6",
            TextPrimary = "#E8EEF4",
            TextSecondary = "#AFC0CF",
            Success = "#6BD3A0",
            Warning = "#F0B860",
            Error = "#F2A9A3",
            Divider = "#26333F",
            LinesDefault = "#26333F",
            TableLines = "#22303C",
            TableStriped = "#1A242E",
            TableHover = "#1F2C38"
        };

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "272px",
            AppbarHeight = "64px"
        };

        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "system-ui", "-apple-system", "Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif" },
                FontSize = "0.95rem",
                LineHeight = "1.5"
            },
            H1 = new H1Typography { FontSize = "2rem", FontWeight = "600" },
            H2 = new H2Typography { FontSize = "1.6rem", FontWeight = "600" },
            H3 = new H3Typography { FontSize = "1.35rem", FontWeight = "600" },
            H4 = new H4Typography { FontSize = "1.15rem", FontWeight = "600" },
            H5 = new H5Typography { FontSize = "1.05rem", FontWeight = "600" },
            H6 = new H6Typography { FontSize = "0.95rem", FontWeight = "600" },
            Button = new ButtonTypography { TextTransform = "none", FontWeight = "600", FontSize = "0.9rem" }
        };

        Shadows = new Shadow();
        ZIndex = new ZIndex();
    }
}
