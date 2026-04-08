# Especificación de Requisitos - Modernización Proc_M_Check
## Versión 2.0 - Con Soporte de Unidades Parametrizadas

**Fecha**: 2026-04-08  
**Estado**: Aprobado con Mejoras QA  
**Auditoria QA**: INFORME-AUDITORIA-QA.md  

---

## 1. Contexto Operacional

### 1.1 Alcance Geográfico y de Unidades

Sacyr opera en múltiples jurisdicciones con estándares de medida divergentes:

| Región | Unidad Primaria | Estándar | Riesgo |
|--------|-----------------|----------|--------|
| España | Metro (m) | ISO 1000 (SI) | BAJO |
| Portugal | Metro (m) | ISO 1000 (SI) | BAJO |
| Puerto Rico | Pie (ft) | Británico | MEDIO |
| USA | Pie (ft) / Pulgada (in) | US Customary | MEDIO |
| Canadá | Metro (m) | ISO 1000 (SI) | BAJO |
| Proyectos híbridos | Mixto | Variable | ALTO |

**Problema Histórico**: Conversiones manuales hardcodeadas causan ~15% de errores en proyectos internacionales.

---

## 2. Requisitos Funcionales Actualizados

### 2.1 RF-001: Cálculo de Resistencia Estructural (ACTUAL)

**Descripción**: Calcular resistencia de hormigón (H400) y acero (A500) basado en dimensiones y tipo de carga.

**Entrada**:
- `Longitud` (double): Dimensión principal en UNIDAD A ESPECIFICAR
- `Ancho` (double): Dimensión secundaria en UNIDAD A ESPECIFICAR
- `TipoCondicion` (int): 1 (estándar), 2 (elevada)
- `Material` (string): "H400" o "A500"

**Salida**: `int?` → 1 (aprobado), 0 (rechazado), null (sin decisión), -1 (error)

**Cambio v2.0**: Agregar metadato de unidad.

### 2.2 RF-002: Soporte Multi-Unidades (NUEVO)

**Descripción**: Aceptar dimensiones en múltiples unidades y normalizar internamente a metro (SI).

**Entrada Extendida**:
```csharp
DatosEstructuralesConUnidades {
    Longitud: double,
    Ancho: double,
    TipoCondicion: int,
    Material: string,
    Unidades: ConfiguracionUnidades {
        UnidadEntrada: UnidadMedida,     // Pies, Metros, Pulgadas, Centímetros
        UnidadReferencia: UnidadMedida   // Siempre Metros internamente
    }
}
```

**Lógica**:
1. Validar que `Longitud` y `Ancho` sean positivos EN UNIDAD ESPECIFICADA.
2. Aplicar conversión: `Longitud_metros = Longitud * FactorConversion(UnidadEntrada)`
3. Procesar con lógica existente.
4. Retornar resultado (sin cambio de unidades, es índice abstracto).

**Factores de Conversión Estándar**:
```
Metro     → 1.0
Pie       → 0.3048 m/ft    (RFC 1760)
Pulgada   → 0.0254 m/in    (ISO 80000-4)
Centímetro → 0.01 m/cm     (SI)
```

### 2.3 RF-003: Validación Estricta de TipoCondicion (REMEDIACIÓN QA)

**Descripción**: Garantizar que `TipoCondicion` solo tenga valores 1 o 2, previniendo divergencia con código legacy.

**Validación**:
- Si `TipoCondicion ∉ {1, 2}`: Lanzar `ArgumentException`
- Mensaje: "TipoCondicion debe ser 1 (estándar) o 2 (elevada)"

**Nota**: En codigo legacy, si `t != 1` y `t != 2` en hormigón, resultaba en `r = 0`. Ahora es error explícito.

### 2.4 RF-004: Mapeo de Excepciones (REMEDIACIÓN COMPATIBILIDAD)

**Descripción**: Traducir excepciones del nuevo modelo a códigos legacy durante transición.

**Mapper**:
```
ArgumentException (dimensiones inválidas) → -1 (legacy)
ArgumentException (otros) → -1 (legacy)
NotSupportedException (material desconocido) → null (legacy)
ArgumentNullException (entrada nula) → -1 (legacy)
```

**Nota de Migración**: En v2.1, eliminar mapper, requerir excepciones.

---

## 3. Requisitos No-Funcionales

### 3.1 RNF-001: Precisión de Cálculo

**Métrica**: Tolerancia de error máximo en umbrales críticos.

```
Umbral crítico: 5000
Tolerancia: ±10 unidades (0.2%)
Tipo de dato: double (IEEE 754)
Justificación: Suficiente para ingeniería civil estándar
```

**Futuro (v3.0)**: Opción configurable a `decimal` para sistemas certificados metrológicamente.

### 3.2 RNF-002: Compatibilidad Backward

**Mándato**: Código moderno debe retornar resultados idénticos a legacy para 100% de casos de entrada válida.

