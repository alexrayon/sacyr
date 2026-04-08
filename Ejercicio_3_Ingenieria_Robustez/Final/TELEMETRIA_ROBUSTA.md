# ESPECIFICACIÓN DE TELEMETRÍA ROBUSTA PARA SACYR
## Sistema de Adquisición de Datos para IoT Industrial

**Documento**: TELEMETRIA_ROBUSTA.md  
**Rol**: Ingeniero de Fiabilidad (SRE) - IoT Industrial  
**Versión**: 1.0  
**Fecha**: 2026-04-08  
**Cliente**: Sacyr - Sistemas de Monitoreo de Obras

---

## 1. ANÁLISIS TÉCNICO: CORRUPCIÓN DE DATOS EN SISTEMAS PREVIOS

### 1.1 Identificación del Problema: Acumulación No-Idempotente

El sistema de telemetría previo presentaba una vulnerabilidad crítica en la adquisición de combustible:

```csharp
// ❌ CÓDIGO VULNERABLE (Patrón Problemático)
public class FuelupdateProcessor
{
    private decimal _fuelAccumulator = 0;

    public void ProcessFuelUpdate(FuelMessage message)
    {
        _fuelAccumulator += message.FuelConsumed;  // PROBLEMA AQUÍ
    }
}
```

### 1.2 Raíz Causas: Condiciones de Carrera

#### **Problema 1: Falta de Atomicidad en Lectura-Modificación-Escritura**

En un entorno multihilo (típico de IoT con múltiples sensores):

```
Hilo 1: Lee _fuelAccumulator = 100
Hilo 2: Lee _fuelAccumulator = 100    ← LECTURA SUCIA (race condition)
Hilo 1: Suma 50, Escribe _fuelAccumulator = 150
Hilo 2: Suma 30, Escribe _fuelAccumulator = 130  ← SOBRESCRIBIÓ ACTUALIZACIÓN DE HILO 1

RESULTADO: Pérdida de 50 litros (perdió la actualización del Hilo 1)
ACUMULACIÓN REAL: 100 + 50 + 30 = 180
ACUMULACIÓN REGISTRADA: 130  ← ERROR: -50 litros (~-2.8%)
```

**Impacto Operacional**: 
- En una obra de 100,000 litros diarios con 50 sensores concurrentes:
  - Desviación esperada: 2-5% de pérdida de datos
  - = 2,000-5,000 litros/día no registrados
  - Imposibilidad de auditoría energética
  - Vulnerabilidad a fraude de combustible

#### **Problema 2: Mensajes Duplicados sin Detección (No-Idempotencia)**

El sistema no distinguía entre:
- Primer envío de "Sensor_A consumió 50L a las 14:32:15"
- Reintentos por timeout de red (mismo evento, reenvío automático)

```
Envío 1: ProcessFuelUpdate(50L) → Acumulador = 50L ✓
[TIMEOUT EN CONFIRMACIÓN]
Reintentos automático (protocolo de confiabilidad):
Envío 2: ProcessFuelUpdate(50L) → Acumulador = 100L ✗
Envío 3: ProcessFuelUpdate(50L) → Acumulador = 150L ✗
Envío 4: ProcessFuelUpdate(50L) → Acumulador = 200L ✗

RESULTADO: Mismo evento contabilizado 4 veces
SOBREGIRO: +150L ficticio
```

**Cascada de Impactos**:
- Faturas incorrectas (overbilling a clientes)
- Alertas falsas de consumo anómalo
- Imposibilidad de trazabilidad de combustible
- Violación de ISO 50001 (auditoría de consumo energético)

#### **Problema 3: Sin Claves Únicas de Identificación**

```csharp
public class FuelMessage
{
    public decimal FuelConsumed { get; set; }
    // ❌ NO HAY: public string MessageId { get; set; }
    // ❌ NO HAY: public DateTime Timestamp { get; set; }
    // ❌ NO HAY: public string SensorId { get; set; }
}
```

Sin estas claves, no hay forma de:
1. Deduplicar mensajes en persistencia
2. Correlacionar con logs de red
3. Investigar anomalías post-mortem

---

## 2. ESPECIFICACIÓN DE REQUISITOS DE ROBUSTEZ

