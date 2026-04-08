# Tareas de Implementación - Modernización de Resistencia Estructural

## Infraestructura de Datos y Contratos

### Tarea 1.1: Definir DTO DatosEstructurales con validaciones
**Objetivo**: Crear la clase `DatosEstructurales` con propiedades descriptivas y validaciones de rango que prevengan valores inválidos (dimensiones negativas, materiales vacíos).

**Criterio de Hecho**:
- Propiedades `Longitud`, `Ancho` (double > 0), `TipoCondicion` (int 1-2), `Material` (string no vacío).
- Método `EsValido()` que retorna true solo si todas las validaciones pasan.
- Tests unitarios verifican validaciones correctas para casos edge (0, negativo, null).

### Tarea 1.2: Implementar interfaz IResistenciaStrategy
**Objetivo**: Definir la interfaz `IResistenciaStrategy` con métodos para cálculo, validación de seguridad y aceptación de resistencia.

**Criterio de Hecho**:
- Métodos: `CalcularResistencia(DatosEstructurales)`, `RequiereValidacionSeguridad(double)`, `EsResistenciaAceptable(double)`.
- Documentación XML completa explicando cada método.
- Interfaz compilable sin errores.

### Tarea 1.3: Crear interfaz IValidadorSeguridad
**Objetivo**: Definir abstracción para validación de seguridad legacy.

**Criterio de Hecho**:
- Método `ValidarSeguridad(double resistencia)` que retorna bool.
- Documentación indicando su propósito de abstracción.

## Implementación de Lógica de Materiales

### Tarea 2.1: Implementar HormigonStrategy
**Objetivo**: Crear estrategia para hormigón H400 con constantes nombradas para factores de corrección.

**Criterio de Hecho**:
- Constantes: `FACTOR_ESTANDAR = 0.95`, `FACTOR_ELEVADO = 0.88`.
- Lógica: `area = Longitud * Ancho; return TipoCondicion == 1 ? area * FACTOR_ESTANDAR : area * FACTOR_ELEVADO`.
- `RequiereValidacionSeguridad` retorna true si resistencia > 5000.
- `EsResistenciaAceptable` siempre true (validación externa).

### Tarea 2.2: Implementar AceroStrategy
**Objetivo**: Crear estrategia para acero A500 con constantes nombradas para factores de amplificación.

**Criterio de Hecho**:
- Constantes: `FACTOR_ESTANDAR = 1.45`, `FACTOR_ELEVADO = 1.10`, `UMBRAL_MINIMO = 150`.
- Lógica: `dimension = Longitud + Ancho; return TipoCondicion == 1 ? dimension * FACTOR_ESTANDAR : dimension * FACTOR_ELEVADO`.
- `RequiereValidacionSeguridad` siempre false.
- `EsResistenciaAceptable` retorna true si resistencia >= UMBRAL_MINIMO.

## Desarrollo del Servicio Orquestador

### Tarea 3.1: Implementar CalculadorResistencia
**Objetivo**: Crear el servicio orquestador que selecciona estrategia y gestiona umbrales de seguridad.

**Criterio de Hecho**:
- Constructor con inyección de `IValidadorSeguridad`.
- Método `Calcular(DatosEstructurales)` que retorna int? compatible con legacy.
- Lógica: Validar datos, seleccionar estrategia, calcular resistencia, aplicar validación si requerida.
- Retornos: -1 para inválido, null para material no soportado, 0/1 para aprobado/rechazado.

### Tarea 3.2: Implementar selección de estrategia
**Objetivo**: Método privado para mapear material a estrategia concreta.

**Criterio de Hecho**:
- Switch expression: "H400" -> HormigonStrategy, "A500" -> AceroStrategy, default -> null.
- Extensible para futuros materiales sin modificar lógica existente.

### Tarea 3.3: Crear AdaptadorSeguridadLegacy
**Objetivo**: Implementar adaptador que envuelve `Check_Legacy_Security_V2`.

**Criterio de Hecho**:
- Implementa `IValidadorSeguridad`.
- Método `ValidarSeguridad` invoca el método legacy y retorna su resultado.
- Documentación indicando que es puente temporal.

## Manejo de Errores y Trazabilidad

### Tarea 4.1: Definir excepciones personalizadas
**Objetivo**: Crear excepciones para escenarios específicos (material no soportado, datos inválidos).

**Criterio de Hecho**:
- `MaterialNoSoportadoException` para materiales no implementados.
- `DatosInvalidosException` para validaciones fallidas.
- Heredan de `ArgumentException` con mensajes descriptivos.

### Tarea 4.2: Implementar logging de auditoría
**Objetivo**: Agregar trazabilidad para cada cálculo realizado.

**Criterio de Hecho**:
- Usar `ILogger` inyectado en `CalculadorResistencia`.
- Loggear: entrada, estrategia seleccionada, resistencia calculada, resultado final.
- Niveles: Information para cálculos exitosos, Warning para rechazos, Error para excepciones.

### Tarea 4.3: Actualizar métodos para lanzar excepciones
**Objetivo**: Modificar `CalculadorResistencia` para usar excepciones en lugar de códigos de retorno.

**Criterio de Hecho**:
- Lanzar `DatosInvalidosException` en lugar de retornar -1.
- Lanzar `MaterialNoSoportadoException` en lugar de retornar null.
- Mantener compatibilidad opcional con códigos legacy si requerido.

## Validación de Paridad Funcional

### Tarea 5.1: Crear suite de pruebas de paridad
**Objetivo**: Definir tests que comparen resultados nuevos vs legacy.

**Criterio de Hecho**:
- Clase `ParidadTests` con método `CompararConLegacy`.
- Casos de prueba: Todos los escenarios del documento REGLAS_RESISTENCIA.md.
- Para cada input, ejecutar legacy y nuevo, afirmar igualdad de resultados.

### Tarea 5.2: Implementar wrapper para código legacy
**Objetivo**: Crear adaptador para ejecutar `Proc_M_Check` en tests.

**Criterio de Hecho**:
- Clase `LegacyWrapper` que invoca `Proc_M_Check` con mapeo de `DatosEstructurales`.
- Método `CalcularLegacy(DatosEstructurales)` que retorna int?.

### Tarea 5.3: Ejecutar y validar paridad
**Objetivo**: Correr tests y asegurar 100% de paridad.

**Criterio de Hecho**:
- Todos los tests pasan.
- Cobertura incluye casos edge (umbrales 5000, 150, 20000).
- Reporte de cobertura generado y revisado.