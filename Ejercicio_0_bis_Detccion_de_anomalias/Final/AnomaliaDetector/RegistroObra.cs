using System;

namespace AnomaliaDetector
{
    public class RegistroObra
    {
        public string IdObra { get; set; }
        public string NombreObra { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public decimal? Presupuesto { get; set; }
        public string Estado { get; set; }
        public int NumeroLinea { get; set; }

        public RegistroObra(string idObra, string nombreObra, DateTime? fechaInicio, DateTime? fechaFin, decimal? presupuesto, string estado, int numeroLinea)
        {
            IdObra = idObra;
            NombreObra = nombreObra;
            FechaInicio = fechaInicio;
            FechaFin = fechaFin;
            Presupuesto = presupuesto;
            Estado = estado;
            NumeroLinea = numeroLinea;
        }

        public override string ToString()
        {
            return $"ID: {IdObra}, Nombre: {NombreObra}, Inicio: {FechaInicio?.ToString("dd/MM/yyyy")}, Fin: {FechaFin?.ToString("dd/MM/yyyy")}, Presupuesto: {Presupuesto}, Estado: {Estado}, Línea: {NumeroLinea}";
        }
    }
}