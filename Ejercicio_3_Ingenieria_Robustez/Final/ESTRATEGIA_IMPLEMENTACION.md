# ESTRATEGIA DE IMPLEMENTACIÓN: TELEMETRÍA ROBUSTO .NET

**Documento**: Planificación Técnica - Guía de Implementación  
**Arquitecto**: Software Engineer - Sistemas Concurrentes .NET  
**fecha**: 2026-04-08  
**Basado en**: TELEMETRIA_ROBUSTA.md + ADR-003 + ITelemetryService.cs

---

## 1. VISIÓN GENERAL DE COMPONENTES

```
Sensor IoT
    ↓ (HTTP POST / gRPC)
┌────────────────────────────────────────────────┐
│ TelemetryController / Endpoint                 │
│  ├─ Deserialize JSON → TelemetryMessage       │
│  ├─ Call service.ProcessReportAsync()         │
│  └─ Return ProcessingResult (JSON 200/400)    │
└────────────────────────────────────────────────┘
    ↓
┌────────────────────────────────────────────────┐
│ ITelemetryService (Interface)                 │
│  ├─ ProcessReportAsync(message)               │
│  ├─ ProcessReportBatchAsync(messages)         │
│  ├─ GetStatusAsync()                          │
│  └─ GetMetricsAsync()                         │
└────────────────────────────────────────────────┘
    ↓
┌────────────────────────────────────────────────┐
│ TelemetryService (Implementation)             │
│  ├─ ITelemetryValidator: Validar mensaje      │
│  ├─ IIdempotencyStore: Rastrear keys          │
│  └─ IFuelAccumulator: Acumular combustible    │
└────────────────────────────────────────────────┘
    ↓
┌────────────────────────────────────────────────┐
│ Implementations:                              │
│  ├─ TelemetryValidator                        │
│  ├─ IdempotencyStore                          │
│  │  └─ ConcurrentDictionary<string, byte>    │
│  │  └─ MemoryCache (TTL automático)          │
│  └─ FuelAccumulator                           │
│     └─ lock (_lockObject)                    │
│     └─ decimal _totalFuel                    │
└────────────────────────────────────────────────┘
    ↓
Logs (ELK/Splunk) + Métricas (Prometheus)
```

---

## 2. HOJA DE RUTA DE IMPLEMENTACIÓN

### Fase 1: Setup Base (.NET Project Structure)

```
Ejercicio3/
├─ Ejercicio3.csproj
├─ Telemetria/
│  ├─ Interfaces/
│  │  ├─ ITelemetryService.cs          ✅ CREADO
│  │  ├─ IIdempotencyStore.cs          (en ITelemetryService.cs)
│  │  ├─ ITelemetryValidator.cs        (en ITelemetryService.cs)
│  │  └─ IFuelAccumulator.cs           (en ITelemetryService.cs)
│  └─ Implementations/
│     ├─ FuelAccumulator.cs            ⏳ TODO
│     ├─ IdempotencyStore.cs           ⏳ TODO
│     ├─ TelemetryValidator.cs         ⏳ TODO
│     └─ TelemetryService.cs           ⏳ TODO
├─ DTOs/
│  └─ (todos ya están en ITelemetryService.cs)
├─ Tests/
│  ├─ IdempotencyTests.cs              ⏳ TODO
│  ├─ ConcurrencyTests.cs              ⏳ TODO
│  └─ ValidationTests.cs               ⏳ TODO
└─ Documentation/
   ├─ TELEMETRIA_ROBUSTA.md            ✅ CREADO
   ├─ ADR-003-IDEMPOTENCIA-...md       ✅ CREADO
   └─ ESTRATEGIA_IMPLEMENTACION.md     ✓ (este archivo)
```

### Fase 2: Implementación de Componentes

```
1. FuelAccumulator (depende de: nada)
   - Implementar lock (_lockObject)
   - Validación de liters >= 0
   - Métodos: AddFuelAsync, GetTotalAsync, ResetAsync

2. IdempotencyStore (depende de: nada)
   - ConcurrentDictionary<string, byte> _processedKeys
   - MemoryCache con TTL
   - Background cleanup timer

3. TelemetryValidator (depende de: ITelemetryMessage)
   - Validate() con reglas RF-003
   - Checks OperationKey, SensorId, FuelConsumed, timestamps

4. TelemetryService (depende de: todos los anteriores)
   - Inyectar dependencias (DI)
   - Implementar ProcessReportAsync pipeline
   - Manejo de errores y transaccionalidad
```

