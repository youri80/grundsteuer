using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Core.Validation;

namespace GrundsteuerPortal.Web.Services;

/// <summary>
/// Zustand EINER Formularsitzung (der Wizard-Vorgang im aktuellen Browser-Tab).
/// Scoped Service = je Blazor-Circuit eine Instanz. Die Komponenten halten dadurch keinen
/// eigenen fachlichen Zustand mehr; Schrittwechsel, Autosave und Validierung greifen auf dieselbe
/// Quelle zu. Fachlogik steckt NICHT hier, sondern in Core (Validator, Rechner).
/// </summary>
public sealed class GrundsteuerFormularSitzung
{
    private readonly IGrundsteuerApiService _api;
    private readonly ILogger<GrundsteuerFormularSitzung> _logger;

    public GrundsteuerFormularSitzung(IGrundsteuerApiService api, ILogger<GrundsteuerFormularSitzung> logger)
    {
        _api = api;
        _logger = logger;
        Meldung = Neu();
    }

    /// <summary>Die aktuell bearbeitete Meldung.</summary>
    public GrundsteuerMeldungDto Meldung { get; private set; }

    /// <summary>Aktueller Wizard-Schritt (Index des MudStepper).</summary>
    public int AktiverSchritt { get; set; }

    /// <summary>Läuft gerade ein API-Aufruf?</summary>
    public bool Beschaeftigt { get; private set; }

    /// <summary>Ist die Sitzung eine Neuanlage (noch nie gespeichert)?</summary>
    public bool IstNeuanlage => Meldung.ErstelltAm is null;

    /// <summary>Fehler- und Warnhinweise, wie sie zuletzt berechnet wurden.</summary>
    public IReadOnlyList<ValidierungsHinweisDto> Hinweise => Meldung.Hinweise;

    public IReadOnlyList<ValidierungsHinweisDto> Fehler =>
        Meldung.Hinweise.Where(h => h.Schwere == HinweisSchwere.Fehler).ToList();

    public IReadOnlyList<ValidierungsHinweisDto> Warnungen =>
        Meldung.Hinweise.Where(h => h.Schwere == HinweisSchwere.Warnung).ToList();

    /// <summary>Fehlerzahl je Schritt für die HasError-Markierung am MudStep.</summary>
    public IReadOnlyDictionary<WizardSchritt, int> FehlerJeSchritt =>
        MeldungsValidator.FehlerJeSchritt(Meldung);

    public GrundsteuerBerechnungDto? Berechnung => Meldung.Berechnung;

    /// <summary>Ereignis, das die Komponenten nach jeder Zustandsänderung neu rendern lässt.</summary>
    public event Action? Geaendert;

    private void Melde() => Geaendert?.Invoke();

    /// <summary>Neue, leere Meldung mit sinnvollen Vorbelegungen.</summary>
    public static GrundsteuerMeldungDto Neu() => new()
    {
        Id = Guid.Empty,
        Bundesland = Bundesland.Hessen,
        Erklaerungsart = Erklaerungsart.Erstmalig,
        Hauptfeststellungszeitpunkt = 2022,
        Grundstuecksart = Grundstuecksart.Einfamilienhaus,
        Lage = new AdresseDto(),
        Eigentuemer =
        {
            new EigentuemerDto { Art = EigentuemerArt.NatuerlichePerson, Anteil = 1m }
        }
    };

    /// <summary>Sitzung für eine Neuanlage zurücksetzen.</summary>
    public void Zuruecksetzen()
    {
        Meldung = Neu();
        AktiverSchritt = 0;
        NeuValidieren();
    }

    /// <summary>Bestehende Meldung in die Sitzung laden (Bearbeiten oder Kopieren).</summary>
    public async Task<bool> LadenAsync(Guid id, CancellationToken ct = default)
    {
        Beschaeftigt = true;
        Melde();
        try
        {
            var geladen = await _api.GetMeldungByIdAsync(id, ct);
            if (geladen is null) return false;

            Meldung = geladen;
            if (Meldung.Eigentuemer.Count == 0)
                Meldung.Eigentuemer.Add(new EigentuemerDto { Art = EigentuemerArt.NatuerlichePerson, Anteil = 1m });

            NeuValidieren();
            return true;
        }
        finally
        {
            Beschaeftigt = false;
            Melde();
        }
    }

