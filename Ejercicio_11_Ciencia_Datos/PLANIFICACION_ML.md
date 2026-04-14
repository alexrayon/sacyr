# Planificacion de Modelo ML - Prediccion de Fallo

## 1. Seleccion de Modelo

### Modelo propuesto
Se propone usar `RandomForestClassifier` para predecir la variable objetivo `fallo`.

### Justificacion frente a regresion lineal
`RandomForestClassifier` es mas adecuado que una regresion lineal para este caso por las siguientes razones:

- El problema es de clasificacion binaria (`fallo` en {0,1}), no de regresion continua.
- Captura relaciones no lineales entre sensores (por ejemplo, interacciones entre `temperatura` y `vibracion`) sin necesidad de definirlas manualmente.
- Es robusto ante ruido y outliers moderados en variables de entrada.
- Suele rendir bien con poca ingenieria de caracteristicas y ofrece importancias de variables para interpretacion tecnica.
- Permite optimizar el compromiso Recall/Precision ajustando umbral y parametros, alineado con el objetivo de negocio de priorizar Recall.

Una regresion lineal no modela adecuadamente probabilidades de clase para este escenario y puede generar predicciones fuera del rango esperado para decisiones binarias.

## 2. Diseño de Pipeline

Se define un pipeline reproducible con `StandardScaler` para normalizar datos de sensores y facilitar comparabilidad entre variables:

1. Carga de `sensores_maquinaria.csv`.
2. Limpieza segun especificacion:
   - eliminar filas con `temperatura > 150`
   - eliminar filas con `temperatura < -10`
3. Separacion de variables:
   - `X = [temperatura, vibracion, ...]`
   - `y = fallo`
4. Split de datos (80/20, estratificado).
5. Pipeline de entrenamiento:
   - `StandardScaler`
   - `RandomForestClassifier`
6. Entrenamiento y evaluacion en test.
7. Medicion principal: Recall para clase `fallo=1`.

### Nota tecnica
Los modelos basados en arboles no requieren escalado estricto, pero se mantiene `StandardScaler` dentro del pipeline para estandarizar el preprocesado y facilitar comparacion futura con otros algoritmos (por ejemplo, LogisticRegression o SVM) sin cambiar el flujo.

## 3. Validacion

La validacion inicial se realizara con particion hold-out:

- 80% del dataset para entrenamiento.
- 20% del dataset para prueba.
- Uso de `stratify=y` para conservar la proporcion de clase `fallo` entre train y test.
- Uso de `random_state` fijo para reproducibilidad.

### Resultado esperado de validacion
- Recall alto en la clase positiva (`fallo=1`).
- Seguimiento adicional de Precision y matriz de confusion para controlar el volumen de falsas alarmas.

## 4. Implementacion de referencia (scikit-learn)

```python
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import recall_score

# Split 80/20 estratificado
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.20, stratify=y, random_state=42
)

pipeline = Pipeline([
    ("scaler", StandardScaler()),
    ("model", RandomForestClassifier(
        n_estimators=300,
        random_state=42,
        class_weight="balanced"
    ))
])

pipeline.fit(X_train, y_train)
y_pred = pipeline.predict(X_test)
recall = recall_score(y_test, y_pred)
print({"recall_fallo": recall})
```
