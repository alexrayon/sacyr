# ADR-011: Uso de Python + scikit-learn para prototipado de deteccion de fallo

- Estado: Aprobado
- Fecha: 2026-04-14
- Decisores: Equipo de Data Science / Arquitectura ML
- Ambito: Ejercicio_11_Ciencia_Datos

## Contexto
Se necesita construir rapidamente un prototipo de clasificacion para predecir `fallo` a partir de sensores de maquinaria (temperatura, vibracion, etc.) en entorno GitHub Codespaces.

Requisitos clave:
- Iteracion rapida de limpieza, entrenamiento y evaluacion.
- Reproducibilidad tecnica del experimento.
- Facilidad de mantenimiento por un equipo mixto de datos y software.
- Alineacion con metrica principal de negocio: Recall para detectar fallos reales.

## Decision
Adoptar Python como lenguaje de implementacion y scikit-learn como framework principal de machine learning para el prototipo inicial.

Adicionalmente, se estandariza una arquitectura de pipeline con:
- `StandardScaler` para normalizacion de variables de sensores.
- `RandomForestClassifier` como modelo de clasificacion base.
- `train_test_split(test_size=0.20, stratify=y, random_state=42)` para validacion hold-out 80/20 reproducible.

## Justificacion
Python + scikit-learn se eligen por su rapidez de prototipado en Codespaces:

- Setup rapido del entorno y ecosistema maduro para analisis de datos.
- API sencilla y consistente para pipelines, validacion y metricas.
- Biblioteca estable para pasar de PoC a version productiva con bajo coste de cambio.
- Integracion natural con notebooks, scripts y CI en repositorios GitHub.
- Curva de aprendizaje baja para equipos tecnicos multidisciplinares.

## Consecuencias
Consecuencias positivas:
- Menor tiempo de puesta en marcha del modelo inicial.
- Mejor trazabilidad de experimentos y decisiones de preprocesado.
- Facilidad para comparar modelos alternativos reutilizando el mismo pipeline.

Riesgos y mitigaciones:
- Riesgo: sobreajuste del Random Forest.
  - Mitigacion: validacion cruzada posterior y ajuste de hiperparametros.
- Riesgo: dependencia de librerias Python.
  - Mitigacion: versionado de dependencias y ejecucion reproducible en Codespaces.
- Riesgo: uso de `StandardScaler` no imprescindible para arboles.
  - Mitigacion: se mantiene por consistencia de pipeline y compatibilidad con futuros modelos.

## Alternativas consideradas
1. Regresion lineal
- Descartada por no ser el enfoque correcto para clasificacion binaria de fallo.
- Menor capacidad para capturar no linealidades e interacciones entre sensores.

2. Desarrollo custom sin scikit-learn
- Descartado por mayor tiempo de implementacion y mas riesgo tecnico.

3. Frameworks de deep learning (TensorFlow/PyTorch) en esta fase
- Descartados para la fase inicial por complejidad innecesaria respecto al tamano y objetivo del problema.

## Criterios de validacion de la decision
- Entrenamiento exitoso en Codespaces con pipeline reproducible.
- Evaluacion con split 80/20 y reporte de Recall en `fallo=1`.
- Capacidad de iterar hiperparametros y comparar resultados sin rediseñar arquitectura.
