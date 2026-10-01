using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Persistence;
using GrundsteuerPortal.Persistence.Abstractions;
using GrundsteuerPortal.Web.Components;
using GrundsteuerPortal.Web.Health;
using GrundsteuerPortal.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
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
// DataProtection-Schlüssel persistieren (Container-Betrieb). Ohne das wird bei jedem neuen
// Container ein frischer Schlüsselbund erzeugt und alle bestehenden Cookies (Blazor-Circuit,
// Antiforgery) sind ungültig - der Fehler tritt erst beim nächsten Deployment auf.
// ---------------------------------------------------------------------------------------------
var dataProtectionPersistiert = DataProtectionSetup.Konfiguriere(builder);

// ---------------------------------------------------------------------------------------------
// Persistenz: EF Core + SQLite (lokale Ablage für Entwürfe, Stammdaten und Verlauf).
// Der Pfad kommt aus der Konfiguration; im Container/auf dem Server wird ein beschreibbares
// Verzeichnis verwendet, nicht das Installationsverzeichnis.
// ---------------------------------------------------------------------------------------------
var datenbankPfad = builder.Configuration["Datenbank:Pfad"] ?? "data/grundsteuer.db";
builder.Services.AddGrundsteuerPersistenz(datenbankPfad);

// Salt für die Pseudonymisierung der Steueridentifikationsnummer. Gehört in die Konfiguration,
// nicht in den Code: ein Saltwechsel in einer bestehenden Datenbank macht gespeicherte Hashes
// unvergleichbar (die letzten drei Stellen bleiben zur Wiedererkennung erhalten).
GrundsteuerService.SetzeIdNrSalt(builder.Configuration["Datenbank:IdNrSalt"]);

// ---------------------------------------------------------------------------------------------
// Zugriff auf die vorhandene ELSTER-WebAPI: Typed HttpClient.
// Dieses Projekt selbst enthält KEINE Finanzverwaltungs-Schnittstelle - es ruft nur die
// bestehende REST-WebAPI auf. Ist sie nicht erreichbar oder nicht konfiguriert
// (Api:UseMock=true bzw. Api:IstKonfiguriert=false), bleibt die Oberfläche voll bedienbar:
// Entwürfe gehen in die lokale SQLite-Ablage, nur die ELSTER-Aktionen melden sich klar.
// ---------------------------------------------------------------------------------------------
var apiBasis = builder.Configuration["Api:BasisAdresse"];
var apiKonfiguriert = builder.Configuration.GetValue("Api:IstKonfiguriert", false)
                      && Uri.TryCreate(apiBasis, UriKind.Absolute, out _);

if (apiKonfiguriert)
{
    builder.Services.AddHttpClient<GrundsteuerApiService>(client =>
    {
        client.BaseAddress = new Uri(apiBasis!);
        client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Api:TimeoutSekunden", 60));
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    builder.Services.AddScoped<IGrundsteuerApiService>(sp => sp.GetRequiredService<GrundsteuerApiService>());
}
else
{
    // Kein HTTP-Client registriert: IGrundsteuerApiService bleibt als optionaler Parameter null.
    builder.Logging.AddFilter("GrundsteuerPortal.Web.Services.GrundsteuerService", LogLevel.Information);
}

// Die Fassade, die die UI verwendet: lokale Ablage + optionale ELSTER-WebAPI.
builder.Services.AddScoped<IGrundsteuerApiService>(sp =>
{
    var repo = sp.GetRequiredService<IGrundsteuerRepository>();
    var logger = sp.GetRequiredService<ILogger<GrundsteuerService>>();
    var elster = apiKonfiguriert ? sp.GetRequiredService<GrundsteuerApiService>() : null;

    return new GrundsteuerService(repo, logger, elster);
});

// ---------------------------------------------------------------------------------------------
// Anwendungszustand (Formularsitzung): scoped = ein Satz je Blazor-Circuit (je Browser-Tab).
// Damit überlebt der Wizard-Nutzerzustand Schrittwechsel, ohne dass etwas global wird.
// ---------------------------------------------------------------------------------------------
builder.Services.AddScoped<GrundsteuerFormularSitzung>();
builder.Services.AddScoped<MeldungsUebersichtState>();

var app = builder.Build();

// ---------------------------------------------------------------------------------------------
// Datenbank anlegen und Grunddaten einspielen (idempotent).
// Bewusst beim Start: SQLite braucht kein separates Deployment, und der erste Zugriff auf eine
// leere Datei würde sonst mitten im Wizard eine Ausnahme werfen.
// ---------------------------------------------------------------------------------------------
await using (var scope = app.Services.CreateAsyncScope())
{
    var repo = scope.ServiceProvider.GetRequiredService<IGrundsteuerRepository>();
    await repo.InitialisierenAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// ---------------------------------------------------------------------------------------------
// Proxy-Header auswerten: terminiert ein Reverse Proxy das TLS (im Container der Regelfall),
// muss X-Forwarded-Proto ankommen - sonst hält die App jede Anfrage für HTTP.
// Die Standardliste enthält nur Loopback; im Container läuft der Proxy in einem anderen Netz,
// deshalb werden KnownIPNetworks/KnownProxies geleert. (KnownNetworks ist in .NET 10 veraltet
// und löst eine Warnung aus - die aktuelle Eigenschaft ist KnownIPNetworks.)
// ---------------------------------------------------------------------------------------------
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownIPNetworks = { },
    KnownProxies = { }
});

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Statische Assets MÜSSEN vor den Razor-Components registriert werden - sonst fehlt
// _framework/blazor.web.js und die Seite ist totes HTML (kein Circuit, keine Klicks).
app.MapStaticAssets();

app.UseAntiforgery();

// Health-Endpunkte: /health/live (Prozess), /health/ready (DB), /health/assets (Blazor-Assets).
app.MapHealthEndpoints();
app.MapAssetCheck();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Selbstprüfung der Assets beim Start. Fehlen sie, ist die Oberfläche totes HTML, obwohl jede
// Seite 200 liefert - das muss im Log als Fehler stehen, nicht still passieren.
AssetSelfCheck.LogResult(app, app.Logger);

// Die Entscheidungen, die im Container zählen, beim Start protokollieren. Sonst wirkt das
// Fehlen (z. B. der HTTPS-Umleitung) wie ein Versehen und ist schwer zu finden.
app.Logger.LogInformation(
    "GrundsteuerPortal startet: Umgebung={Umgebung}, Datenbank={Datenbank}, "
    + "DataProtection persistiert={DataProtection}, ELSTER-WebAPI konfiguriert={Api}.",
    app.Environment.EnvironmentName,
    datenbankPfad,
    dataProtectionPersistiert,
    apiKonfiguriert);

app.Run();
