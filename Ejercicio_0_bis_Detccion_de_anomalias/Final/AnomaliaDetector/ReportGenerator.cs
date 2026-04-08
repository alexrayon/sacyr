using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AnomaliaDetector
{
    public class ReportGenerator
    {
        public void GenerarReporte(List<Anomalia> anomalias, string formato, string rutaSalida, int totalRegistros)
        {
            int registrosValidos = totalRegistros - anomalias.Count; // Simplificado, asume una anomalía por registro inválido

            if (formato.ToLower() == "txt")
            {
                GenerarTxt(anomalias, rutaSalida, totalRegistros, registrosValidos);
            }
            else if (formato.ToLower() == "json")
            {
                GenerarJson(anomalias, rutaSalida, totalRegistros, registrosValidos);
            }
            else
            {
                throw new ArgumentException("Formato no soportado. Use 'txt' o 'json'.");
            }
        }

        private void GenerarTxt(List<Anomalia> anomalias, string rutaSalida, int totalRegistros, int registrosValidos)
        {
            using (var writer = new StreamWriter(rutaSalida))
            {
                writer.WriteLine("Numero_Linea,Tipo_Error,Descripcion");
                foreach (var anomalia in anomalias)
                {
                    writer.WriteLine($"{anomalia.NumeroLinea},{anomalia.TipoError},{anomalia.Descripcion}");
                }
                writer.WriteLine($"Total registros procesados: {totalRegistros}, Registros válidos: {registrosValidos}, Anomalías detectadas: {anomalias.Count}");
            }
        }

        private void GenerarJson(List<Anomalia> anomalias, string rutaSalida, int totalRegistros, int registrosValidos)
        {
            var data = new
            {
                Anomalias = anomalias,
                Resumen = new
                {
                    TotalRegistros = totalRegistros,
                    RegistrosValidos = registrosValidos,
                    AnomaliasDetectadas = anomalias.Count
                }
            };
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(rutaSalida, json);
        }
    }
}