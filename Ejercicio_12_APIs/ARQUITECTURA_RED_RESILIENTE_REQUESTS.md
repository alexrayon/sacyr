# ARQUITECTURA DE RED RESILIENTE
## Monitor de Seguridad Climática con requests

## 1. Objetivo de arquitectura
Garantizar decisiones de seguridad confiables para grúas torre cuando la API climática sufre latencia, errores transitorios o degradación de calidad de datos.

## 2. Principios de diseño
- Seguridad primero: ante duda o falta de datos, activar fail-safe.
- Latencia acotada: timeout duro de 5 segundos por consulta.
- Desacoplo: lógica de negocio separada del cliente HTTP.
- Observabilidad: logs estructurados y métricas de salud.

## 3. Componentes
1. `MonitorRunner`
- Orquesta ciclo infinito de muestreo y decisiones.

2. `WeatherApiClient`
- Encapsula `requests.Session`, timeout, cabeceras y parsing básico.

3. `WeatherResponseValidator`
- Valida esquema JSON, tipos, rangos y timestamp.

4. `SafetyEngine`
- Calcula nivel Verde/Ámbar/Rojo y comando operativo.

5. `FailSafeManager`
- Gestiona transiciones por timeout, datos congelados y degradación.

6. `TelemetryLogger`
- Emite logs JSON y métricas técnicas/operativas.

## 4. Flujo operacional por ciclo
1. `MonitorRunner` inicia ciclo y crea `correlation_id`.
2. `WeatherApiClient` consulta API con timeout 5 s.
3. Si timeout o fallo de red, `FailSafeManager` emite `FAIL_SAFE_SIN_DATOS`.
4. Si respuesta OK, `WeatherResponseValidator` valida contrato y frescura.
5. `SafetyEngine` clasifica alerta según viento.
6. Si nivel rojo, emitir `PARADA_INMEDIATA`.
7. Registrar salida de ciclo con trazabilidad completa.
8. Esperar `sampling_interval` y repetir.

## 5. Estrategia de resiliencia HTTP (requests)

### 5.1 Sesión persistente
- Usar `requests.Session()` para reutilizar conexiones TCP y reducir sobrecarga.

### 5.2 Timeout y límites
- `timeout=5` en cada `GET`.
- No permitir llamadas sin timeout.

### 5.3 Reintentos acotados
- Reintentar solo errores transitorios (p. ej. 502/503/504 o `ConnectionError`).
- Máximo recomendado: 1 reintento inmediato para no sobrepasar ventana de decisión.
- Si falla, activar fail-safe y no bloquear ciclo.

### 5.4 Circuit breaker básico (recomendado)
- Tras N fallos consecutivos (por ejemplo 3), abrir circuito durante ventana corta (30 s).
- Mientras circuito abierto, no insistir al proveedor y operar en fail-safe con alertas.
- Cerrar circuito al detectar recuperación.

## 6. Gestión de calidad de datos

### 6.1 Frescura
- Calcular `edad_dato` desde `ultima_actualizacion`.
- Si `edad_dato > 10 s`: `DATOS_CONGELADOS`.
- Si persiste dos ciclos: elevar a `FAIL_SAFE_SIN_DATOS`.

### 6.2 Ráfagas fantasmales
- Detectar saltos abruptos de viento entre muestras.
- Si salto supera umbral interno y no hay consistencia, marcar `possible_ghost_gust`.
- En cualquier caso, si lectura >45 km/h se mantiene parada por principio de seguridad.

## 7. Topología lógica de despliegue
- Contenedor/servicio monitor (zona OT segura).
- Egreso controlado solo al proveedor meteorológico (puerto 443).
- DNS redundante y resolución cacheada.
- Sin exposición de endpoint de control al exterior.

## 8. Observabilidad y operación

### 8.1 Logs
- JSON estructurado por evento y ciclo.
- Campos: `timestamp_utc`, `event`, `severity`, `wind_kmh`, `alert_level`, `command`, `latency_ms`, `correlation_id`.

### 8.2 Métricas mínimas
- `api_latency_ms` (p50/p95/p99).
- `api_timeout_count`.
- `stale_data_count`.
- `red_alert_count`.
- `failsafe_activation_count`.

### 8.3 Alertas operativas
- Alerta inmediata ante cualquier `red_alert_count > 0`.
- Alerta de disponibilidad si `api_timeout_count` supera umbral en ventana de 5 min.

## 9. Riesgos y mitigaciones
- Riesgo: dependencia de un único proveedor.
  - Mitigación: estrategia futura multi-fuente con consenso.
- Riesgo: falsos positivos por picos espurios.
  - Mitigación: etiquetado y post-análisis, sin relajar la parada en rojo.
- Riesgo: degradación silenciosa de telemetría.
  - Mitigación: comprobación explícita de frescura y métricas.

## 10. Criterios de aceptación de arquitectura
- El ciclo nunca queda bloqueado por errores de red.
- El tiempo de decisión por ciclo es acotado y trazable.
- El sistema activa fail-safe en ausencia de datos dentro de la ventana de seguridad.
- La lógica de negocio no depende de detalles de `requests`.
