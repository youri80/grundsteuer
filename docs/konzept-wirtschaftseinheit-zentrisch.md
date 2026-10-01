# Konzept: Wirtschaftseinheit-zentrisches Datenmodell

Stand: 2026-10-01 · Status: **abgestimmt, bereit zur Umsetzung**

## Ausgangslage und Ziel

Die Anwendung ist heute **meldungszentrisch**: `GrundsteuerMeldungEntity` (Tabelle `Meldungen`)
hält sowohl die fachlichen Bestandsdaten (Gemarkung, Flurstücke, Eigentümer, Flächen,
Bodenrichtwert) als auch die Vorgangsdaten (Status, Aktenzeichen, Übermittlung, Verlauf).

Ziel ist die Umkehr: Die **Wirtschaftseinheit** wird ein eigenständiger, wiederverwendbarer
Bestand mit eigenen Pflegemasken. Die **Meldung an das Finanzamt** wird ein Vorgang, der sich auf
eine Einheit bezieht und **bei Bedarf daraus erzeugt** wird. Der bestehende Wizard-Weg bleibt als
Alternative erhalten.

Zusätzliche Anforderung (Nutzer): Ein Eigentümer besitzt typischerweise **mehrere
Wirtschaftseinheiten in verschiedenen Bundesländern**. Eigentümer **und** meldende Person sollen
deshalb ebenfalls separat gepflegt und bearbeitet werden können. Die meldende Stelle kann eine
andere sein als ein Eigentümer — z. B. eine Steuerberatungs-GmbH.

## Drei Entitäten statt einer

```
PERSON (Master, wiederverwendbar)
  Name, Anrede, Art (natürlich/juristisch/…), IdNr (Hash), Steuernummer, Adresse, Geburtsdatum
  Status: Aktiv / Archiviert

WIRTSCHAFTSEINHEIT (Bestand)
  Lage-Adresse, Gemarkung, Flur, Flurstück, Grundbuchblatt, Flurstücke (Liste)
  Grundstücksart, Flächen, Baujahr, Bodenrichtwert, Wohnlage, Denkmal, Sozialbau
  Bundesland + Bundesfinanzamtsnummer + Finanzamt-Name
  Eigentümer-Zuordnung (n:m zu Person, mit Anteil)   → EinheitEigentuemer
  Status: Aktiv / Archiviert

MELDUNG (Vorgang, Snapshot)
  Status, Erklärungsart, Hauptfeststellungszeitpunkt
  Aktenzeichen / AktenzeichenElster / Steuernummer
  MeldendePersonId → Person (freier Verweis, NICHT an Eigentümer gebunden)
  Übermittlungsreferenz, ÜbermitteltAm, MessbescheidAm, FestgestellterMessbetrag
  Berechnung, Hinweise, Statusverlauf, Übermittlungsprotokoll
  + Snapshot-Kopie der Einheit-Felder und Eigentümer (eingefrorener Abdruck)
```

## Drei Rollen klar getrennt

| Rolle | Wo gepflegt | Verbindung |
|---|---|---|
| **Person** (natürlich/juristisch) | Maske „Personen" | Master, einmal angelegt |
| **Eigentümer** | Zuordnung in der Einheit | `EinheitEigentuemer`: PersonId + Anteil (Miteigentum 1/2+1/2) |
| **Meldende Stelle** | Auswahl in der Meldung | `MeldendePersonId` → Person |

