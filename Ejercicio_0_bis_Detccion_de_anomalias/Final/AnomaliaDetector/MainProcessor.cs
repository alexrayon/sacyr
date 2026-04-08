using System;
using System.Collections.Generic;

namespace AnomaliaDetector
{
    public class MainProcessor
    {
        private FileReader fileReader = new FileReader();
        private RecordParser recordParser = new RecordParser();
        private RuleValidator ruleValidator = new RuleValidator();
        private ReportGenerator reportGenerator = new ReportGenerator();

        public List<Anomalia> ProcesarArchivo(string rutaEntrada, string rutaSalida, string formato)
        {
            var lineas = fileReader.LeerArchivo(rutaEntrada);
            var anomalias = new List<Anomalia>();
            int numeroLinea = 1; // Comenzar desde 1 para datos (después del encabezado)

            foreach (var linea in lineas)
            {
                numeroLinea++;
                try
                {
                    var registro = recordParser.ParsearLinea(linea, numeroLinea);
                    anomalias.AddRange(ruleValidator.ValidarRegistro(registro));
                }
                catch (FormatException ex)
                {
                    anomalias.Add(new Anomalia(numeroLinea, "A001", $"Formato de Campo Inválido: {ex.Message}"));
                }
            }

            reportGenerator.GenerarReporte(anomalias, formato, rutaSalida, lineas.Count);
            return anomalias;
        }
    }
}