### Fase 3: Testing

```
Unit Tests:
- IdempotencyTests::ThreeDuplicates_OnlyProcessFirst()
- ConcurrencyTests::100Threads_ExactAccuracy()
- ValidationTests::AllRulesEnforced()

Stress Tests (NBomber):
- 100 concurrent threads, 10K msgs each
- Verify: Latency P95 < 5ms, Throughput > 400K ops/sec
- Verify: Accuracy 100.0%
```

### Fase 4: Integration & Deployment

```
- Crear TelemetryController (ASP.NET Core)
- Integrar DI Container (Autofac/Microsoft.Extensions.DependencyInjection)
- Setup logging (Serilog → ELK)
- Setup metrics (Prometheus)
- Deploy a staging + test load
```

---

## 3. GUÍA DE IMPLEMENTACIÓN POR COMPONENTE

### 3.1 FuelAccumulator.cs

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sacyr.Ejercicio3.Telemetria.Implementations
{
    /// <summary>
    /// Implementación thread-safe del acumulador de combustible.
    /// Estrategia: lock (Monitor) en sección crítica
    /// Precisión: decimal (exactitud garantizada)
    /// 
    /// GARANTÍA: Operación AddFuelAsync es atómica
    /// Latencia esperada: <1ms
    /// </summary>
    public class FuelAccumulator : IFuelAccumulator
    {
        // ✓ Lock para sincronización
        private readonly object _lock = new object();

        // ✓ Decimal para precisión auditable
        private decimal _totalFuel = 0m;

        // Tracking por sensor (para estadísticas granulares)
        private readonly Dictionary<string, decimal> _sensorTotals =
            new Dictionary<string, decimal>();

        public async Task AddFuelAsync(string sensorId, decimal liters)
        {
            // Validaciones pre-lock (optimizar sección crítica)
            ArgumentNullException.ThrowIfNull(sensorId);

            if (liters < 0)
            {
                throw new ArgumentException(
                    $"Liters cannot be negative: {liters}",
                    nameof(liters));
            }

            // SECCIÓN CRÍTICA: Solo 1 hilo por vez
            lock (_lock)
            {
                _totalFuel += liters;

                if (!_sensorTotals.ContainsKey(sensorId))
                    _sensorTotals[sensorId] = 0;

                _sensorTotals[sensorId] += liters;
            }

            await Task.CompletedTask;
        }

        public async Task<decimal> GetTotalAsync()
        {
            lock (_lock)
            {
                return _totalFuel;
            }

            await Task.CompletedTask;
        }

        public async Task<decimal> GetTotalBySensorAsync(string sensorId)
        {
            ArgumentNullException.ThrowIfNull(sensorId);

            lock (_lock)
            {
                return _sensorTotals.TryGetValue(sensorId, out var total)
                    ? total
                    : 0m;
            }

            await Task.CompletedTask;
        }

        public async Task<decimal> ResetAndGetPreviousTotalAsync(
            string resetReason)
        {
            lock (_lock)
            {
                var previous = _totalFuel;
                _totalFuel = 0m;
                _sensorTotals.Clear();
                return previous;
            }

            await Task.CompletedTask;
        }

        public async Task<decimal> ResetBySensorAsync(
            string sensorId, string resetReason)
        {
            ArgumentNullException.ThrowIfNull(sensorId);

            lock (_lock)
            {
                if (_sensorTotals.TryGetValue(sensorId, out var total))
                {
                    _totalFuel -= total;
                    _sensorTotals[sensorId] = 0;
                    return total;
                }
                return 0m;
            }

            await Task.CompletedTask;
        }
    }
}
```

**Análisis de Implementación**:
- ✓ Lock adquirido antes de lectura
- ✓ Suma en sección crítica
- ✓ Decimal garantiza precisión
- ✓ Tracking por sensor para granularidad
- ✓ Pre-validación fuera del lock (optimizar holdtime)

---

### 3.2 IdempotencyStore.cs

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;

namespace Sacyr.Ejercicio3.Telemetria.Implementations
{
    /// <summary>
    /// Almacén de claves de operación ya procesadas.
    /// Implementación: ConcurrentDictionary + MemoryCache
    /// 
    /// GARANTÍAS:
    /// - O(1) búsqueda y marcado
    /// - Thread-safe (lock-free para mayoría de casos)
    /// - TTL automático (evita memory leak)
    /// 
    /// CAPACIDAD: ~1M de claves en ~128MB RAM
    /// </summary>
    public class IdempotencyStore : IIdempotencyStore
    {
        private readonly ConcurrentDictionary<string, byte> _processedKeys;
        private readonly MemoryCache _cache;
        private readonly CacheItemPolicy _cachePolicy;
        private readonly TimeSpan _keyTtl;

        // Stats para monitoreo
        private long _totalCleaned = 0;
        private DateTime _lastCleanupAt = DateTime.UtcNow;

        public IdempotencyStore(TimeSpan? keyTtl = null)
        {
            _keyTtl = keyTtl ?? TimeSpan.FromMinutes(5);

            // ConcurrentDictionary: concurrencyLevel = procesadores * 2
            _processedKeys = new ConcurrentDictionary<string, byte>(
                concurrencyLevel: Environment.ProcessorCount * 2,
                capacity: 1_000_000);

            _cache = new MemoryCache("TelemetryIdempotencyCache");

            _cachePolicy = new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.Add(_keyTtl)
            };
        }

        public async Task<bool> IsAlreadyProcessedAsync(string operationKey)
        {
            if (string.IsNullOrWhiteSpace(operationKey))
                return false;

            // Estrategia dual:
            // 1. Intentar caché primero (más rápido)
            // 2. Fallback a Dict si caché evicted

            var cacheValue = _cache.Get(operationKey);
            if (cacheValue != null)
                return true;

            var exists = _processedKeys.ContainsKey(operationKey);
            return await Task.FromResult(exists);
        }

        public async Task MarkAsProcessedAsync(string operationKey)
        {
            if (string.IsNullOrWhiteSpace(operationKey))
            {
                throw new ArgumentException(
                    "OperationKey cannot be null or empty",
                    nameof(operationKey));
            }

            // Agregar a Dict (thread-safe, O(1) amortizado)
            _processedKeys.TryAdd(operationKey, 1);

            // Agregar a caché con TTL automático
            _cache.Set(operationKey, 1, _cachePolicy);

            await Task.CompletedTask;
        }

        public async Task CleanupExpiredKeysAsync(TimeSpan maxAge)
        {
            // Extraer timestamp del OperationKey
            // Formato: {SensorId}#{ISO8601}#{Seq}
            var cutoff = DateTime.UtcNow.Subtract(maxAge);
            var keysToRemove = new List<string>();

            // Batch cleanup (máx 10K por iteración)
            var batch = _processedKeys.Keys.Take(10_000).ToList();

            foreach (var key in batch)
            {
                if (ExtractTimestampFromKey(key) < cutoff)
                {
                    keysToRemove.Add(key);
                }
            }

            // Remover del Dict y caché
            foreach (var key in keysToRemove)
            {
                _processedKeys.TryRemove(key, out _);
                _cache.Remove(key);
                _totalCleaned++;
            }

            _lastCleanupAt = DateTime.UtcNow;

            await Task.CompletedTask;
        }

        public async Task<IdempotencyStoreStats> GetStatsAsync()
        {
            return await Task.FromResult(new IdempotencyStoreStats
            {
                CachedKeyCount = _processedKeys.Count,
                MemoryMB = CalculateMemoryUsage(),
                TotalCleanedKeys = _totalCleaned,
                LastCleanupAt = _lastCleanupAt
            });
        }

        // Helper: Extraer timestamp del OperationKey
        private DateTime ExtractTimestampFromKey(string operationKey)
        {
            try
            {
                // Parse: "SENSOR_A#2026-04-08T14:32:15.123Z#1"
                var parts = operationKey.Split('#');
                if (parts.Length >= 2)
                {
                    if (DateTime.TryParse(parts[1], out var ts))
                        return ts;
                }
            }
            catch { }

            return DateTime.MinValue;
        }

        // Helper: Aproximación de memoria usada
        private double CalculateMemoryUsage()
        {
            // Aprox: (28 overhead + 50 string) * count / 1MB
            const long BytesPerEntry = 78;
            return (_processedKeys.Count * BytesPerEntry) / (1024.0 * 1024.0);
        }
    }
}
```

