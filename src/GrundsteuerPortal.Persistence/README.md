# Persistenz (EF Core + SQLite)

Dieses Projekt hält die lokale Ablage des GrundsteuerPortals. Es kennt **keine** UI und wird von
der Web-Schicht nur über `IGrundsteuerRepository` benutzt (mit Core-DTOs, nicht mit Entities).

## Aufgabenteilung: lokal vs. ELSTER-WebAPI

| Daten | Ablage | Begründung |
|---|---|---|
| Erfasste Meldungen inkl. aller Fachangaben | **lokal (SQLite)** | Der Wizard muss jederzeit bedienbar sein, auch ohne Verbindung zur Finanzverwaltung; ein Absturz darf keine Eingabe kosten. |
| Finanzamt- und PLZ-Stammdaten | **lokal (Cache)** | Beschleunigt die Auswahllisten und hält sie offline verfügbar; Quelle bleibt die WebAPI. |
| Statusverlauf | **lokal (append-only)** | Im Steuerverfahren muss nachvollziehbar sein, wann welcher Stand erreicht wurde. |
| ELSTER-Übermittlung, Statusabfrage, PDF, ERiC-Vorprüfung | **WebAPI** | Das kann nur die Finanzverwaltung. Jeder Versuch wird lokal protokolliert. |

## Objektmodell

Das Aggregat ist die **Meldung** (eine Erklärung). Alles, was ohne sie keine Bedeutung hat,
hängt daran:

```
Meldungen (Aggregat)
├── Lage_* (Owned Type, gleiche Tabelle)      Lageadresse des Grundstücks
├── Berechnung_* (Owned Type, gleiche Tabelle) zuletzt ermittelter Steuermessbetrag
├── Flurstuecke          1:n   weitere Flurstücke der wirtschaftlichen Einheit
├── Eigentuemer          1:n   Personen/Firmen mit Eigentumsanteil
├── Hinweise             1:n   Prüfhinweise der letzten Validierung (werden ersetzt)
├── StatusVerlauf        1:n   append-only Protokoll aller Statuswechsel
└── Uebermittlungen      1:n   append-only Protokoll aller ELSTER-Versuche

Stammdaten (unabhängig)
├── Finanzaemter         Cache: Bundesfinanzamtsnummer → Name/Ort
└── PlzZuordnungen       Cache: PLZ → Ort/Bundesland
```

**Owned Types** (Lage, Berechnung) werden in derselben Tabelle gehalten: sie gehören exklusiv zu
einer Meldung, ein Join wäre unnötig, und sie werden immer als Ganzes gelesen/geschrieben.

**Getrennte Tabellen** für die Listen: sie haben eigene fachliche Identität (ein einzelnes
Flurstück, ein einzelner Eigentümer), können einzeln abgefragt werden und wachsen unabhängig.

## SQLite-Eigenheiten, die dieses Projekt bewusst behandelt

### 1. `decimal` als TEXT, nicht als REAL
SQLite kennt kein echtes DECIMAL. Als REAL verlieren Flächen und Geldbeträge Nachkommastellen —
bei einem Steuermessbetrag ist das ein Fehler, kein Schönheitsproblem. Alle Dezimalwerte sind
deshalb explizit `HasColumnType("TEXT")`. Testabdeckung: `Decimal_werte_ueberleben_den_sqlite_roundtrip_unveraendert`.

### 2. Enums als `int`
Die Zahlen in `Core/Domain/Enums.cs` sind stabil vergeben. `HasConversion<int>()` sorgt dafür, dass
ein Umbenennen im Code die Datenbank nicht bricht.

### 3. Kein `rowversion` → eigenes Nebenläufigkeitstoken
SQLite hat kein `rowversion`. `Meldungen.RowVersion` ist ein 16-Byte-Token, als
`IsConcurrencyToken()` konfiguriert und wird bei jeder Änderung neu gesetzt. Der Nutzen ist konkret:
zwei Browser-Tabs am selben Entwurf — der zweite Speicherversuch wird abgelehnt statt den ersten
stillschweigend zu überschreiben.

