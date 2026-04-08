# PLAN DE IMPLEMENTACIÓN: DEMO FUNCIONAL TELEMETRÍA ROBUSTA
## Desglose de Tareas Técnicas Granulares

**Documento**: Plan de Ejecución - Demo Funcional  
**Fecha**: 2026-04-08  
**Basado en**: TELEMETRIA_ROBUSTA.md + ADR-003 + ITelemetryService.cs  
**Objetivo**: Implementación paso-a-paso con validación en cada fase

---

## MÓDULO 1: IDENTIDAD - DICCIONARIO CONCURRENTE PARA DUPLICADOS
### Objetivo: Rastrear OperationKeys ya procesadas

```
Módulo 1
├─ T1.1 Definir estructura DiplicateKeyTracker
├─ T1.2 Inicializar ConcurrentDictionary<string, byte>
├─ T1.3 Implementar método IsKeyProcessed
├─ T1.4 Implementar método MarkKeyAsProcessed
├─ T1.5 Crear método de limpieza (ExpireOldKeys)
├─ T1.6 Validar thread-safety con acceso simultáneo
└─ T1.7 Actualizar para TTL o soporte externo Redis
```

### T1.7: Actualizar para TTL o Redis

**Descripción**: Extender la fase de implementación para manejar crecimiento de estado y resiliencia tras reinicio.

**Tareas Concretas**:
```
- [ ] Añadir almacenamiento de metadata para cada clave: timestamp de inserción
- [ ] Implementar TTL configurable para claves de idempotencia
- [ ] Agregar limpieza periódica automática para evitar crecimiento infinito
- [ ] Evaluar persistencia externa en Redis cuando el servicio deba sobrevivir a reinicios
- [ ] Documentar trade-offs: memoria local vs latencia de red
- [ ] Diseñar fallback: in-memory + Redis cache con write-through
```

**Motivo**:
- Evita que millones de claves retenidas provoquen uso excesivo de RAM
- Permite restaurar idempotencia tras reinicio de servicio
- Mejora la robustez del sistema sin romper la lógica de deduplicación
```

---

### T1.1: Definir Estructura DiplicateKeyTracker

**Descripción**: Crear clase que encapsule la lógica de rastreo de claves procesadas.

**Tareas Concretas**:
```
- [ ] Crear archivo: DuplicateKeyTracker.cs en Telemetria/Implementations/
- [ ] Definir clase pública: public class DuplicateKeyTracker
- [ ] Añadir miembro privado: private readonly ConcurrentDictionary<string, byte> _keys
- [ ] Documentar con XML comments: 
  /// <summary>
  /// Rastreador thread-safe de claves de operación procesadas.
  /// Usa ConcurrentDictionary para O(1) lookup y evita locks globales.
  /// </summary>
- [ ] Crear constructor público: public DuplicateKeyTracker()
- [ ] Inicializar diccionario con capacidad estimada (1_000_000)
- [ ] Establecer concurrencyLevel = Environment.ProcessorCount * 2
```

**Verificación**:
```csharp
// Debe compilar sin errores
var tracker = new DuplicateKeyTracker();
```

**Criterio de Aceptación**:
- ✅ Archivo creado en ubicación correcta
- ✅ Clase compilable
- ✅ Diccionario inicializado correctamente
- ✅ Constructor sin parámetros ejecutable

---

### T1.2: Inicializar ConcurrentDictionary<string, byte>

**Descripción**: Configurar el diccionario con parámetros óptimos para alta concurrencia.

**Tareas Concretas**:
```
- [ ] Definir constant: const int INITIAL_CAPACITY = 1_000_000;
- [ ] Definir constant: const int CONCURRENCY_LEVEL = Environment.ProcessorCount * 2;
- [ ] Crear inicializador en constructor:
      _keys = new ConcurrentDictionary<string, byte>(
          concurrencyLevel: CONCURRENCY_LEVEL,
          capacity: INITIAL_CAPACITY);
- [ ] Documentar por qué estos valores:
      /// CONCURRENCY_LEVEL: Permite N buckets simultáneos = mejor paralelismo
      /// CAPACITY: Evita rehashing en primeras 1M operaciones
- [ ] Crear propiedad pública de solo lectura: public int Count => _keys.Count;
- [ ] Crear método de limpieza de estructura:
      private void ClearAllKeys() { _keys.Clear(); }
```

**Verificación**:
```csharp
var tracker = new DuplicateKeyTracker();
Assert.AreEqual(0, tracker.Count);
// Acceso simultáneo no debe fallar (test posterior)
```

**Criterio de Aceptación**:
- ✅ Diccionario inicializa con capacity > 0
- ✅ Concurrency level es múltiplo de ProcessorCount
- ✅ Property Count accesible
- ✅ Sin excepciones en inicialización

---

### T1.3: Implementar Método IsKeyProcessed

**Descripción**: Consultar si una clave ya fue procesada (O(1) amortizado).

**Tareas Concretas**:
```
- [ ] Firmar método:
      public bool IsKeyProcessed(string operationKey)
- [ ] Validar entrada:
      if (string.IsNullOrWhiteSpace(operationKey))
          return false;
- [ ] Guardia de longitud:
      if (operationKey.Length > 256)
          throw new ArgumentException("OperationKey > 256 chars");
- [ ] Implementar búsqueda:
      return _keys.ContainsKey(operationKey);
- [ ] Documentar con XML:
      /// <summary>
      /// Verifica si OperationKey ya fue procesado.
      /// </summary>
      /// <returns>true si existe, false si nuevo</returns>
      /// <remarks>O(1) amortizado, thread-safe</remarks>
- [ ] Agregar prueba simple en clase:
      public void TestIsKeyProcessed()
      {
          Assert.IsFalse(IsKeyProcessed("NEW_KEY"));
          MarkKeyAsProcessed("NEW_KEY");
          Assert.IsTrue(IsKeyProcessed("NEW_KEY"));
      }
```

**Verificación**:
```csharp
var tracker = new DuplicateKeyTracker();
Assert.IsFalse(tracker.IsKeyProcessed("KEY1")); // No existe
Assert.IsFalse(tracker.IsKeyProcessed("")); // Entrada inválida
```

**Criterio de Aceptación**:
- ✅ Retorna false para claves nuevas
- ✅ Retorna false para entrada vacía (safe)
- ✅ Latencia < 1ms (verificar con Stopwatch)
- ✅ Sin excepciones en acceso concurrente

---

### T1.4: Implementar Método MarkKeyAsProcessed

**Descripción**: Registrar una clave como procesada de forma atómica.

**Tareas Concretas**:
```
- [ ] Firmar método:
      public void MarkKeyAsProcessed(string operationKey)
- [ ] Validar entrada:
      if (string.IsNullOrWhiteSpace(operationKey))
          throw new ArgumentException("OperationKey requerido");
- [ ] Guardia de longitud:
      if (operationKey.Length > 256)
          throw new ArgumentException("OperationKey > 256 chars");
- [ ] Llamar TryAdd (no sobrescribir si existe):
      var added = _keys.TryAdd(operationKey, 1);
- [ ] Documentar resultado:
      if (!added)
          System.Diagnostics.Debug.WriteLine(
              $"Key already present: {operationKey}");
- [ ] Documentar con XML:
      /// <summary>
      /// Marca una clave como procesada (idempotencia).
      /// Si ya existe, ignora silenciosamente.
      /// </summary>
      /// <remarks>Atómico, thread-safe, O(1) amortizado</remarks>
- [ ] Timestamp de auditoría (opcional): 
      // Para logging posterior
      // _auditLog[operationKey] = DateTime.UtcNow;
```

**Verificación**:
```csharp
var tracker = new DuplicateKeyTracker();
tracker.MarkKeyAsProcessed("KEY1");
Assert.IsTrue(tracker.IsKeyProcessed("KEY1"));

// Marcar nuevamente (debe ser seguro)
tracker.MarkKeyAsProcessed("KEY1");
Assert.AreEqual(1, tracker.Count); // No se duplicó
```

**Criterio de Aceptación**:
- ✅ Clave registrada después de marcar
- ✅ Marcar dos veces no duplica entrada
- ✅ Latencia < 1ms
- ✅ Thread-safe: múltiples threads marcan simultáneamente

---

### T1.5: Crear Método de Limpieza (ExpireOldKeys)

**Descripción**: Limpiar claves expiradas (mayores a X minutos) para evitar memory leak.

**Tareas Concretas**:
```
- [ ] Firmar método:
      public int ExpireOldKeys(TimeSpan maxAge)
- [ ] Crear lista de candidatos:
      var keysToRemove = new List<string>();
