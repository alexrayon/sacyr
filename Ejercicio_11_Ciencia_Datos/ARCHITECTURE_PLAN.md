# ARCHITECTURE_PLAN.md

## 1. Resumen de Arquitectura

Se propone una arquitectura de mantenimiento predictivo batch-first con capacidad de inferencia online, desacoplada por puertos e implementaciones inyectables.

Objetivos de diseño:
- Robustez operativa en entorno industrial.
- Explicabilidad para mecánicos y jefes de obra.
- Escalabilidad a nuevas máquinas/sensores sin reescribir lógica de negocio.

## 2. Flujo Lógico de Datos

Diagrama de flujo lógico (alto nivel):

Ingesta CSV
  -> Validación de esquema y calidad
  -> Limpieza y enriquecimiento
  -> División Train/Validation/Test temporal
  -> Pipeline Scikit-Learn
     - StandardScaler
     - RandomForestClassifier
  -> Evaluación (Recall, F2, Precision, matriz de confusión)
  -> Registro de artefactos (modelo, métricas, versión de datos)
  -> Servicio evaluar_riesgo()
  -> Alertas de mantenimiento (BAJO/MEDIO/ALTO)

Diagrama de despliegue lógico:

Fuentes de telemetría/CSV
  -> Módulo de Ingesta
  -> Módulo de Calidad de Datos
  -> Módulo de Entrenamiento
  -> Registro de Modelos
  -> Módulo de Inferencia
  -> Panel Operativo / Sistema de Órdenes de Trabajo

## 3. Diseño por Capas y DI

### 3.1 Componentes principales

1. Domain Layer
   - Entidades: SensorRecord, RiesgoMantenimiento, ResultadoEvaluacion.
   - Reglas: umbrales operativos y política de decisión.

2. Application Layer
   - Casos de uso: entrenar_modelo, evaluar_modelo, evaluar_riesgo.
   - Puertos (interfaces): IDataIngestionPort, IModelRepositoryPort, IAlertPublisherPort.

3. Infrastructure Layer
   - Adaptadores CSV/Pandas.
   - Implementación Scikit-Learn Pipeline.
   - Persistencia de modelo y métricas (fichero o almacenamiento remoto).

4. Delivery Layer
   - Script/CLI y futura API de scoring.

### 3.2 Inyección de Dependencias obligatoria

- Los casos de uso reciben interfaces, no implementaciones concretas.
- Sustitución transparente de fuente de datos (CSV hoy, streaming mañana).
- Sustitución transparente de repositorio de modelos (local, nube, registry corporativo).

## 4. Pipeline de Scikit-Learn (Diseño Técnico)

Pipeline obligatorio:

1. Selección de features numéricas:
   - temperatura_motor
   - vibracion_eje
2. Escalado:
   - StandardScaler para normalizar magnitud y facilitar comparabilidad entre señales.
3. Modelo:
   - RandomForestClassifier con class_weight balance o balance_subsample.
4. Salida:
   - predict_proba para alimentar umbrales de riesgo.

Parámetros iniciales recomendados (ajustables por validación):
- n_estimators: 300
- max_depth: 8-14 (tuning)
- min_samples_leaf: 3-10
- random_state fijo para reproducibilidad

## 5. Estrategia de Evaluación y Umbrales

### 5.1 Evaluación offline

Métricas objetivo:
- Recall_fallo >= 0.90 (objetivo inicial)
- F2-score maximizado
- Precision_fallo controlado para evitar saturación operativa

Validación:
- Corte temporal (evitar leakage).
- Reportes por tipo de máquina.

### 5.2 Política de umbrales para probabilidad

Riesgo sugerido:
- BAJO: p < 0.35
- MEDIO: 0.35 <= p < 0.65
- ALTO: p >= 0.65

Acciones asociadas:
- BAJO: continuar operación y monitoreo normal.
- MEDIO: inspección programada en próxima ventana.
- ALTO: inspección inmediata y posible parada preventiva.

## 6. Resiliencia y Gestión de Errores

1. Timeouts
   - Ingesta y lectura de fuentes con timeout configurable.

2. Retry
   - Reintentos exponenciales para accesos transitorios a almacenamiento remoto.

3. Circuit Breaker
   - Si falla repetidamente el repositorio de modelos o el canal de alertas, abrir circuito y activar modo degradado local.

4. Degradación controlada
   - Si no hay modelo actualizado, usar último modelo validado.
   - Si no hay canal de alertas, registrar eventos para replay.

5. Observabilidad
   - Logs estructurados, métricas de latencia de scoring, tasa de alertas y tasa de errores por componente.

## 7. ADRs

### ADR-011-001: Uso de RandomForestClassifier para Mantenimiento Predictivo

Estado: Aprobado

Contexto:
- Se requiere modelo robusto en datos tabulares de sensores industriales.
- Necesidad de explicabilidad para personal mecánico y supervisión de obra.
- Clase positiva (fallo) con importancia operativa prioritaria.

Decisión:
- Adoptar RandomForestClassifier dentro de un Pipeline con StandardScaler.

Justificación:
1. Buen desempeño en tabular con relaciones no lineales.
2. Menor sensibilidad a ruido y outliers moderados frente a modelos lineales simples.
3. Permite obtener Feature Importance global (temperatura y vibración), útil para explicar por qué sube el riesgo.
4. Facilita comunicación con mecánicos: se puede traducir a mensajes accionables (ejemplo: riesgo elevado principalmente por vibración).

Consecuencias:
- Positivas:
  - Alta interpretabilidad operativa.
  - Robustez inicial sin ingeniería excesiva.
- Negativas:
  - Importancia global no equivale a causalidad.
  - Puede requerir calibración de probabilidad para decisiones finas.

Alternativas consideradas:
- Regresión logística: más simple, pero menor capacidad no lineal.
- Gradient Boosting/XGBoost: potencialmente mayor rendimiento, pero mayor complejidad de ajuste e interpretación para primer despliegue.

## 8. Seguridad y Configuración

1. Variables de entorno para rutas, umbrales y configuraciones.
2. Prohibido hardcodear credenciales o parámetros sensibles.
3. Control de versiones de modelo, datos y configuración para auditoría.

## 9. Criterios de Aceptación de Arquitectura

1. Pipeline reproducible y versionado.
2. Dependencias desacopladas por interfaces.
3. Métricas centradas en Recall y validadas temporalmente.
4. Explicabilidad entregable al equipo mecánico.
5. Mecanismos de resiliencia definidos para fallos externos.