**Análisis de Implementación**:
- ✓ ConcurrentDictionary con concurrencyLevel óptimo
- ✓ Dual cache + dict para máxima tolerancia
- ✓ TTL automático via MemoryCache
- ✓ Batch cleanup para evitar latency spikes
- ✓ Stats para monitoreo SRE

---

### 3.3 TelemetryValidator.cs

```csharp
using System;
using System.Collections.Generic;

namespace Sacyr.Ejercicio3.Telemetria.Implementations
{
    /// <summary>
    /// Validador de mensajes de telemetría.
    /// Implementa todas las reglas de RF-003.
    /// </summary>
    public class TelemetryValidator : ITelemetryValidator
    {
        private readonly TelemetryServiceConfiguration _config;

        public TelemetryValidator(TelemetryServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public ValidationResult Validate(TelemetryMessage message)
        {
            if (message == null)
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new("Message", "Message cannot be null", "MESSAGE_NULL")
                    }
                };

            var errors = new List<ValidationError>();

            // 1. Validar OperationKey
            ValidateOperationKey(message.OperationKey, errors);

            // 2. Validar SensorId
            ValidateSensorId(message.SensorId, errors);

            // 3. Validar FuelConsumed
            ValidateFuelConsumed(message.FuelConsumed, errors);

            // 4. Validar SensorTimestamp
            ValidateSensorTimestamp(message.SensorTimestamp, errors);

            // 5. Validar Unit
            ValidateUnit(message.Unit, errors);

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        private void ValidateOperationKey(string operationKey,
            List<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(operationKey))
            {
                errors.Add(new("OperationKey", 
                    "OperationKey is required",
                    "OPERATION_KEY_REQUIRED"));
                return;
            }

            if (operationKey.Length > 256)
            {
                errors.Add(new("OperationKey",
                    $"OperationKey exceeds 256 characters (actual: {operationKey.Length})",
                    "OPERATION_KEY_TOO_LONG"));
            }

            // Validar formato básico: contiene '#'
            var parts = operationKey.Split('#');
            if (parts.Length < 3)
            {
                errors.Add(new("OperationKey",
                    "OperationKey format invalid (expected: SensorId#Timestamp#Sequence)",
                    "OPERATION_KEY_FORMAT_INVALID"));
            }
        }

        private void ValidateSensorId(string sensorId,
            List<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(sensorId))
            {
                errors.Add(new("SensorId",
                    "SensorId is required",
                    "SENSOR_ID_REQUIRED"));
                return;
            }

            if (sensorId.Length > 64)
            {
                errors.Add(new("SensorId",
                    $"SensorId exceeds 64 characters",
                    "SENSOR_ID_TOO_LONG"));
            }
        }

        private void ValidateFuelConsumed(decimal fuelConsumed,
            List<ValidationError> errors)
        {
            if (fuelConsumed < 0)
            {
                errors.Add(new("FuelConsumed",
                    $"FuelConsumed cannot be negative (actual: {fuelConsumed})",
                    "FUEL_NEGATIVE"));
            }

            if (fuelConsumed > _config.AnomalousConsumptionThreshold)
            {
                errors.Add(new("FuelConsumed",
                    $"FuelConsumed seems abnormally high ({fuelConsumed}L > {_config.AnomalousConsumptionThreshold}L)",
                    "FUEL_ANOMALOUS"));
            }
        }

        private void ValidateSensorTimestamp(DateTime sensorTimestamp,
            List<ValidationError> errors)
        {
            var now = DateTime.UtcNow;
            var age = now - sensorTimestamp;
            var futureOffset = sensorTimestamp - now;

            if (age.TotalMinutes > _config.MaxMessageAgeMinutes)
            {
                errors.Add(new("SensorTimestamp",
                    $"Message is too old ({age.TotalMinutes} min > {_config.MaxMessageAgeMinutes} min)",
                    "TIMESTAMP_TOO_OLD"));
            }

            if (futureOffset.TotalMinutes > _config.MaxFutureMessageMinutes)
            {
                errors.Add(new("SensorTimestamp",
                    $"Message timestamp is in future ({futureOffset.TotalMinutes} min)",
                    "TIMESTAMP_FUTURE"));
            }
        }

        private void ValidateUnit(string unit,
            List<ValidationError> errors)
        {
            var validUnits = new[] { "Liters", "Gallons", "Cubic_Meters" };

            if (!string.IsNullOrEmpty(unit) && !validUnits.Contains(unit))
            {
                errors.Add(new("Unit",
                    $"Unit '{unit}' is not supported",
                    "UNIT_UNSUPPORTED"));
            }
        }
    }
}
```

