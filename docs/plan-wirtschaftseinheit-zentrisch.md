# Implementierungsplan: Wirtschaftseinheit-zentrisches Datenmodell

Umsetzung in einem Zug auf Branch `wirtschaftseinheit-zentrisch`. Konzept:
`docs/konzept-wirtschaftseinheit-zentrisch.md`.

## Reihenfolge (jede Stufe endet mit grünem Build)

1. **Core-Domain**: neue Enums `PersonStatus`, `EinheitStatus` (Aktiv/Archiviert).
2. **Core-DTOs**: `PersonDto`, `PersonUebersichtDto`, `WirtschaftseinheitDto`,
   `WirtschaftseinheitUebersichtDto`, `EinheitEigentuemerDto`.
3. **Persistence-Entities**: `PersonEntity`, `WirtschaftseinheitEntity`,
   `EinheitEigentuemerEntity`; `GrundsteuerMeldungEntity` bekommt `WirtschaftseinheitId?`,
   `MeldendePersonId?` (Snapshot bleibt: Flurstücke/Eigentümer bleiben an der Meldung als Kopie).
4. **DbContext**: neue DbSets + Beziehungen + Indizes; Schema-Nachzug ohne EF-Migrationen.
5. **Migration der Bestandsdaten** in `InitialisierenAsync`: jede bestehende Meldung →
   `Wirtschaftseinheit` (Aktiv) + `Person` (dedupliziert über IdNr-Hash) + `EinheitEigentuemer`;
   Meldung behält Vorgangsfelder, bekommt FK auf Einheit + meldende Person (= erster Eigentümer).
6. **Mapper**: `PersonMapper`, `WirtschaftseinheitMapper` (DTO ↔ Entity, IdNr-Hash-Grenze wie gehabt).
7. **Repository + Interface**: CRUD für Person und Einheit; `ErstelleMeldungAusEinheitAsync` mit
   Snapshot-Übernahme und der Regel „eine aktive Meldung je Einheit".
8. **Service (`GrundsteuerService`)**: neue Methoden durchreichen; „Meldung erstellen"-Logik.
9. **UI Pflegemasken**: `Pages/Personen/…` (Übersicht + Bearbeiten),
   `Pages/Wirtschaftseinheiten/…` (Übersicht + Bearbeiten), Drawer-Einstieg, Wizard-Anpassung
   (Einheit auswählen oder neu erfassen).
10. **Tests**: Person/Einheit CRUD, Migration, Snapshot, eine-aktive-Meldung-Regel, Dedupe.
11. **Testdaten**: Kernfall „eine Person → zwei Einheiten in zwei Ländern; eine Einheit → zwei
    aufeinanderfolgende Meldungen".
12. **Doku** + Commit.

## Kernentscheidungen (aus dem Konzept)

- Snapshot + Herkunfts-FK; Meldung bleibt eigenständig nach Erstellung.
- Einheit/Person: Status Aktiv/Archiviert.
- Eine aktive Meldung je Einheit (Service verweigert Zweite, solange aktiv).
- Meldende Person = freier FK auf Person-Master (auch Nicht-Eigentümer, z. B. Steuerberatungs-GmbH);
  Default erster Eigentümer nur als Vorbelegung.
- Person-Deduplizierung über IdNr-Hash.
