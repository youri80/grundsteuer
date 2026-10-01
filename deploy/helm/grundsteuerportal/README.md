# =============================================================================================
#  Helm-Chart: GrundsteuerPortal
#
#  Blazor-Server-Anwendung (siehe ../..) auf OpenShift 4.17.
# =============================================================================================

## Kurzbeginn

```bash
# 1. Ins Projekt wechseln bzw. eines anlegen
oc new-project grundsteuer

# 2. Image bereitstellen (Beispiel: aus dem Quelltext im Cluster bauen)
oc new-build --name grundsteuerportal --binary --strategy docker
oc start-build grundsteuerportal --from-dir=../.. --follow

# 3. Ausrollen
helm upgrade --install grundsteuer . \
  -n grundsteuer \
  --set image.repository=image-registry.openshift-image-registry.svc:5000/grundsteuer/grundsteuerportal \
  --set image.tag=latest \
  --set app.idNrSalt="$(openssl rand -base64 32)"
```

## Vor dem Produktivbetrieb

| Einstellung | Warum |
|---|---|
| `app.idNrSalt` | Das Salt ist der einzige Schutz der gespeicherten IdNr-Hashes. Der Vorgabewert ist kein Geheimnis. **Ein späterer Wechsel macht bestehende Hashes unvergleichbar.** |
| `image.repository` / `image.tag` | Ohne eigene Werte wird versucht, `grundsteuerportal:1.0.0` aus Docker Hub zu ziehen. |
| `api.basisAdresse` + `api.istKonfiguriert` | Ohne diese Werte laufen Entwürfe lokal; Übermittlung, Statusabfrage und PDF melden sich mit klarer Meldung. |
| `persistence.storageClassName` | Ohne Angabe entscheidet der Cluster; das Ergebnis ist nicht immer das gewünschte. |

## Was das Chart für OpenShift bereits berücksichtigt

**Deployment statt DeploymentConfig.** `DeploymentConfig` ist seit OpenShift 4.16 deprecated und
wird in einer künftigen Version entfernt. Das Chart nutzt `apps/v1`.

**Kein festes `runAsUser`.** Der `restricted-v2` SCC vergibt eine beliebige UID aus dem
Projektbereich (Strategie `MustRunAsRange`). Ein eingetragenes `runAsUser` führt dazu, dass der
Pod abgelehnt wird. Das Image ist stattdessen darauf vorbereitet: Verzeichnisse gehören der
Gruppe 0 und sind gruppenschreibbar, `HOME` zeigt auf ein beschreibbares Verzeichnis.

**`seccompProfile: RuntimeDefault`** — in 4.17 eine Anforderung des restricted-v2 SCC.

**`livenessProbe` ohne Datenbankzugriff.** Sie prüft `/health/live`. Würde sie wie die
readiness-Prüfung die Datenbank abfragen, schickte ein Datenbankausfall den Pod in eine
Neustartschleife — was das Problem nicht löst, sondern verschärft. Nur `/health/ready` bricht
dann mit 503 ein, und der Pod wird aus dem Loadbalancer genommen.

**`startupProbe`** — der erste Start legt die SQLite-Datenbank an und führt das Seeding aus.
Ohne eigene Anlaufprüfung schlägt die liveness-Prüfung währenddessen fehl und der Pod wird
neu gestartet, bevor er fertig ist.

**Sitzungsaffinität für den Blazor-Circuit.** Der Circuit liegt im Speicher einer Instanz. Das
Chart setzt `sessionAffinity: ClientIP` am Service und belässt die Cookie-Affinität des Routers
(`disable_cookies` wird bewusst **nicht** gesetzt).

**Route-Timeout 300s.** Der HAProxy-Vorgabewert ist 30s und trennt die SignalR-Verbindung,
sobald ein Nutzer länger nichts anklickt — beim Ausfüllen eines langen Formulars fällt das auf.

**Persistenz für Datenbank *und* DataProtection-Schlüssel.** Ohne persistierte Schlüssel erzeugt
jeder neue Pod frische, und alle Cookies werden ungültig (Blazor-Circuit, Antiforgery). Der
Fehler zeigt sich erst nach dem nächsten Deployment.

**Salt als Secret per `secretKeyRef`** — nicht als Klartext-`value` in der Pod-Vorlage, sonst
wäre es im Deployment-Manifest lesbar und das Secret sinnlos.

## Prüfungen, die das Chart selbst durchführt

Das Rendern **bricht mit Fehler ab**, statt still etwas Falsches zu erzeugen:

| Bedingung | Meldung |
|---|---|
| `app.datenbankPfad` außerhalb von `persistence.mountPath` | Daten wären nach dem nächsten Pod-Wechsel verloren |
| `app.dataProtectionPfad` außerhalb von `persistence.mountPath` | Bei jedem Pod-Wechsel würden alle Cookies ungültig |
| `replicaCount > 1` ohne `service.sessionAffinity: ClientIP` | Nutzer verlieren ihren Circuit |
| `replicaCount > 1` mit `strategy.type: Recreate` | Kein unterbrechungsfreier Rollout möglich |

## Wichtige Werte

| Wert | Vorgabe | Bedeutung |
|---|---|---|
| `replicaCount` | `1` | Bei mehr als einer Instanz greift die Sitzungsaffinität; `strategy` muss dann `RollingUpdate` sein |
| `strategy.type` | `Recreate` | ReadWriteOnce-Volumes dürfen nicht von zwei Pods beschrieben werden |
| `persistence.enabled` | `true` | `false` nur für Tests: Daten liegen dann im Container |
| `route.enabled` | `true` | Route anlegen; bei `false` Zugriff nur innerhalb des Clusters |
| `api.istKonfiguriert` | `false` | `true` = echte ELSTER-WebAPI verwenden |
| `serviceAccount.create` | `false` | Die App greift nicht auf die Kubernetes-API zu |

## Mehrere Instanzen

Erst sinnvoll, wenn die Datenbank das trägt. SQLite ist eine Datei: für mehr als einen Pod wird
`persistence.accessModes: ReadWriteMany` gebraucht (nicht von jeder Speicherklasse unterstützt),
und der letzte Schreiber gewinnt. Für echten Mehrbenutzerbetrieb gehört eine
Client-Server-Datenbank darunter (die Anwendung nutzt EF Core — der Wechsel ist im Projekt
`GrundsteuerPortal.Persistence` vorbereitet).

## Prüfen nach dem Ausrollen

```bash
helm status grundsteuer -n grundsteuer
oc get pods,route,pvc -n grundsteuer
oc logs deploy/grundsteuer-grundsteuerportal -n grundsteuer
```

## Tests ohne Cluster

`helm lint` prüft die Vorlagen, **fängt aber keine unbekannten Variablen**: Helm rendert sie
still zu einer leeren Zeichenkette. Deshalb zusätzlich die gerenderte Ausgabe gegen echte Schemas
validieren:

```bash
helm template grundsteuer . -n grundsteuer > /tmp/gerendert.yaml
kubeconform -strict -summary /tmp/gerendert.yaml
```

Für die Route wird ein OpenShift-Schema gebraucht:
<https://github.com/datreeio/CRDs-catalog> (`openshift/v4.15-strict/route_route.openshift.io_v1.json`).