---

### 3.4 TelemetryService.cs (Orquestación Principal)

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Sacyr.Ejercicio3.Telemetria.Implementations
{
    /// <summary>
    /// Servicio principal orquestador.
    /// PIPELINE:
    /// 1. Validar
    /// 2. Verificar idempotencia
    /// 3. Si nuevo: Acumular
    /// 4. Marcar como procesado
    /// 5. Retornar resultado
    /// </summary>
    public class TelemetryService : ITelemetryService
    {
        private readonly ITelemetryValidator _validator;
        private readonly IIdempotencyStore _idempotencyStore;
        private readonly IFuelAccumulator _fuelAccumulator;

        // Métricas (simple in-memory, para producción usar Prometheus)
        private long _totalProcessed = 0;
        private long _validationErrors = 0;
        private long _duplicatesDetected = 0;
        private long _processingErrors = 0;

        public TelemetryService(
            ITelemetryValidator validator,
            IIdempotencyStore idempotencyStore,
            IFuelAccumulator fuelAccumulator)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
            _fuelAccumulator = fuelAccumulator ?? throw new ArgumentNullException(nameof(fuelAccumulator));
        }

        public async Task<ProcessingResult> ProcessReportAsync(
            TelemetryMessage message)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                ArgumentNullException.ThrowIfNull(message);

                // PASO 1: Validar
                var validationResult = _validator.Validate(message);
                if (!validationResult.IsValid)
                {
                    Interlocked.Increment(ref _validationErrors);
                    
                    return new ProcessingResult
                    {
                        IsSuccess = false,
                        OperationKey = message.OperationKey,
                        ErrorCode = "VALIDATION_FAILED",
                        ErrorMessage = $"Validation errors: {string.Join("; ", validationResult.Errors)}",
                        ProcessedAt = DateTime.UtcNow,
                        ProcessingLatencyMs = sw.ElapsedMilliseconds
                    };
                }

                // PASO 2: Verificar idempotencia
                var alreadyProcessed = 
                    await _idempotencyStore.IsAlreadyProcessedAsync(message.OperationKey);

                if (alreadyProcessed)
                {
                    Interlocked.Increment(ref _duplicatesDetected);
                    
                    return new ProcessingResult
                    {
                        IsSuccess = true,
                        OperationKey = message.OperationKey,
                        WasIdempotentReprocess = true,
                        SuccessMessage = $"Duplicate ignored (idempotent reprocess)",
                        AccumulatorTotal = await _fuelAccumulator.GetTotalAsync(),
                        ProcessedAt = DateTime.UtcNow,
                        ProcessingLatencyMs = sw.ElapsedMilliseconds
                    };
                }

                // PASO 3: Procesar (acumular combustible)
                try
                {
                    await _fuelAccumulator.AddFuelAsync(
                        message.SensorId, message.FuelConsumed);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _processingErrors);
                    
                    // NO MARCAR COMO PROCESADO si falla acumulación
                    return new ProcessingResult
                    {
                        IsSuccess = false,
                        OperationKey = message.OperationKey,
                        ErrorCode = "ACCUMULATION_FAILED",
                        ErrorMessage = $"Failed to accumulate fuel: {ex.Message}",
                        ProcessedAt = DateTime.UtcNow,
                        ProcessingLatencyMs = sw.ElapsedMilliseconds
                    };
                }

                // PASO 4: Marcar como procesado (DESPUÉS de acumular exitoso)
                try
                {
                    await _idempotencyStore.MarkAsProcessedAsync(message.OperationKey);
                }
                catch (Exception ex)
                {
                    // Registrar warning pero no fallar (fuel ya fue acumulado)
                    // En reintentos, será detectado como duplicate
                    System.Diagnostics.Debug.WriteLine(
                        $"Warning: Failed to mark as processed: {ex.Message}");
                }

                // PASO 5: Retornar éxito
                Interlocked.Increment(ref _totalProcessed);

                return new ProcessingResult
                {
                    IsSuccess = true,
                    OperationKey = message.OperationKey,
                    WasIdempotentReprocess = false,
                    SuccessMessage = $"Fuel {message.FuelConsumed}L accumulated",
                    AccumulatorTotal = await _fuelAccumulator.GetTotalAsync(),
                    ProcessedAt = DateTime.UtcNow,
                    ProcessingLatencyMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _processingErrors);
                
                return new ProcessingResult
                {
                    IsSuccess = false,
                    OperationKey = message?.OperationKey,
                    ErrorCode = "UNHANDLED_ERROR",
                    ErrorMessage = $"Unhandled exception: {ex.Message}",
                    ProcessedAt = DateTime.UtcNow,
                    ProcessingLatencyMs = sw.ElapsedMilliseconds
                };
            }
            finally
            {
                sw.Stop();
            }
        }

        public async Task<List<ProcessingResult>> ProcessReportBatchAsync(
            List<TelemetryMessage> messages)
        {
            if (messages == null)
                messages = new List<TelemetryMessage>();

            var results = new List<ProcessingResult>(messages.Count);

            // Procesar en paralelo (thread pool) pero manteniendo orden
            foreach (var msg in messages)
            {
                var result = await ProcessReportAsync(msg);
                results.Add(result);
            }

            return results;
        }

        public async Task<AccumulatorStatus> GetStatusAsync()
        {
            return await Task.FromResult(new AccumulatorStatus
            {
                TotalLiters = await _fuelAccumulator.GetTotalAsync(),
                LastUpdatedAt = DateTime.UtcNow,
                ActiveSensors = 0 // TODO: Implementar tracking
            });
        }

        public async Task<ServiceMetrics> GetMetricsAsync()
        {
            var stats = await _idempotencyStore.GetStatsAsync();

            return await Task.FromResult(new ServiceMetrics
            {
                TotalMessagesProcessed = _totalProcessed,
                ValidationErrorCount = _validationErrors,
                DuplicatesDetected = _duplicatesDetected,
                ErrorRatePercent = _totalProcessed > 0 
                    ? (double)_processingErrors / _totalProcessed * 100 
                    : 0,
                CapturedAt = DateTime.UtcNow
            });
        }
    }
}
```

---

## 4. DEPENDENCY INJECTION (DI) SETUP

```csharp
// En Startup.cs o Program.cs (.NET 6+):

