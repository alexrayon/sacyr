using System;
using System.Globalization;

namespace AnomaliaDetector
{
    public class RecordParser
    {
        public RegistroObra ParsearLinea(string linea, int numeroLinea)
        {
            var campos = linea.Split(',');
            if (campos.Length != 6)
            {
                throw new FormatException($"Número incorrecto de campos en la línea {numeroLinea}. Esperados 6, encontrados {campos.Length}.");
            }

            string idObra = campos[0].Trim();
            string nombreObra = campos[1].Trim();
            DateTime? fechaInicio = ParsearFecha(campos[2].Trim());
            DateTime? fechaFin = string.IsNullOrEmpty(campos[3].Trim()) ? (DateTime?)null : ParsearFecha(campos[3].Trim());
            decimal? presupuesto = ParsearDecimal(campos[4].Trim());
            string estado = campos[5].Trim();

            return new RegistroObra(idObra, nombreObra, fechaInicio, fechaFin, presupuesto, estado, numeroLinea);
        }

        private DateTime? ParsearFecha(string fechaStr)
        {
            if (string.IsNullOrEmpty(fechaStr)) return null;
            if (DateTime.TryParseExact(fechaStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fecha))
            {
                return fecha;
            }
            return null; // Indica error de formato
        }

        private decimal? ParsearDecimal(string decimalStr)
        {
            if (string.IsNullOrEmpty(decimalStr)) return null;
            if (decimal.TryParse(decimalStr, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal valor))
            {
                return valor;
            }
            return null; // Indica error de formato
        }
    }
}