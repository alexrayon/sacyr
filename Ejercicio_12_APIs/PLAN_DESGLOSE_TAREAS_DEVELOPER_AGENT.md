# PLAN DE DESGLOSE PARA @developer-agent
## Implementación del Monitor de Seguridad Climática

## Objetivo de implementación
Construir un servicio Python resiliente que consulte una API de viento en tiempo real con `requests`, clasifique nivel de alerta y emita orden de parada inmediata en nivel rojo.

## Fase A: Estructura y Configuración
1. Crear módulo de configuración (`config.py`)
- Cargar variables de entorno (`WEATHER_API_BASE_URL`, `WEATHER_API_KEY`, `WEATHER_TIMEOUT_SECONDS`, `SAMPLING_INTERVAL_SECONDS`).
- Aplicar guard clauses de configuración al arranque.
- Fallar en modo seguro si faltan variables críticas.

2. Definir modelos de datos (`models.py`)
- `WeatherSample` (estación, viento, dirección, timestamp).
- `SafetyDecision` (nivel alerta, comando, flags, frescura).

## Fase B: Cliente HTTP Resiliente (requests)
3. Implementar cliente de API (`weather_client.py`)
- Usar `requests.Session` para reutilización de conexiones.
- Establecer timeout estricto de 5 segundos por petición.
- Añadir cabecera `X-API-Key` desde entorno.

4. Manejo de excepciones de conexión
- Capturar explícitamente:
  - `requests.exceptions.Timeout`
  - `requests.exceptions.ConnectionError`
  - `requests.exceptions.HTTPError`
  - `requests.exceptions.RequestException`
- Traducir a errores de dominio (`WeatherUnavailableError`, `WeatherContractError`) sin tumbar el proceso principal.

## Fase C: Validación de JSON y Contrato
5. Implementar validador de respuesta (`validators.py`)
- Comprobar presencia y tipo de `status`, `data`, `viento_kmh`, `ultima_actualizacion`.
- Rechazar valores fuera de rango físico.
- Normalizar y convertir tipos numéricos de forma segura.

6. Validar frescura de datos
- Calcular `edad_dato` contra UTC actual.
- Marcar `DATOS_CONGELADOS` si supera 10 segundos.
- Si persiste 2 ciclos, escalar a `FAIL_SAFE_SIN_DATOS`.

## Fase D: Motor de Decisión de Seguridad
7. Implementar clasificador de alertas (`safety_engine.py`)
- `<=35`: VERDE.
- `>35 y <=45`: AMBAR.
- `>45`: ROJO + `PARADA_INMEDIATA`.

8. Gestionar ráfagas fantasmales
- Detectar deltas abruptos entre muestras consecutivas.
- No suprimir parada en rojo; etiquetar flag `possible_ghost_gust`.
- Solicitar confirmación de segunda lectura para cerrar incidente.

## Fase E: Bucle de muestreo infinito con logs profesionales
9. Implementar runner (`monitor_runner.py`)
- Bucle `while True` con intervalo configurable.
- Bloque `try/except` en cada ciclo para evitar caída total.
- Estrategia fail-safe ante ausencia de datos.

10. Logging estructurado
- Formato JSON por línea con campos mínimos:
  - `timestamp_utc`, `level`, `event`, `station_id`, `wind_kmh`, `alert_level`, `command`, `correlation_id`.
- Eventos mínimos:
  - `weather_poll_started`
  - `weather_poll_success`
  - `weather_timeout_fail_safe`
  - `stale_weather_data`
  - `safety_command_emitted`
  - `monitor_cycle_error`

## Fase F: Pruebas automatizadas
11. Unit tests del motor de decisión
- Verificar umbrales exactos 35 y 45.
- Verificar `PARADA_INMEDIATA` para >45.

12. Tests de robustez del cliente
- Timeout en 5 segundos.
- JSON malformado o campos ausentes.
- Timestamp viejo (dato congelado).
- Error de red transitorio.

13. Test del bucle de continuidad
- Simular múltiples fallos consecutivos y verificar que el proceso sigue activo.

## Definición de terminado (DoD)
- Sin credenciales hardcodeadas.
- Timeout efectivo de 5 segundos validado por test.
- Clasificación Verde/Ámbar/Rojo validada.
- Emisión de `PARADA_INMEDIATA` en rojo validada.
- Modo fail-safe trazable en logs ante timeout/falta de datos.
- Cobertura mínima recomendada: 85% en módulos críticos (`weather_client`, `validators`, `safety_engine`).
