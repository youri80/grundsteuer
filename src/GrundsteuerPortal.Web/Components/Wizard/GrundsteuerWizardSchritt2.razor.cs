using GrundsteuerPortal.Core.Api;
using GrundsteuerPortal.Web.Services;
using GrundsteuerPortal.Core.Domain;
using GrundsteuerPortal.Core.Validation;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GrundsteuerPortal.Web.Components.Wizard;

/// <summary>Schritt 2: Grundstücksdaten (Kataster, Flächen, Bodenrichtwert, weitere Flurstücke).</summary>
public partial class GrundsteuerWizardSchritt2 : ComponentBase, IDisposable
{
    [Inject] private GrundsteuerFormularSitzung Sitzung { get; set; } = default!;

    private bool _gemarkungsnummerFehler;
    private string? _gemarkungsnummerFehlertext;
    private bool _flurstueckFehler;
    private string? _flurstueckFehlertext;
    private bool _plzFehler;
    private string? _plzFehlertext;
    private bool _grundflaecheFehler;
    private string? _grundflaecheFehlertext;
    private bool _baujahrFehler;
    private string? _baujahrFehlertext;

    protected override void OnInitialized() => Sitzung.Geaendert += NeuRendern;

    public void Dispose() => Sitzung.Geaendert -= NeuRendern;

    private void NeuRendern() => InvokeAsync(StateHasChanged);

    // ---- Katasterangaben -------------------------------------------------------------------
    private void GemarkungGeaendert(string? wert)
    {
        Sitzung.Meldung.Gemarkung = wert ?? string.Empty;
        Sitzung.NeuValidieren();
    }

    private void GemarkungsnummerGeaendert(string? wert)
    {
        Sitzung.Meldung.Gemarkungsnummer = wert;
        var pruefung = ElsterFormate.PruefeGemarkungsnummer(wert);
        _gemarkungsnummerFehler = !pruefung.IstGueltig;
        _gemarkungsnummerFehlertext = pruefung.Meldung;
        Sitzung.NeuValidieren();
    }

    private void FlurGeaendert(string? wert)
    {
        Sitzung.Meldung.Flur = wert;
        Sitzung.NeuValidieren();
    }

    private void ZaehlerGeaendert(string? wert)
    {
        Sitzung.Meldung.FlurstueckZaehler = wert;
        FlurstueckPruefen();
    }

    private void NennerGeaendert(string? wert)
    {
        Sitzung.Meldung.FlurstueckNenner = wert;
        FlurstueckPruefen();
    }

    private void FlurstueckPruefen()
    {
        var pruefung = ElsterFormate.PruefeFlurstueck(
            Sitzung.Meldung.FlurstueckZaehler, Sitzung.Meldung.FlurstueckNenner);
        _flurstueckFehler = !pruefung.IstGueltig;
        _flurstueckFehlertext = pruefung.Meldung;
        Sitzung.NeuValidieren();
    }

    private void GrundbuchblattGeaendert(string? wert)
    {
        Sitzung.Meldung.Grundbuchblatt = wert;
        Sitzung.NeuValidieren();
    }

    private void GrundstuecksartGeaendert(Grundstuecksart art)
    {
        Sitzung.Meldung.Grundstuecksart = art;
        Sitzung.NeuValidieren();
    }

    // ---- Adresse ---------------------------------------------------------------------------
    private void StrasseGeaendert(string? wert) { Sitzung.Meldung.Lage.Strasse = wert ?? string.Empty; Sitzung.NeuValidieren(); }
    private void HausnummerGeaendert(string? wert) { Sitzung.Meldung.Lage.Hausnummer = wert ?? string.Empty; Sitzung.NeuValidieren(); }
    private void HausnummerZusatzGeaendert(string? wert) { Sitzung.Meldung.Lage.HausnummerZusatz = wert; Sitzung.NeuValidieren(); }
    private void OrtGeaendert(string? wert) { Sitzung.Meldung.Lage.Ort = wert ?? string.Empty; Sitzung.NeuValidieren(); }

    private void PlzGeaendert(string? wert)
    {
        Sitzung.Meldung.Lage.Postleitzahl = wert ?? string.Empty;
        var pruefung = ElsterFormate.PruefePostleitzahl(wert);
        _plzFehler = !string.IsNullOrWhiteSpace(wert) && !pruefung.IstGueltig;
        _plzFehlertext = pruefung.Meldung;
        Sitzung.NeuValidieren();
    }