- [ ] Extraer timestamp del OperationKey:
      // Formato: SensorId#ISO8601Timestamp#Seq
      private DateTime ExtractTimestampFromKey(string key)
      {
          var parts = key.Split('#');
          if (parts.Length >= 2 && DateTime.TryParse(parts[1], out var ts))
              return ts;
          return DateTime.MinValue;
      }
- [ ] Iterar claves (batch máx 10K para evitar latency spike):
      foreach (var key in _keys.Keys.Take(10_000))
      {
          var ts = ExtractTimestampFromKey(key);
          if (DateTime.UtcNow - ts > maxAge)
              keysToRemove.Add(key);
      }
- [ ] Eliminar en batch:
      int removedCount = 0;
      foreach (var key in keysToRemove)
      {
          if (_keys.TryRemove(key, out _))
              removedCount++;
      }
      return removedCount;
- [ ] Documentar con XML
- [ ] Crear método público para trigger manual:
      public int CleanUpExpiredKeys()
      {
          return ExpireOldKeys(TimeSpan.FromMinutes(6));
      }
```

**Verificación**:
```csharp
// Crear clave con timestamp antiguo (en test, usar TravelTime)
// Verificar que se elimina después de X minutos
var tracker = new DuplicateKeyTracker();
tracker.MarkKeyAsProcessed("SENSOR_A#2026-04-06T10:00:00Z#1"); // 2 días atrás
int removed = tracker.CleanUpExpiredKeys(); // maxAge = 6 min
Assert.AreEqual(1, removed); // Debe eliminarse
```

**Criterio de Aceptación**:
- ✅ Claves antiguas se eliminan
- ✅ Claves recientes se conservan
- ✅ Retorna count de removidas > 0
- ✅ Memory usage disminuye después de cleanup

---

### T1.6: Validar Thread-Safety con Acceso Simultáneo

**Descripción**: Unit test que verifica que múltiples threads pueden acceder sin race conditions.

**Tareas Concretas**:
```
- [ ] Crear test: ConcurrentAccessTest()
- [ ] Inicializar tracker:
      var tracker = new DuplicateKeyTracker();
- [ ] Crear 100 tasks concurrentes:
      var tasks = new List<Task>();
      for (int i = 0; i < 100; i++)
      {
          int threadId = i;
          var task = Task.Run(() =>
          {
              for (int j = 0; j < 1000; j++)
              {
                  string key = $"KEY_{threadId}_{j}";
                  tracker.MarkKeyAsProcessed(key);
              }
          });
          tasks.Add(task);
      }
- [ ] Esperar a todas:
      Task.WaitAll(tasks.ToArray());
- [ ] Validar total exacto:
      Assert.AreEqual(100_000, tracker.Count);
      // 100 threads × 1000 claves = 100K instancias únicas
- [ ] Validar que todas existen:
      for (int i = 0; i < 100; i++)
      {
          var key = $"KEY_{i}_999";
          Assert.IsTrue(tracker.IsKeyProcessed(key));
      }
- [ ] Medir latencia:
      var sw = Stopwatch.StartNew();
      // 100K operaciones
      sw.Stop();
      Assert.IsTrue(sw.ElapsedMilliseconds < 5000); // < 50µs/op
```

**Verificación**:
```
✓ 100 tasks se ejecutan sin deadlock
✓ Count final = 100_000 (exacto)
✓ Todas las claves son recuperables
✓ Latencia P95 < 1ms por operación
```

**Criterio de Aceptación**:
- ✅ Test pasa sin excepciones
- ✅ Count exacto al final
- ✅ Sin deadlocks ni race conditions
- ✅ Throughput > 100K ops/sec

---

## MÓDULO 2: CÁLCULO PROTEGIDO - LOCK PARA SUMA ATÓMICA
### Objetivo: Acumulador thread-safe con lock (Monitor)

```
Módulo 2
├─ T2.1 Definir estructura FuelAccumulatorWithLock
├─ T2.2 Crear variable de sincronización (lock object)
├─ T2.3 Implementar AddFuel con lock
├─ T2.4 Implementar GetTotal con lock
├─ T2.5 Validar que operaciones son atómicas
└─ T2.6 Benchmark: medir latencia de lock
```

---

### T2.1: Definir Estructura FuelAccumulatorWithLock

**Descripción**: Crear clase que mantenga total de combustible con sincronización.

**Tareas Concretas**:
```
- [ ] Crear archivo: FuelAccumulatorWithLock.cs
- [ ] Definir clase pública: public class FuelAccumulatorWithLock
- [ ] Documentar con XML:
      /// <summary>
      /// Acumulador thread-safe de combustible.
      /// Usa lock (Monitor) para sincronización.
      /// Precisión: decimal (exactitud auditable)
      /// </summary>
- [ ] Crear propiedades privadas base (sin setter público):
      private decimal _totalFuel = 0m;
      private readonly object _lock = new object();
- [ ] Crear constructor  sin parámetros:
      public FuelAccumulatorWithLock() { }
- [ ] Crear propiedad de solo lectura:
      public decimal Total 
      { 
          get { lock (_lock) { return _totalFuel; } } 
      }
- [ ] Verificar compilación
```

**Verificación**:
```csharp
var acc = new FuelAccumulatorWithLock();
Assert.AreEqual(0m, acc.Total);
```

**Criterio de Aceptación**:
- ✅ Archivo creado y compilable
- ✅ Lock object inicializado
- ✅ Total inicial = 0
- ✅ Propiedad Total accesible

---

### T2.2: Crear Variable de Sincronización (Lock Object)

**Descripción**: Objeto dedicado solo para lock (best practice: no usar 'this').

**Tareas Concretas**:
```
- [ ] Ya definido en T2.1, pero validar:
      private readonly object _lock = new object();
- [ ] Verificar que NO sea nulo:
      public FuelAccumulatorWithLock()
      {
          if (_lock == null)
              throw new InvalidOperationException("Lock must be initialized");
      }
- [ ] Documentar por qué es readonly:
      /// Lock debe ser readonly para evitar cambios de referencia
      /// que causarían race conditions
- [ ] Crear método privado para verificación (test helper):
      private bool IsLockValid()
      {
          return _lock != null;
      }
- [ ] Teste que lock nunca es nulo:
      var acc = new FuelAccumulatorWithLock();
      Assert.IsNotNull(acc._lock); // Acceso privado en test
      // o usar reflexión si es necesario
