# Testdaten

Zwölf vollständige Beispielmeldungen, damit der Meldungsprozess **und** die Statusfilter im
Navigationsbereich in der Oberfläche durchgespielt werden können — **ohne** dass Daten von Hand
eingetippt werden müssen.

Acht davon sind **Entwürfe**: sie durchlaufen den Assistenten von Schritt 1 bis 4. Vier tragen einen
**Endzustand** (übermittelt, festgestellt, in Prüfung, Validierungsfehler) — ohne sie hätten die
Filter „Übermittelt" und „Festgestellt" nie einen Treffer und ließen sich nicht prüfen.

## Einspielen

GrundsteuerPortal starten, Dashboard öffnen, auf **Testdaten** klicken und bestätigen. Der Vorgang
ist wiederholbar: bereits eingespielte Datensätze werden erkannt und nicht dupliziert.

Die Datensätze erscheinen anschließend im Dashboard und lassen sich wie echte Meldungen öffnen,
bearbeiten, validieren und speichern.

## Was enthalten ist

| Bundesland | Modell | Ordnungskriterium | Besonderheit |
|---|---|---|---|
| Hessen | Flächen-Faktor-Verfahren | Aktenzeichen | Lagefaktor aus Bodenrichtwert und Gemeindedurchschnitt |
| Hamburg | Wohnlagenmodell | **Steuernummer** | Wohnlage Pflichtangabe, 25 % Ermäßigung bei normaler Wohnlage |
| Bayern | Wertunabhängiges Flächenmodell | Aktenzeichen | 17-stelliges Aktenzeichen, Bezirksnummer ≥ 100 |
| Baden-Württemberg | Modifiziertes Bodenwertmodell | Aktenzeichen | Unbebautes Grundstück, Eigentümer ist eine juristische Person |
| Niedersachsen | Flächen-Lage-Modell | Aktenzeichen | Geschäftsgrundstück, reine Gewerbenutzung |
| Nordrhein-Westfalen | Bundesmodell | Aktenzeichen | 13-stelliges Aktenzeichen mit 4-stelliger Bezirksnummer |
| Berlin | Bundesmodell | **Steuernummer** | Wohnungseigentum |
| Sachsen | Bundesmodell | Aktenzeichen | **Miteigentum** zu je 1/2 (zwei Eigentümer) |

Damit sind beide Ordnungskriterien, alle fünf Berechnungsmodelle, bebaut und unbebaut, natürliche
und juristische Personen sowie Einzel- und Miteigentum abgedeckt.

### Zusätzlich: Datensätze mit Endzustand

| Status | Ableitung | Zeigt in der Oberfläche |
|---|---|---|
| Übermittelt | Bayern, eigene Nummer | Referenz und Übermittlungsdatum, schreibgeschützt |
| Festgestellt | NRW, eigene Nummer | Messbescheid liegt vor, nur noch stornierbar |
| In Prüfung | Sachsen, eigene Nummer | beim Finanzamt, Messbetrag steht aus |
| Validierungsfehler | BW, eigene Nummer | Zeile hervorgehoben, Pflichtangabe fehlt |

Diese vier leiten sich von den Entwürfen ab, tragen aber eine **eigene Nummer**. Ohne das wären sie
für die Dublettenerkennung beim Einspielen derselbe Datensatz — der zweite würde stillschweigend
fehlen. Abgesichert durch `AlleDatensaetze_HabenEindeutigeNummern`.

## Wie die Nummern entstehen

**Die Prüfziffern werden nicht von Hand gesetzt.** Jede Nummer entsteht in drei Schritten:

1. Grundteil bilden (Finanzamtsanteil, Bezirksnummer, laufende Nummer).
2. Prüfziffer mit **derselben Funktion berechnen, die der Validator verwendet**
   (`ElsterFormate.BerechneIdNrPruefziffer`, `BerechneSteuernummerPruefziffern`,
   `Aktenzeichen*`).
3. Ergebnis mit dem Validator **gegenprüfen**; bei Ablehnung wird ein neuer Grundteil gebildet.

Ein zweiter, abgeschriebener Algorithmus wäre genau die Stelle, an der Testdaten still ungültig
werden. Die Schleife kann deshalb keinen Datensatz liefern, den die Prüfung ablehnt — belegt durch
`tests/GrundsteuerPortal.Tests/TestdatenTests.cs`.

Die Nummern sind **stabil**: ein fester Startwert je Zweck sorgt dafür, dass dieselben Werte bei
jedem Lauf entstehen. Zufällige Testdaten machen jede Fehlersuche und jede Absprache unmöglich.

## Gültig, aber fiktiv

Die Formate und Prüfziffern entsprechen den ELSTER-Vorgaben, die **Finanzamtsnummern** stammen aus
den zugelassenen Bereichen des Verfahrens. Es sind aber **keine echten Steuerfälle**: die Namen,
Adressen und laufenden Nummern sind erfunden. Für eine Übermittlung an ein echtes Finanzamt sind
sie nicht bestimmt.

Alle Datensätze stehen im Status **Entwurf** und haben keine Übermittlungsdaten.

## Warum die Testdaten nicht im Persistence-Projekt liegen

`TestdatenFactory` liegt im **Core** (Domäne und Formatprüfung), das Einspielen in der **Web**-Schicht
(`TestdatensatzDienst`). Der Core kennt nur die Erzeugung, nicht den Speicherweg — und der Dienst
liegt getrennt von der Seite, damit die Dublettenerkennung ohne Oberfläche prüfbar bleibt.

## Einspielen ohne Oberfläche

Für Skripte und Prüfläufe lässt sich derselbe Dienst direkt aufrufen:

```csharp
var antwort = await TestdatensatzDienst.EinspielenAsync(api, logger);
```

## Bekannte Einschränkung

Die Steueridentifikationsnummer wird aus Datenschutzgründen **nur als Hash** gespeichert
(§ 139 AO, siehe `Persistence/README.md`). Nach dem Laden steht im Formular nur ein Platzhalter
(„…471"). Das ist so gewollt: der Platzhalter bedeutet „unverändert", und beim Speichern bleibt der
bestehende Hash erhalten. Der Validator meldet ihn deshalb als **Hinweis**, nicht als Fehler — sonst
würde jeder geladene Entwurf als fehlerhaft erscheinen und ließe sich nicht absenden.

Wer den Meldungsprozess mehrfach **neu** durchspielen will, löscht am einfachsten die Datenbankdatei
und startet die Anwendung neu (die Grunddaten werden dann neu eingespielt), oder legt über
**Kopieren** im Dashboard eine Kopie ohne Aktenzeichen an.