### 4. Neue Kind-Entitäten explizit als `Added` anmelden
**Wichtigste Falle im Projekt.** Wird ein Kind an eine Navigationsliste einer *getrackten* Entität
gehängt (`entity.Verlauf.Add(...)`) und seine `Id` ist bereits über einen Feldinitialisierer
gesetzt (`= Guid.NewGuid()`), schließt EF aus der gesetzten Id auf "existiert schon" und erzeugt
ein `UPDATE ... WHERE Id = @p` statt `INSERT`. Das trifft 0 Zeilen und endet in einer
`DbUpdateConcurrencyException` — obwohl niemand sonst geschrieben hat.

Lösung: Kind-Entitäten über den DbSet anmelden, nicht über die Navigationsliste:

```csharp
// richtig
_db.StatusVerlauf.Add(new StatusVerlaufEntity { GrundsteuerMeldungId = id, Status = status });

// falsch -> UPDATE statt INSERT -> DbUpdateConcurrencyException
entity.Verlauf.Add(new StatusVerlaufEntity { Status = status });
```

### 5. `ChangeTracker.Clear()` nach jedem Schreibvorgang
Das Repository ist **scoped** und lebt damit so lange wie der Blazor-Circuit. Ohne
`ChangeTracker.Clear()` hält der Kontext nach dem Speichern veraltete Originalwerte für
`RowVersion`, und der nächste Schreibvorgang scheitert an der Nebenläufigkeitsprüfung.

### 6. GUID-Vergleich in rohem SQL
EF Core legt `Guid`-Werte in SQLite als **TEXT in Großbuchstaben** ab. Ein roher
`WHERE GrundsteuerMeldungId = $id`-Vergleich gegen die `ToString()`-Form liefert sonst
stillschweigend null Zeilen:

```csharp
p.Value = guid.ToString().ToUpperInvariant();   // so, nicht ToByteArray()
```

## Datenschutz: Steueridentifikationsnummer

Die IdNr (§ 139 AO) ist ein Identifikationsmerkmal und liegt **nicht im Klartext** in der Datei.
Gespeichert werden:

* `IdNrHash` — SHA-256 über (IdNr + Salt), erlaubt den Vergleich "schon erfasst?" ohne Kenntnis
  der Nummer;
* `IdNrLetzteDrei` — die letzten drei Stellen zur Wiedererkennung in der Oberfläche ("…901").

Der Salt steht in `Datenbank:IdNrSalt` und **muss in Produktion gesetzt werden** (Umgebungsvariable
`Datenbank__IdNrSalt`). Ein Saltwechsel macht bestehende Hashes unvergleichbar.

Das DTO führt die IdNr weiterhin im Klartext, weil das Formular sie braucht — der Mapper
(`MeldungMapper`) ist die Grenze zwischen beiden Welten.

## Schema-Änderungen

Aktuell: `EnsureCreatedAsync()`. Das genügt für eine Einzelplatz-SQLite-Datei und erspart die
Migrationskette — hat aber eine harte Konsequenz: **`EnsureCreated` ändert ein bestehendes Schema
nicht.** Sobald sich das Modell ändert, ist auf Migrationen umzustellen:

```bash
export PATH="$PATH:/opt/data/.dotnet_cli/.dotnet/tools"
dotnet ef migrations add <Name> --project src/GrundsteuerPortal.Persistence \
    --startup-project src/GrundsteuerPortal.Web
dotnet ef database update --project src/GrundsteuerPortal.Persistence \
    --startup-project src/GrundsteuerPortal.Web
```

Danach in `GrundsteuerRepository.InitialisierenAsync` `EnsureCreatedAsync` durch
`MigrateAsync` ersetzen. `dotnet-ef` 10.0.10 ist im Container installiert.

## Tests

```bash
dotnet test tests/GrundsteuerPortal.Tests/GrundsteuerPortal.Tests.csproj
```

Jeder Test arbeitet gegen eine **eigene echte SQLite-Datei** im Temp-Verzeichnis — bewusst keine
In-Memory-Datenbank, weil nur die Datei die Eigenheiten oben aufdeckt.
