# ESPECIFICACION FUNCIONAL Y TECNICA
## Monitor de Seguridad Climática para Grúas Torre Sacyr

## 1. Objetivo
Diseñar un monitor de seguridad operacional que consuma datos de viento en tiempo real y emita decisiones de seguridad para operación de grúa torre.

Regla crítica de negocio:
- Si `viento_kmh > 45.0`, el sistema debe emitir `PARADA_INMEDIATA`.

## 2. Contrato de API Externa (Proveedor Meteorológico)

### 2.1 Endpoint
- Método: `GET`
- Ruta lógica: `/v1/weather/wind/current`
- Autenticación: Header `X-API-Key: <valor>`
- Formato de respuesta: `application/json`

### 2.2 JSON esperado (contrato mínimo)
```json
{
  "status": "success",
  "data": {
    "estacion_id": "Sacyr-Madrid-Norte",
    "viento_kmh": 42.7,
    "direccion": "NE",
    "ultima_actualizacion": "2026-04-17T12:30:05+00:00"
  }
}
```

### 2.3 Reglas de validación de contrato
- `status`: obligatorio, tipo `string`, valor permitido `success`.
- `data`: obligatorio, tipo `object`.
- `data.estacion_id`: obligatorio, tipo `string`, no vacío.
- `data.viento_kmh`: obligatorio, tipo numérico (`int` o `float`), rango físico válido `[0, 120]` km/h.
- `data.direccion`: obligatorio, tipo `string`, valores esperados `N|NE|E|SE|S|SW|W|NW`.
- `data.ultima_actualizacion`: obligatorio, fecha ISO-8601 con zona horaria.

## 3. Protocolo de Timeout y Fail-safe

### 3.1 Timeout operativo
- Timeout máximo por llamada: `5 segundos`.
- Tecnología objetivo: `requests.get(..., timeout=5)`.

### 3.2 Política de fallo por falta de datos
Si la API no responde en 5 segundos, o hay error de red no recuperable:
- Estado del monitor: `FAIL_SAFE_SIN_DATOS`.
- Acción operacional: `ALERTA_OPERADOR`.
- Acción de control: mantener último estado conocido y elevar severidad operativa a al menos Ámbar hasta recuperar telemetría fresca.
- Log obligatorio: evento estructurado `weather_timeout_fail_safe` con `timestamp_utc`, `endpoint`, `timeout_s`, `correlation_id`.

## 4. Mapeo de niveles de alerta por viento

- Verde: `viento_kmh <= 35.0`
  - Acción: operación permitida.
- Ámbar: `35.0 < viento_kmh <= 45.0`
  - Acción: reducir maniobras críticas, activar vigilancia reforzada y confirmar lecturas consecutivas.
- Rojo: `viento_kmh > 45.0`
  - Acción: `PARADA_INMEDIATA` de la grúa y notificación de incidente de seguridad.

## 5. Resiliencia frente a datos anómalos

### 5.1 Ráfagas fantasmales
Definición operativa:
- Incremento abrupto no plausible entre muestras consecutivas (por ejemplo, delta > 20 km/h en 1 ciclo) no corroborado por muestra inmediata posterior.

Tratamiento:
- No ignorar automáticamente una lectura roja; priorizar seguridad.
- Emitir `PARADA_INMEDIATA` si supera 45 km/h.
- Etiquetar evento como `possible_ghost_gust` para investigación.
- Solicitar confirmación en ventana corta (segunda lectura en <= 2 s) para clasificación final del incidente.

### 5.2 Datos congelados (lecturas antiguas)
Definición operativa:
- `edad_dato = now_utc - ultima_actualizacion`.
- Umbral de frescura recomendado: `edad_dato <= 10 s`.

Tratamiento:
- Si `edad_dato > 10 s`, estado `DATOS_CONGELADOS`.
- Acción: `ALERTA_OPERADOR` y transición a `FAIL_SAFE_SIN_DATOS` si persiste por 2 ciclos consecutivos.
- Registrar evento `stale_weather_data` con metadatos de latencia.

## 6. Salida esperada del monitor (contrato interno)

```json
{
  "timestamp_utc": "2026-04-17T12:30:10+00:00",
  "wind_kmh": 46.3,
  "alert_level": "ROJO",
  "command": "PARADA_INMEDIATA",
  "source_station": "Sacyr-Madrid-Norte",
  "data_freshness_seconds": 1.4,
  "flags": ["possible_ghost_gust"]
}
```

## 7. Criterios de aceptación mínimos
- El monitor devuelve `PARADA_INMEDIATA` para cualquier lectura `viento_kmh > 45`.
- Ante timeout de 5 s, activa `FAIL_SAFE_SIN_DATOS` y deja trazabilidad en logs.
- Rechaza JSON inválido o incompleto sin colapsar el proceso.
- Detecta y marca lecturas antiguas (`edad_dato > 10 s`).
- Mantiene bucle de muestreo continuo con manejo robusto de excepciones.
