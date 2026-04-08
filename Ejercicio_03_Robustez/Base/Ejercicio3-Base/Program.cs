using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sacyr.Telemetry.FailingDemo
{
    public class Program
    {
        private static decimal _totalFuel = 0;

        public static async Task Main()
        {
            Console.WriteLine("--- SIMULACIÓN DE FALLO DE TELEMETRÍA ---");
            var tasks = new List<Task>();
            
            // Simulamos 1000 envíos de 10L cada uno. Debería sumar 10,000L.
            // Pero hay concurrencia y no hay control de duplicados.
            for (int i = 0; i < 1000; i++)
            {
                tasks.Add(Task.Run(() => {
                    // Fallo 1: Condición de carrera (No es Thread-Safe)
                    var snapshot = _totalFuel;
                    Thread.SpinWait(5000); // Amplifica la ventana de carrera.
                    _totalFuel = snapshot + 10;
                }));
            }

            await Task.WhenAll(tasks);
            
            Console.WriteLine($"Total Esperado: 10,000L");
            Console.WriteLine($"Total Calculado: {_totalFuel}L");
            Console.WriteLine($"Diferencia (Pérdida de datos): {10000 - _totalFuel}L");
        }
    }
}
