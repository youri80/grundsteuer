using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Web.Components;
using GrundsteuerPortal.Web.Services;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
// Blazor Server (interaktives Server-Rendering)
// ---------------------------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------------------------
// MudBlazor: Komponenten-Dienste (u. a. Dialoge, Snackbar, Tooltip, Scroll-Spy) + Theme.
// Die MudTheme-Instanz ist ein Singleton, damit AppBar, Drawer und Dialoge dasselbe Theme nutzen.
// ---------------------------------------------------------------------------------------------
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 6000;
    config.SnackbarConfiguration.HideTransitionDuration = 200;
    config.SnackbarConfiguration.ShowTransitionDuration = 200;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

builder.Services.AddSingleton<GrundsteuerTheme>();

// ---------------------------------------------------------------------------------------------
// API-Zugriff: Typed HttpClient auf die vorhandene ELSTER-WebAPI.
// Ohne erreichbare API (oder mit Api:UseMock=true) übernimmt die Mock-Implementierung,
// damit die Oberfläche sofort lokal bedienbar ist.
// ---------------------------------------------------------------------------------------------
builder.Services.AddHttpClient<GrundsteuerApiService>(client =>
{
    var basis = builder.Configuration["Api:BasisAdresse"] ?? "https://localhost:7443/";
    client.BaseAddress = new Uri(basis);
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Api:TimeoutSekunden", 60));
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddScoped<IGrundsteuerApiService>(sp =>
{
    var konfiguration = sp.GetRequiredService<IConfiguration>();
    if (konfiguration.GetValue("Api:UseMock", true))
        return new MockGrundsteuerApiService();

    return sp.GetRequiredService<GrundsteuerApiService>();
});

// ---------------------------------------------------------------------------------------------
// Anwendungszustand (Formularsitzung): scoped = ein Satz je Blazor-Circuit (je Browser-Tab).
// Damit überlebt der Wizard-Nutzerzustand Schrittwechsel, ohne dass etwas global wird.
// ---------------------------------------------------------------------------------------------
builder.Services.AddScoped<GrundsteuerFormularSitzung>();
builder.Services.AddScoped<MeldungsUebersichtState>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Statische Assets MÜSSEN vor den Razor-Components registriert werden - sonst fehlt
// _framework/blazor.web.js und die Seite ist totes HTML (kein Circuit, keine Klicks).
app.MapStaticAssets();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
