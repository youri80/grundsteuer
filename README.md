# GrundsteuerPortal — Betrieb im Container

Frontend für ELSTER-Grundsteuermeldungen (Blazor Server, .NET 10, MudBlazor).
Die Anbindung an die Finanzverwaltung läuft über eine **vorhandene REST-WebAPI**; dieses Projekt
enthält nur das Frontend.

## Schnellstart

```bash
docker compose up -d --build
# Oberfläche:  http://localhost:8080
# Health:      curl -s localhost:8080/health/ready
```

Oder ohne compose:

```bash
docker build -t grundsteuerportal:1.0.0 .
docker run -d --name grundsteuerportal -p 8080:8080 \
  -v grundsteuer-daten:/data \
  -e Datenbank__IdNrSalt="<eigenes-salt-setzen>" \
  grundsteuerportal:1.0.0
```

## Was das Image mitbringt

| | |
|---|---|
| Basis | `mcr.microsoft.com/dotnet/aspnet:10.0-noble` |
| Benutzer | nicht-privilegiert, UID `1654` (aus dem dotnet-docker-Repo, nicht geraten) |
| Port | `8080`, nur HTTP — TLS endet im Reverse Proxy davor |
| Daten | SQLite unter `/data` (Volume) |
| Culture | `libicu74` vorhanden, deutsche Formate funktionieren |

**Kein `alpine`, kein `-chiseled`:** chiseled läuft im Invariant-Globalization-Modus. Die App setzt
deutsche Kultur; mit chiseled wären Zahlen- und Datumsformate still falsch.

## Konfiguration per Umgebungsvariable

Verschachtelte Schlüssel mit doppeltem Unterstrich. Das Image ist für alle Installationen gleich;
unterschieden wird über diese Werte und den Datenpfad.

| Umgebungsvariable | Bedeutung |
|---|---|
| `Datenbank__Pfad` | Ort der SQLite-Datei, z. B. `/data/grundsteuer.db` |
| `Datenbank__IdNrSalt` | Salt für die Pseudonymisierung der IdNr — **ändern**. Ein Wechsel macht bestehende Hashes unvergleichbar (die letzten drei Stellen bleiben zur Wiedererkennung) |
| `Datenbank__DataProtectionPfad` | Verzeichnis für die DataProtection-Schlüssel |
| `Datenbank__DataProtectionApplicationName` | stabiler Anwendungsname, z. B. `GrundsteuerPortal` |
| `Api__IstKonfiguriert` | `true` = echte ELSTER-WebAPI verwenden |
| `Api__BasisAdresse` | Basisadresse der ELSTER-WebAPI |
| `Api__TimeoutSekunden` | Timeout für API-Aufrufe (Standard 60) |

`ASPNETCORE_ENVIRONMENT` wird **absichtlich nicht** gesetzt: Standard ist `Production`, und
`appsettings.Development.json` wird weder in den Build-Kontext noch ins Publish aufgenommen.

## Volumes — wichtig

Ohne gemountetes `/data` sind **alle erfassten Meldungen, der Statusverlauf und die
DataProtection-Schlüssel beim nächsten Containerwechsel weg.** Zwei Folgen:

1. Fachlich: die Erklärungen sind verloren.
2. Technisch: mit neuen DataProtection-Schlüsseln wird jedes vorhandene Cookie ungültig
   (Blazor-Circuit, Antiforgery) — der Fehler zeigt sich erst beim nächsten Deployment.

## Health-Endpunkte

| Route | Bedeutung | Verhalten |
|---|---|---|
| `/health/live` | läuft der Prozess | immer `200`, kein DB-Zugriff — **das fragt der `HEALTHCHECK` ab** |
| `/health/ready` | ist die Datenbank nutzbar | `200` oder `503` |
| `/health/assets` | sind die Blazor-Assets im Publish | `200` oder `503` |

Liveness und Readiness sind bewusst getrennt: ein Datenbankausfall darf den Container **nicht** in
eine Neustartschleife schicken — das löst das Problem nicht.

## Reverse Proxy

TLS endet vor dem Container. Zwei Dinge müssen stimmen, sonst ist die Oberfläche tot:

1. `X-Forwarded-Proto` durchreichen. Die App wertet `ForwardedHeaders` aus; ohne den Header hält
   sie jede Anfrage für HTTP.
2. `/_framework/blazor.web.js` **nicht** umleiten. Eine HTTPS-Umleitung vor dem Blazor-Skript
   verhindert den Circuit-Start: die Seite sieht normal aus, reagiert aber auf nichts.

Prüfen:

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8080/_framework/blazor.web.js
# 200 = in Ordnung; 307 = eine Umleitung steht davor
```

## Fehlersuche: „Die Oberfläche reagiert auf nichts, aber die Seiten laden"

Mit hoher Wahrscheinlichkeit fehlen die Blazor-Framework-Assets im Image. Das Symptom ist
heimtückisch, weil **jede Seite HTTP 200 liefert**.

```bash
curl -s localhost:8080/health/assets
```

Antwortet der Endpunkt `503`, fehlt `_framework/blazor.web.js` im Publish. Ursache ist fast immer
ein `dotnet publish --no-restore` nach einem Restore, der nur die csproj-Dateien gesehen hat: der
frühe Restore löst das Static-Web-Assets-Manifest nicht vollständig auf, und `--no-restore`
übernimmt es so. Das Dockerfile lässt das `--no-restore` deshalb weg und bricht ab, wenn die Datei
nach dem Publish fehlt.

**Nicht** über `curl` auf `/_framework/blazor.web.js` diagnostizieren: im Build-Kontext kann
`MapStaticAssets` die Datei notfalls aus dem Quellpfad bedienen und antwortet mit `200`, obwohl sie
im Publish fehlt. Verlässlich ist nur der Dateitest (macht der Build) oder `/health/assets`.

## Neu bauen

Nach einer Code-Änderung ohne Cache neu bauen — die `COPY . .`- und `publish`-Layer sind sonst
unter Umständen identisch und die Container-CLI nimmt weiter das alte Image:

```bash
docker compose build --no-cache --pull
docker compose up -d
```

## Testdaten

Auf dem Dashboard legt ein Klick auf **Testdaten** acht vollständige Beispielmeldungen an — alle
fünf Berechnungsmodelle, beide Ordnungskriterien, Einzel- und Miteigentum. Damit lässt sich der
Meldungsprozess in der Oberfläche durchspielen, ohne Daten von Hand einzutippen.

Die Formate und Prüfziffern entsprechen den ELSTER-Vorgaben, die Finanzamtsnummern stammen aus den
zugelassenen Bereichen; die Fälle selbst sind erfunden. Die Übermittlung an die ELSTER-WebAPI ist
davon unberührt — ohne konfigurierte API melden die ELSTER-Aktionen wie bisher, dass sie nicht
verfügbar sind.

Details: `src/GrundsteuerPortal.Core/Testdaten/README.md`.

## Datenbank-Schema

Das Schema entsteht beim ersten Start über `EnsureCreatedAsync`. Das ändert ein **bestehendes**
Schema nicht — sobald sich das Modell ändert, ist auf Migrationen umzustellen (Anleitung in
`src/GrundsteuerPortal.Persistence/README.md`).

## Tests

Das Testprojekt gehört nicht ins Image (per `.dockerignore` ausgeschlossen). Es läuft auf dem
Entwicklungsrechner:

```bash
dotnet test tests/GrundsteuerPortal.Tests/GrundsteuerPortal.Tests.csproj
```