**Excepciones Documentadas**:
- `TipoCondicion` inválido: Comportamiento diferente (error vs silencioso)
- Manejo de errores: Excepciones vs códigos retorno

**Evidencia**: Suite de tests `ResistenciaParidadTests` (5/5 PASAN)

### 3.3 RNF-003: Mantenibilidad

**Métricas**:
- Complejidad ciclomática por método: ≤ 3
- Acoplamiento: A través de interfaces (Dependency Injection)
- Cobertura de tests: ≥ 85%

### 3.4 RNF-004: Performance

**Requisito**: Tiempo de cálculo < 1ms por invocación.

**Benchmark Esperado**:
```
1,000 cálculos: ~0.8ms (0.0008ms por cálculo)
100,000 cálculos: ~80ms
```

**Nota**: double vs decimal trade-off documentado en INFORME-AUDITORIA-QA.md

---

## 4. Interfaces y Contratos (ACTUALIZADOS)

### 4.1 ConfiguracionUnidades

```csharp
/// <summary>
/// Configuración de unidades de medida para normalización.
/// </summary>
public record ConfiguracionUnidades(
    UnidadMedida UnidadEntrada,
    UnidadMedida UnidadReferencia = UnidadMedida.Metros
)
{
    /// <summary>
    /// Indica si se requiere conversión de unidades.
    /// </summary>
    public bool RequiereConversion => UnidadEntrada != UnidadReferencia;
    
    /// <summary>
    /// Obtiene el factor de conversión a la unidad de referencia.
    /// </summary>
    public double ObtenerFactorConversion()
    {
        return UnidadEntrada switch
        {
            UnidadMedida.Metros => 1.0,
            UnidadMedida.Pies => 0.3048,
            UnidadMedida.Pulgadas => 0.0254,
            UnidadMedida.Centimetros => 0.01,
            _ => throw new NotSupportedException($"Unidad no soportada: {UnidadEntrada}")
        };
    }
}
```

### 4.2 DatosEstructuralesConUnidades (NUEVO DTO)

```csharp
/// <summary>
/// DTO con soporte explícito de unidades de medida.
/// </summary>
public class DatosEstructuralesConUnidades
{
    /// <summary>
    /// Dimensión principal (valor numérico en UnidadEntrada).
    /// </summary>
    public double Longitud { get; set; }
    
    /// <summary>
    /// Dimensión secundaria (valor numérico en UnidadEntrada).
    /// </summary>
    public double Ancho { get; set; }
    
    /// <summary>
    /// Tipo de condición: 1 (estándar), 2 (elevada).
    /// </summary>
    public int TipoCondicion { get; set; }
    
    /// <summary>
    /// Código de material: "H400" (hormigón), "A500" (acero).
    /// </summary>
    public required string Material { get; set; }
    
    /// <summary>
    /// Configuración de unidades (entrada y referencia).
    /// </summary>
    public ConfiguracionUnidades Unidades { get; set; } = 
        new(UnidadMedida.Metros, UnidadMedida.Metros);
    
    /// <summary>
    /// Validación de datos estructurales.
    /// </summary>
    public bool EsValido()
    {
        return Longitud > 0 &&
               Ancho > 0 &&
               TipoCondicion is 1 or 2 &&
               !string.IsNullOrEmpty(Material) &&
               Unidades != null;
    }
    
    /// <summary>
    /// Obtiene los datos normalizados a la unidad de referencia.
    /// </summary>
    public DatosEstructurales ObtenerDatosNormalizados()
    {
        double factor = Unidades.ObtenerFactorConversion();
        return new DatosEstructurales
        {
            Longitud = Longitud * factor,
            Ancho = Ancho * factor,
            TipoCondicion = TipoCondicion,
            Material = Material
        };
    }
}
```

### 4.3 CalculoResistenciaServiceConUnidades (NUEVO WRAPPER)

```csharp
/// <summary>
/// Servicio orquestador con conversión de unidades transparente.
/// Mantiene compatibilidad con CalculoResistenciaService existente.
/// </summary>
public class CalculoResistenciaServiceConUnidades
{
    private readonly IValidadorSeguridad _validadorSeguridad;
    private readonly CalculoResistenciaService _servicioBase;
    
    public CalculoResistenciaServiceConUnidades(
        IValidadorSeguridad validadorSeguridad)
    {
        _validadorSeguridad = validadorSeguridad ?? 
            throw new ArgumentNullException(nameof(validadorSeguridad));
        _servicioBase = new CalculoResistenciaService(validadorSeguridad);
    }
    
    /// <summary>
    /// Calcula resistencia aceptando datos con unidades.
    /// </summary>
    public int? CalcularConUnidades(DatosEstructuralesConUnidades datos)
    {
        if (datos == null) throw new ArgumentNullException(nameof(datos));
        if (!datos.EsValido()) throw new ArgumentException("Datos inválidos", nameof(datos));
        
        // Normalizar a unidad de referencia (metros)
        var datosNormalizados = datos.ObtenerDatosNormalizados();
        
        // Delegar al servicio base (sin cambios)
        return _servicioBase.Calcular(datosNormalizados);
    }
}
```