    /// <summary>Übernimmt Änderungen aus dem Bundeslandwechsel: Sätze, Pflichtfelder und Hinweise nachziehen.</summary>
    public void BundeslandGeaendert(Bundesland land)
    {
        Meldung.Bundesland = land;

        // Ordnungskriterium wechselt mit dem Land (Aktenzeichen vs. Steuernummer): das jeweils
        // andere Feld wird geleert, damit keine veraltete Angabe stehen bleibt.
        var info = BundeslandKatalog.Fuer(land);
        if (info.Ordnungskriterium == Ordnungskriterium.Steuernummer)
            Meldung.Aktenzeichen = null;
        else
            Meldung.Steuernummer = null;

        // Die Wohnlage ist nur in Hamburg bewertungsrelevant.
        if (!info.BrauchtWohnlage) Meldung.Wohnlage = null;

        NeuValidieren();
    }

    /// <summary>Berechnet Hinweise und Messbetragsvorschau neu (synchron, ohne API-Roundtrip).</summary>
    public void NeuValidieren()
    {
        Meldung.Hinweise = MeldungsValidator.PruefeAlles(Meldung).ToList();
        Meldung.Berechnung = MessbetragRechner.Berechne(Meldung);
        Melde();
    }

    /// <summary>Speichert den Entwurf. Liefert die Antwort der API (inkl. Erfolgskennzeichen).</summary>
    public async Task<ApiResponse> SpeichernAsync(CancellationToken ct = default)
    {
        Beschaeftigt = true;
        Melde();
        try
        {
            var antwort = await _api.SaveDraftAsync(Meldung, ct);
            if (antwort.Id.HasValue && Meldung.Id == Guid.Empty)
                Meldung.Id = antwort.Id.Value;

            if (antwort.RowVersion is not null) Meldung.RowVersion = antwort.RowVersion;
            if (antwort.Berechnung is not null) Meldung.Berechnung = antwort.Berechnung;
            if (antwort.Hinweise.Count > 0) Meldung.Hinweise = antwort.Hinweise;

            Meldung.ZuletztGeaendertAm ??= DateTime.Now;
            Meldung.ErstelltAm ??= DateTime.Now;
            Melde();
            return antwort;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Entwurf konnte nicht gespeichert werden");
            return ApiResponse.Fehler("Der Entwurf konnte nicht gespeichert werden.", "UNERWARTET");
        }
        finally
        {
            Beschaeftigt = false;
            Melde();
        }
    }

    /// <summary>Übermittelt an ELSTER. Vorher wird die Validierung erzwungen.</summary>
    public async Task<ApiResponse> UebermittelnAsync(CancellationToken ct = default)
    {
        NeuValidieren();
        if (Fehler.Count > 0)
        {
            return ApiResponse.Fehler("Bitte zuerst die markierten Fehler beheben.", "VALIDIERUNG", Fehler.ToList());
        }

        // Der Server braucht eine persistierte Meldung - ungespeichertes wird zuerst gesichert.
        if (Meldung.Id == Guid.Empty || IstNeuanlage)
        {
            var gespeichert = await SpeichernAsync(ct);
            if (!gespeichert.Erfolg && Meldung.Id == Guid.Empty)
                return gespeichert;
        }

        Beschaeftigt = true;
        Melde();
        try
        {
            var antwort = await _api.SubmitToElsterAsync(Meldung.Id, ct);
            if (antwort.Erfolg)
            {
                Meldung.Status = MeldungStatus.Uebermittelt;
                Meldung.UebermitteltAm = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(antwort.Meldung))
                    Meldung.UebermittlungsReferenz = antwort.Meldung;
            }
            return antwort;
        }
        finally
        {
            Beschaeftigt = false;
            Melde();
        }
    }

    /// <summary>Status beim Finanzamt prüfen und in die Meldung zurückschreiben.</summary>
    public async Task<StatusPruefungDto?> StatusPruefenAsync(CancellationToken ct = default)
    {
        if (Meldung.Id == Guid.Empty) return null;
        Beschaeftigt = true;
        Melde();
        try
        {
            var status = await _api.PruefeStatusAsync(Meldung.Id, ct);
            if (status is not null)
            {
                Meldung.Status = status.Status;
                Meldung.UebermittlungsReferenz ??= status.Aktenzeichen;
                Melde();
            }
            return status;
        }
        finally
        {
            Beschaeftigt = false;
            Melde();
        }
    }

    /// <summary>Markiert die Meldung als geändert (Aufruf aus den MudField-Bindungen).</summary>
    public void AlsGeaendertMarkieren()
    {
        Meldung.ZuletztGeaendertAm = DateTime.Now;
        Melde();
    }
}