    // ---- Flächen und Wert ------------------------------------------------------------------
    private void GrundstuecksflaecheGeaendert(decimal? wert)
    {
        Sitzung.Meldung.Grundstuecksflaeche = wert;
        var pruefung = ElsterFormate.PruefeFlaeche(wert, "Die Grundstücksfläche");
        _grundflaecheFehler = wert is not null && !pruefung.IstGueltig;
        _grundflaecheFehlertext = pruefung.Meldung;
        Sitzung.NeuValidieren();
    }

    private void WohnflaecheGeaendert(decimal? wert)
    {
        Sitzung.Meldung.Wohnflaeche = wert;
        Sitzung.NeuValidieren();
    }

    private void NutzflaecheGeaendert(decimal? wert)
    {
        Sitzung.Meldung.Nutzflaeche = wert;
        Sitzung.NeuValidieren();
    }

    private void BodenrichtwertGeaendert(decimal? wert)
    {
        Sitzung.Meldung.Bodenrichtwert = wert;
        Sitzung.NeuValidieren();
    }

    private void DurchschnittsBodenrichtwertGeaendert(decimal? wert)
    {
        Sitzung.Meldung.DurchschnittlicherBodenrichtwert = wert;
        Sitzung.NeuValidieren();
    }

    private void BaujahrGeaendert(int? wert)
    {
        Sitzung.Meldung.Baujahr = wert;
        var pruefung = ElsterFormate.PruefeBaujahr(wert);
        _baujahrFehler = wert is not null && !pruefung.IstGueltig;
        _baujahrFehlertext = pruefung.Meldung;
        Sitzung.NeuValidieren();
    }

    private void WohnlageGeaendert(Wohnlage? lage)
    {
        Sitzung.Meldung.Wohnlage = lage;
        Sitzung.NeuValidieren();
    }

    private void DenkmalGeaendert(bool wert)
    {
        Sitzung.Meldung.IstDenkmalgeschuetzt = wert;
        Sitzung.NeuValidieren();
    }

    private void SozialbauGeaendert(bool wert)
    {
        Sitzung.Meldung.IstSozialerWohnungsbau = wert;
        Sitzung.NeuValidieren();
    }

    private decimal? Lagefaktor() =>
        MessbetragRechner.Lagefaktor(Sitzung.Meldung.Bodenrichtwert,
            Sitzung.Meldung.DurchschnittlicherBodenrichtwert);

    // ---- Weitere Flurstücke ----------------------------------------------------------------
    private void FlurstueckHinzufuegen()
    {
        Sitzung.Meldung.Flurstuecke.Add(new FlurstueckDto
        {
            Gemarkung = Sitzung.Meldung.Gemarkung,
            Gemarkungsnummer = Sitzung.Meldung.Gemarkungsnummer
        });
        Sitzung.NeuValidieren();
    }

    /// <summary>Änderung an einem Flurstück der dynamischen Liste.</summary>
    private void FlurstueckGeaendert(FlurstueckDto flurstueck, Action<FlurstueckDto> aenderung)
    {
        aenderung(flurstueck);
        Sitzung.NeuValidieren();
    }

    private void FlurstueckEntfernen(FlurstueckDto flurstueck)
    {
        Sitzung.Meldung.Flurstuecke.Remove(flurstueck);
        Sitzung.NeuValidieren();
    }

    // ---- Hinweise --------------------------------------------------------------------------
    private static readonly HashSet<string> SchrittFelder = new(StringComparer.Ordinal)
    {
        "Gemarkung", "Gemarkungsnummer", "FlurstueckZaehler", "Grundstuecksflaeche", "Bodenrichtwert",
        "DurchschnittlicherBodenrichtwert", "Wohnflaeche", "Baujahr", "Wohnlage", "Grundbuchblatt",
        "Lage.Postleitzahl", "Lage.Ort", "Lage"
    };

    private List<ValidierungsHinweisDto> HinweiseFuerSchritt() =>
        Sitzung.Meldung.Hinweise
            .Where(h => h.Schwere != HinweisSchwere.Hinweis
                        && (SchrittFelder.Contains(h.Feld) || h.Feld.StartsWith("Flurstuecke[", StringComparison.Ordinal)))
            .ToList();

    private static Severity Schwere(HinweisSchwere schwere) => schwere switch
    {
        HinweisSchwere.Fehler => Severity.Error,
        HinweisSchwere.Warnung => Severity.Warning,
        _ => Severity.Info
    };
}
