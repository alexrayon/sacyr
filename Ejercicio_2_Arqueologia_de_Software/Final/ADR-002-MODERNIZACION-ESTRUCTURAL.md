# ADR-002: Modernización de Lógica de Resistencia Estructural

## Estado
Aceptado

## Fecha
2026-04-08

## Contexto
El sistema legacy contiene el método `Proc_M_Check` en `Program.cs`, que implementa lógica de cálculo de resistencia estructural para materiales H400 (hormigón) y A500 (acero). Este código presenta significativa deuda técnica:

- **Variables crípticas**: Uso de letras sueltas (`l`, `w`, `t`, `m`) sin significado semántico.
- **Lógica acoplada**: El método contiene condicionales anidados que mezclan cálculo de fórmulas, validación de seguridad y lógica de materiales.
- **Falta de extensibilidad**: Agregar nuevos materiales requiere modificar el código existente, violando el Principio Abierto/Cerrado (OCP).
- **Dificultad de testing**: La lógica está enterrada en un método estático, complicando las pruebas unitarias.
- **Dependencia legacy**: Acoplamiento directo con `Check_Legacy_Security_V2`, un componente externo no controlado.

Esta deuda técnica aumenta el riesgo de errores en mantenimiento y dificulta la evolución del sistema hacia nuevos requisitos de ingeniería estructural.

## Decisión
Implementar el Patrón Estrategia (Strategy Pattern) para desacoplar la lógica de cálculo de resistencia por material. La solución incluirá:

- **Interfaz IResistenciaStrategy**: Define el contrato para estrategias de cálculo.
- **Estrategias concretas**: `HormigonStrategy` y `AceroStrategy` implementando `IResistenciaStrategy`.
- **Contexto de cálculo**: Clase `CalculadorResistencia` que selecciona y ejecuta la estrategia apropiada.
- **Objeto de datos**: `DatosEstructurales` con propiedades descriptivas (`Longitud`, `Ancho`, `TipoCondicion`, `Material`).
- **Adaptador de seguridad**: `AdaptadorSeguridadLegacy` para abstraer la interacción con `Check_Legacy_Security_V2`.

## Consecuencias

### Positivas
- **Mantenibilidad**: Cada estrategia encapsula su propia lógica, facilitando modificaciones y debugging.
- **Extensibilidad**: Nuevos materiales se agregan creando nuevas estrategias sin alterar código existente (cumple OCP).
- **Testabilidad**: Estrategias independientes permiten pruebas unitarias aisladas con mocks.
- **Legibilidad**: Nombres descriptivos mejoran la comprensión del dominio.
- **Principio de Responsabilidad Única**: Cada clase tiene una única razón para cambiar.
- **Separación de concerns**: Cálculo, validación y seguridad están desacoplados.

### Negativas
- **Complejidad inicial**: Mayor número de clases y archivos comparado con el código legacy.
- **Curva de aprendizaje**: El equipo debe familiarizarse con el patrón Strategy.
- **Overhead de indirección**: Llamadas adicionales a través de interfaces pueden impactar performance mínima (no significativo en este contexto).

### Riesgos
- **Migración incremental**: Debe coexistir con código legacy durante transición.
- **Dependencia externa**: `Check_Legacy_Security_V2` sigue siendo un punto de fallo; el adaptador mitiga pero no elimina este riesgo.

## Alternativas Consideradas
- **Método Factory**: Menos flexible para extensiones futuras comparado con Strategy.
- **Herencia**: Crearía una jerarquía rígida, menos mantenible que composición.
- **Refactor inline**: Mantendría deuda técnica sin resolver problemas fundamentales.

## Implementación
La migración se realizará en fases:
1. Crear contratos e interfaces.
2. Implementar estrategias para materiales existentes.
3. Crear adaptador de seguridad.
4. Refactorizar `Proc_M_Check` para usar el nuevo diseño.
5. Actualizar tests y documentación.

## Notas
Este ADR se basa en el análisis documentado en `REGLAS_RESISTENCIA.md`. La implementación debe mantener compatibilidad con la API existente durante la transición.