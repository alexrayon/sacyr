# Auditoria Senior - Robustez del Monitor de Viento

- Fecha: 2026-04-14
- Alcance: Evaluacion tecnica de robustez para despliegue en obra real

## 1) Analisis de datos congelados (ultima_actualizacion)

### Hallazgo
El monitor actual valida estructura y tipo del viento, pero no valida frescura temporal de la lectura. Esto permite un falso estado operativo si el dato llega con retraso excesivo.

### Riesgo operacional
Si la API entrega un valor antiguo (por ejemplo, de hace 40 minutos), el sistema podria autorizar maniobras con una condicion meteorologica ya no vigente.

### Requisito recomendado
Declarar lectura invalida cuando `ahora_utc - ultima_actualizacion_utc > 30 minutos`.

### Modificacion propuesta de codigo
1. Parsear `ultima_actualizacion` a `datetime` con zona horaria.
2. Calcular antiguedad en minutos.
3. Si excede 30 min, elevar alerta de sistema:
   - `ALERTA DE SISTEMA: DATOS CONGELADOS (>30 min)`.
4. Tratar esta condicion al mismo nivel de criticidad que timeout de red.

### Criterios de aceptacion
- Caso A: lectura con 10 minutos de antiguedad -> valida.
- Caso B: lectura con 31 minutos de antiguedad -> alerta de sistema.
- Caso C: fecha invalida/no parseable -> alerta de sistema por contrato temporal invalido.

## 2) Auditoria de frecuencia (consulta cada 10 segundos)

### Evaluacion tecnica
Consultar cada 10 segundos por grua equivale a 6 peticiones/minuto por grua. Para 100 gruas, el total seria 600 peticiones/minuto, sin contar reintentos y picos sincronizados.

### Conclusion de riesgo
- En una sola grua: 10 segundos es razonable para seguridad.
- En despliegue multi-grua: existe riesgo real de rate limiting si no se aplica control de tasa.

### Controles obligatorios
1. Jitter inicial de 0 a 2 segundos por grua para desincronizar.
2. Limite de concurrencia con semaforo.
3. Backoff exponencial para 429/503.
4. Respeto de cabecera `Retry-After`.
5. Presupuesto maximo de peticiones por minuto por tenant/proyecto.

## 3) Actualizacion de planificacion para escalabilidad

Se incorpora una fase de escalabilidad asincrona en la planificacion tecnica:
- Implementacion con `asyncio`.
- Cliente HTTP asincrono (`aiohttp`).
- Orquestacion de 100 gruas en paralelo controlado.
- Mecanismos anti-rate-limit.
- Telemetria centralizada por grua y por proyecto.

Referencia de plan actualizado:
- Ver fase 7 en TAREAS_TECNICAS_MONITOR_VIENTO.md.

## 4) Certificacion final para despliegue en obra real

### Estado de certificacion
Certificacion condicionada (Apto con acciones previas obligatorias).

### Fortalezas verificadas
- Manejo robusto de errores de red y timeout.
- Contrato de parsing del atributo critico `data.viento_kmh`.
- Clasificacion operacional de alertas implementada.
- Bucle continuo de monitorizacion con frecuencia contractual.

### Brechas criticas antes de produccion
1. No existe control de datos congelados por `ultima_actualizacion`.
2. Falta estrategia activa de rate limiting para despliegue masivo.
3. Falta evidencia de prueba de carga para 100 gruas.

### Dictamen
El sistema esta bien encaminado para piloto controlado de una o pocas gruas. Para despliegue real multi-obra, debe completarse la validacion temporal de datos y la arquitectura asincrona con control de tasa.
