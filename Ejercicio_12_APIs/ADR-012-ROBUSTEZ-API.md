# ADR-012: Robustez de Integracion API en Dominio de Seguridad

- Estado: Aprobado
- Fecha: 2026-04-14
- Ambito: Ejercicio 12 - Monitorizacion de viento para gruas

## Contexto
El sistema de seguridad operacional depende de lecturas de viento obtenidas por API. El contrato funcional establece umbrales criticos para parada de maniobra y, adicionalmente, exige una respuesta explicita cuando no hay datos disponibles por fallo de red (timeout de 5 segundos).

En un entorno industrial, la ausencia de dato no equivale a ausencia de riesgo. Por ello, los errores de comunicacion deben tratarse como eventos de seguridad y no como errores tecnicos secundarios.

## Decision
Se decide que el manejo de errores de red sea una responsabilidad critica dentro del dominio de seguridad operacional, con las siguientes medidas:

1. Encapsular todas las llamadas HTTP en `CraneWeatherClient`.
2. Capturar `requests.exceptions.RequestException` como entrada unica de fallos de red.
3. Transformar el fallo tecnico en evento de dominio:
   - `ALERTA DE SISTEMA: DATOS NO DISPONIBLES`.
4. Mantener el ciclo de monitorizacion cada 10 segundos, incluso tras un fallo.
5. Aplicar timeout contractual maximo de 5 segundos por consulta.

## Justificacion
- Seguridad primero: sin telemetria valida no existe base confiable para permitir maniobras.
- Coherencia de negocio: el contrato explicita una alerta formal ante indisponibilidad.
- Reduccion de ambiguedad: tratar la indisponibilidad como estado de seguridad evita decisiones operativas inseguras.
- Mantenibilidad: centralizar la resiliencia en el cliente HTTP simplifica pruebas y auditorias.
- Auditabilidad: convierte incidentes de red en eventos trazables y medibles.

## Consecuencias
### Positivas
- Comportamiento determinista ante timeout y errores de conectividad.
- Menor riesgo de falsa sensacion de normalidad cuando faltan datos.
- Facil cobertura de pruebas de resiliencia (timeout, DNS, 5xx, SSL).

### Costes y trade-offs
- Mayor volumen de alertas en entornos con red inestable.
- Necesidad de observabilidad y gobierno de alertas para evitar fatiga operativa.

## Alternativas consideradas
1. Ignorar errores de red y continuar con ultimo valor conocido.
   - Rechazada: puede ocultar cambios rapidos de viento y degradar seguridad.
2. Reintentos continuos hasta obtener respuesta antes de decidir.
   - Rechazada: puede superar la ventana temporal util de decision operacional.
3. Delegar manejo de errores solo a infraestructura.
   - Rechazada: el impacto es de negocio/seguridad, no solo tecnico.

## Implementacion derivada
- Cliente HTTP resiliente con `try-except RequestException`.
- Mapeo contractual `data.viento_kmh` con validacion de tipo numerico.
- Emision de alerta de sistema en fallo de disponibilidad.
- Parametrizacion segura mediante variables de entorno (`.env` en desarrollo).