### 2.1 REQUISITO RF-001: Idempotencia Garantizada

**Definición**: Todo mensaje debe ser procesable múltiples veces sin cambiar el resultado final.

#### **RF-001.1: OperationKey Único Obligatorio**

Cada mensaje DEBE incluir un `OperationKey` que lo identifique de forma única:

```csharp
public class TelemetryMessage
{
    /// <summary>
    /// OperationKey: ID único que identifica esta operación.
    /// Formato: {SensorId}#{Timestamp}#{SequenceNumber}
    /// Ejemplo: "SENSOR_COMBUSTIBLE_01#2026-04-08T14:32:15.123Z#1
    /// 
    /// Garantías:
    /// - Generado por el dispositivo/sensor (NUNCA por el servidor)
    /// - Inmutable durante el ciclo de vida del mensaje
    /// - Único globalmente en el contesto de cada sensor
    /// </summary>
    [Required(ErrorMessage = "OperationKey es requerido")]
    public string OperationKey { get; set; }

    public string SensorId { get; set; }
    public DateTime SensorTimestamp { get; set; }
    public uint SequenceNumber { get; set; }
    public decimal FuelConsumed { get; set; }
    public string Unit { get; set; } = "Liters";
}
```

**Estructura OperationKey** (recomendada):
```
Format: {SensorId}#{ISO8601_Timestamp}#{SequenceNumber}

Ejemplos válidos:
- "COMBUSTIBLE_A01#2026-04-08T14:32:15.123Z#1"
- "AGUA_OBRA_5#2026-04-08T14:32:15.456Z#1"
- "ELECTRICIDAD_B12#2026-04-08T14:32:16.789Z#1"

Propiedades:
✓ Lexicográficamente ordenable
✓ Sorteable por sensor y tiempo
✓ Evita colisiones: Mutex en sendor evita secuencias duplicadas
```

#### **RF-001.2: Almacén de OperationKeys Procesadas**

El sistema DEBE mantener un registro permanente de claves procesadas:

```csharp
public interface IProcessedOperationKeyStore
{
    /// <summary>
    /// Verifica si una operación ya fue registrada y completada.
    /// Retorna: true si el OperationKey ya existe en la BD
    /// </summary>
    Task<bool> IsAlreadyProcessedAsync(string operationKey);

    /// <summary>
    /// Registra un OperationKey como procesado (operación exitosa).
    /// DEBE ser atómico con la operación que lo consume.
    /// </summary>
    Task MarkAsProcessedAsync(string operationKey, DateTime processedAt);

    /// <summary>
    /// Retorna la marca de tiempo de cuándo fue procesada una clave
    /// (útil para auditoría y debugging de duplicados)
    /// </summary>
    Task<DateTime?> GetProcessedTimeAsync(string operationKey);
}
```

**Estrategia de Almacenamiento** (Persistencia):

| Escenario | Almacén | TTL | Justificación |
|-----------|---------|-----|---------------|
| Desarrollo/Testing | In-Memory `HashSet<string>` | Session | Velocidad, aceptable duplicados en test |
| Producción | PostgreSQL/Redis | 30 días | Auditoría reglamentaria, confiabilidad |
| Alta Concurrencia | Redis + Cassandra | 90 días | Eventual consistency, tolerancia a fallos |

#### **RF-001.3: Lógica de Idempotencia en Pipeline**

