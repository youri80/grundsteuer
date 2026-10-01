{{/*
  Gemeinsame Namens- und Label-Helfer.
  Alle Namen werden auf 63 Zeichen begrenzt (DNS-Label-Grenze) und mit dem Release-Namen
  versehen, damit mehrere Installationen in einem Projekt koexistieren können.
*/}}

{{/* Vollständiger Name des Charts (z. B. grundsteuer-grundsteuerportal) */}}
{{- define "grundsteuer.fullname" -}}
{{- if contains .Chart.Name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name .Chart.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{/* Name des ServiceAccount */}}
{{- define "grundsteuer.serviceAccountName" -}}
{{- if .Values.serviceAccount.create -}}
{{- default (include "grundsteuer.fullname" .) .Values.serviceAccount.name -}}
{{- else -}}
{{- default "default" .Values.serviceAccount.name -}}
{{- end -}}
{{- end -}}

{{/* Image-Referenz inklusive Tag; wenn kein Tag gesetzt ist, wird appVersion verwendet */}}
{{- define "grundsteuer.image" -}}
{{- printf "%s:%s" .Values.image.repository (.Values.image.tag | default .Chart.AppVersion) -}}
{{- end -}}

{{/* Gemeinsame Labels */}}
{{- define "grundsteuer.labels" -}}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{ include "grundsteuer.selectorLabels" . }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
app.kubernetes.io/part-of: grundsteuerportal
{{- end -}}

{{/* Selektor-Labels - dürfen sich nach der Installation NICHT ändern */}}
{{- define "grundsteuer.selectorLabels" -}}
app.kubernetes.io/name: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{/*
  Prüfung der Datenkonfiguration: der Datenbankpfad muss unter dem Einhängepunkt liegen,
  sonst sind die Daten beim nächsten Pod-Wechsel verloren. Das ist ein stiller Fehler -
  die App läuft, nur die Daten sind weg. Deshalb hier hart abbrechen.
*/}}
{{- define "grundsteuer.pruefeDatenpfad" -}}
{{- if .Values.persistence.enabled -}}
{{- if not (hasPrefix (printf "%s/" .Values.persistence.mountPath) .Values.app.datenbankPfad) -}}
{{- fail (printf "app.datenbankPfad ('%s') muss unterhalb von persistence.mountPath ('%s') liegen, sonst sind die Daten nicht persistent." .Values.app.datenbankPfad .Values.persistence.mountPath) -}}
{{- end -}}
{{- if not (hasPrefix (printf "%s/" .Values.persistence.mountPath) .Values.app.dataProtectionPfad) -}}
{{- fail (printf "app.dataProtectionPfad ('%s') muss unterhalb von persistence.mountPath ('%s') liegen, sonst werden bei jedem Pod-Wechsel alle Cookies ungültig." .Values.app.dataProtectionPfad .Values.persistence.mountPath) -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{/*
  Warnung: mehr als eine Instanz ohne Sitzungsaffinität ist nicht betriebsfähig.
  Der Blazor-Circuit liegt im Speicher einer Instanz.
*/}}
{{- define "grundsteuer.pruefeReplicas" -}}
{{- if and (gt (int .Values.replicaCount) 1) (ne .Values.service.sessionAffinity "ClientIP") -}}
{{- fail "Mehr als eine Instanz erfordert service.sessionAffinity=ClientIP, sonst verliert der Nutzer bei jedem Pod-Wechsel seinen Blazor-Circuit." -}}
{{- end -}}
{{- if and (gt (int .Values.replicaCount) 1) (eq .Values.strategy.type "Recreate") -}}
{{- fail "Mehr als eine Instanz ist mit strategy=Recreate nicht sinnvoll (kein unterbrechungsfreier Rollout). Bitte RollingUpdate mit Shared-Volume verwenden." -}}
{{- end -}}
{{- end -}}