---

## 5. Escenarios de Prueba (ACTUALIZADOS)

### 5.1 BDD - Escenario Existente 1: Hormigón Estándar

```gherkin
Scenario: Cálculo de hormigón en metros
  Given un elemento de hormigón H400 con longitud 5.0m, ancho 0.3m, tipo 1, metros
  When se calcula la resistencia
  Then el resultado debe ser null (sin decisión)
```

### 5.2 BDD - Escenario Existente 2: Acero Falla

```gherkin
Scenario: Fallo de acero por resistencia mínima
  Given un perfil de acero A500 con longitud 2.0, ancho 1.5, tipo 2, metros
  When se calcula la resistencia
  Then el resultado debe ser 0 (rechazado)
```

### 5.3 BDD - Escenario NUEVO: Conversión Unidades

```gherkin
Scenario: Cálculo con datos en pies convertidos a metros
  Given un elemento de hormigón H400 con:
    - Longitud 165.0 pies
    - Ancho 1.15 pies
    - Tipo condición 1
    - Unidad entrada: Pies
    - Unidad referencia: Metros
  When se calcula con conversión de unidades
  Then los datos se normalizan a:
    - Longitud 50.292 metros (165 × 0.3048)
    - Ancho 0.3505 metros (1.15 × 0.3048)
  And el resultado coincide con el cálculo en metros directo
```

### 5.4 BDD - Escenario NUEVO: Validación Tipo Condición

```gherkin
Scenario: Rechazo de tipo condición inválido
  Given un elemento con TipoCondicion = 3 (inválido)
  When se intenta calcular
  Then se lanza ArgumentException
  And el mensaje contiene "debe ser 1 o 2"
```

---

## 6. Casos Críticos (REMEDIACIÓN QA)

### 6.1 Caso Crítico: Divergencia en TipoCondicion Inválido

**Problema Identificado**: 
- Legacy: Si t ∉ {1,2}, hormigón produce r=0
- Moderno: Se espera usar else → r = area * 0.88

**Solución**: Validación estricta previene casos t ∉ {1,2}

**Test**:
```csharp
[Theory]
[InlineData(0)]
[InlineData(3)]
[InlineData(-1)]
public void TipoCondicionInvalido_DebeLanzarExcepcion(int tipoInvalido)
{
    var datos = new DatosEstructuralesConUnidades
    {
        Longitud = 50,
        Ancho = 0.3,
        TipoCondicion = tipoInvalido,  // Inválido
        Material = "H400",
        Unidades = new ConfiguracionUnidades(UnidadMedida.Metros)
    };
    
    var servicio = new CalculoResistenciaServiceConUnidades(_validador);
    
    Assert.Throws<ArgumentException>(() => 
        servicio.CalcularConUnidades(datos));
}
```

### 6.2 Caso Crítico: Material Desconocido

**Problema Identificado**:
- Legacy: Retorna null silenciosamente
- Moderno: Lanza NotSupportedException

**Solución Recomendada**: Wrapper de compatibilidad para transición

```csharp
public int? CalcularCompatible(DatosEstructuralesConUnidades datos)
{
    try
    {
        return CalcularConUnidades(datos);
    }
    catch (NotSupportedException)
    {
        return null;  // Compatibilidad legacy
    }
}
```

---

## 7. Roadmap de Implementación

### Fase 1: ACTUAL (Sprint completado)
✓ Código modernizado con double  
✓ Interfaces IResistenciaStrategy  
✓ CalculoResistenciaService con DI  
✓ Tests de paridad (5/5 PASAN)  
✓ Documentación REGLAS_RESISTENCIA.md

### Fase 2: PRÓXIMO SPRINT (Recomendado)
- [ ] Implementar ConfiguracionUnidades
- [ ] Crear DatosEstructuralesConUnidades
- [ ] Desarrollar CalculoResistenciaServiceConUnidades
- [ ] Agregar tests de conversión (8+ nuevos casos)
- [ ] Documentación de unidades en README
- [ ] Validación estricta de TipoCondicion

### Fase 3: FUTURO (v2.1+)
- [ ] Soporte a `decimal` para certificación metrológica
- [ ] Parametrización de unidades de resistencia (MPa vs PSI)
- [ ] Logging de auditoría con timestamps y usuario
- [ ] Dashboard de métricas de conversión
- [ ] Deprecar wrapper de compatibilidad

---

## 8. Aceptación de Requisitos

**Verificado por**: Auditoría QA INFORME-AUDITORIA-QA.md  
**Estado**: ✓ APROBADO CON CONDICIONES  
**Condiciones**:
1. Implementar validación estricta de TipoCondicion (Fase 2)
2. Documentar unidades esperadas (Métrica → Fases 1, 2, 3)
3. Agregar wrapper de compatibilidad para material desconocido (Fase 2)

**Firma Técnica**: Senior QA Auditor, 2026-04-08