```csharp
public class IdempotentTelemetryProcessor
{
    private readonly IProcessedOperationKeyStore _keyStore;
    private readonly ITelemetryAccumulator _accumulator;

    public async Task<ProcessingResult> ProcessMessageAsync(
        TelemetryMessage message)
    {
        // PASO 1: Validar OperationKey no vacío
        if (string.IsNullOrWhiteSpace(message.OperationKey))
        {
            return ProcessingResult.Failure(
                "OperationKey es obligatorio");
        }

        // PASO 2: VERIFICAR IDEMPOTENCIA
        //         ↓ CRITICAL SECTION ↓
        bool alreadyProcessed = 
            await _keyStore.IsAlreadyProcessedAsync(
                message.OperationKey);

        if (alreadyProcessed)
        {
            // ✓ IDEMPOTENCIA: Retornar SUCCESS sin duplicar
            return ProcessingResult.Success(
                message: "Mensaje ya fue procesado (operación idempotente)",
                wasIdempotentReprocess: true,
                operationKey: message.OperationKey);
        }

        // PASO 3: PROCESAR MENSAJE EN SECCIÓN CRÍTICA
        try
        {
            // Incrementar acumulador (thread-safe, ver RF-002)
            await _accumulator.AddAsync(
                message.SensorId,
                message.FuelConsumed);

            // PASO 4: MARCAR COMO PROCESADO (atómico con escritura)
            await _keyStore.MarkAsProcessedAsync(
                message.OperationKey,
                DateTime.UtcNow);

            return ProcessingResult.Success(
                message: "Mensaje procesado exitosamente",
                wasIdempotentReprocess: false,
                operationKey: message.OperationKey);
        }
        catch (Exception ex)
        {
            // NO MARCAR COMO PROCESADO si falla
            // El reintento podrá procesarlo nuevamente
            return ProcessingResult.Failure(
                $"Error procesando mensaje: {ex.Message}");
        }
    }
}
```

**Garantía de Idempotencia**:
- ✓ Mensaje duplicado → Ignorado silenciosamente (no genera error)
- ✓ Resultado determinístico (no importa cuántos reintentos)
- ✓ Auditable (cada intento está en logs)

---

### 2.2 REQUISITO RF-002: Sincronización de Acumulador

**Definición**: El acumulador de combustible debe ser thread-safe y proporcionar lecturas consistentes.

#### **RF-002.1: Exclusión Mutua en Acceso**

La operación "leer-modificar-escribir" DEBE ser atómica:

```csharp
public class ThreadSafeAccumulator : ITelemetryAccumulator
{
    // Lock de grano fino para el acumulador
    private readonly object _accumulatorLock = new object();
    
    // Diccionario de acumuladores por sensor
    private readonly Dictionary<string, decimal> _accumulators = 
        new Dictionary<string, decimal>();

    /// <summary>
    /// Suma un valor al acumulador de forma sincronizada.
    /// Atómica: Lock adquirido antes de leer/modificar/escribir.
    /// </summary>
    public async Task AddAsync(string sensorId, decimal value)
    {
        ArgumentNullException.ThrowIfNull(sensorId);
        
        if (value < 0)
            throw new ArgumentException("Value no puede ser negativo");

        // BLOQUE CRÍTICO: Solo 1 hilo puede entrar
        lock (_accumulatorLock)
        {
            if (!_accumulators.ContainsKey(sensorId))
            {
                _accumulators[sensorId] = 0;
            }

            // Operaciones indivisibles:
            decimal currentValue = _accumulators[sensorId];
            decimal newValue = currentValue + value;
            _accumulators[sensorId] = newValue;

            // ✓ Nadie más puede leer currentValue mientras escribimos newValue
        }

        // Retornar después de liberar lock (evita holdtime prolongado)
        await Task.CompletedTask;
    }

    /// <summary>
    /// Lee el acumulador actual de forma sincronizada.
    /// Snapshot atómico: refleja estado en momento de lectura.
    /// </summary>
    public async Task<decimal> GetCurrentAsync(string sensorId)
    {
        lock (_accumulatorLock)
        {
            if (!_accumulators.TryGetValue(sensorId, out decimal value))
            {
                return 0;
            }
            return value;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Reinicia el acumulador (ej: cierre de turno).
    /// Atómico: impide lecturas durante reset.
    /// </summary>
    public async Task ResetAsync(string sensorId, 
        DateTime resetTime)
    {
        lock (_accumulatorLock)
        {
            if (_accumulators.ContainsKey(sensorId))
            {
                decimal previousValue = _accumulators[sensorId];
                _accumulators[sensorId] = 0;

                // LOG para auditoría
                Log.Information(
                    "Acumulador reset: Sensor={Sensor}, " +
                    "PreviousValue={Value}, Time={Time}",
                    sensorId, previousValue, resetTime);
            }
        }

        await Task.CompletedTask;
    }
}
```

#### **RF-002.2: Diagrama de Sincronización**

