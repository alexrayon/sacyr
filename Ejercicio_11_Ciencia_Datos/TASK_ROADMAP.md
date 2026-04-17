# TASK_ROADMAP.md

## 1. Resumen de Ejecución

Roadmap secuencial para @sacyr-developer, desde ingesta de sensores_maquinaria.csv hasta la función evaluar_riesgo() con umbrales de probabilidad y plan de detección de Data Drift.

Orden de implementación obligatorio:
1. Modelos y contratos
2. Datos e ingesta
3. Lógica de entrenamiento y evaluación
4. Orquestación de inferencia y alertas
5. MLOps y monitoreo

## 2. Backlog Técnico Atómico

### Bloque A. Fundaciones de Dominio

Tarea A1: Definir entidades de dominio
- Alcance: SensorRecord, ResultadoModelo, RiesgoMantenimiento.
- DoD: tipos explícitos, sin dependencias de infraestructura.

Tarea A2: Definir puertos/interfaces
- Alcance: IDataLoader, IModelStore, IAlertDispatcher.
- DoD: interfaces en capa aplicación, sin imports circulares.

Tarea A3: Definir configuración central
- Alcance: umbrales de probabilidad y límites físicos por variable.
- DoD: carga desde configuración/entorno; cero hardcodeo de secretos.

### Bloque B. Ingesta y Calidad de Datos

Tarea B1: Implementar lector de CSV
- Alcance: cargar sensores_maquinaria.csv con parseo de fechas.
- DoD: lectura reproducible y validación de columnas obligatorias.

Tarea B2: Validar esquema y tipos
- Alcance: comprobar dominio de fallo binario y tipos numéricos.
- DoD: reporte de errores de esquema con conteos.

Tarea B3: Implementar limpieza industrial
- Alcance: nulos, duplicados, outliers físicos, huecos temporales.
- DoD: archivo de auditoría con filas retenidas/descartadas y motivo.

Tarea B4: Partición temporal del dataset
- Alcance: train/validation/test por tiempo, sin leakage.
- DoD: pruebas que verifiquen no solapamiento temporal.

### Bloque C. Entrenamiento y Evaluación

Tarea C1: Construir Pipeline Scikit-Learn
- Alcance: StandardScaler + RandomForestClassifier.
- DoD: pipeline serializable y reproducible con random_state fijo.

Tarea C2: Entrenar modelo base
- Alcance: entrenamiento inicial con class_weight balance.
- DoD: modelo entrenado y persistido con metadatos de versión.

Tarea C3: Evaluar métricas de negocio
- Alcance: Recall, Precision, F2, matriz de confusión.
- DoD: informe por tipo de máquina y objetivo de Recall documentado.

Tarea C4: Ajuste de hiperparámetros
- Alcance: búsqueda limitada para mejorar Recall sin explosión de falsos positivos.
- DoD: tabla comparativa de experimentos y selección justificada.

Tarea C5: Extraer Feature Importance
- Alcance: ranking de variables del RandomForest.
- DoD: reporte legible para mecánicos con interpretación operativa.

### Bloque D. Inferencia y Decisión Operativa

Tarea D1: Implementar caso de uso evaluar_riesgo()
- Alcance: entrada de registro sensor, salida con prob_fallo, nivel_riesgo y recomendación.
- DoD: cobertura de tests para BAJO/MEDIO/ALTO.

Tarea D2: Definir umbrales de probabilidad
- Alcance: BAJO < 0.35, MEDIO [0.35, 0.65), ALTO >= 0.65.
- DoD: umbrales parametrizables vía configuración.

Tarea D3: Implementar publicador de alertas
- Alcance: generar evento accionable para mantenimiento.
- DoD: contratos de salida validados y trazabilidad por máquina/timestamp.

Tarea D4: Modo degradado
- Alcance: fallback a último modelo válido si falla carga del modelo vigente.
- DoD: test de resiliencia con error simulado en repositorio.

### Bloque E. MLOps y Data Drift

Tarea E1: Definir baseline estadístico
- Alcance: distribución inicial de temperatura y vibración (media, desviación, percentiles).
- DoD: baseline versionado por periodo y tipo de máquina.

Tarea E2: Implementar monitor de Data Drift
- Alcance: comparar ventanas recientes vs baseline con PSI/KS test.
- DoD: alerta de drift cuando PSI > 0.2 o p-valor KS bajo umbral definido.

Tarea E3: Detectar descalibración de sensores
- Alcance: reglas de valores planos, offsets persistentes y varianza anómala.
- DoD: clasificación de evento como posible drift de datos vs posible fallo mecánico.

Tarea E4: Política de respuesta al drift
- Alcance: acciones por severidad (recalibrar sensor, reentrenar modelo, bloquear despliegue).
- DoD: runbook operativo aprobado por mantenimiento.

Tarea E5: Cadencia de reentrenamiento
- Alcance: reentrenamiento periódico o por evento de drift severo.
- DoD: pipeline de reentrenamiento con validación mínima de Recall antes de promoción.

### Bloque F. QA, Seguridad y Entrega

Tarea F1: Pruebas unitarias y de integración
- Alcance: limpieza, entrenamiento, evaluar_riesgo, drift.
- DoD: cobertura mínima acordada y sin fallos críticos.

Tarea F2: Controles de seguridad y configuración
- Alcance: revisar ausencia de secretos hardcodeados.
- DoD: checklist de seguridad aprobado.

Tarea F3: Documentación técnica final
- Alcance: guía operativa, ADR, contratos y procedimiento de soporte.
- DoD: documentación publicada y trazable a versión de modelo.

## 3. Definición de Hecho Global (DoD Global)

1. Sin imports circulares ni acoplamiento indebido entre capas.
2. Cumplimiento de estilo y calidad estática del lenguaje usado.
3. Reproducibilidad de entrenamiento con versión de datos y semillas.
4. Recall de fallo validado como métrica principal de aceptación.
5. Mecanismo activo de detección de drift y protocolo de respuesta.

## 4. Hitos de Entrega

Hito 1: Datos confiables
- Completado al cerrar Bloques A y B.

Hito 2: Modelo validado
- Completado al cerrar Bloque C con reporte de métricas.

Hito 3: Riesgo operacional en producción controlada
- Completado al cerrar Bloque D.

Hito 4: MLOps operativo
- Completado al cerrar Bloques E y F.
