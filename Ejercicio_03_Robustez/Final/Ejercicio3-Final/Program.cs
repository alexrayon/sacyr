using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sacyr.Telemetry.RobustDemo
{
    public class RobustTelemetryService
    {
        private decimal _totalFuel = 0;
        private readonly object _lock = new();
        private readonly ConcurrentDictionary<string, byte> _processedKeys = new();
        private int _duplicatesBlocked = 0;
        public int DuplicatesBlocked => _duplicatesBlocked;

        public async Task ProcessDataAsync(decimal liters, string opKey)
        {
            // 1. Cláusula de Guarda: Idempotencia (Falla Rápido)
            if (!_processedKeys.TryAdd(opKey, 0))
            {
                System.Threading.Interlocked.Increment(ref _duplicatesBlocked);
                return;
            }

            // 2. Sección Crítica: Thread-Safety
            lock (_lock)
            {
                _totalFuel += liters;
            }

            await Task.Yield(); 
        }

        public decimal GetFinalTotal() => _totalFuel;
    }

    public class Program
    {
        public static async Task Main()
        {
            var service = new RobustTelemetryService();
            var tasks = new List<Task>();
            
            Console.WriteLine("--- SIMULACIÓN DE TELEMETRÍA ROBUSTA SACYR ---");

            for (int i = 0; i < 1000; i++)
            {
                // Generamos una clave cada 2 envíos para forzar duplicados
                string key = $"OP-{(i / 2)}"; 
                tasks.Add(service.ProcessDataAsync(10, key));
            }

            await Task.WhenAll(tasks);

            Console.WriteLine($"Total Acumulado: {service.GetFinalTotal()}L");
            Console.WriteLine($"Duplicados Bloqueados: {service.DuplicatesBlocked}");
            Console.WriteLine($"Resultado: {(service.GetFinalTotal() == 5000 ? "ÉXITO: Precisión Total" : "FALLO")}");
        }
    }
}