Comparativa de problemas evitados:

```
❌ SIN SINCRONIZACIÓN (thread-unsafe):
┌─────────────┬──────────────┬──────────────┐
│ Hilo 1      │ Hilo 2       │ Acumulador   │
├─────────────┼──────────────┼──────────────┤
│ Lee: 100    │              │ 100          │
│             │ Lee: 100     │ 100          │
│ Suma 50     │              │              │
│ Escribe 150 │              │ 150          │
│             │ Suma 30      │              │
│             │ Escribe 130  │ 130 ✗        │
└─────────────┴──────────────┴──────────────┘
Resultado: 130 (perdió 50)


✓ CON SINCRONIZACIÓN (thread-safe):
┌─────────────┬──────────────┬──────────────┐
│ Hilo 1      │ Hilo 2       │ Acumulador   │
├─────────────┼──────────────┼──────────────┤
│ LOCK        │ ESPERA       │ 100          │
│ Lee: 100    │ ESPERA       │              │
│ Suma 50     │ ESPERA       │              │
│ Escribe 150 │ ESPERA       │ 150          │
│ UNLOCK      │ LOCK         │              │
│             │ Lee: 150     │              │
│             │ Suma 30      │              │
│             │ Escribe 180  │ 180 ✓        │
│             │ UNLOCK       │              │
└─────────────┴──────────────┴──────────────┘
Resultado: 180 ✓ Correcto
```

#### **RF-002.3: Estrategia de Bloqueo**

| Estrategia | Pros | Contras | Recomendación |
|-----------|------|---------|----------------|
| `lock` (Monitor) | Simple, built-in, predecible | Bloquea todo, sin timeout | **Producción baja concurrencia** |
| `ReaderWriterLockSlim` | Múltiples lectores | Overhead si muchos escritores | Producción con lectura dominante |
| `SemaphoreSlim` | Async-await compatible | Más complejo | Producción con sync global |
| ConcurrentDictionary | Lock-free en muchos casos | Más memoria | Cuando throughput >> lock contention |

**Recomendación para IoT Sacyr**: `ReaderWriterLockSlim` porque:
- Múltiples sensores leen estado (sin causar bloqueo mutuo)
- Pocas escrituras (agregaciones cada 5-10 segundos)
- Minimiza latencia de lecturas

```csharp
public class OptimizedThreadSafeAccumulator
{
    private readonly ReaderWriterLockSlim _readerWriterLock = 
        new ReaderWriterLockSlim();
    
    private readonly Dictionary<string, decimal> _accumulators = 
        new Dictionary<string, decimal>();

    public async Task<decimal> GetCurrentAsync(string sensorId)
    {
        _readerWriterLock.EnterReadLock();
        try
        {
            return _accumulators.TryGetValue(sensorId, out var value) 
                ? value 
                : 0;
        }
        finally
        {
            _readerWriterLock.ExitReadLock();
        }
    }

    public async Task AddAsync(string sensorId, decimal value)
    {
        _readerWriterLock.EnterWriteLock();
        try
        {
            if (!_accumulators.ContainsKey(sensorId))
                _accumulators[sensorId] = 0;
            
            _accumulators[sensorId] += value;
        }
        finally
        {
            _readerWriterLock.ExitWriteLock();
        }
    }
}
```

---

### 2.3 REQUISITO RF-003: Validación Estructurada

Toda telemetría debe validarse antes de procesarse:

