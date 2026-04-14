# Tareas Tecnicas - Implementacion Monitor de Viento (Python)

## Objetivo
Implementar un monitor de viento robusto en Python, alineado con el contrato operativo:
- ALERTA ROJA si viento > 45 km/h.
- ALERTA AMBAR si 35-45 km/h.
- ALERTA DE SISTEMA si no hay respuesta de API en 5 segundos.
- Consulta periodica cada 10 segundos.

## Secuencia de implementacion

### Fase 1 - Setup tecnico

1. Verificar version de Python y entorno de ejecucion del proyecto.
- Accion: confirmar interprete activo y ruta de trabajo.
- Resultado esperado: entorno listo para ejecutar el monitor.

2. Instalar y fijar dependencia de red.
- Accion: instalar requests en el entorno del proyecto.
- Resultado esperado: import requests sin errores.

3. Definir constantes operativas en un unico bloque de configuracion.
- Accion: declarar constantes:
  - WIND_ALERT_RED_THRESHOLD = 45.0
  - WIND_ALERT_AMBER_THRESHOLD = 35.0
  - REQUEST_TIMEOUT_SECONDS = 5
  - POLL_INTERVAL_SECONDS = 10
- Resultado esperado: umbrales y temporizadores centralizados.

4. Preparar configuracion de endpoint y credenciales.
- Accion: leer WEATHER_API_BASE_URL y WEATHER_API_KEY desde variables de entorno.
- Resultado esperado: sin credenciales hardcodeadas en codigo.

## Fase 2 - Implementacion de conexion HTTP

5. Crear clase de cliente de API.
- Accion: implementar CraneWeatherClient con constructor para base_url, api_key y timeout.
- Resultado esperado: clase unica responsable de comunicacion HTTP.

6. Implementar metodo GET con timeout estricto.
- Accion: crear metodo fetch_weather_payload que ejecute requests.get(..., timeout=5).
- Resultado esperado: toda llamada a red usa timeout contractual.

7. Gestionar codigos HTTP no exitosos.
- Accion: invocar raise_for_status tras la respuesta.
- Resultado esperado: errores 4xx/5xx tratados como fallo de consulta.

8. Implementar manejo de excepciones de red.
- Accion: bloque try-except capturando requests.exceptions.RequestException.
- Resultado esperado: ante error de red, emitir estado ALERTA DE SISTEMA: DATOS NO DISPONIBLES.

## Fase 3 - Validacion de datos (parsing)

9. Parsear cuerpo JSON de forma segura.
- Accion: convertir respuesta a dict con response.json().
- Resultado esperado: payload accesible para validacion de contrato.

10. Verificar existencia de claves obligatorias.
- Accion: validar que existe data y data.viento_kmh.
- Resultado esperado: deteccion temprana de contrato roto.

11. Validar tipo de viento.
- Accion: convertir viento_kmh a float y manejar ValueError/TypeError.
- Resultado esperado: valor numerico usable para decision operativa.

12. Normalizar estructura de salida.
- Accion: retornar objeto/dict con viento_kmh, estacion_id y timestamp.
- Resultado esperado: formato estable para capa de decision.

## Fase 4 - Logica de decision operativa

13. Implementar evaluador de severidad.
- Accion: crear funcion evaluar_alerta(viento_kmh).
- Resultado esperado:
  - viento > 45 -> ALERTA ROJA
  - 35 <= viento <= 45 -> ALERTA AMBAR
  - viento < 35 -> ESTADO NORMAL

14. Definir mensajes operativos por nivel.
- Accion: mapear severidad a mensaje y accion recomendada.
- Resultado esperado: salida clara para operador y trazabilidad.

15. Incorporar salida para fallo de disponibilidad.
- Accion: cuando falle la red, devolver ALERTA DE SISTEMA.
- Resultado esperado: no confundir fallo de red con estado normal.

## Fase 5 - Bucle de monitoreo continuo

16. Crear funcion principal de monitorizacion.
- Accion: implementar run_monitor() como orquestador.
- Resultado esperado: punto unico de ejecucion del sistema.

