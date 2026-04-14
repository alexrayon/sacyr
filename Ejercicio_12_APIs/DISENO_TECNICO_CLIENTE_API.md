# Diseno Tecnico - Cliente de Viento para Gruas

## Objetivo
Definir la arquitectura del cliente Python para consultar viento en una API meteorologica y aplicar las reglas de seguridad de `CONTRATO_API.md`.

## 1. Estructura de la clase CraneWeatherClient

### Responsabilidad principal
Encapsular toda la comunicacion HTTP y la normalizacion de la respuesta para que la logica de seguridad opere sobre datos confiables.

### Responsabilidades concretas
- Construir peticiones GET a la API de viento.
- Aplicar timeout contractual de 5 segundos.
- Validar estructura JSON (`data.viento_kmh`).
- Retornar un DTO interno con:
  - `wind_kmh` (float)
  - `station_id` (str)
  - `updated_at` (str)
- Propagar errores de red y de contrato como excepciones de dominio.

### Interfaz propuesta
```python
class CraneWeatherClient:
    def __init__(self, base_url: str, api_key: str, timeout_seconds: int = 5):
        ...

    def fetch_wind_data(self) -> dict:
        """Obtiene y valida la respuesta del endpoint de viento."""
        ...
```

### Flujo interno recomendado
1. Construir URL y cabeceras (incluyendo autenticacion).
2. Ejecutar `requests.get(..., timeout=5)`.
3. Ejecutar `raise_for_status()` para errores HTTP.
4. Parsear JSON y validar `data.viento_kmh` numerico.
5. Retornar estructura normalizada.

## 2. Estrategia de resiliencia de red

### Bloque try-except
Toda llamada HTTP debe encapsularse en un bloque:

```python
try:
    response = requests.get(url, headers=headers, timeout=5)
    response.raise_for_status()
    payload = response.json()
except requests.exceptions.RequestException as exc:
    # timeout, DNS, conexion, SSL, HTTPError, etc.
    raise WeatherDataUnavailableError(
        "ALERTA DE SISTEMA: DATOS NO DISPONIBLES"
    ) from exc
```

### Motivo tecnico
`requests.exceptions.RequestException` es la jerarquia base de errores de red en `requests`, por lo que permite unificar timeout, caidas de red, errores TLS y respuestas HTTP invalidas en una sola via de manejo de fallo operacional.

### Comportamiento esperado ante error
- Emitir evento de seguridad: `ALERTA DE SISTEMA: DATOS NO DISPONIBLES`.
- Registrar metadatos: timestamp, endpoint, tipo de error y mensaje tecnico.
- No detener el proceso de monitorizacion: mantener ciclo de consulta cada 10 s.

## 3. Separacion de responsabilidades

### Capa Cliente API
- `CraneWeatherClient`
- Solo I/O de red, parseo y validacion de contrato.

### Capa de Dominio de Seguridad
- `SafetyAlertService` (o equivalente)
- Evalua umbrales:
  - `> 45`: ALERTA ROJA
  - `35-45`: ALERTA AMBAR
  - `< 35`: ESTADO NORMAL

### Capa de Orquestacion
- `MonitoringLoop`
- Ejecuta sondeo cada 10 segundos y coordina cliente + dominio + logging.

## 4. Configuracion con .env (seguridad de secretos)

### Principios
- No hardcodear credenciales en codigo fuente.
- No commitear archivos `.env` con secretos reales.
- Versionar solo `.env.example` con placeholders.

### Variables recomendadas
- `WEATHER_API_BASE_URL`
- `WEATHER_API_KEY`
- `WEATHER_TIMEOUT_SECONDS=5`
- `WEATHER_POLL_INTERVAL_SECONDS=10`

### Carga en Python
- Usar `python-dotenv` en desarrollo para cargar `.env`.
- En produccion, inyectar variables desde el entorno (no depender de archivo local).

## 5. Criterios de diseno para produccion

- Retries: no aplicar retry agresivo en la misma ventana de 5 s para no ocultar latencia critica.
- Observabilidad: logs estructurados y contadores de fallos de red consecutivos.
- Trazabilidad: registrar correlacion entre lectura de viento y decision de alerta.
- Testabilidad: mock de `requests.get` para simular timeout y HTTPError.