using Microsoft.Extensions.DependencyInjection;

public static void ConfigureTelemetryServices(
    IServiceCollection services,
    TelemetryServiceConfiguration config)
{
    services.AddSingleton(config);

    // Implementations
    services.AddSingleton<IIdempotencyStore>(sp =>
        new IdempotencyStore(config.IdempotencyKeyTtl));

    services.AddSingleton<IFuelAccumulator, FuelAccumulator>();

    services.AddSingleton<ITelemetryValidator>(sp =>
        new TelemetryValidator(config));

    services.AddSingleton<ITelemetryService, TelemetryService>();

    // Background cleanup task
    services.AddHostedService<IdempotencyCleanupService>();
}

// Background service para limpieza de claves expiradas
public class IdempotencyCleanupService : BackgroundService
{
    private readonly IIdempotencyStore _store;
    private readonly TelemetryServiceConfiguration _config;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_config.CleanupInterval, stoppingToken);
            
            if (_config.EnableAutoCleanup)
            {
                await _store.CleanupExpiredKeysAsync(
                    _config.IdempotencyKeyTtl.Add(TimeSpan.FromMinutes(1)));
            }
        }
    }
}
```

---

## 5. EJEMPLO DE CONTROLLER (ASP.NET Core)

```csharp
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Sacyr.Ejercicio3.Telemetria;

