# syntax=docker/dockerfile:1

# =============================================================================================
#  GrundsteuerPortal - Blazor Server (.NET 10, MudBlazor)
#
#  Mehrstufiger Build: SDK-Stage baut, aspnet-Stage laeuft.
#  Basis-Tags geprueft (nicht extrapoliert): fuer .NET 10 gibt es kein 'bookworm-slim';
#  verfuegbar sind noble (Ubuntu), alpine, azurelinux3.0 und resolute.
# =============================================================================================

# ---------------------------------------------------------------------------------------------
#  Stage 1: Build
# ---------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# Nur die csproj-Dateien kopieren: der Restore-Layer bleibt im Cache, solange sich die
# Paketreferenzen nicht aendern. Bei mehreren Projekten MUESSEN alle beteiligten csproj
# dabei sein (Core, Persistence, Web), sonst scheitert der Restore am unvollstaendigen Baum.
# Das Testprojekt wird bewusst nicht kopiert - es gehoert nicht ins Image.
COPY src/GrundsteuerPortal.Core/GrundsteuerPortal.Core.csproj src/GrundsteuerPortal.Core/
COPY src/GrundsteuerPortal.Persistence/GrundsteuerPortal.Persistence.csproj src/GrundsteuerPortal.Persistence/
COPY src/GrundsteuerPortal.Web/GrundsteuerPortal.Web.csproj src/GrundsteuerPortal.Web/
RUN dotnet restore src/GrundsteuerPortal.Web/GrundsteuerPortal.Web.csproj

# Quellen kopieren. Durch .dockerignore bleiben bin/ und obj/ draussen - sonst wuerde dieses
# COPY das obj/ des Restore-Layers mit dem lokalen Stand ueberschreiben.
COPY . .

# KEIN --no-restore. Der zweite Restore kostet kaum Zeit (Pakete liegen im Cache), loest aber
# das Static-Web-Assets-Manifest vollstaendig auf. Mit --no-restore fehlt spaeter der komplette
# _framework-Ordner inklusive blazor.web.js: die Oberflaeche ist dann totes HTML, waehrend jede
# Seite HTTP 200 liefert. Das ist der teuerste Fehler beim Containerisieren einer Blazor-App.
RUN dotnet publish src/GrundsteuerPortal.Web/GrundsteuerPortal.Web.csproj \
        -c Release \
        -o /app/publish

# Abbruchpruefung: lieber hier scheitern als ein Image ausliefern, das erst im Browser tot ist.
RUN test -f /app/publish/wwwroot/_framework/blazor.web.js \
    || (echo "FEHLER: _framework/blazor.web.js fehlt im Publish - die Blazor-Oberflaeche waere totes HTML." >&2; exit 1)

# ---------------------------------------------------------------------------------------------
#  Stage 2: Laufzeit
# ---------------------------------------------------------------------------------------------
# noble statt alpine/chiseled: die App setzt deutsche Kultur ('de-DE'), und chiseled laeuft im
# Invariant-Globalization-Modus - deutsche Zahlen- und Datumsformate waeren dann still falsch.
# noble bringt libicu74 mit (geprueft im dotnet-docker-Repo).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final

WORKDIR /app

# Kein curl/wget im aspnet-Image - der HEALTHCHECK unten nutzt deshalb bash-Bordmittel.
# bash ist in noble vorhanden.

COPY --from=build /app/publish .

# ---------------------------------------------------------------------------------------------
#  Laufzeitkonfiguration
# ---------------------------------------------------------------------------------------------
# Nur HTTP im Container; TLS endet im Reverse Proxy davor. Ein Zertifikat im Image waere pro
# Umgebung neu zu bauen.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    LANG=C.UTF-8 \
    LC_ALL=C.UTF-8 \
    TZ=Europe/Berlin

# HOME explizit auf ein beschreibbares Verzeichnis setzen.
# Auf OpenShift laeuft der Container unter einer zufaelligen UID; deren HOME waere '/' und damit
# nicht beschreibbar. .NET legt dort unter anderem den Schluesselbund der DataProtection ab,
# sobald kein Verzeichnis konfiguriert ist - ohne die Variable scheitert das Schreiben.
ENV HOME=/app

# Datenbank ausserhalb des App-Verzeichnisses, damit ein Volume darauf gelegt werden kann.
# ASP.NET Core ueberschreibt appsettings.json durch die gleichnamige Umgebungsvariable.
ENV Datenbank__Pfad=/data/grundsteuer.db

# ASPNETCORE_ENVIRONMENT wird bewusst NICHT gesetzt: Standard ist Production, und
# appsettings.Development.json ist per .dockerignore ausgeschlossen (zweite Verteidigungslinie).

# DataProtection-Schluessel persistieren: ohne das erzeugt jeder neue Container frische Schluessel
# und ALLE bestehenden Cookies (Blazor-Circuit, Antiforgery) werden ungueltig.
# Die ENV-Namen entsprechen exakt den Schluesseln, die Program.cs liest:
#   Datenbank:DataProtectionPfad  <-  Datenbank__DataProtectionPfad
#   Datenbank:DataProtectionApplicationName <- Datenbank__DataProtectionApplicationName
ENV Datenbank__DataProtectionPfad=/data/keys \
    Datenbank__DataProtectionApplicationName=GrundsteuerPortal

# Das Datenverzeichnis MUSS fuer den Laufzeitbenutzer beschreibbar sein: die App legt es beim
# Start an und bricht sonst beim Seeding ab (der Prozess stirbt vor dem Serverstart, die
# Health-Endpunkte kommen gar nicht erst hoch).
#
# WICHTIG fuer OpenShift: dort wird die USER-Angabe vom restricted-v2 SCC ueberschrieben und der
# Container laeuft unter einer beliebigen, vorher unbekannten UID - aber immer mit der Gruppe 0.
# Ein 'chown 1654:1654' wuerde dort ins Leere laufen: die zufaellige UID ist nicht Mitglied der
# Gruppe 1654 und kann nicht schreiben. Deshalb Gruppe 0 + Gruppenrechte (chgrp -R 0 / chmod g=u).
# Fuer Docker/Kubernetes mit UID 1654 funktioniert dieselbe Regel, weil der Eigentuemer seine
# Rechte behaelt.
RUN mkdir -p /data/keys /app && \
    chgrp -R 0 /data /app && \
    chmod -R g=u /data /app

VOLUME ["/data"]

# Ohne Volume sind alle erfassten Meldungen beim Containerwechsel weg - ins README aufnehmen.
USER 1654

EXPOSE 8080

# Liveness (Prozess), NICHT Readiness: ein Datenbankausfall darf den Container nicht in eine
# Neustartschleife schicken - das loest das Problem nicht.
# bash-Bordmittel statt curl (im Image nicht vorhanden): /dev/tcp oeffnen, HTTP/1.0 anfragen.
HEALTHCHECK --interval=30s --timeout=5s --start-period=25s --retries=3 \
    CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health/live HTTP/1.0\r\n\r\n" >&3 && head -c 20 <&3 | grep -q "200"' || exit 1

ENTRYPOINT ["dotnet", "GrundsteuerPortal.Web.dll"]
