# SPECIFICATION.md

## 1. Resumen Ejecutivo

Se define la frontera técnica para un sistema de mantenimiento predictivo orientado a maquinaria pesada de Sacyr (TBM y excavadoras), usando telemetría de sensores para anticipar averías mecánicas.

Objetivo operativo: priorizar la detección temprana de fallos para reducir roturas no planificadas, riesgos de seguridad en obra y tiempos de inactividad.

## 2. Contrato de Datos

### 2.1 Entrada

Fuente principal:
- Archivo CSV: sensores_maquinaria.csv

Esquema esperado:

| Campo | Tipo lógico | Tipo físico recomendado | Obligatorio | Regla |
|---|---|---|---|---|
| timestamp | datetime | datetime64[ns] | Sí | UTC normalizado, granularidad horaria |
| maquina_id | categórico | string | Sí | Catálogo válido de maquinaria activa |
| temperatura_motor | numérico continuo | float64 | Sí | Unidad: grados Celsius |
| vibracion_eje | numérico continuo | float64 | Sí | Unidad: mm/s RMS |
| fallo | binario | int8 | Sí (entrenamiento) | 0=no fallo, 1=fallo |

Formato de intercambio:
- Ingesta batch en CSV delimitado por coma.
- Serialización interna para inferencia: JSON con las dos señales y metadatos de máquina/instante.

### 2.2 Salida

Salida del modelo para inferencia:

| Campo | Tipo | Descripción |
|---|---|---|
| prob_fallo | float | Probabilidad de fallo en ventana operativa definida |
| nivel_riesgo | string | BAJO, MEDIO o ALTO |
| recomendacion | string | Acción sugerida para mantenimiento |
| principales_factores | lista de pares | Variables con mayor contribución al riesgo (feature importance global + trazas locales simplificadas) |

Contrato de la función operativa objetivo:
- evaluar_riesgo(registro_sensor) -> objeto de riesgo con probabilidad, nivel y recomendación.

## 3. Criterios de Limpieza para Sensores Industriales

### 3.1 Calidad estructural

1. Validar columnas obligatorias y tipos esperados.
2. Convertir timestamp a datetime y ordenar cronológicamente.
3. Eliminar duplicados exactos (timestamp, maquina_id).
4. Detectar huecos temporales por máquina (intervalos > 1 hora) y marcar bandera de continuidad.

### 3.2 Calidad semántica

1. Nulos:
   - Si falta fallo en dataset de entrenamiento, excluir fila.
   - Si faltan sensores, imputar solo si la ventana es corta y documentada; si no, excluir.
2. Rango físico de plausibilidad:
   - temperatura_motor: fuera de [20, 130] °C se marca como outlier físico.
   - vibracion_eje: fuera de [0, 10] mm/s se marca como outlier físico.
3. Outliers estadísticos no físicos:
   - Aplicar control robusto (IQR o percentiles 0.5-99.5 por máquina) sin borrar automáticamente eventos potencialmente críticos.
4. Consistencia operacional:
   - Valores planos prolongados (sensor congelado) se etiquetan como posible fallo de sensado.
   - Saltos abruptos no compatibles con dinámica mecánica se registran para revisión.

### 3.3 Reglas de trazabilidad y auditoría

1. Cada transformación debe registrar conteos antes/después.
2. Mantener dataset limpio y dataset descartado con motivos de descarte.
3. Versionar esquema y reglas de limpieza para reproducibilidad.

## 4. Regla de Dependencias (Arquitectura Limpia)

Capas y permisos de importación:

1. Dominio (entidades y reglas): no importa de ninguna otra capa.
2. Aplicación (casos de uso): importa solo Dominio y puertos (interfaces).
3. Infraestructura de datos (CSV, almacenamiento, serialización): implementa puertos de Aplicación.
4. Orquestación/Entrega (CLI, API, jobs): depende de Aplicación e Infraestructura.

Restricción anti-acoplamiento:
- Prohibido que servicios de negocio importen directamente implementaciones concretas de acceso a datos.
- Obligatoria la Inyección de Dependencias para repositorios, cargadores y publicadores de alertas.

## 5. Límites Físicos y de Negocio en Obra

### 5.1 Umbrales de seguridad operativa (iniciales, calibrables)

- Temperatura de alerta preventiva: >= 90 °C.
- Temperatura crítica: >= 100 °C.
- Vibración preventiva: >= 4.0 mm/s.
- Vibración crítica: >= 4.8 mm/s.

### 5.2 Impacto de negocio

- Coste de falso negativo (no detectar avería): alto.
  Consecuencias: rotura, parada no planificada, potencial incidente de seguridad, retraso contractual.
- Coste de falso positivo (revisión innecesaria): medio-bajo.
  Consecuencias: inspección preventiva y coste de mano de obra.

## 6. Métrica de Éxito: Recall sobre Accuracy

Prioridad: maximizar Recall de la clase fallo.

Justificación técnica y económica:

1. En mantenimiento predictivo de obra, un falso negativo tiene coste desproporcionadamente mayor que un falso positivo.
2. Accuracy puede ser engañosa con clases desbalanceadas (muchas horas sin fallo); un modelo con alta accuracy podría ignorar fallos.
3. Recall alto garantiza capturar el mayor número posible de averías reales, reduciendo eventos críticos.
4. La política operativa admite más inspecciones preventivas si con ello disminuyen roturas severas.

Métricas complementarias obligatorias:
- Precision de fallo (control de sobrealerta).
- F2-score (pondera Recall por encima de Precision).
- Matriz de confusión por tipo de máquina.

## 7. Criterios de Aceptación de Especificación

1. Contrato de datos definido y validable automáticamente.
2. Reglas de limpieza trazables y reproducibles.
3. Umbrales iniciales documentados y revisables por ingeniería de mantenimiento.
4. Criterio de éxito centrado en Recall, con evidencia de coste-riesgo.