Die **meldende Stelle** ist ein freier Verweis auf den Person-Master. Sie muss **nicht** Eigentümer
sein: eine Steuerberatungs-GmbH (Person vom Typ „Juristische Person") kann die Erklärung senden,
ohne je Eigentümer zu sein. Der Default „erster Eigentümer" ist nur eine Vorbelegung in der UI,
keine fachliche Regel.

## Snapshot-Semantik (abgestimmt: Punkt 1 = Snapshot)

Beim Erstellen einer Meldung aus einer Einheit wird alles kopiert, was das Finanzamt braucht —
**danach ist die Meldung eigenständig**:

- Einheit-Felder (Flurstücke, Flächen, Gemarkung) → Kopie in die Meldung
- Eigentümer (Name, Adresse, IdNr-Hash, Anteil) → Kopie in die Meldung
- Meldende Person (Name, Adresse, IdNr-Hash) → Kopie in die Meldung

Begründung: Eine abgegebene Erklärung muss den Stand zum Erklärungszeitpunkt festhalten. Änderungen
an Person oder Einheit danach lassen bereits gesendete Meldungen unberührt. Die Einheit bleibt
Referenz für die Ersterfassung; die Meldung ist ein eingefrorener Abdruck. Die Meldung behält einen
`WirtschaftseinheitId`-Fremdschlüssel für die Herkunft.

## Regeln

- **Einheit-Status**: `Aktiv` / `Archiviert`. Archiviert = nicht mehr meldbar, bleibt lesbar.
- **Person-Status**: `Aktiv` / `Archiviert`. Archivierte Person ist nicht mehr neu zuordenbar,
  bestehende Zuordnungen bleiben lesbar.
- **Eine aktive Meldung je Einheit**: Der Service verweigert eine zweite Meldung, solange eine
  aktive (nicht storniert/erledigt) existiert. Erst nach Abschluss ist die nächste
  (Änderung/Berichtigung) erlaubt.

## Pflegemasken

1. **Personen** (neu): Übersicht + Bearbeiten. Name, Art, Anrede, IdNr, Steuernummer, Adresse.
   Wiederverwendbar.
2. **Wirtschaftseinheiten** (neu): Übersicht + Bearbeiten (ein Formular, kein Wizard). Lage,
   Flurstücke, Eigentümer-Zuordnung (Person wählen + Anteil), Bundesland/Finanzamt.
3. **Meldungen** (bestehend): Wizard bleibt. Neu: beim Start eine Einheit wählen (Snapshot
   übernehmen) oder alles neu erfassen (legt implizit Einheit + ggf. Person an).

Drawer-Einstieg: „Personen / Wirtschaftseinheiten / Meldungen".

## Migration der Bestandsdaten

Beim Start wird jede bestehende Meldung rückwirkend aufgeteilt:

- Grundstücks-/Eigentümerfelder → neue `Wirtschaftseinheit` (Status `Aktiv`)
- Eigentümer → `Person`-Master (Deduplizierung über **IdNr-Hash**, nicht Name) + `EinheitEigentuemer`
- Vorgangsfelder bleiben in der Meldung

Bestehende Meldungen behalten ihre Daten und bekommen eine Einheit + Personen als Herkunft. Da das
Projekt keine EF-Migrationen nutzt (`EnsureCreatedAsync`), läuft das als idempotenter Schema-Nachzug.

## Testdaten

Die bestehenden 12 Sätze werden erweitert um den Kernfall: **eine Person besitzt zwei Einheiten in
zwei Bundesländern** (z. B. Einfamilienhaus in Hessen + Eigentumswohnung in Bayern), und eine Einheit
erzeugt zwei aufeinanderfolgende Meldungen (Hauptfeststellung, nach Abschluss eine Änderung). Belegt
Snapshot, Wiederverwendung der Person und die „eine aktive Meldung"-Regel.

## Abgestimmte Entscheidungen

1. **Snapshot** + `WirtschaftseinheitId`-FK (nicht reine Referenz).
2. Einheit hat Status **Aktiv/Archiviert**.
3. **Eine aktive Meldung je Einheit**.
4. Testdaten werden erweitert.
5. Person-Status **Aktiv/Archiviert**.
6. **Meldende Person liegt an der Meldung** (freier FK), Default erster Eigentümer nur als Vorbelegung.
7. Person-Deduplizierung über **IdNr-Hash**.
8. Meldende Stelle ist ein **freier Verweis** auf den Person-Master (auch Nicht-Eigentümer, z. B.
   Steuerberatungs-GmbH).

## Umsetzungsstufen (Reihenfolge)

1. Datenmodell: `WirtschaftseinheitEntity`, `PersonEntity`, `EinheitEigentuemerEntity` +
   Verschlankung der Meldung + Schema-Nachzug/Migration.
2. Repository/Service/DTOs/Mapper für Person und Einheit (CRUD).
3. Pflegemasken: Personen + Wirtschaftseinheiten (Übersicht + Bearbeiten).
4. „Meldung erstellen" (Snapshot-Übernahme) + meldende Stelle.
5. Wizard-Weg B: Einheit auswählen oder neu erfassen; „eine aktive Meldung"-Regel.
6. Tests + Doku.