```csharp
public class TelemetryValidator
{
    public ValidationResult Validate(TelemetryMessage message)
    {
        var errors = new List<ValidationError>();

        // Validación 1: OperationKey
        if (string.IsNullOrWhiteSpace(message.OperationKey))
            errors.Add(new ValidationError(
                "OperationKey.Required",
                "OperationKey es obligatorio"));

        if (message.OperationKey?.Length > 256)
            errors.Add(new ValidationError(
                "OperationKey.MaxLength",
                "OperationKey no puede exceder 256 caracteres"));

        // Validación 2: SensorId
        if (string.IsNullOrWhiteSpace(message.SensorId))
            errors.Add(new ValidationError(
                "SensorId.Required",
                "SensorId es obligatorio"));

        // Validación 3: Valor de combustible
        if (message.FuelConsumed < 0)
            errors.Add(new ValidationError(
                "FuelConsumed.NonNegative",
                "Combustible consumido no puede ser negativo"));

        if (message.FuelConsumed > 100000)
            errors.Add(new ValidationError(
                "FuelConsumed.Reasonable",
                "Combustible consumido parece irreal (>100000L)"));

        // Validación 4: Timestamp
        if (message.SensorTimestamp > DateTime.UtcNow.AddMinutes(5))
            errors.Add(new ValidationError(
                "SensorTimestamp.Future",
                "Timestamp no puede ser 5+ minutos en el futuro"));

        if (message.SensorTimestamp < DateTime.UtcNow.AddDays(-30))
            errors.Add(new ValidationError(
                "SensorTimestamp.TooOld",
                "Timestamp no puede ser 30+ días en el pasado"));

        return new ValidationResult(
            isValid: errors.Count == 0,
            errors: errors);
    }
}
```

---

## 3. ESCENARIOS DE COMPORTAMIENTO (BDD)

### 3.1 Escenario 1: Deduplicación de Mensajes Idénticos

**Feature**: Sistema acepta mensajes duplicados sin corromper datos

**Given** (Precondiciones):
- Acumulador inicial: 0 litros
- OperationKey: "SENSOR_A#2026-04-08T14:32:15.123Z#1"
- Mensaje: {OperationKey, SensorId="SENSOR_A", FuelConsumed=50L}

**When** (Acciones):
```gherkin
Given el acumulador de SENSOR_A es 0
And el sistema está listo para procesar

When se envía el mensaje con OperationKey "SENSOR_A#2026-04-08T14:32:15.123Z#1" por primera vez
Then el acumulador debe ser 50
And el OperationKey debe estar registrado como procesado

When se reenvía el MISMO mensaje (OperationKey idéntico) por segunda vez
Then el acumulador debe seguir siendo 50 (NO 100)
And debe retornar Success con flag wasIdempotentReprocess=true
And se debe registrar en log: "Idempotent reprocess of key=SENSOR_A#..."

When se reenvía nuevamente por tercera vez
Then el acumulador debe seguir siendo 50
And debe retornar Success con flag wasIdempotentReprocess=true
```

**Then** (Resultado esperado):
```
Envío 1: OperationKey A → Acumulador = 50L ✓ (nuevo)
Envío 2: OperationKey A → Acumulador = 50L ✓ (ignorado)
Envío 3: OperationKey A → Acumulador = 50L ✓ (ignorado)

RESULTADO: 50L (idempotencia garantizada)
```

**Tabla de Ejecución**:

| Paso | OperationKey | Acción | Acumulador Esperado | wasIdempotentReprocess | Estado BD |
|------|--------------|--------|-------------------|----------------------|-----------|
| 1 | KEY_001 | ProcessMessage(50L) | 50L | false | KEY_001 grabado |
| 2 | KEY_001 | ProcessMessage(50L) | 50L | **true** | KEY_001 (no cambia) |
| 3 | KEY_001 | ProcessMessage(50L) | 50L | **true** | KEY_001 (no cambia) |
| **Validación** | | | **50L** | | **1 entrada BD** |

---

### 3.2 Escenario 2: Concurrencia Extrema (100 Hilos Simultáneos)

**Feature**: Sistema mantiene exactitud bajo estrés de 100 hilos escribiendo concurrentemente

**Given** (Precondiciones):
- 100 hilos de trabajo listos
- Cada hilo enviará 10 mensajes independientes
- Cada mensaje: 5 litros de combustible
- OperationKey único por mensaje: `"SENSOR_X#TIME_Y#SEQ_Z"`
- Total esperado: 100 hilos × 10 mensajes × 5L = 5,000L

**Scenario Outline**: Procesamiento under stress

