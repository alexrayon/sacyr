# ADR-012: Gestión de API Keys por Variables de Entorno en Monitor Climático

## Estado
Propuesto

## Contexto
El monitor de seguridad climática consume un proveedor externo de meteorología para tomar decisiones críticas de operación (`PARADA_INMEDIATA` ante viento > 45 km/h). El uso de credenciales embebidas en código fuente expone riesgos de seguridad:
- fuga de secretos en repositorio,
- incumplimiento de auditorías de ciberseguridad,
- rotación compleja de claves,
- propagación accidental en logs y trazas.

## Decisión
Adoptar gestión de secretos exclusivamente mediante variables de entorno, con soporte de archivo `.env` en desarrollo local.

Reglas obligatorias:
- Prohibido hardcodear API Keys en cualquier archivo fuente.
- La clave se leerá desde `WEATHER_API_KEY`.
- El endpoint base se leerá desde `WEATHER_API_BASE_URL`.
- Si falta cualquiera de las variables críticas, el sistema no inicia en modo operativo y entra en fail-safe de configuración.
- `.env` solo para local/dev; nunca versionado.

## Implementación de referencia
- Librerías:
  - `python-dotenv` para cargar `.env` en entorno local.
  - `os.getenv` para resolución de variables.
- Archivo `.env.example` con placeholders no sensibles.
- Validación temprana (guard clauses) al arranque:
  - `WEATHER_API_KEY` requerido.
  - `WEATHER_API_BASE_URL` requerido.
  - `WEATHER_TIMEOUT_SECONDS` por defecto `5`.

## Justificación
1. Seguridad operacional
   - Reduce superficie de exposición de credenciales.
   - Permite revocación/rotación sin desplegar cambios de código.

2. Cumplimiento y auditoría
   - Se alinea con prácticas DevSecOps y controles de secretos.
   - Facilita demostrar ausencia de secretos en repositorio.

3. Escalabilidad y despliegue
   - Misma base de código para dev, test, pre y prod.
   - Configuración desacoplada del binario.

## Consecuencias
Positivas:
- Menor riesgo de filtrado de credenciales.
- Operación coherente en múltiples entornos.

Costes:
- Necesidad de gestionar secretos en plataforma de despliegue.
- Requiere documentación de bootstrap para el equipo.

## Controles complementarios
- Añadir patrones de detección de secretos en CI.
- Añadir `.env` a `.gitignore`.
- Sanitizar logs para no imprimir `WEATHER_API_KEY`.

## No permitido
- `API_KEY = "abc123"` en código.
- Incluir tokens en ficheros de configuración versionados.
- Exponer claves en mensajes de excepción o logs.