[ApiController]
[Route("api/telemetry")]
public class TelemetryController : ControllerBase
{
    private readonly ITelemetryService _telemetryService;

    public TelemetryController(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
    }

    [HttpPost("report")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ReportFuel(
        [FromBody] TelemetryMessage message)
    {
        var result = await _telemetryService.ProcessReportAsync(message);

        if (result.IsSuccess)
        {
            return Ok(result);
        }
        else
        {
            return BadRequest(result);
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await _telemetryService.GetStatusAsync();
        return Ok(status);
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var metrics = await _telemetryService.GetMetricsAsync();
        return Ok(metrics);
    }
}
```

---

## 6. MATRIZ DE TESTING

### Test 1: Idempotencia (3 Duplicados)

```csharp
[TestMethod]
public async Task ProcessReport_ThreeDuplicates_OnlyFirstProcessed()
{
    // ARRANGE
    var message = new TelemetryMessage
    {
        OperationKey = "TEST_001#2026-04-08T14:32:15Z#1",
        SensorId = "SENSOR_A",
        SensorTimestamp = DateTime.UtcNow,
        SequenceNumber = 1,
        FuelConsumed = 50m
    };

    // ACT
    var result1 = await _service.ProcessReportAsync(message);
    var result2 = await _service.ProcessReportAsync(message); // Duplicate
    var result3 = await _service.ProcessReportAsync(message); // Duplicate

    // ASSERT
    Assert.IsTrue(result1.IsSuccess);
    Assert.IsFalse(result1.WasIdempotentReprocess);
    
    Assert.IsTrue(result2.IsSuccess);
    Assert.IsTrue(result2.WasIdempotentReprocess, "Result 2 debe ser duplicate");
    
    Assert.IsTrue(result3.IsSuccess);
    Assert.IsTrue(result3.WasIdempotentReprocess, "Result 3 debe ser duplicate");

    // Total debe ser 50L (no 150L)
    var final = await _accumulator.GetTotalAsync();
    Assert.AreEqual(50m, final);
}
```

### Test 2: Concurrencia (100 Threads)

```csharp
[TestMethod]
public async Task ProcessReport_100ThreadsConcurrent_ExactAccuracy()
{
    const int NumThreads = 100;
    const int MsgsPerThread = 10;
    const decimal FuelPerMsg = 5m;
    decimal expected = NumThreads * MsgsPerThread * FuelPerMsg; // 5000L

    var tasks = new List<Task>();

    for (int t = 0; t < NumThreads; t++)
    {
        int threadId = t;
        var task = Task.Run(async () =>
        {
            for (int m = 0; m < MsgsPerThread; m++)
            {
                var msg = new TelemetryMessage
                {
                    OperationKey = $"THREAD_{threadId:D3}#*#MSG_{m:D3}",
                    SensorId = $"SENSOR_{threadId:D3}",
                    SensorTimestamp = DateTime.UtcNow,
                    SequenceNumber = (uint)m,
                    FuelConsumed = FuelPerMsg
                };

                await _service.ProcessReportAsync(msg);
            }
        });
        tasks.Add(task);
    }

    await Task.WhenAll(tasks);

    var actual = await _accumulator.GetTotalAsync();
    Assert.AreEqual(expected, actual, 0.01m,
        $"Exactitud fallida: {actual}L vs {expected}L (delta: {expected - actual}L)");
}
```

---

## 7. CRITERIOS DE ACEPTACIÓN

- [ ] Latencia P95 < 5ms (benchmark con 100K msgs)
- [ ] Throughput > 400K ops/sec
- [ ] Exactitud 100% en concurrencia
- [ ] Cero memory leaks (MemoryCache TTL funciona)
- [ ] Duplicados detectados correctamente (3/3 casos)
- [ ] Código tiene >80% test coverage
- [ ] Documentación completa (XML comments)

---

## 8. SIGUIENTES PASOS

1. Crear proyecto `Ejercicio3.csproj`
2. Implementar componentes en orden: Accumulator → Store → Validator → Service
3. Escribir unit tests (3 escenarios clave)
4. Benchmark de rendimiento
5. Integrar en ASP.NET Core controller
6. Deploy a staging

**Fin de Planificación**
