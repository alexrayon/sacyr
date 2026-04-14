# Contrato Técnico de Integración API

## Contexto
Este documento define el contrato técnico para un cliente Python de monitorización de viento orientado a seguridad industrial en grúas de Sacyr.

Fuente de referencia actual:
- Archivo: Base.py
- Estructura JSON principal:
  - status
  - data
    - estacion_id
    - viento_kmh
    - direccion
    - ultima_actualizacion

## 1. Mapeo de Atributos

### Atributo crítico de seguridad
- Nombre funcional: Velocidad de viento
- Ruta JSON exacta: data.viento_kmh
- Tipo de dato esperado: numérico (float preferente; int aceptado)
- Unidad: km/h

### Reglas de validación del dato
- El campo data debe existir.
- El campo viento_kmh debe existir dentro de data.
- El valor de viento_kmh debe ser convertible a número real.
- Si el valor no está presente o no es numérico, se considera dato inválido y debe tratarse como condición de fallo de disponibilidad operativa.

## 2. Lógica de Alerta Contractual

La evaluación de alertas se realiza sobre el valor de data.viento_kmh (km/h):

- Condición: viento_kmh > 45
  - Resultado: ALERTA ROJA
  - Acción operativa: parada inmediata de maniobras

- Condición: 35 <= viento_kmh <= 45
  - Resultado: ALERTA ÁMBAR
  - Acción operativa: operación restringida y vigilancia reforzada

- Condición: viento_kmh < 35
  - Resultado: ESTADO NORMAL
  - Acción operativa: operación permitida según procedimiento estándar

## 3. Protocolo de Fallo de Red

### Requisito de timeout
- Tiempo máximo de espera de respuesta API: 5 segundos.

### Comportamiento obligatorio al superar timeout o no obtener respuesta
- Evento: timeout de red o fallo de comunicación equivalente.
- Resultado contractual: ALERTA DE SISTEMA: DATOS NO DISPONIBLES.
- Acción recomendada: conmutar a modo seguro y notificar al operador/supervisión.

Nota: Esta alerta es de sistema (disponibilidad de datos), independiente del nivel de viento, y debe priorizar visibilidad inmediata en consola/log/telemetría.

## 4. Frecuencia de Muestreo

- Intervalo de consulta API: cada 10 segundos.
- Objetivo: balancear actualización de seguridad con carga del servidor.

### Consideraciones operativas
- El ciclo debe ser estable (polling periódico de 10 s).
- Si ocurre timeout, el siguiente intento mantiene el ciclo nominal.
- Se recomienda registrar timestamp de cada consulta para trazabilidad.

## 5. Reglas resumidas para implementación en Python

1. Consultar API cada 10 segundos.
2. Aplicar timeout de 5 segundos por petición.
3. Extraer data.viento_kmh y validar tipo numérico (float/int).
4. Clasificar alerta:
   - > 45: ALERTA ROJA
   - 35 a 45: ALERTA ÁMBAR
   - < 35: ESTADO NORMAL
5. Ante timeout o no respuesta: ALERTA DE SISTEMA: DATOS NO DISPONIBLES.

## 6. Criterios de aceptación

- Dado un JSON válido con data.viento_kmh=48.7, el sistema emite ALERTA ROJA.
- Dado un JSON válido con data.viento_kmh=40, el sistema emite ALERTA ÁMBAR.
- Dado un JSON válido con data.viento_kmh=20, el sistema emite ESTADO NORMAL.
- Si la API no responde en más de 5 segundos, el sistema emite ALERTA DE SISTEMA: DATOS NO DISPONIBLES.
- El cliente ejecuta consultas periódicas con un intervalo de 10 segundos.
