namespace GrundsteuerPortal.Persistence.Entities;

/// <summary>
/// Stammdaten eines Finanzamts (Bundesfinanzamtsnummer + Name), gecacht aus der ELSTER-WebAPI.
///
/// Warum persistieren und nicht bei jedem Aufruf laden? Die Auswahlliste in Schritt 1 ist
/// statisch genug, um sie lokal zu halten; dadurch bleibt der Wizard auch dann bedienbar, wenn die
/// WebAPI gerade nicht erreichbar ist. Bei jedem erfolgreichen API-Abruf wird der Cache aktualisiert.
/// </summary>
public class FinanzamtEntity
{
    /// <summary>Primärschlüssel = 4-stellige Bundesfinanzamtsnummer.</summary>
    public string Bundesfinanzamtsnummer { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public Core.Domain.Bundesland Bundesland { get; set; }
    public string? Ort { get; set; }

    /// <summary>Wann dieser Eintrag zuletzt aus der WebAPI bestätigt wurde.</summary>
    public DateTime ZuletztAktualisiertAmUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// PLZ→Ort/Bundesland-Zuordnung. Reiner Anzeige-Cache: füllt im Wizard beim Verlassen des
/// PLZ-Feldes den Ort vor und schlägt das Bundesland vor.
/// </summary>
public class PlzZuordnungEntity
{
    public string Postleitzahl { get; set; } = string.Empty;
    public string Ort { get; set; } = string.Empty;
    public Core.Domain.Bundesland Bundesland { get; set; }
}