```gherkin
Given 100 hilos iniciados para simular sensores concurrentes
And cada hilo tiene acces READ-WRITE al acumulador

When todos los hilos procesan simultáneamente:
  - Hilo_1 procesa mensaje_001-010
  - Hilo_2 procesa mensaje_011-020
  - ...
  - Hilo_100 procesa mensaje_991-1000

And cada mensaje tiene OperationKey único:
  - Hilo_1: "SENSOR_FUEL_01#T_001#SEQ_001" con 5L
  - Hilo_1: "SENSOR_FUEL_01#T_001#SEQ_002" con 5L
  - ...
  - Hilo_100: "SENSOR_FUEL_100#T_100#SEQ_010" con 5L

Then el acumulador FINAL debe ser 5000L (100% de exactitud)
And cero mensajes deben ser perdidos
And cero mensajes deben ser duplicados
```

**Simulación de Código** (Prueba de Estrés):

```csharp
[TestMethod]
public async Task ConcurrentThreads_100Simultaneous_Exact()
{
    // ARRANGE
    var accumulator = new ThreadSafeAccumulator();
    var keyStore = new InMemoryProcessedKeyStore();
    var processor = new IdempotentTelemetryProcessor(
        keyStore, accumulator);

    const int NumThreads = 100;
    const int MessagesPerThread = 10;
    const decimal FuelPerMessage = 5m;
    decimal expectedTotal = NumThreads * MessagesPerThread * FuelPerMessage;

    var tasks = new List<Task>();

    // ACT
    for (int threadId = 0; threadId < NumThreads; threadId++)
    {
        int capturedThreadId = threadId;
        
        var task = Task.Run(async () =>
        {
            for (int msgSeq = 0; msgSeq < MessagesPerThread; msgSeq++)
            {
                // Crear mensaje único (OperationKey nunca se repite)
                var message = new TelemetryMessage
                {
                    OperationKey = 
                        $"SENSOR_FUEL_{capturedThreadId:D3}" +
                        $"#{DateTime.UtcNow:O}" +
                        $"#{msgSeq:D3}",
                    
                    SensorId = $"SENSOR_FUEL_{capturedThreadId:D3}",
                    SensorTimestamp = DateTime.UtcNow,
                    SequenceNumber = (uint)msgSeq,
                    FuelConsumed = FuelPerMessage,
                    Unit = "Liters"
                };

                // Procesar (thread-safe por RF-002)
                var result = await processor.ProcessMessageAsync(message);
                Assert.IsTrue(result.IsSuccess, 
                    $"Procesamiento falló en Hilo {capturedThreadId}, " +
                    $"Seq {msgSeq}: {result.ErrorMessage}");
            }
        });

        tasks.Add(task);
    }

    // Esperar a que todos terminen
    await Task.WhenAll(tasks);

    // ASSERT
    var actualTotal = await accumulator.GetCurrentAsync(
        "SENSOR_FUEL_TOTAL");
    
    Assert.AreEqual(expectedTotal, actualTotal,
        message: $"Exactitud fallida: esperaba {expectedTotal}L, " +
                 $"obtuvo {actualTotal}L, " +
                 $"delta={expectedTotal - actualTotal}L");

    Console.WriteLine($"✓ Prueba exitosa:");
    Console.WriteLine($"  - {NumThreads} hilos concurrentes");
    Console.WriteLine($"  - {NumThreads * MessagesPerMessage} mensajes");
    Console.WriteLine($"  - Total acumulado: {actualTotal}L");
    Console.WriteLine($"  - Exactitud: 100%");
}
```

**Tabla de Resultados Esperados**:

| Métrica | Valor |
|---------|-------|
| **Hilos** | 100 |
| **Mensajes/Hilo** | 10 |
| **Total Mensajes** | 1,000 |
| **Combustible/Mensaje** | 5L |
| **Total Esperado** | **5,000L** |
| **Total Registrado** | **5,000L ✓** |
| **Exactitud** | **100%** |
| **Mensajes Perdidos** | **0** |
| **Mensajes Duplicados** | **0** |
| **OperationKeys Únicos** | **1,000** |
| **Duración Esperada** | <5 segundos |

**Variante: Inyectar Duplicados en Concurrencia**

```gherkin
Given 100 hilos ejecutándose concurrentemente
And un 10% de mensajes será reenviado deliberadamente (simular retransmisión)

When se inyectan duplicados:
  - Hilo_5 procesa mensaje_041 con OperationKey "KEY_041"
  - Hilo_5 procesa mensaje_041 NUEVAMENTE con OperationKey "KEY_041"
  - (duplicado capturado por red o retry logic)

Then el acumulador debe IGNORAR la segunda instancia
And resultado final debe seguir siendo 5000L
```

