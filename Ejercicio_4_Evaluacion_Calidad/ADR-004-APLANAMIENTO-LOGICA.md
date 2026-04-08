# ADR-004: Aplanamiento de la lógica de cálculo de margen

## Estado
Propuesto

## Contexto
El método `CalculateRiskMargin` actual presenta una complejidad ciclomática calculada en 12 y una complejidad cognitiva estimada en 21, con un nivel de anidación máximo de 5. Esto provoca un riesgo elevado de errores manuales y dificultades de mantenimiento en el cálculo de márgenes de licitaciones para Sacyr.

## Decisión
Adoptar un diseño que aplana la lógica mediante:
- cláusulas de guarda tempranas que reducen el anidamiento,
- delegación a métodos privados especializados por tipo de obra,
- separación de la configuración de márgenes (porcentajes) de la lógica de decisión.

## Justificación
1. Reducción de errores manuales en licitaciones
   - La lógica actual mezcla múltiples dimensions de decisión dentro de ramas anidadas.
   - Al aplanar el flujo, cada ruta de negocio se vuelve explícita y más fácil de verificar.
   - Las decisiones comerciales quedan más visibles, lo que reduce el riesgo de aplicar un margen erróneo en una licitación.
2. Mejora de la cobertura de tests unitarios
   - Con métodos especializados y reglas explícitas, es sencillo crear pruebas unitarias puntuales para cada caso.
   - Las cláusulas de guarda permiten aislar condiciones inválidas o de fallback.
   - La reducción de ramas anidadas minimiza combinaciones implícitas, lo que facilita la construcción de una suite de pruebas exhaustiva.
3. Mantenibilidad y extensibilidad
   - La lógica se vuelve más modular: cada tipo de obra puede crecer de forma independiente.
   - La configuración de márgenes separada permite ajustar valores comerciales sin modificar la lógica de control.

## Consecuencias
- Se disminuirá la probabilidad de errores por interpretación incorrecta de reglas de margen.
- La nueva estructura será más amigable para revisiones de código y auditorías internas.
- El cambio requiere construir una suite de pruebas de regresión sólida antes de reemplazar la implementación legacy.
- Se facilita la incorporación de nuevos tipos de obra y regiones con menos riesgo de introducir fallos en rutas existentes.

## Alternativas consideradas
- Mantener la lógica actual con comentarios más claros: insuficiente, ya que no reduce la complejidad real.
- Usar una sola tabla de decisiones sin métodos especializados: mejora marginal, pero conserva un punto de control único demasiado complejo.

## Decisión adoptada
Implementar el aplanamiento con guard clauses y métodos privados por tipo de obra, apoyado por una matriz de configuración de márgenes. Esto ofrece el mejor balance entre claridad, control de riesgos y capacidad de prueba.
