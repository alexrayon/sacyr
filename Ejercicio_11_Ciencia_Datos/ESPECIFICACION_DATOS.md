# Especificación de Datos - Predicción de Fallo en Maquinaria

## 1. Objetivo
Desarrollar un modelo de clasificación supervisada para predecir la columna `fallo` a partir de variables de sensores industriales (principalmente temperatura y vibración) del dataset `sensores_maquinaria.csv`.

Contexto de negocio:
- `fallo = 1`: indica avería real o condición crítica de la máquina.
- `fallo = 0`: indica funcionamiento normal.

El propósito operativo es anticipar incidencias para habilitar mantenimiento preventivo y reducir paradas no planificadas, daños en activos y costes asociados.

## 2. Reglas de Limpieza de Datos
Para asegurar calidad de señal y evitar sesgos por lecturas físicamente inverosímiles, se aplicará la siguiente regla sobre la variable de temperatura:

- Cualquier registro con `temperatura > 150` °C se considerará error de lectura y se eliminará.
- Cualquier registro con `temperatura < -10` °C se considerará error de lectura y se eliminará.

Criterio técnico:
- El rango válido de trabajo para esta especificación será `-10 <= temperatura <= 150`.
- Los registros fuera de rango no se imputan; se excluyen del entrenamiento y validación para evitar contaminar el patrón de fallo.

## 3. Métrica de Éxito
La métrica principal será **Recall** (sensibilidad) para la clase positiva (`fallo = 1`).

Definición:
- Recall = TP / (TP + FN)
- Mide qué proporción de fallos reales detecta el modelo.

Justificación de negocio:
- En mantenimiento industrial es más costoso **no detectar un fallo real** (falso negativo) que disparar una alerta preventiva innecesaria (falso positivo).
- Un falso negativo puede provocar rotura de máquina, parada de producción, riesgos de seguridad y sobrecostes de reparación.
- Una falsa alarma suele traducirse en revisión adicional o intervención preventiva, coste asumible frente al impacto de ignorar una avería real.

Por tanto, se prioriza maximizar Recall aunque exista una reducción moderada en Precisión.

## 4. Hipótesis de Trabajo
Hipótesis principal:
- A mayor nivel de vibración, mayor probabilidad de avería (`fallo = 1`).

Racional técnico:
- Incrementos de vibración suelen estar asociados a desalineación, desgaste de rodamientos, holguras mecánicas o desequilibrios rotacionales.
- Estas condiciones son precursores habituales de degradación y fallo en equipos rotativos.

Implicación para modelado:
- Se espera una relación positiva entre la variable `vibracion` y la probabilidad estimada de `fallo`.
- Esta hipótesis se validará empíricamente en análisis exploratorio y en la importancia de variables del modelo final.