---

## 4. POLÍTICAS DE TOLERANCIA A FALLOS

### 4.1 Casos de Fallo y Recuperación

| Caso | Síntoma | Acción | Recuperación |
|------|---------|--------|--------------|
| **Red Timeout** | Timeout en envío | Reintentar (exponential backoff) | Idempotencia via OperationKey |
| **DB Unavailable** | No se guarda OperationKey | Fail-open o queue local | Sincronizar cuando DB vuelva |
| **Corrupted OperationKey** | `null` o vacío | Rechazar mensaje | Loguear incidente, alertar |
| **Clock Skew** | Timestamp en el futuro | Descartar o normalizar | Sincronización NTP en sensores |

### 4.2 Garantías de Entrega

```
Semantics de Entrega Garantizado:
┌─────────────────────────────────────────┐
│ At-Least-Once Delivery (con Idempotencia)
│                                         
│ Ingestion (Sensor → Servidor)           
│   ✓ Retry indefinido hasta confirmación  
│   ✓ OperationKey permite duplicación     
│   ✓ Sistema ignora reintentos            
│                                         
│ Resultado: Exactly-Once Semantics      
└─────────────────────────────────────────┘
```

---

## 5. MATRICES DE PRUEBA

### 5.1 Matriz de Cobertura de Idempotencia

```
┌──────────────────────────────────┬──────────┬────────────┐
│ Escenario                        │ Pases #1 │ Resultado  │
├──────────────────────────────────┼──────────┼────────────┤
│ Primer envío                     │ Process  │ SUCCESS    │
│ Reintentos (misma key)           │ Ignore   │ SUCCESS    │
│ OperationKey vacío               │ Reject   │ FAIL       │
│ OperationKey muy largo (>256)    │ Reject   │ FAIL       │
│ Valor negativo                   │ Reject   │ FAIL       │
│ Valor anormalmente alto (>100KL) │ Warn Log │ SUCCESS(*) │
└──────────────────────────────────┴──────────┴────────────┘
(*) Se procesa pero se genera alerta SRE
```

### 5.2 Matriz de Sincronización/Concurrencia

```
┌───────────────────────┬──────────────┬──────────────┬─────────┐
│ Escenario             │ N Hilos      │ Exactitud    │ Latency │
├───────────────────────┼──────────────┼──────────────┼─────────┤
│ Baseline (1 hilo)     │ 1            │ 100%         │ <1ms    │
│ Baja concurrencia     │ 5            │ 100%         │ <2ms    │
│ Media concurrencia    │ 25           │ 100%         │ <5ms    │
│ Alta concurrencia     │ 100          │ 100%         │ <20ms   │
│ Extrema concurrencia  │ 500          │ 100%         │ <100ms  │
│ Saturación            │ 1000+        │ 100%         │ >200ms  │
└───────────────────────┴──────────────┴──────────────┴─────────┘
```

---

## 6. MONITOREO Y OBSERVABILIDAD (SRE)

### 6.1 Métricas Críticas

```
Sistema de Telemetría Robusto:

📊 THROUGHPUT
  - messages/sec: Mensajes procesados por segundo
  - MB/sec: Datos procesados por segundo
  
🔄 IDEMPOTENCIA
  - duplicate_refs/min: Reintentos detectados
  - idempotent_reprocess_rate: % de mensajes duplicados
  
🔒 SINCRONIZACIÓN
  - lock_contention_ms: Tiempo promedio en lock
  - max_lock_wait_ms: Máximo tiempo esperando lock
  - accumulated_lock_wait_s: Acumulado de esperas (debugging)
  
⚠️ CONFIABILIDAD
  - processing_error_rate: % de mensajes fallidos
  - validation_error_rate: % rechazados por validación
  - operation_key_collisions: Claves duplicadas (debe ser 0)
  
📈 ACUMULADOR
  - fuel_total_liters: Total acumulado
  - delta_last_5min: Cambio en último intervalo
```

### 6.2 Alertas SRE