17. Implementar ciclo infinito de consulta.
- Accion: usar while True para iteracion continua.
- Resultado esperado: monitoreo permanente.

18. Limpiar consola en cada ciclo.
- Accion: ejecutar limpieza multiplataforma en cada iteracion antes de imprimir estado.
- Resultado esperado: vista actualizada y legible para operacion en tiempo real.

19. Ejecutar consulta, evaluacion y presentacion por iteracion.
- Accion: en cada vuelta:
  - pedir datos al cliente
  - evaluar severidad
  - imprimir estado
- Resultado esperado: pipeline completo por ciclo.

20. Respetar muestreo cada 10 segundos.
- Accion: usar sleep(POLL_INTERVAL_SECONDS) al final de cada iteracion.
- Resultado esperado: frecuencia contractual estable.

## Fase 6 - Endurecimiento minimo y pruebas

21. Registrar eventos con timestamp.
- Accion: incluir marca temporal en salidas de consola/log.
- Resultado esperado: trazabilidad de decisiones y fallos.

22. Probar escenario de alerta roja.
- Accion: inyectar o simular viento_kmh > 45.
- Resultado esperado: emision de ALERTA ROJA.

23. Probar escenario de alerta ambar.
- Accion: simular 35-45 km/h.
- Resultado esperado: emision de ALERTA AMBAR.

24. Probar escenario normal.
- Accion: simular viento_kmh < 35.
- Resultado esperado: estado normal correcto.

25. Probar timeout y fallo de red.
- Accion: simular endpoint no disponible o timeout > 5 s.
- Resultado esperado: ALERTA DE SISTEMA: DATOS NO DISPONIBLES.

## Fase 7 - Escalabilidad asincrona para 100 gruas

26. Diseñar modelo de concurrencia asincrona.
- Accion: adoptar `asyncio` con una tarea por grua y un orquestador central.
- Resultado esperado: monitorizacion simultanea de 100 gruas sin 100 hilos bloqueantes.

27. Migrar cliente HTTP a modo no bloqueante.
- Accion: sustituir flujo bloqueante por `aiohttp.ClientSession` con timeout por peticion.
- Resultado esperado: mejor uso de CPU y latencia controlada en alta concurrencia.

28. Definir identificador unico de grua en configuracion.
- Accion: incluir `crane_id`, `project_id` y `endpoint` por unidad monitorizada.
- Resultado esperado: trazabilidad internacional por obra y por grua.

29. Incorporar control de tasa de peticiones (rate limiting).
- Accion: aplicar semaforo asincrono y desfase inicial (jitter) para evitar picos sincronizados.
- Resultado esperado: menor riesgo de bloqueo por exceso de peticiones.

30. Implementar backoff exponencial ante 429/503.
- Accion: ante respuesta de limite superado, esperar con backoff y respetar `Retry-After`.
- Resultado esperado: recuperacion ordenada sin sobrecargar la API.

31. Mantener SLA de seguridad en escenario degradado.
- Accion: si una grua queda sin datos por timeout reiterado, emitir alerta de sistema por esa grua.
- Resultado esperado: aislamiento de fallos sin afectar el resto de gruas.

32. Consolidar telemetria y panel operacional.
- Accion: enviar estado por grua a logs estructurados/metricas (latencia, errores, alertas).
- Resultado esperado: observabilidad central para operaciones globales.

33. Pruebas de carga de 100 gruas.
- Accion: ejecutar prueba controlada con simulacion de 100 endpoints y medir tasa de error.
- Resultado esperado: evidencia cuantitativa para paso a produccion.

## Checklist de cierre tecnico

- requests instalado y operativo.
- Umbrales y tiempos definidos como constantes.
- GET con timeout estricto de 5 s.
- Parsing con validacion de data.viento_kmh.
- Evaluador de severidad implementado y probado.
- while True con consulta cada 10 s y limpieza de consola.
- Manejo explicito de errores de red como alerta de sistema.
- Plan de escalabilidad asincrona definido para 100 gruas con control de tasa.
