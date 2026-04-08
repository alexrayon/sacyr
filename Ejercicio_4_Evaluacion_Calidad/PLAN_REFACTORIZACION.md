# Plan de Refactorización para `CalculateRiskMargin`

## 1. Estrategia de refactorización

### Objetivo
Reducir la complejidad ciclomática y cognitiva del método `CalculateRiskMargin` mediante:
- cláusulas de guarda que eliminan el anidamiento profundo,
- delegación a métodos privados especializados por tipo de obra,
- separación de la lógica de decisión de los valores numéricos del negocio.

### Enfoque propuesto
1. Validar condiciones básicas inmediatamente.
   - `if (budget <= 0) return 0;`
   - `if (!IsKnownType(type)) return CalculateDefaultMargin(budget);`
2. Usar `switch` / `pattern matching` en el valor `type` para delegar a un método privado:
   - `CalculateMarginForAutopistas(...)`
   - `CalculateMarginForTuneles(...)`
   - `CalculateMarginForOtros(...)`
3. Dentro de cada método especializado, aplicar cláusulas de guarda y patrones claros:
   - `if (region == "EMEA" && urgent && complexity > 7) return budget * MarginTable.AutopistasEmeaUrgenteAlto;`
   - `if (region == "LATAM" && complexity > 5) return budget * MarginTable.AutopistasLatamComplejidadAlta;`
4. Evitar anidamientos condicionales profundos con retornos tempranos y condiciones de exclusión.

### Ejemplo de modelo lógico
- entrada general: `budget`, `type`, `region`, `urgent`, `complexity`
- lógica principal: `return type switch { 1 => CalculateMarginForAutopistas(...), 2 => CalculateMarginForTuneles(...), _ => CalculateMarginForOtros(...) };`
- métodos privados especializados definen rutas ligeras y legibles.

## 2. Definición de contratos y configuración de márgenes

### Principio
Separar la lógica de negocio de los valores de margen permite cambiar parámetros sin tocar el flujo de decisión.

### Estructura propuesta
- `static class MarginConstants` con valores numéricos identificados:
  - `public const decimal AutopistasEmeaUrgenteAlto = 0.15m;`
  - `public const decimal AutopistasEmeaUrgenteNormal = 0.10m;`
  - `public const decimal AutopistasEmeaNoUrgente = 0.05m;`
  - `public const decimal AutopistasLatamAlto = 0.12m;`
  - `public const decimal AutopistasLatamBajo = 0.08m;`
  - `public const decimal TunelesUrgenteAlto = 0.25m;`
  - `public const decimal TunelesNoUrgenteAlto = 0.20m;`
  - `public const decimal TunelesNormal = 0.18m;`
  - `public const decimal OtrosGranContrato = 0.05m;`
  - `public const decimal OtrosContratoPequeno = 0.02m;`
- Alternativa: una matriz de configuración inmutable:
  - `private static readonly MarginRule[] Rules = { new MarginRule(type, region, urgent, complexityThreshold, margin) };`

### Beneficios
- cambios de negocio se realizan solo en la tabla de márgenes.
- el método principal se mantiene limpio y estable.
- se reduce el riesgo de mezclar valores de negocio directamente en la lógica.

## 3. Plan de verificación

### Objetivo
Asegurar que la nueva implementación produce resultados idénticos a la versión actual para todas las rutas de cálculo definidas en el diagnóstico.

### Estrategia de pruebas
1. Implementar un conjunto de pruebas de regresión con los casos actuales:
   - `type == 1`, `region == "EMEA"`, `urgent == true`, `complexity > 7`
   - `type == 1`, `region == "EMEA"`, `urgent == true`, `complexity <= 7`
   - `type == 1`, `region == "EMEA"`, `urgent == false`
   - `type == 1`, `region == "LATAM"`, `complexity > 5`
   - `type == 1`, `region == "LATAM"`, `complexity <= 5`
   - `type == 2`, `complexity > 8`, `urgent == true`
   - `type == 2`, `complexity > 8`, `urgent == false`
   - `type == 2`, `complexity <= 8`
   - `type != 1 && type != 2`, `budget > 1000000`
   - `type != 1 && type != 2`, `budget <= 1000000`
   - `budget <= 0`
2. Validar con `expected = oldImplementation(...)` versus `actual = newImplementation(...)` en cada caso.
3. Añadir pruebas de límite para garantizar que los umbrales de complejidad y presupuesto se comportan igual.
4. Verificar el valor por defecto ante datos fuera de los casos esperados (`region` desconocida, nuevo `type`).

### Criterios de aceptación
- la nueva implementación debe pasar todas las pruebas de regresión existentes.
- los valores calculados deben coincidir en un 100% con la implementación legacy para los casos documentados.
- cualquier nuevo caso deberá estar cubierto con una prueba adicional antes de aceptar el cambio.

## 4. Resultado esperado
- Método principal reducido a una delegación simple.
- Lógica por tipo de obra ubicada en métodos especializados más pequeños.
- Porcentaje de márgenes gestionados por constantes o tabla de configuración.
- Código más testeable, mantenible y preparado para extender nuevas categorías sin aumentar la complejidad.
