# Auditoría de Calidad de Software

## Objeto del informe
Análisis técnico del método legacy `CalculateRiskMargin` del proyecto `Ejercicio_4_Evaluacion_Calidad/Ejercicio4/Program.cs`. El foco está en la calidad de código, métricas de mantenibilidad, hotspots de anidación y riesgos de negocio asociados al cálculo de márgenes de riesgo financieros.

## 1. Análisis de métricas

### Complejidad ciclomática de McCabe
- Método `CalculateRiskMargin`: 12

Justificación:
- 1 punto base por el método.
- 11 estructuras de decisión (`if`, `else if`) en el flujo actual.

Interpretación:
- Un valor de 12 indica que existen 12 rutas de ejecución distintas.
- Para código financiero crítico, este nivel es demasiado alto: cada ruta adicional aumenta el riesgo de defectos, pruebas insuficientes y regresiones.
- Un umbral aceptable en servicios críticos debería situarse por debajo de 6-8.

### Complejidad cognitiva
- Estimación SonarQube/SonarLint: 21

Justificación:
- El método presenta múltiples decisiones anidadas en distintas capas de negocio (presupuesto, tipo de obra, región, urgencia, complejidad).
- Cada nivel de anidación y cada bifurcación suma peso en la comprensión.

Interpretación:
- Una complejidad cognitiva de 21 es alta para una única operación de cálculo de margen.
- Valores altos dificultan el entendimiento rápido del flujo y aumentan la probabilidad de introducir errores al modificar la lógica.
- En código de negocio, valores ideales suelen ser < 10; valores hasta 15 pueden ser tolerables si están bien estructurados.

## 2. Mapa de hotspots de anidación

### Nivel de anidación máximo observado
- Máximo de 5 niveles de anidación.
- Camino crítico:
  1. `if (budget > 0)`
  2. `if (type == 1)`
  3. `if (region == "EMEA")`
  4. `if (urgent)`
  5. `if (complexity > 7)`

### Por qué esto viola buenas prácticas
- Regla de 3 niveles: más de 3 niveles de anidación ya se considera una señal de código difícil de mantener.
- Los 5 niveles generan un árbol de decisión profundo, lo cual causa:
  - mayor dificultad para seguir el flujo lógico,
  - mayor número de combinaciones no cubiertas en pruebas unitarias,
  - mayor riesgo de introducir comportamientos inconsistentes cuando se modifica una rama.
- El código financiero debe ser explícito y fácil de auditar; la anidación profunda oculta condiciones clave.

### Hotspots adicionales
- El bloque `type == 1` contiene dos ramas de región (`EMEA`, `LATAM`) sin un caso de fallback claro.
- El bloque `type == 2` introduce otra rama de urgencia y complejidad que duplica la lógica de decisiones.
- La rama `else` final depende solo de presupuesto, lo que muestra una mezcla de condiciones de tipo de obra y características del proyecto en el mismo método.

## 3. Taxonomía de errores potenciales

### Riesgo 1: nueva clase de obra
- Si se añade un nuevo `type` distinto de 1 o 2, el método cae en la rama `else` genérica.
- Esto provoca que toda nueva categoría de obra use un cálculo de margen basado únicamente en el presupuesto, ignorando región, urgencia y complejidad.
- Riesgo de negocio: márgenes subestimados o sobredimensionados para nuevas líneas de negocio.

### Riesgo 2: nueva región o valores de región inesperados
- Para `type == 1`, solo hay lógica explícita para `EMEA` y `LATAM`.
- Una región diferente no asigna ningún margen, porque no hay rama `else` que capture casos no previstos.
- Riesgo de negocio: margen igual a cero para proyectos en regiones nuevas o maltipadas, con impacto directo en la rentabilidad.

### Riesgo 3: incoherencia en criterios urgentes/complejos
- La urgencia se trata de forma distinta en `type == 1` y `type == 2`.
- En `type == 1` puede haber margen 5%, 10% o 15% según región y complejidad, mientras que `type == 2` aplica 18%, 20% o 25% con otras condiciones.
- Riesgo de negocio: comportamiento difícil de auditar, posible incumplimiento de políticas de precios internas y rotura de consistencia cuando se ajusta una condición.

### Riesgo 4: entrada inválida o presupuesto no positivo
- El método devuelve `0` cuando `budget <= 0`, sin validación ni alertas.
- Esto puede ocultar errores de datos y permitir que una operación inválida termine con un margen nulo.
- Riesgo de negocio: entradas erróneas pasan desapercibidas y se utilizan en cálculos agregados.

## 4. Establecimiento de umbrales para refactorización

### Objetivos de métrica
- Complejidad ciclomática: < 5
- Complejidad cognitiva: < 10
- Profundidad de anidación máxima: 2
- Longitud del método: idealmente < 25 líneas lógicas

### Objetivos de práctica de diseño
- Separar la lógica de decisión por dimensión de negocio: tipo de obra, región, urgencia y complejidad.
- Evitar combinaciones de condiciones implícitas en una sola función.
- Introducir un modelo de reglas o estrategia de cálculo que permita extender tipos/regiones sin alterar el flujo principal.
- Garantizar rutas explícitas de fallback para valores no previstos.

## 5. Conclusión
El método `CalculateRiskMargin` presenta un nivel de complejidad excesivo para su propósito: cálculo de margen financiero crítico. La estructura actual genera un riesgo alto de mantenimiento y de errores funcionales, especialmente si se añaden nuevos tipos de obra o regiones.

Recomendación de auditoría: refactorizar antes de extender la funcionalidad, utilizando una aproximación más distribuida y declarativa para las reglas de negocio, y reduciendo la anidación profunda para que el cálculo sea fácil de auditar y de probar.
