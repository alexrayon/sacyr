using System;
using System.Collections.Generic;

namespace AnomaliaDetector
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Uso: AnomaliaDetector <ruta_entrada.txt> <ruta_salida> <formato: txt|json>");
                return;
            }

            string rutaEntrada = args[0];
            string rutaSalida = args[1];
            string formato = args[2];

            var processor = new MainProcessor();
            try
            {
                var anomalias = processor.ProcesarArchivo(rutaEntrada, rutaSalida, formato);
                Console.WriteLine($"Procesamiento completado. Anomalías detectadas: {anomalias.Count}");
                foreach (var anomalia in anomalias)
                {
                    Console.WriteLine(anomalia.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
