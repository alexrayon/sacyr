/// <summary>
/// Contrato del Servicio de Telemetría Robusto
/// Especificación: TELEMETRIA_ROBUSTA.md, Sección 2
/// Arquitectura: ADR-003-IDEMPOTENCIA-SINCRONIZACION.md
/// 
/// Interfaz principal para procesamiento de telemetría IoT con garantías de:
/// - Idempotencia: OperationKey deduplicación
/// - Sincronización: Thread-safe usando lock para acumulador
/// - Precisión: decimal para exactitud auditable
/// </summary>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sacyr.Ejercicio3.Telemetria
{
    /// <summary>
    /// DTO que representa un mensaje de telemetría de combustible.
    /// Debe ser deserializable desde JSON y validable según RF-003.
    /// </summary>
    public class TelemetryMessage
    {
        /// <summary>
        /// OperationKey: Identificador único de la operación.
        /// Formato recomendado: {SensorId}#{ISO8601Timestamp}#{SequenceNumber}
        /// Ejemplo: "SENSOR_COMBUSTIBLE_01#2026-04-08T14:32:15.123Z#1"
        /// 
        /// Usado para: Deduplicación idempotente de mensajes
        /// Requerimiento: Único globalmente, obligatorio, Max 256 caracteres
        /// </summary>
        public string OperationKey { get; set; }

        /// <summary>
        /// ID del sensor que reporta (ej: "SENSOR_COMBUSTIBLE_01")
        /// Usado para: Auditoría, correlación, reseteo por sensor
        /// Requerimiento: Obligatorio, Max 64 caracteres
        /// </summary>
        public string SensorId { get; set; }

        /// <summary>
        /// Timestamp cuando el sensor registró la medición (UTC)
        /// Requerimiento: ±5 minutos de ahora, no >30 días atrás
        /// Usado para: Trazabilidad temporal, detección de mensajes stale
        /// </summary>
        public DateTime SensorTimestamp { get; set; }

        /// <summary>
        /// Número de secuencia del mensaje en el sensor
        /// Formato: 0-indexed, reinicia al reiniciar sensor
        /// Requerimiento: Usado en OperationKey para evitar colisiones
        /// Rango: 0 a 4,294,967,295 (uint.MaxValue)
        /// </summary>
        public uint SequenceNumber { get; set; }

        /// <summary>
        /// Cantidad de combustible consumido/reportado (en Litros)
        /// Requerimiento: >= 0 (no negativo), teóricamente <= 100,000L
        /// Precisión: Exacto a 0.01L (centilitros)
        /// 
        /// NOTA IMPORTANTE: En caso de error de sensor, enviar 0L
        /// (no omitir el mensaje), para mantener trazabilidad
        /// </summary>
        public decimal FuelConsumed { get; set; }

        /// <summary>
        /// Unidad de medida del combustible (default: "Liters")
        /// Valores permitidos: "Liters", "Gallons", "Cubic_Meters"
        /// Extensible para soporte futuro de otras métricas
        /// </summary>
        public string Unit { get; set; } = "Liters";

        /// <summary>
        /// Información del dispositivo/sensor (opcional, para debugging)
        /// Formato libre: versión firmware, modelo, etc.
        /// </summary>
        public string DeviceInfo { get; set; }

        /// <summary>
        /// Checksum/Hash para detectar corrupción en trancmisión (opcional)
        /// Si se proporciona, validar contra payload
        /// </summary>
        public string MessageChecksum { get; set; }
    }

    /// <summary>
    /// DTO que representa la respuesta al procesamiento de telemetría.
    /// Retorna información de éxito/fallo y flags de diagnostico.
    /// </summary>
    public class ProcessingResult
    {
        /// <summary>
        /// Indicador de éxito del procesamiento
        /// TRUE: Mensaje fue procesado exitosamente (nuevo o duplicado aceptado)
        /// FALSE: Error ocurrió, mensaje rechazado
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// El OperationKey que fue procesado
        /// Útil para correlacionar entre sensor y servidor en logs
        /// </summary>
        public string OperationKey { get; set; }

        /// <summary>
        /// Flag: ¿Este fue un reintento de un mensaje ya procesado?
        /// TRUE: Duplicado, fue ignorado (idempotencia)
        /// FALSE: Primer procesamiento, fue acumulado
        /// 
        /// Uso: Para metricas de duplicates_detected_per_min
        /// </summary>
        public bool WasIdempotentReprocess { get; set; }

        /// <summary>
        /// Mensaje descriptivo en caso de éxito
        /// Ejemplo: "Fuel 50L accumulated", "Duplicate ignored"
        /// </summary>
        public string SuccessMessage { get; set; }

        /// <summary>
        /// Código de error en caso de fallo (si IsSuccess=false)
        /// Formatos: "VALIDATION_ERROR", "IDEMPOTENCY_FAILED", etc.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Mensaje de error detallado
        /// Incluir contexto para debugging del sensor/cliente
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Estado del acumulador DESPUÉS del procesamiento (si exitoso)
        /// Null si fallo. Útil para validación en cliente.
        /// </summary>
        public decimal? AccumulatorTotal { get; set; }

        /// <summary>
        /// Timestamp del servidor cuando fue procesado
        /// Para post-audit y debugging de latencia
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// Latencia de procesamiento en milisegundos
        /// Métrica para SRE monitoring (debe ser <5ms)
        /// </summary>
        public long ProcessingLatencyMs { get; set; }
    }

    /// <summary>
    /// Resultado de validación de un mensaje de telemetría.
    /// Retorna lista de errores si hay validacion fallidas.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// ¿Pasó toda validación?
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Lista de errores encontrados (vacía si IsValid=true)
        /// </summary>
        public List<ValidationError> Errors { get; set; } = new();
    }

    /// <summary>
    /// Un error individual de validación
    /// </summary>
    public class ValidationError
    {
        public ValidationError(string field, string message, string code = null)
        {
            Field = field;
            Message = message;
            Code = code ?? $"{field.ToUpperInvariant()}_ERROR";
        }

        /// <summary>Campo que falló validación</summary>
        public string Field { get; set; }

        /// <summary>Descripción del error</summary>
        public string Message { get; set; }

        /// <summary>Código de error para programación</summary>
        public string Code { get; set; }
    }

    /// <summary>
    /// Interfaz para almacén de rastreo de OperationKeys ya procesadas.
    /// Garantiza idempotencia mediante deduplicación.
    /// 
    /// GARANTÍAS:
    /// - Thread-safe: Múltiples threads pueden acceder simultáneamente
    /// - O(1): Búsqueda y marcado son operaciones constantes
    /// - TTL: Claves antiguas se limpian automáticamente (5+ minutos)
    /// 
    /// IMPLEMENTACIÓN RECOMENDADA:
    /// ConcurrentDictionary<string, byte> + MemoryCache
    /// (ver ADR-003 para justificación)
    /// </summary>
    public interface IIdempotencyStore
    {
        /// <summary>
        /// Verifica si un OperationKey ya fue procesado exitosamente.
        /// 
        /// GARANTÍA: Si retorna TRUE, la operación DEBE ser ignorada
        ///           (resultado es determinístico)
        /// 
        /// Latencia esperada: <1ms (in-memory)
        /// </summary>
        /// <param name="operationKey">La clave a verificar</param>
        /// <returns>true si ya fue procesada, false si es nueva</returns>
        Task<bool> IsAlreadyProcessedAsync(string operationKey);

        /// <summary>
        /// Marca un OperationKey como procesado exitosamente.
        /// DEBE ser llamado DESPUÉS de que la operación sea exitosa
        /// (por ejemplo, DESPUÉS de actualizar acumulador).
        /// 
        /// GARANTÍA: Operación atómica, no hay race conditions
        /// TTL Automático: Clave expira en ~5 minutos
        /// 
        /// Latencia esperada: <1ms
        /// </summary>
        /// <param name="operationKey">La clave a marcar</param>
        /// <exception cref="ArgumentNullException">Si operationKey es null/empty</exception>
        Task MarkAsProcessedAsync(string operationKey);

        /// <summary>
        /// Realiza limpieza periódica de claves expiradas.
        /// DEBE ejecutarse cada 1-5 minutos (ej: via background task)
        /// 
        /// Propósito: Evitar memory leak, liberar RAM
        /// Batch: Limita a ~10K claves por limpieza (evita latency spike)
        /// </summary>
        /// <param name="maxAge">Edad máxima antes de considerar expirada
        ///                      (ej: 6 minutos si TTL=5min)</param>
        Task CleanupExpiredKeysAsync(TimeSpan maxAge);

        /// <summary>
        /// Obtiene estadísticas de uso del almacén (opcional, para monitoreo).
        /// </summary>
        /// <returns>Estadísticas como: Count=12345, MemoryMB=50, etc.</returns>
        Task<IdempotencyStoreStats> GetStatsAsync();
    }

    /// <summary>
    /// Estadísticas del almacén de idempotencia (para monitoreo SRE)
    /// </summary>
    public class IdempotencyStoreStats
    {
        /// <summary>Número actual de claves cached</summary>
        public long CachedKeyCount { get; set; }

        /// <summary>Memoria utilizada en MB</summary>
        public double MemoryMB { get; set; }

        /// <summary>Número de claves limpias desde startup</summary>
        public long TotalCleanedKeys { get; set; }

        /// <summary>Última vez que se ejecutó limpieza</summary>
        public DateTime LastCleanupAt { get; set; }
    }

    /// <summary>
    /// Interfaz para acumulador de combustible.
    /// Proporciona operaciones thread-safe para sumar consumo de combustible.
    /// 
    /// GARANTÍAS:
    /// - Atomic: Operación read-modify-write es indivisible
    /// - Thread-safe: Múltiples hilos pueden sumar simultáneamente sin race conditions
    /// - Precision: decimal (exactitud a 0.01L)
    /// 
    /// IMPLEMENTACIÓN RECOMENDADA:
    /// lock (Monitor) en sección crítica, variable decimal
    /// (ver ADR-003 para justificación)
    /// </summary>
    public interface IFuelAccumulator
    {
        /// <summary>
        /// Suma combustible al acumulador total.
        /// 
        /// OPERACIÓN ATÓMICA:
        ///   1. Lee total actual
        ///   2. Suma 'liters'
        ///   3. Escribe nuevo total
        ///   (NADIE puede interrumpir entre 1-3)
        /// 
        /// Latencia esperada: <1ms
        /// </summary>
        /// <param name="sensorId">Sensor que reporta (para metricas)</param>
        /// <param name="liters">Cantidad a sumar (debe ser >= 0)</param>
        /// <exception cref="ArgumentNullException">Si sensorId es null</exception>
        /// <exception cref="ArgumentException">Si liters < 0</exception>
        Task AddFuelAsync(string sensorId, decimal liters);

        /// <summary>
        /// Obtiene snapshot atómico del total de combustible acumulado.
        /// 
        /// Snapshot: Refleja estado en momento exacto de lectura,
        ///           no cambia durante la lectura
        /// </summary>
        /// <returns>Total en Litros (decimal, exactitud 0.01L)</returns>
        Task<decimal> GetTotalAsync();

        /// <summary>
        /// Obtiene total para un sensor específico (granularidad fina).
        /// </summary>
        /// <param name="sensorId">Sensor a consultar</param>
        /// <returns>Total acumulado por ese sensor</returns>
        Task<decimal> GetTotalBySensorAsync(string sensorId);

        /// <summary>
        /// Reinicia acumulador total (ej: cierre de turno daily).
        /// Útil para reportes por período.
        /// 
        /// NOTA: Esta es operación destructiva, usar con cuidado
        ///       (es auditada en logs)
        /// </summary>
        /// <param name="resetReason">Motivo del reset (ej: "daily_closeout")</param>
        /// <returns>Valor anterior (antes de reset)</returns>
        Task<decimal> ResetAndGetPreviousTotalAsync(string resetReason);

        /// <summary>
        /// Reinicia acumulador para un sensor específico.
        /// </summary>
        Task<decimal> ResetBySensorAsync(string sensorId, string resetReason);
    }

    /// <summary>
    /// Validador de mensajes de telemetría.
    /// Implementa reglas de validación definidas en RF-003.
    /// </summary>
    public interface ITelemetryValidator
    {
        /// <summary>
        /// Valida un mensaje completo de telemetría.
        /// 
        /// Checks:
        /// - OperationKey: required, max 256 chars, formato válido
        /// - SensorId: required, max 64 chars
        /// - FuelConsumed: >= 0, < 100,000 (warning si >= 100,000)
        /// - SensorTimestamp: within ±5 min of now, >= 30 days old
        /// - Unit: uno de {"Liters", "Gallons", "Cubic_Meters"}
        /// 
        /// Retorna resultado con todos los errores encontrados
        /// </summary>
        ValidationResult Validate(TelemetryMessage message);
    }

    /// <summary>
    /// Servicio principal de telemetría robusto.
    /// 
    /// RESPONSABILIDADES:
    /// 1. Validar mensaje (ITelemetryValidator)
    /// 2. Verificar idempotencia (IIdempotencyStore)
    /// 3. Si nuevo: Acumular combustible (IFuelAccumulator)
    /// 4. Retornar resultado de procesamiento
    /// 
    /// GARANTÍAS:
    /// - Exactly-once semantics (At-least-once + dedup = exactly-once)
    /// - Exactitud: 100% de datos capturados, cero pérdidas
    /// - Idempotencia: Mensajes duplicados ignorados silenciosamente
    /// 
    /// LATENCY SLA:
    /// - P50: <1ms
    /// - P95: <5ms
    /// - P99: <20ms
    /// </summary>
    public interface ITelemetryService
    {
        /// <summary>
        /// Procesa un reporte de telemetría de combustible.
        /// 
        /// PIPELINE:
        /// 1. ValidateMessage(message) → ValidationResult
        /// 2. IsAlreadyProcessed(message.OperationKey) → bool
        ///    ├─ YES: Return Success (wasIdempotentReprocess=true)
        ///    └─ NO:  Continue
        /// 3. AddFuel(message.SensorId, message.FuelConsumed) → void
        /// 4. MarkAsProcessed(message.OperationKey) → void
        /// 5. Return Success (wasIdempotentReprocess=false)
        /// 
        /// MANEJO DE ERRORES:
        /// - Validación falla: Return ProcessingResult.Failure (validation errors)
        /// - AddFuel falla: Return ProcessingResult.Failure (detalle error)
        /// - Idempotencia ok: Return ProcessingResult.Success (siempre)
        /// 
        /// GARANTÍA ATÓMICA:
        /// Marcar como processed SOLO si acumular fue exitoso
        /// (evita inconsistencia: acumulado pero no deduplicado)
        /// </summary>
        /// <param name="message">Mensaje de telemetría a procesar</param>
        /// <returns>ProcessingResult con detalles de éxito/fallo</returns>
        /// <exception cref="ArgumentNullException">Si message es null</exception>
        Task<ProcessingResult> ProcessReportAsync(TelemetryMessage message);

        /// <summary>
        /// Procesa múltiples mensajes en batch.
        /// NOTA: Es idempotente, no duplica incluso con mensajes repetidos.
        /// </summary>
        /// <param name="messages">Lista de mensajes a procesar</param>
        /// <returns>List de resultados en el MISMO orden</returns>
        Task<List<ProcessingResult>> ProcessReportBatchAsync(
            List<TelemetryMessage> messages);

        /// <summary>
        /// Obtiene estado actual del acumulador.
        /// Puede usarse para healthcheck o reconciliación.
        /// </summary>
        Task<AccumulatorStatus> GetStatusAsync();

        /// <summary>
        /// Obtiene métricas de rendimiento del servicio.
        /// Usado para SRE monitoring.
        /// </summary>
        Task<ServiceMetrics> GetMetricsAsync();
    }

    /// <summary>
    /// Estado actual del acumulador
    /// </summary>
    public class AccumulatorStatus
    {
        /// <summary>Total actual de combustible acumulado (Litros)</summary>
        public decimal TotalLiters { get; set; }

        /// <summary>Timestamp de la última actualización</summary>
        public DateTime LastUpdatedAt { get; set; }

        /// <summary>Número de sensores activos reportando</summary>
        public int ActiveSensors { get; set; }

        /// <summary>Estadísticas por sensor (opcional)</summary>
        public Dictionary<string, SensorStats> SensorDetails { get; set; }
    }

    /// <summary>
    /// Estadísticas de un sensor individual
    /// </summary>
    public class SensorStats
    {
        public string SensorId { get; set; }
        public decimal TotalLiters { get; set; }
        public int MessageCount { get; set; }
        public DateTime LastReportedAt { get; set; }
    }

    /// <summary>
    /// Métricas de rendimiento del servicio (para monitoreo SRE)
    /// </summary>
    public class ServiceMetrics
    {
        /// <summary>Total de mensajes procesados desde startup</summary>
        public long TotalMessagesProcessed { get; set; }

        /// <summary>Total de mensajes rechazados por validación</summary>
        public long ValidationErrorCount { get; set; }

        /// <summary>Total de duplicados detectados y ignorados</summary>
        public long DuplicatesDetected { get; set; }

        /// <summary>Tasa de procesamiento (msgs/sec) en últimos 60 seg</summary>
        public double ThroughputMsgsPerSec { get; set; }

        /// <summary>Latencia P95 en milisegundos (últimos 1000 mensajes)</summary>
        public double LatencyP95Ms { get; set; }

        /// <summary>Latencia P99 en milisegundos</summary>
        public double LatencyP99Ms { get; set; }

        /// <summary>Tasa de error (% de mensajes fallidos)</summary>
        public double ErrorRatePercent { get; set; }

        /// <summary>Uptime del servicio en horas</summary>
        public double UptimeHours { get; set; }

        /// <summary>Timestamp de cuando fueron capturadas estas métricas</summary>
        public DateTime CapturedAt { get; set; }
    }

    /// <summary>
    /// Configuración del servicio de telemetría
    /// </summary>
    public class TelemetryServiceConfiguration
    {
        /// <summary>TTL para claves de idempotencia (default: 5 minutos)</summary>
        public TimeSpan IdempotencyKeyTtl { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>Intervalo de limpieza de claves expiradas (default: 1 minuto)</summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>Rango válido de timestamp: ¿cuántos minutos en el pasado?</summary>
        public int MaxMessageAgeMinutes { get; set; } = 5;

        /// <summary>Rango válido de timestamp: ¿cuántos minutos en el futuro?</summary>
        public int MaxFutureMessageMinutes { get; set; } = 5;

        /// <summary>Threshold de warning para combustible anormalmente alto (Liters)</summary>
        public decimal AnomalousConsumptionThreshold { get; set; } = 100_000m;

        /// <summary>¿Registrar en log cada operación? (true = verbose, false = solo errores)</summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>¿Habilitar limpieza automática en background?</summary>
        public bool EnableAutoCleanup { get; set; } = true;
    }
}