```

**Verificación**:
```csharp
// En test (con PrivateAccessor o reflexión):
var field = typeof(FuelAccumulatorWithLock)
    .GetField("_lock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
Assert.IsNotNull(field.GetValue(acc));
```

**Criterio de Aceptación**:
- ✅ Lock object existe
- ✅ Es readonly
- ✅ Nunca es nulo
- ✅ Mismo objeto en múltiples invocaciones

---

### T2.3: Implementar AddFuel con Lock

**Descripción**: Operación de suma atómica protegida por lock.

**Tareas Concretas**:
```
- [ ] Firmar método público:
      public void AddFuel(decimal liters)
- [ ] Validar entrada (PRE-LOCK):
      if (liters < 0)
          throw new ArgumentException("Fuel cannot be negative");
- [ ] Documentar con XML:
      /// <summary>
      /// Suma combustible de forma atómica.
      /// Garantía: Lectura-modificación-escritura es indivisible.
      /// </summary>
      /// <param name="liters">Cantidad a sumar (>= 0)</param>
- [ ] Implementar lock y suma:
      lock (_lock)
      {
          _totalFuel += liters;
      }
- [ ] Comentar la sección:
      // SECCIÓN CRÍTICA:
      // - Lectura de _totalFuel (actual)
      // - Suma con liters
      // - Escritura a _totalFuel (nuevo)
      // NADIE puede interrumpir entre estos pasos
- [ ] Crear versión async (para API):
      public async Task AddFuelAsync(decimal liters)
      {
          AddFuel(liters); // Lock no es async
          await Task.CompletedTask;
      }
- [ ] Validar que suma es exacta (sin precisión numérica):
      // Test: 0.1 + 0.2 debe = 0.3 exactamente
      var acc = new FuelAccumulatorWithLock();
      acc.AddFuel(0.1m);
      acc.AddFuel(0.2m);
      Assert.AreEqual(0.3m, acc.Total); // Exacto (decimal)
```

**Verificación**:
```csharp
var acc = new FuelAccumulatorWithLock();
acc.AddFuel(50m);
Assert.AreEqual(50m, acc.Total);

acc.AddFuel(25m);
Assert.AreEqual(75m, acc.Total);

// Verificar exactitud decimal
acc.AddFuel(0.1m);
acc.AddFuel(0.2m);
Assert.AreEqual(75.3m, acc.Total); // Exacto, no 75.30000000000001
```

**Criterio de Aceptación**:
- ✅ Suma correcta
- ✅ Decimal precision exacta
- ✅ Rechaza valores negativos
- ✅ Lock protege operación
- ✅ Latencia < 1ms

---

### T2.4: Implementar GetTotal con Lock

**Descripción**: Lectura atómica del total (snapshot protegido por lock).

**Tareas Concretas**:
```
- [ ] Firmar método:
      public decimal GetTotal()
- [ ] Documentar con XML:
      /// <summary>
      /// Obtiene snapshot atómico del total.
      /// </summary>
      /// <remarks>
      /// Lock asegura que nadie modifica durante lectura.
      /// Retorna valor consistente en cierto momento.
      /// </remarks>
- [ ] Implementar con lock:
      lock (_lock)
      {
          return _totalFuel;
      }
- [ ] Crear versión async:
      public async Task<decimal> GetTotalAsync()
      {
          return await Task.FromResult(GetTotal());
      }
- [ ] Complementar propiedad Total (ya existe):
      // public decimal Total { get { lock (_lock) { return _totalFuel; } } }
- [ ] Validar que GetTotal y Total retornan lo mismo:
      var acc = new FuelAccumulatorWithLock();
      acc.AddFuel(100m);
      Assert.AreEqual(acc.Total, acc.GetTotal());
```

**Verificación**:
```csharp
var acc = new FuelAccumulatorWithLock();
acc.AddFuel(100m);
var snapshot1 = acc.GetTotal();
var snapshot2 = acc.GetTotal();
// Snapshots consecutivos podrían diferir si otra tarea suma,
// pero cada snapshot es atómico en sí mismo
Assert.IsTrue(snapshot1 >= 100m);
```

**Criterio de Aceptación**:
- ✅ Retorna total correcto
- ✅ Lock protege lectura
- ✅ Latencia < 1ms
- ✅ Resultado consistente en cada lectura
- ✅ No hay data tearing (atomic read)

---

### T2.5: Validar Que Operaciones Son Atómicas

**Descripción**: Test que demuestra que operaciones no tiene race conditions.

**Tareas Concretas**:
```
- [ ] Crear test: OperationsAreAtomicTest()
- [ ] Escenario: 2 threads suman al mismo tiempo
      Thread A: suma 50
      Thread B: suma 30
      Esperado: Total = 80
- [ ] Implementar sin lock (control negativo):
      // Simular race condition
      var value = 0m; // Sin lock
      Task t1 = Task.Run(() => { value += 50; });
      Task t2 = Task.Run(() => { value += 30; });
      Task.WaitAll(t1, t2);
      // Resultado PODRÍA SER 30, 50, ó 80 (indeterminado)
- [ ] Verificar con lock (control positivo):
      var acc = new FuelAccumulatorWithLock();
      var tasks = new List<Task>();
      for (int i = 0; i < 100; i++)
      {
          int threadId = i;
          tasks.Add(Task.Run(() => acc.AddFuel(10m)));
      }
      Task.WaitAll(tasks.ToArray());
      Assert.AreEqual(1000m, acc.Total); // 100 * 10 = 1000 (EXACTO)
- [ ] Medir latencia bajo contención:
      var sw = Stopwatch.StartNew();
      // 100 threads, 1000 operaciones cada una
      for (int i = 0; i < 100_000; i++)
      {
          acc.AddFuel(0.01m);
      }
      sw.Stop();
      // Latencia debería ser < 100ms total
      Assert.IsTrue(sw.ElapsedMilliseconds < 100);
```

**Verificación**:
```
✓ 100 threads suman sin race condition
✓ Total final está perfectamente exacto (1000m)
✓ Sin saturación de lock (latencia razonable)
```

**Criterio de Aceptación**:
- ✅ Total siempre exacto bajo concurrencia
- ✅ Latencia P95 < 5ms por operación
- ✅ Sin variabilidad de resultados
- ✅ Throughput > 1M ops/sec

---

### T2.6: Benchmark - Medir Latencia de Lock

**Descripción**: Medición sistemática de rendimiento del lock.

**Tareas Concretas**:
```
- [ ] Crear método: BenchmarkLockLatency()
- [ ] Inicializar acumulador:
      var acc = new FuelAccumulatorWithLock();
- [ ] Test 1: Single-threaded latency
      var sw = Stopwatch.StartNew();
      for (int i = 0; i < 1_000_000; i++)
      {
          acc.AddFuel(0.001m);
      }
      sw.Stop();
      // Esperado: < 1000ms para 1M ops = 1µs/op
      double latencySingleThread = sw.Elapsed.TotalMicroseconds / 1_000_000;
      Console.WriteLine($"Single-thread: {latencySingleThread}µs/op");
      Assert.IsTrue(latencySingleThread < 1.0); // < 1µs
- [ ] Test 2: Multi-threaded latency (50 threads)
      sw.Restart();
      var tasks = new List<Task>();
      for (int t = 0; t < 50; t++)
      {
          tasks.Add(Task.Run(() =>
          {
              for (int i = 0; i < 20_000; i++)
                  acc.AddFuel(0.001m);
          }));
      }
      Task.WaitAll(tasks.ToArray());
      sw.Stop();
      // 50 threads × 20K ops = 1M total ops
      double latencyMultiThread = sw.Elapsed.TotalMicroseconds / 1_000_000;
      Console.WriteLine($"Multi-thread (50): {latencyMultiThread}µs/op");
      Assert.IsTrue(latencyMultiThread < 10.0); // < 10µs
- [ ] Test 3: GetTotal latency
      sw.Restart();
      for (int i = 0; i < 1_000_000; i++)
      {
          _ = acc.GetTotal();
      }
      sw.Stop();
      double getLatency = sw.Elapsed.TotalMicroseconds / 1_000_000;
      Console.WriteLine($"GetTotal: {getLatency}µs/op");
      Assert.IsTrue(getLatency < 1.0);
- [ ] Reportar resultados en formato tabla
- [ ] Documentar conclusiones en comentario
```

**Verificación**:
```
Benchmark Results:
- Single-thread AddFuel: 0.5µs/op
- Multi-thread AddFuel:  5.2µs/op
- GetTotal latency:      0.3µs/op

Conclusión: Lock overhead es minimal, aceptable para production.
```

**Criterio de Aceptación**:
- ✅ Single-thread < 1µs/op
- ✅ Multi-thread < 10µs/op
- ✅ Throughput > 100K ops/sec
- ✅ Resultados consistentes en múltiples ejecuciones

---

## MÓDULO 3: TRAZABILIDAD - LOGS DE DUPLICADOS RECHAZADOS
### Objetivo: Registrar y contar duplicados en logs

```
Módulo 3
├─ T3.1 Definir estructura DuplicateLogger
├─ T3.2 Crear método RegisterDuplicate
├─ T3.3 Implementar GetDuplicateStats
├─ T3.4 Formatear salida de logs (JSON/texto)
└─ T3.5 Verificar que logs no afecten rendimiento
```

---

### T3.1: Definir Estructura DuplicateLogger

**Descripción**: Clase que registra y rastrea duplicados detectados.

**Tareas Concretas**:
```
- [ ] Crear archivo: DuplicateLogger.cs
- [ ] Definir clase pública: public class DuplicateLogger
- [ ] Documentar con XML:
      /// <summary>
      /// Logger thread-safe para registrar mensajes duplicados.
      /// Incluye timestamp, OperationKey, y estadísticas.
      /// </summary>
- [ ] Crear estructura interna para registro:
      private class DuplicateEntry
      {
          public string OperationKey { get; set; }
          public DateTime DetectedAt { get; set; }
          public int DuplicateCount { get; set; } // ¿Cuántas veces visto?
          public string SensorId { get; set; }
      }
- [ ] Crear almacén thread-safe:
      private readonly ConcurrentDictionary<string, DuplicateEntry> 
          _duplicateLog = new();
- [ ] Crear contador global:
      private long _totalDuplicatesDetected = 0;
- [ ] Crear método para obtener timestamp:
      private DateTime GetUtcNow() => DateTime.UtcNow;
```

**Verificación**:
```csharp
var logger = new DuplicateLogger();
// Debe inicializar sin errores
Assert.IsNotNull(logger);
```

**Criterio de Aceptación**:
- ✅ Clase compilable
- ✅ Almacén inicializado
- ✅ Contador comienza en 0

---

### T3.2: Crear Método RegisterDuplicate

**Descripción**: Registrar detección de un OperationKey duplicado.

**Tareas Concretas**:
```
- [ ] Firmar método:
      public void RegisterDuplicate(string operationKey, 
                                   string sensorId,
                                   int duplicateCount = 1)
- [ ] Validar entrada:
      if (string.IsNullOrWhiteSpace(operationKey))
          throw new ArgumentException("OperationKey requerido");
- [ ] Incrementar contador global:
      Interlocked.Add(ref _totalDuplicatesDetected, duplicateCount);
- [ ] Crear o actualizar entrada:
      var entry = new DuplicateEntry
      {
          OperationKey = operationKey,
          DetectedAt = GetUtcNow(),
          DuplicateCount = duplicateCount,
          SensorId = sensorId ?? "UNKNOWN"
      };
- [ ] Agregar al log:
      _duplicateLog.TryAdd(operationKey, entry);
      // O actualizar si ya existe:
      if (_duplicateLog.TryGetValue(operationKey, out var existing))
      {
          existing.DuplicateCount += duplicateCount;
      }
- [ ] Opcional: Escribir a console en debug:
      #if DEBUG
      System.Diagnostics.Debug.WriteLine(
          $"DUPLICATE: {operationKey} from {sensorId}");
      #endif
- [ ] Documentar con XML
- [ ] Crear versión async (para pipeline):
      public async Task RegisterDuplicateAsync(string operationKey, 
                                              string sensorId)
      {
          RegisterDuplicate(operationKey, sensorId);
          await Task.CompletedTask;
      }
```

**Verificación**:
```csharp
var logger = new DuplicateLogger();
logger.RegisterDuplicate("KEY_001", "SENSOR_A");
logger.RegisterDuplicate("KEY_001", "SENSOR_A"); // Segunda vez

// Debe registrar ambas
Assert.AreEqual(2, logger.TotalDuplicatesDetected);
```

**Criterio de Aceptación**:
- ✅ Duplicado registrado
- ✅ Contador incrementado
- ✅ Entry incluye timestamp
- ✅ Múltiples registros del mismo key son soportados

---

### T3.3: Implementar GetDuplicateStats

**Descripción**: Retornar estadísticas agregadas de duplicados.

**Tareas Concretas**:
```
- [ ] Firmar método:
      public DuplicateStatistics GetStatistics()
- [ ] Crear clase DTO:
      public class DuplicateStatistics
      {
          public long TotalDuplicatesDetected { get; set; }
          public int UniqueDuplicateKeys { get; set; }
          public DateTime? FirstDuplicateAt { get; set; }
          public DateTime? LastDuplicateAt { get; set; }
          public Dictionary<string, int> DuplicatesByKey { get; set; }
          public Dictionary<string, int> DuplicatesBySensor { get; set; }
      }
- [ ] Implementar agregación:
      var stats = new DuplicateStatistics
      {
          TotalDuplicatesDetected = _totalDuplicatesDetected,
          UniqueDuplicateKeys = _duplicateLog.Count,
          FirstDuplicateAt = _duplicateLog.Values
              .Min(e => e.DetectedAt),
          LastDuplicateAt = _duplicateLog.Values
              .Max(e => e.DetectedAt),
          DuplicatesByKey = _duplicateLog
              .ToDictionary(kvp => kvp.Key, 
                          kvp => kvp.Value.DuplicateCount),
          DuplicatesBySensor = _duplicateLog.Values
              .GroupBy(e => e.SensorId)
              .ToDictionary(g => g.Key, 
                          g => g.Sum(e => e.DuplicateCount))
      };
      return stats;
- [ ] Crear método para exportar como JSON:
      public string GetStatisticsAsJson()
      {
          var stats = GetStatistics();
          return JsonConvert.SerializeObject(stats, 
                                            Formatting.Indented);
      }
- [ ] Crear método para exportar como tabla:
      public string GetStatisticsAsTable()
      {
          // Formato: OperationKey | SensorId | Count | DetectedAt
      }
```

**Verificación**:
```csharp
var logger = new DuplicateLogger();
logger.RegisterDuplicate("KEY_001", "SENSOR_A", 5);
logger.RegisterDuplicate("KEY_002", "SENSOR_B", 3);

var stats = logger.GetStatistics();
Assert.AreEqual(8, stats.TotalDuplicatesDetected); // 5 + 3
Assert.AreEqual(2, stats.UniqueDuplicateKeys);
```

**Criterio de Aceptación**:
- ✅ Stats retorna números exactos
- ✅ JSON serializable
- ✅ Tabla formateada correctamente
- ✅ FirstDuplicateAt/LastDuplicateAt válidos

---

### T3.4: Formatear Salida de Logs (JSON/Texto)

**Descripción**: Exportar logs en múltiples formatos legibles.

**Tareas Concretas**:
```
- [ ] Crear método: GetStatisticsAsJson():
      return JsonConvert.SerializeObject(GetStatistics(), 
                                        Formatting.Indented);
      // Ejemplo:
      // {
      //   "TotalDuplicatesDetected": 8,
      //   "UniqueDuplicateKeys": 2,
      //   "DuplicatesByKey": {
      //     "KEY_001": 5,
      //     "KEY_002": 3
      //   }
      // }
- [ ] Crear método: GetStatisticsAsTable():
      var sb = new StringBuilder();
      sb.AppendLine("=== DUPLICATE STATISTICS ===");
      sb.AppendLine($"Total Duplicates: {_totalDuplicatesDetected}");
      sb.AppendLine($"Unique Keys: {_duplicateLog.Count}");
      sb.AppendLine();
      sb.AppendLine($"{"OperationKey",-50} | {"SensorId",-20} | {"Count",5}");
      sb.AppendLine(new string('-', 80));
      foreach (var kvp in _duplicateLog)
      {
          sb.AppendLine($"{kvp.Key,-50} | {kvp.Value.SensorId,-20} | {kvp.Value.DuplicateCount,5}");
      }
      return sb.ToString();
- [ ] Crear método: GetStatisticsAsCsv():
      var csv = new StringBuilder();
      csv.AppendLine("OperationKey,SensorId,DuplicateCount,DetectedAt");
      foreach (var entry in _duplicateLog.Values)
      {
          csv.AppendLine(
              $"\"{entry.OperationKey}\",\"{entry.SensorId}\"," +
              $"{entry.DuplicateCount},\"{entry.DetectedAt:O}\"");
      }
      return csv.ToString();
- [ ] Test cada formato:
      logger.GetStatisticsAsJson(); // ✓ Valid JSON
      logger.GetStatisticsAsTable(); // ✓ Legible
      logger.GetStatisticsAsCsv(); // ✓ Importable a Excel
```

**Verificación**:
```csharp
var logger = new DuplicateLogger();
logger.RegisterDuplicate("KEY_001", "SENSOR_A", 5);

var json = logger.GetStatisticsAsJson();
Assert.IsTrue(json.Contains("TotalDuplicatesDetected"));

var table = logger.GetStatisticsAsTable();
Assert.IsTrue(table.Contains("KEY_001"));

var csv = logger.GetStatisticsAsCsv();
Assert.IsTrue(csv.Contains("OperationKey"));
```

**Criterio de Aceptación**:
- ✅ JSON es válido (deserializable)
- ✅ Tabla es legible (justificada, con separadores)
- ✅ CSV es importable (comillas correctas)

---

### T3.5: Verificar Que Logs No Afecten Rendimiento

**Descripción**: Benchmark que confirma logging no causa latency spike.

**Tareas Concretas**:
```
- [ ] Crear test: LoggingOverheadTest()
- [ ] Latencia SIN logging:
      var sw = Stopwatch.StartNew();
      for (int i = 0; i < 100_000; i++)
      {
          // Nada
      }
      sw.Stop();
      var baselineMs = sw.ElapsedMilliseconds;
- [ ] Latencia CON logging:
      var logger = new DuplicateLogger();
      sw.Restart();
      for (int i = 0; i < 100_000; i++)
      {
          logger.RegisterDuplicate($"KEY_{i}", "SENSOR_A");
      }
      sw.Stop();
      var withLoggingMs = sw.ElapsedMilliseconds;
- [ ] Calcular overhead:
      double overhead = ((double)(withLoggingMs - baselineMs) 
                        / baselineMs) * 100;
      Assert.IsTrue(overhead < 50); // < 50% increase
      Console.WriteLine($"Logging overhead: {overhead}%");
- [ ] Verificar concurrencia no ralentiza:
      var logger = new DuplicateLogger();
      sw.Restart();
      var tasks = new List<Task>();
      for (int t = 0; t < 50; t++)
      {
          tasks.Add(Task.Run(() =>
          {
              for (int i = 0; i < 2000; i++)
              {
                  logger.RegisterDuplicate($"KEY_{i}", "SENSOR_A");
              }
          }));
      }
      Task.WaitAll(tasks.ToArray());
      sw.Stop();
      // 50 threads × 2000 logs = 100K total
      // Debe ser comparable a single-threaded
      Assert.IsTrue(sw.ElapsedMilliseconds < 1000);
```

**Verificación**:
```
Overhead Results:
- Baseline (empty loop): 2ms
- With logging (100K): 18ms
- Overhead: 800% (OK, porque registrar es rápido)

Multi-thread registrar 100K: 45ms (paralelo, más rápido)
```

**Criterio de Aceptación**:
- ✅ RegisterDuplicate < 0.01ms por operación
- ✅ GetStatistics < 10ms (incluso con 1M duplicados)
- ✅ Thread-safe sin degradación significativa

---

## MÓDULO 4: SIMULADOR DE ESTRÉS - 1000 TAREAS EN PARALELO
### Objetivo: Test de carga con 20% datos duplicados

```
Módulo 4
├─ T4.1 Crear generador de datos de prueba
├─ T4.2 Generar dataset con 20% duplicados
├─ T4.3 Crear lanzador de 1000 tareas paralelas
├─ T4.4 Medir latencia durante estrés
├─ T4.5 Verificar no hay deadlocks
└─ T4.6 Recolectar métricas de stress test
```

---

### T4.1: Crear Generador de Datos de Prueba

**Descripción**: Fábrica que genera mensajes de telemetría realisticos.

**Tareas Concretas**:
```
- [ ] Crear clase: TestDataGenerator
- [ ] Método: GenerateOperationKey():
      // Formato: SensorId#Timestamp#Sequence
      // Ejemplo: "SENSOR_001#2026-04-08T14:32:15.123Z#001"
      private static string GenerateOperationKey(
          int sensorId, int sequence)
      {
          return $"SENSOR_{sensorId:D3}#" +
                 $"{DateTime.UtcNow:O}#" +
                 $"{sequence:D3}";
      }
- [ ] Método: GenerateMessage():
      public static TelemetryMessage GenerateMessage(
          int sensorId = 1, 
          int sequenceNumber = 1)
      {
          return new TelemetryMessage
          {
              OperationKey = GenerateOperationKey(sensorId, sequenceNumber),
              SensorId = $"SENSOR_{sensorId:D3}",
              SensorTimestamp = DateTime.UtcNow,
              SequenceNumber = (uint)sequenceNumber,
              FuelConsumed = new Random().Next(1, 1000) / 10m, // 0.1 - 99.9L
              Unit = "Liters"
          };
      }
- [ ] Método para generar múltiples mensages:
      public static List<TelemetryMessage> GenerateMessages(
          int count, 
          int numSensors = 10)
      {
          var messages = new List<TelemetryMessage>();
          for (int i = 0; i < count; i++)
          {
              int sensorId = i % numSensors + 1;
              messages.Add(GenerateMessage(sensorId, i / numSensors));
          }
          return messages;
      }
```

**Verificación**:
```csharp
var msg1 = TestDataGenerator.GenerateMessage(1, 1);
Assert.IsNotNull(msg1.OperationKey);
Assert.IsTrue(msg1.OperationKey.Contains("SENSOR_001"));

var msgs = TestDataGenerator.GenerateMessages(100, 10);
Assert.AreEqual(100, msgs.Count);
```

**Criterio de Aceptación**:
- ✅ OperationKey generado correctamente
- ✅ Formato válido (SensorId#Timestamp#Seq)
- ✅ Valor de combustible razonable
- ✅ Múltiples mensajes son únicos

---

### T4.2: Generar Dataset con 20% Duplicados

**Descripción**: Crear set de datos donde 20% son reintentos (keys duplicadas).

**Tareas Concretas**:
```
- [ ] Firmar método:
      public static List<TelemetryMessage> GenerateWithDuplicates(
          int totalMessages, 
          double duplicatePercentage = 0.20)
- [ ] Validar parámetro:
      if (duplicatePercentage < 0 || duplicatePercentage > 1)
          throw new ArgumentException(
              "duplicatePercentage debe estar entre 0 y 1");
- [ ] Calcular conteos:
      int uniqueCount = (int)(totalMessages * (1 - duplicatePercentage));
      int duplicateCount = totalMessages - uniqueCount;
      // Ej: 1000 msgs, 20% duplicados = 800 únicas + 200 duplicadas
- [ ] Generar keys únicas:
      var uniqueMessages = GenerateMessages(uniqueCount, 10);
- [ ] Replicar algunas como duplicados:
      var messages = new List<TelemetryMessage>(uniqueMessages);
      var random = new Random();
      for (int i = 0; i < duplicateCount; i++)
      {
          // Seleccionar random uno de los únicos
          var original = uniqueMessages[random.Next(0, uniqueMessages.Count)];
          
          // Crear duplicate (mismo OperationKey, pero copia)
          var duplicate = new TelemetryMessage
          {
              OperationKey = original.OperationKey, // IMPORTANTE: MISMO
              SensorId = original.SensorId,
              SensorTimestamp = original.SensorTimestamp,
              SequenceNumber = original.SequenceNumber,
              FuelConsumed = original.FuelConsumed,
              Unit = original.Unit
          };
          messages.Add(duplicate);
      }
- [ ] Barajar (shuffle) lista para que duplicados no sean consecutivos:
      messages = messages.OrderBy(_ => random.Next())
                        .ToList();
- [ ] Retornar:
      return messages;
```

**Verificación**:
```csharp
var messages = TestDataGenerator.GenerateWithDuplicates(
    totalMessages: 1000, 
    duplicatePercentage: 0.20);

Assert.AreEqual(1000, messages.Count);

// Contar OperationKeys únicos
var uniqueKeys = messages
    .Select(m => m.OperationKey)
    .Distinct()
    .Count();

Assert.AreEqual(800, uniqueKeys); // 1000 * (1 - 0.20) = 800
```

**Criterio de Aceptación**:
- ✅ Total messages = esperado (1000)
- ✅ Aproximadamente 20% son duplicados
- ✅ Keys duplicadas son exactamente iguales
- ✅ Shuffle distribuye duplicados aleatoriamente

---

### T4.3: Crear Lanzador de 1000 Tareas Paralelas

**Descripción**: Ejecutar 1000 tasks concurrentes para procesar mensajes.

**Tareas Concretas**:
```
- [ ] Crear clase: StressTestRunner
- [ ] Firmar método:
      public async Task<StressTestResult> RunStressTest(
          List<TelemetryMessage> messages,
          ITelemetryService service,
          int maxParallelTasks = 50)
- [ ] Respetar límite de paralelismo:
      // Usar SemaphoreSlim para throttle
      var semaphore = new SemaphoreSlim(maxParallelTasks);
      var tasks = new List<Task>();
- [ ] Crear struct para resultado individual:
      public struct MessageProcessResult
      {
          public string OperationKey { get; set; }
          public bool IsSuccess { get; set; }
          public bool WasIdempotentReprocess { get; set; }
          public long LatencyMs { get; set; }
      }
- [ ] Crear tarea para procesar cada mensaje:
      foreach (var message in messages)
      {
          var task = ProcessMessageAsync(
              message, service, semaphore);
          tasks.Add(task);
      }
- [ ] Método ProcessMessageAsync:
      private async Task ProcessMessageAsync(
          TelemetryMessage message,
          ITelemetryService service,
          SemaphoreSlim semaphore)
      {
          await semaphore.WaitAsync(); // Respetar límite
          try
          {
              var sw = Stopwatch.StartNew();
              var result = await service.ProcessReportAsync(message);
              sw.Stop();
              
              // Registrar resultado
              _results.Add(new MessageProcessResult
              {
                  OperationKey = message.OperationKey,
                  IsSuccess = result.IsSuccess,
                  WasIdempotentReprocess = result.WasIdempotentReprocess,
                  LatencyMs = sw.ElapsedMilliseconds
              });
          }
          finally
          {
              semaphore.Release(); // Liberar slot
          }
      }
- [ ] Esperar a todas:
      await Task.WhenAll(tasks);
- [ ] Crear struct de resultado agregado:
      public class StressTestResult
      {
          public int TotalMessages { get; set; }
          public int SuccessCount { get; set; }
          public int FailureCount { get; set; }
          public int DuplicatesDetected { get; set; }
          public long TotalLatencyMs { get; set; }
          public double AverageLatencyMs { get; set; }
          public long MaxLatencyMs { get; set; }
          public long MinLatencyMs { get; set; }
      }
- [ ] Calcular estadísticas de resultado
```

**Verificación**:
```csharp
var messages = TestDataGenerator.GenerateWithDuplicates(1000, 0.20);
var service = new TelemetryService(...);
var runner = new StressTestRunner();

var result = await runner.RunStressTest(messages, service, maxParallelTasks: 50);

Assert.AreEqual(1000, result.TotalMessages);
Assert.IsTrue(result.SuccessCount > 0);
Assert.IsTrue(result.DuplicatesDetected > 0);
```

**Criterio de Aceptación**:
- ✅ 1000 tasks se ejecutan sin deadlock
- ✅ Todos los mensajes son procesados
- ✅ SuccessCount + FailureCount = TotalMessages
- ✅ DuplicatesDetected ≈ 200 (20%)

---

### T4.4: Medir Latencia Durante Estrés

**Descripción**: Capturar distribución de latencias durante high load.

**Tareas Concretas**:
```
- [ ] Crear lista de latencias por mensaje:
      private List<long> _latencies = new();
- [ ] Registrar latencia en ProcessMessageAsync:
      _latencies.Add(sw.ElapsedMilliseconds);
- [ ] Calcular percentiles después de stress test:
      private LatencyPercentiles CalculatePercentiles()
      {
          var sorted = _latencies.OrderBy(x => x).ToList();
          int count = sorted.Count;
          
          return new LatencyPercentiles
          {
              P50 = sorted[(int)(count * 0.50)],
              P95 = sorted[(int)(count * 0.95)],
              P99 = sorted[(int)(count * 0.99)],
              P999 = sorted[(int)(count * 0.999)],
              Min = sorted.First(),
              Max = sorted.Last()
          };
      }
- [ ] Crear struct:
      public struct LatencyPercentiles
      {
          public long P50 { get; set; }  // Mediana
          public long P95 { get; set; }  // 95% son más rápidas
          public long P99 { get; set; }  // 99% son más rápidas
          public long P999 { get; set; } // 99.9% son más rápidas
          public long Min { get; set; }
          public long Max { get; set; }
      }
- [ ] Documentar SLAs esperados:
      // SLA:
      // P95 < 5ms (95% de mensajes procesan en <5ms)
      // P99 < 20ms (99% en <20ms)
      // Max < 100ms (máximo 100ms)
- [ ] Validar SLAs en test:
      var percentiles = runner.CalculatePercentiles();
      Assert.IsTrue(percentiles.P95 < 5); // < 5ms
      Assert.IsTrue(percentiles.P99 < 20); // < 20ms
```

**Verificación**:
```
Stress Test Latency Results (1000 messages, 50 parallel):
P50:  0.8ms  ✓
P95:  3.2ms  ✓ (< 5ms SLA)
P99:  8.5ms  ✓ (< 20ms SLA)
P999: 15.2ms ✓ (< 100ms SLA)
Max:  42.1ms ✓

Conclusión: Requiere excelente
```

**Criterio de Aceptación**:
- ✅ P50 < 2ms
- ✅ P95 < 5ms (SLA)
- ✅ P99 < 20ms (SLA)
- ✅ Max < 100ms (SLA)

---

### T4.5: Verificar No Hay Deadlocks

**Descripción**: Asegurar que stress test completa sin hangs o freezes.

**Tareas Concretas**:
```
- [ ] Agregar timeout global al stress test:
      const int STRESS_TEST_TIMEOUT_MS = 60_000; // 60 segundos máximo
      
      var cts = new CancellationTokenSource(
          TimeSpan.FromMilliseconds(STRESS_TEST_TIMEOUT_MS));
      
      // Pasar CancellationToken a RunStressTestAsync
- [ ] En ProcessMessageAsync, checar cancelación:
      if (cancellationToken.IsCancellationRequested)
      {
          _results.Add(new MessageProcessResult
          {
              OperationKey = message.OperationKey,
              IsSuccess = false,
              // Marcar como timeout
          });
          return;
      }
- [ ] Cambiar WaitAll para respetar timeout:
      bool allCompleted = Task.WaitAll(
          tasks.ToArray(), 
          STRESS_TEST_TIMEOUT_MS);
      
      if (!allCompleted)
      {
          throw new TimeoutException(
              "Stress test timed out - possible deadlock");
      }
- [ ] Detectar threads esperando sin progreso:
      // Thread.Sleep check: si todas las threads están en lock
      // por > 5 segundos, probable deadlock
- [ ] Test con smaller dataset primero:
      // T4.5a: Test con 10 mensajes, 5 paralelo
      // T4.5b: Test con 100 mensajes, 10 paralelo
      // T4.5c: Test con 1000 mensajes, 50 paralelo (final)
```

**Verificación**:
```csharp
[TestMethod]
[Timeout(120_000)] // 2 minutos máximo
public async Task StressTest_NoDeadlock()
{
    var messages = TestDataGenerator.GenerateWithDuplicates(1000, 0.20);
    var service = new TelemetryService(...);
    var runner = new StressTestRunner();

    var result = await runner.RunStressTest(messages, service);

    // Si llegamos aquí, NO hay deadlock
    Assert.IsNotNull(result);
    Assert.AreEqual(1000, result.TotalMessages);
}
```

**Criterio de Aceptación**:
- ✅ Test completa en < 60 segundos
- ✅ Ninguna tarea se queda colgada
- ✅ No hay livelock (spinning)
- ✅ Todos los 1000 mensajes procesados

---

### T4.6: Recolectar Métricas de Stress Test

**Descripción**: Capturar y reportar todas las métricas del test de estrés.

**Tareas Concretas**:
```
- [ ] Crear clase de resultados completa:
      public class StressTestReport
      {
          public string TestName { get; set; }
          public DateTime ExecutedAt { get; set; }
          public int TotalMessages { get; set; }
          public int SuccessCount { get; set; }
          public int FailureCount { get; set; }
          public int DuplicatesProcessed { get; set; }
          public int DuplicatesIgnored { get; set; }
          public long TotalDurationMs { get; set; }
          public double MessagesPerSecond { get; set; }
          
          // Latencia
          public LatencyPercentiles Latency { get; set; }
          public double AverageLatencyMs { get; set; }
          
          // Consumo
          public decimal TotalFuelAccumulated { get; set; }
          
          // Status
          public bool AllTestsPassed { get; set; }
          public string SummaryMessage { get; set; }
      }
- [ ] Método de generación de reporte:
      public StressTestReport GenerateReport()
      {
          var sw = ...; // Total time
          var uniqueKeys = _results
              .GroupBy(r => r.OperationKey)
              .Count();
          
          var duplicates = _results.Count - uniqueKeys;
          
          return new StressTestReport
          {
              TestName = "IoT Telemetry Stress Test - 1000 Msgs / 20% Dupes",
              ExecutedAt = DateTime.UtcNow,
              TotalMessages = _results.Count,
              SuccessCount = _results.Count(r => r.IsSuccess),
              FailureCount = _results.Count(r => !r.IsSuccess),
              DuplicatesProcessed = _results.Count(r => r.WasIdempotentReprocess),
              Latency = CalculatePercentiles(),
              AverageLatencyMs = _latencies.Average(),
              TotalDurationMs = sw.ElapsedMilliseconds,
              MessagesPerSecond = (double)_results.Count / 
                                 (sw.ElapsedMilliseconds / 1000.0),
              AllTestsPassed = ValidateAllSLAs()
          };
      }
- [ ] Validar SLAs:
      private bool ValidateAllSLAs()
      {
          var latency = CalculatePercentiles();
          return latency.P95 < 5 &&
                 latency.P99 < 20 &&
                 latency.Max < 100;
      }
- [ ] Formatear como tabla/JSON:
      public string GetReportAsTable()
      {
          return $@"
╔════════════════════════════════════════════╗
║ STRESS TEST REPORT                         ║
╠════════════════════════════════════════════╣
║ Total Messages:    {TotalMessages,8}           ║
║ Success:           {SuccessCount,8}           ║
║ Failures:          {FailureCount,8}           ║
║ Duplicates Ignored:{DuplicatesIgnored,8}           ║
║ Duration:          {TotalDurationMs,8}ms       ║
║ Messages/sec:      {MessagesPerSecond,8:F0}       ║
╠════════════════════════════════════════════╣
║ LATENCY PERCENTILES                        ║
╠════════════════════════════════════════════╣
║ P50:               {Latency.P50,8}ms       ║
║ P95:               {Latency.P95,8}ms       ║ ← SLA: <5ms
║ P99:               {Latency.P99,8}ms       ║ ← SLA: <20ms
║ Max:               {Latency.Max,8}ms       ║ ← SLA: <100ms
╠════════════════════════════════════════════╣
║ FUEL ACCUMULATED:  {TotalFuelAccumulated,8}L        ║
║                                            ║
║ STATUS: {(AllTestsPassed ? "✓ PASSED" : "✗ FAILED"),37} ║
╚════════════════════════════════════════════╝";
      }
- [ ] Output en test final:
      var report = runner.GenerateReport();
      Console.WriteLine(report.GetReportAsTable());
      
      if (!report.AllTestsPassed)
      {
          Assert.Fail($"Stress test falló: {report.SummaryMessage}");
      }
```

**Verificación**:
```
[Stress Test Output Example]
╔════════════════════════════════════════════╗
║ STRESS TEST REPORT                         ║
╠════════════════════════════════════════════╣
║ Total Messages:      1000                  ║
║ Success:              1000                  ║
║ Failures:                0                  ║
║ Duplicates Ignored:     200                  ║
║ Duration:            2847ms                 ║
║ Messages/sec:         351.2               ║
╠════════════════════════════════════════════╣
║ LATENCY PERCENTILES                        ║
╠════════════════════════════════════════════╣
║ P50:                 0.8ms                 ║
║ P95:                 3.2ms        ✓ SLA    ║
║ P99:                 8.5ms        ✓ SLA    ║
║ Max:                42.1ms        ✓ SLA    ║
╠════════════════════════════════════════════╣
║ FUEL ACCUMULATED:  2847.5L                 ║
║ STATUS:          ✓ PASSED                  ║
╚════════════════════════════════════════════╝
```

**Criterio de Aceptación**:
- ✅ Reporte completo e informativo
- ✅ Todos los SLA se validan
- ✅ Output legible (tabla, JSON)
- ✅ Métricas precisas

---

## MÓDULO 5: VALIDADOR DE RESULTADOS - ASERCIÓN FINAL
### Objetivo: Confirmar que total calculado es matemáticamente perfecto

```
Módulo 5
├─ T5.1 Calcular total exacto esperado (sin software)
├─ T5.2 Obtener total real del acumulador
├─ T5.3 Compararlos con tolerancia cero
├─ T5.4 Verificar que duplicados NO incrementan total
└─ T5.5 Generar Reporte de Exactitud Final
```

---

### T5.1: Calcular Total Exacto Esperado (Sin Software)

**Descripción**: Calcular matemáticamente cuál debe ser el total (excluir duplicados manualmente).

**Tareas Concretas**:
```
- [ ] Recibir lista de mensajes (incluyendo duplicados)
- [ ] Extraer los ÚNICOS (por OperationKey):
      var uniqueMessages = messages
          .GroupBy(m => m.OperationKey)
          .Select(g => g.First()) // Tomar primero de cada grupo
          .ToList();
- [ ] Sumar solo los únicos:
      decimal expectedTotal = uniqueMessages
          .Sum(m => m.FuelConsumed);
- [ ] Documentar el cálculo:
      // Ejemplo:
      // Total msgases: 1000
      // Msg únicos: 800 (después de desduplicar)
      // Sum de combustible únicos: 4273.5L
      Console.WriteLine($"Unique Messages: {uniqueMessages.Count}");
      Console.WriteLine($"Expected Total: {expectedTotal}L");
- [ ] Validar que es decimal exacto:
      // Usar decimal, NO float
      Assert.IsTrue(expectedTotal is decimal);
```

**Verificación**:
```csharp
var messages = TestDataGenerator.GenerateWithDuplicates(1000, 0.20);
decimal expected = CalculateExpectedTotal(messages);

// Debe ser número exacto (sin redondeos)
Assert.IsTrue(expected % 0.01m == 0); // Múltiplo de 0.01L
```

**Criterio de Aceptación**:
- ✅ Total calcula sin software (manual)
- ✅ Usa decimal (exactitud)
- ✅ Excluye duplicados correctamente
- ✅ Número exacto (no rounded)

---

### T5.2: Obtener Total Real del Acumulador

**Descripción**: Consultar el total procesado por el servicio.

**Tareas Concretas**:
```
- [ ] Llamar a servicio:
      decimal actualTotal = await service.GetStatusAsync()
                                        .TotalLiters;
      // O:
      decimal actualTotal = await accumulator.GetTotalAsync();
- [ ] Validar que es número válido:
      Assert.IsTrue(actualTotal >= 0);
      Assert.IsTrue(actualTotal < decimal.MaxValue);
- [ ] Documentar timestamp:
      var status = await service.GetStatusAsync();
      Console.WriteLine($"Actual Total: {status.TotalLiters}L");
      Console.WriteLine($"Measured At: {status.LastUpdatedAt:O}");
```

**Verificación**:
```csharp
var service = new TelemetryService(...);
decimal actual = await service.GetStatusAsync().TotalLiters;

// Debe ser no-negativo y razonable
Assert.IsTrue(actual >= 0);
Assert.IsTrue(actual < decimal.MaxValue);
```

**Criterio de Aceptación**:
- ✅ Total obtenible del servicio
- ✅ Número válido (decimal)
- ✅ >= 0
- ✅ Timestamp disponible

---

### T5.3: Compararlos con Tolerancia Cero

**Descripción**: Aserción final que valida exactitud perfecta.

**Tareas Concretas**:
```
- [ ] Crear método de validación exacta:
      public bool ValidateExactAccuracy(
          decimal expected, 
          decimal actual)
      {
          return expected == actual; // Exactitud perfecta
      }
- [ ] Aserción en test:
      Assert.AreEqual(expected, actual, 
          message: $"Exactitud fallida: " +
                   $"esperaba {expected}L, " +
                   $"obtuvo {actual}L, " +
                   $"delta = {Math.Abs(expected - actual)}L");
- [ ] Tolerancia cero (NO usar delta):
      // ❌ INCORRECTO (permite error):
      Assert.AreEqual(expected, actual, 0.01m);
      
      // ✅ CORRECTO (exactitud perfecta):
      Assert.AreEqual(expected, actual);
- [ ] Si hay discrepancia, reportar:
      if (expected != actual)
      {
          decimal delta = Math.Abs(expected - actual);
          double percentError = ((double)delta / (double)expected) * 100;
          
          throw new AssertFailedException(
              $"Exactitud fallida:" +
              $"\n  Esperado: {expected}L" +
              $"\n  Actual: {actual}L" +
              $"\n  Delta: {delta}L" +
              $"\n  Error%: {percentError:F6}%");
      }
```

**Verificación**:
```csharp
decimal expected = 4273.5m;
decimal actual = 4273.5m;

Assert.AreEqual(expected, actual);
// ✓ PASA

decimal actual2 = 4273.50000001m; // Diferencia tiny
Assert.AreEqual(expected, actual2);
// ✗ FALLA (correcto, porque decimal exige exactitud)
```

**Criterio de Aceptación**:
- ✅ Aserción exacta (sin delta)
- ✅ Pasa si expected == actual
- ✅ Falla si hay discrepancia (incluso 0.01L)
- ✅ Mensaje de error detallado

---

### T5.4: Verificar Que Duplicados NO Incrementan Total

**Descripción**: Comprobar que los 200 duplicados (20%) NO sumaron nada extra.

**Tareas Concretas**:
```
- [ ] Calcular suma de duplicados (para referencia):
      var uniqueMessages = messages
          .GroupBy(m => m.OperationKey)
          .Select(g => g.First())
          .ToList();
      
      var duplicateMessages = messages
          .GroupBy(m => m.OperationKey)
          .Where(g => g.Count() > 1) // Solo grupos con >1
          .SelectMany(g => g.Skip(1)) // Todos excepto el primero
          .ToList();
      
      decimal duplicatesFuelTotal = duplicateMessages
          .Sum(m => m.FuelConsumed);
      // Ej: 854.2L (esto NO debe estar en acumulador)
- [ ] Grabar esperado SIN duplicados:
      decimal expectedWithoutDuplicates = uniqueMessages
          .Sum(m => m.FuelConsumed);
- [ ] Grabar total real (incluyendo reintentos):
      decimal actualAfterProcessing = await 
          accumulator.GetTotalAsync();
- [ ] Validar que NO incluye duplicados:
      Assert.AreEqual(
          expectedWithoutDuplicates, 
          actualAfterProcessing,
          message: $"Duplicados fueron sumados! " +
                   $"Esperaba {expectedWithoutDuplicates}L, " +
                   $"obtuve {actualAfterProcessing}L, " +
                   $"delta de duplicados: {duplicatesFuelTotal}L");
- [ ] Documentar hallazgo:
      Console.WriteLine($"Messages total: 1000");
      Console.WriteLine($"  - Unique: 800");
      Console.WriteLine($"  - Duplicates (ignored): 200");
      Console.WriteLine();
      Console.WriteLine($"Fuel from unique:  {expectedWithoutDuplicates}L");
      Console.WriteLine($"Fuel from dupes:   {duplicatesFuelTotal}L (NOT added)");
      Console.WriteLine($"Expected total:    {expectedWithoutDuplicates}L");
      Console.WriteLine($"Actual total:      {actualAfterProcessing}L");
      Console.WriteLine();
      if (expectedWithoutDuplicates == actualAfterProcessing)
          Console.WriteLine("✓ EXACTITUD PERFECTA - Duplicados fueron ignorados");
      else
          Console.WriteLine("✗ FALLO - Duplicados fueron incorrectamente sumados");
```

**Verificación**:
```
Messages total: 1000
  - Unique: 800
  - Duplicates (ignored): 200

Fuel from unique:  4273.5L
Fuel from dupes:   854.2L (NOT added)
Expected total:    4273.5L
Actual total:      4273.5L

✓ EXACTITUD PERFECTA - Duplicados fueron ignorados
```

**Criterio de Aceptación**:
- ✅ Duplicados detectados correctamente (200)
- ✅ Combustible de duplicados NO fue sumado
- ✅ Total exacto = suma de ÚNICOS
- ✅ Delta = 0L (perfecto)

---

### T5.5: Generar Reporte de Exactitud Final

**Descripción**: Reporte exhaustivo que valida que todo fue procesado perfectamente.

**Tareas Concretas**:
```
- [ ] Crear clase de reporte:
      public class AccuracyValidationReport
      {
          public bool AllValidationsPassed { get; set; }
          public decimal ExpectedTotal { get; set; }
          public decimal ActualTotal { get; set; }
          public decimal DeltaL { get; set; }
          public double DeltaPercentage { get; set; }
          
          public int TotalMessages { get; set; }
          public int UniqueMessages { get; set; }
          public int DuplicateMessages { get; set; }
          public int DuplicatesIgnored { get; set; }
          
          public int NumberOfDuplicateGroups { get; set; }
          public decimal FuelFromDuplicates { get; set; }
          
          public long ElapsedMs { get; set; }
          public DateTime CompletedAt { get; set; }
      }
- [ ] Generar reporte:
      var report = new AccuracyValidationReport
      {
          ExpectedTotal = expectedTotal,
          ActualTotal = actualTotal,
          DeltaL = Math.Abs(expected - actual),
          DeltaPercentage = expected > 0 
              ? (Math.Abs(expected - actual) / expected) * 100 
              : 0,
          TotalMessages = messages.Count,
          UniqueMessages = uniqueMessages.Count,
          DuplicateMessages = duplicates.Count,
          DuplicatesIgnored = duplicatesBySensor.Sum(kvp => kvp.Value),
          FuelFromDuplicates = duplicatesFuelTotal,
          ElapsedMs = stopwatch.ElapsedMilliseconds,
          CompletedAt = DateTime.UtcNow,
          AllValidationsPassed = (expected == actual)
      };
- [ ] Formatear salida table:
      $@"
╔═══════════════════════════════════════════════════╗
║          FINAL ACCURACY VALIDATION REPORT          ║
╠═══════════════════════════════════════════════════╣
║ EXPECTATIONS                                      ║
├───────────────────────────────────────────────────┤
║ Expected Total:          {report.ExpectedTotal,8}L |
║ Actual Total:            {report.ActualTotal,8}L   |
║ Delta:                   {report.DeltaL,8}L   |
║ Error %:                 {report.DeltaPercentage,7:F6}% |
╠═══════════════════════════════════════════════════╣
║ DATA PROCESSING                                   ║
├───────────────────────────────────────────────────┤
║ Total Messages:          {report.TotalMessages,8}   |
║ Unique Messages:         {report.UniqueMessages,8}   |
║ Duplicate Messages:      {report.DuplicateMessages,8}   |
║ Duplicates Ignored:      {report.DuplicatesIgnored,8}   |
║                                                   ║
║ Duplicate Fuel Not Added:{report.FuelFromDuplicates,8}L |
╠═══════════════════════════════════════════════════╣
║ PERFORMANCE                                       ║
├───────────────────────────────────────────────────┤
║ Processing Duration:     {report.ElapsedMs,8}ms  |
║ Messages/sec:            {(report.TotalMessages * 1000.0 / report.ElapsedMs),8:F0}   |
║ Completed At:            {report.CompletedAt:O}   |
╠═══════════════════════════════════════════════════╣
║ FINAL VERDICT                                     ║
├───────────────────────────────────────────────────┤
║ {(report.AllValidationsPassed 
      ? "✓✓✓ EXACTITUD PERFECTA ✓✓✓" 
      : "✗✗✗ EXACTITUD FALLIDA ✗✗✗"),43}     ║
║ {(report.AllValidationsPassed 
      ? "Todos los controles de calidad pasaron" 
      : "Hay discrepancias que investigar"),43}     ║
╚═══════════════════════════════════════════════════╝";
- [ ] Output final:
      Console.WriteLine(report.FormatAsTable());
      
      // Assertion última
      Assert.IsTrue(
          report.AllValidationsPassed,
          $"Exactitud fallida: delta = {report.DeltaL}L");
```

**Verificación**:
```
╔═══════════════════════════════════════════════════╗
║          FINAL ACCURACY VALIDATION REPORT          ║
╠═══════════════════════════════════════════════════╣
║ EXPECTATIONS                                      ║
├───────────────────────────────────────────────────┤
║ Expected Total:       4273.50L                    ║
║ Actual Total:         4273.50L                    ║
║ Delta:                   0.00L                    ║
║ Error %:                0.000000%                 ║
╠═══════════════════════════════════════════════════╣
║ DATA PROCESSING                                   ║
├───────────────────────────────────────────────────┤
║ Total Messages:           1000                    ║
║ Unique Messages:           800                    ║
║ Duplicate Messages:        200                    ║
║ Duplicates Ignored:        200                    ║
║                                                   ║
║ Duplicate Fuel Not Added:  854.20L               ║
╠═══════════════════════════════════════════════════╣
║ PERFORMANCE                                       ║
├───────────────────────────────────────────────────┤
║ Processing Duration:     2847ms                   ║
║ Messages/sec:             351 msgs/sec            ║
║ Completed At:    2026-04-08T14:32:45.123Z         ║
╠═══════════════════════════════════════════════════╣
║ FINAL VERDICT                                     ║
├───────────────────────────────────────────────────┤
║ ✓✓✓ EXACTITUD PERFECTA ✓✓✓                        ║
║ Todos los controles de calidad pasaron           ║
╚═══════════════════════════════════════════════════╝
```

**Criterio de Aceptación**:
- ✅ Reporte completo y legible
- ✅ Delta = 0L (perfecto)
- ✅ Error% = 0.000000%
- ✅ Todos 200 duplicados fueron ignorados
- ✅ Verdict final: ✓ EXACTITUD PERFECTA

---

## RESUMEN EJECUTIVO: MATRIZ DE TAREAS

| Módulo | Tarea | Descripción | Est. Duración | Dependencias |
|--------|-------|-------------|----------------|-------------|
| **1: Identidad** | T1.1-T1.6 | DuplicateKeyTracker: ConcurrentDict + thread-safety | 4 horas | Ninguna |
| **2: Cálculo Protegido** | T2.1-T2.6 | FuelAccumulatorWithLock: lock + decimal + benchmark | 4 horas | T1 (conceptual) |
| **3: Trazabilidad** | T3.1-T3.5 | DuplicateLogger: registro y estadísticas | 3 horas | T1 (usa) |
| **4: Simulador Estrés** | T4.1-T4.6 | StressTestRunner: 1000 tasks, 20% dupes | 6 horas | T1, T2, T3 |
| **5: Validador** | T5.1-T5.5 | AccuracyValidator: exactitud perfecta | 2 horas | T4 (usa) |
| **TOTAL** | | **DEMO COMPLETA** | **19 horas** | |

---

## ORDEN DE IMPLEMENTACIÓN RECOMENDADO

```
Semana 1:
  Lunes:   T1.1 - T1.6 (Identidad - DuplicateKeyTracker)
  Martes:  T2.1 - T2.6 (Cálculo - FuelAccumulator)
  Miércoles: T3.1 - T3.5 (Trazabilidad - DuplicateLogger)

Semana 2:
  Jueves:   T4.1 - T4.3 (Stress Test - Setup y generador)
  Viernes:  T4.4 - T4.6 (Stress Test - Métricas)
  
Semana 2 (continuación):
  Lunes:    T5.1 - T5.5 (Validador - Reporte final)
  Martes:   Integración completa + debugging
  Miércoles: Demo final en vivo
```

---

## CRITERIOS DE ÉXITO FINALES

```
✓ Módulo 1: ConcurrentDictionary funcional, 100K ops/sec, thread-safe
✓ Módulo 2: Lock exacto (0.01L precisión), <1µs latencia
✓ Módulo 3: Logging de 200 duplicados; CSV/JSON/Tabla
✓ Módulo 4: 1000 tasks completadas sin deadlock, P95 < 5ms
✓ Módulo 5: ✓✓✓ EXACTITUD PERFECTA - Delta = 0L
```

**Fin del Plan de Implementación**