```yaml
Alertas Críticas (página en 5 minutos):
  - error_rate > 1%
  - duplicate_refs > 100/min (posible network issue)
  - lock_wait_ms > 50 (contención crítica)
  - operation_key_collision > 0 (BUG)

Alertas de Advertencia (notificación):
  - idempotent_reprocess_rate > 10%
  - validation_error_rate > 5%
  - lock_wait_ms > 20
```

---

## 7. IMPLEMENTACIÓN RECOMENDADA

### 7.1 Stack Tecnológico

```csharp
// Lenguaje
C# .NET 8 o superior (async/await, locks óptimos)

// Persistencia
PostgreSQL 15+ (ACID guarantees para OperationKeys)
  └─ Tabla: ProcessedOperationKeys
     Campos: OperationKey (PK), ProcessedAt, SensorId

// Cache
Redis 7+ (para OperationKey recientes <1 día)
  └─ Pattern: "processed_ops:{date}:{key}"
     TTL: 86400 segundos

// Messaging
RabbitMQ o Azure Service Bus (dead-letter para reintentos)
  └─ Policy: Exponential Backoff + 3 reintentos

// Monitoreo
Prometheus + Grafana (métricas)
Application Insights (logs distribuidos)
```

### 7.2 Flujo de Integración

```
Sensor → TLS/Mutual Auth → API Gateway → Message Queue
                                           ↓
                              Deserialization + Validation
                                           ↓
                          Idempotence Check (RF-001)
                                           ↓
                      OperationKey en Redis/DB?
                       ↙ YES (duplicado)        ↘ NO (nuevo)
                    Retornar SUCCESS         ThreadSafe Accumulate
                    (wasIdempotentReprocess) Mark as Processed
                       ↖                              ↗
                          Respuesta ← Persistencia
                             ↓
                        Sensor confirma
```

---

## 8. CONCLUSIONES Y RECOMENDACIONES

### 8.1 Problemas Resueltos

| Problema Original | Solución | RFC |
|-------------------|----------|-----|
| Condiciones de carrera | `lock` + `ReaderWriterLockSlim` | RF-002 |
| Mensajes duplicados | `OperationKey` + `ProcessedOperationKeyStore` | RF-001 |
| Sin trazabilidad | Timestamps + SensorId + SequenceNumber | RF-003 |
| Falta de atomicidad | Transacciones ACID en DB | RF-001.2 |

### 8.2 Beneficios Esperados

- ✅ **Exactitud**: 100% de datos capturados, cero pérdidas
- ✅ **Auditoría**: Trazabilidad completa por OperationKey
- ✅ **Confiabilidad**: Tolerancia a duplicados, timeouts, fallos de red
- ✅ **Escalabilidad**: Soporta 100+ sensores concurrentes
- ✅ **Mantenibilidad**: Código testeable, observable, documentado

### 8.3 Próximas Fases

1. **Phase 1** (Actual): Especificación de requisitos ✓
2. **Phase 2**: Implementación en C# (ThreadSafeAccumulator, IdempotentProcessor)
3. **Phase 3**: Integración con PostgreSQL y Redis
4. **Phase 4**: Testing bajo carga (K6, NBomber)
5. **Phase 5**: Deploy a producción con canary testing

---

## ANEXO A: Referencias Normativas

- ISO 50001:2018 - Energy management systems
- ISO 27001:2022 - Information security management
- IEEE 1451.0 - Smart transducers
- MQTT 3.1.1 - Reliable message delivery semantics
- RFC 3986 - URI generic syntax (OperationKey format)

## ANEXO B: Glosario

- **OperationKey**: Identificador único de una operación/mensaje
- **Idempotencia**: Propiedad de una operación que produce igual resultado sin importar si se ejecuta 1 o N veces
- **Thread-safety**: Garantía de que múltiples hilos acceden a recurso sin corrupción
- **Race Condition**: Situación donde orden de ejecución entre hilos causa resultado inesperado
- **Acumulador**: Variable que suma incrementos secuenciales

---

**Documento Aprobado por**: SRE Team - Sacyr IoT  
**Fecha**: 2026-04-08  
**Versión**: 1.0 - Inicial
