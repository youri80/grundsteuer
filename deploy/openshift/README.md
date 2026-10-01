# Bereitstellung auf OpenShift (4.17)

Fertig montierte Manifeste für eine Installation **ohne Helm** — erzeugt aus dem Chart mit
`helm template`. Sinnvoll, wenn im Cluster kein Helm verfügbar ist oder die Manifeste durch eine
GitOps-Pipeline (Argo CD, OpenShift GitOps) verwaltet werden.

```bash
oc apply -f deploy/openshift/
```

## Enthaltene Objekte

| Datei | Objekt | Zweck |
|---|---|---|
| `00-namespace.yaml` | Namespace + ResourceQuota + LimitRange | Projekt mit Obergrenzen |
| `01-secret.yaml` | Secret | Salt der IdNr-Pseudonymisierung |
| `02-configmap.yaml` | ConfigMap | Pfade, API-Adresse, Zeitzone |
| `03-pvc.yaml` | PersistentVolumeClaim | SQLite-Datenbank + DataProtection-Schlüssel |
| `04-service.yaml` | Service | Interner Zugang, mit Sitzungsaffinität |
| `05-deployment.yaml` | Deployment | Die Anwendung |
| `06-route.yaml` | Route | Zugang von außen, TLS am Router |

## Vor dem Ausrollen anpassen

**Pflicht — sonst ist die Installation nicht betriebsfähig:**

1. **`01-secret.yaml`**: Salt auf einen eigenen Wert ändern. Der Platzhalter
   `bitte-aendern-vor-produktivbetrieb` ist kein Geheimnis und schützt nichts.
   Ein späterer Wechsel macht bereits gespeicherte Hashes unvergleichbar.
2. **`05-deployment.yaml`**: `image:` auf das eigene Image setzen
   (z. B. `image-registry.openshift-image-registry.svc:5000/grundsteuer/grundsteuerportal:1.0.0`).
3. **`02-configmap.yaml`**: `Api__BasisAdresse` auf die vorhandene ELSTER-WebAPI der
   Finanzverwaltung setzen und `Api__IstKonfiguriert` auf `"true"`.

**Optional:** Speicherklasse im PVC (`storageClassName`), Ressourcen, Hostname der Route
(leer lassen = automatisch vergeben).

## Reihenfolge

Die Nummern geben die Reihenfolge an; `oc apply -f deploy/openshift/` wendet sie alphabetisch an.
Das Deployment wartet über die `startupProbe` von selbst darauf, dass die Datenbank bereit ist —
eine manuelle Wartezeit ist nicht nötig.

## Prüfen

```bash
# Läuft der Pod?
oc get pods -n grundsteuer -l app.kubernetes.io/name=grundsteuerportal

# Ist die Anwendung wirklich bereit (nicht nur der Pod gestartet)?
oc exec deploy/grundsteuer-grundsteuerportal -n grundsteuer -- \
  bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health/ready HTTP/1.0\r\n\r\n" >&3 && head -c 400 <&3'

# URL ermitteln
oc get route grundsteuer-grundsteuerportal -n grundsteuer \
  -o jsonpath='https://{.spec.host}{"\n"}'
```

## Unterschiede zum Helm-Chart

Diese Manifeste sind eine **Momentaufnahme** des Charts mit den Standardwerten. Änderungen
gehören ins Chart (`deploy/helm/grundsteuerportal/`), sonst laufen beide Fassungen auseinander:

```bash
helm template grundsteuer deploy/helm/grundsteuerportal -n grundsteuer \
  --set image.repository=... --set app.idNrSalt=... > deploy/openshift/gesamt.yaml
```
