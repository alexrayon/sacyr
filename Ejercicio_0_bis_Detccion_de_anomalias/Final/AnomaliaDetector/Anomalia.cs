namespace AnomaliaDetector
{
    public class Anomalia
    {
        public int NumeroLinea { get; set; }
        public string TipoError { get; set; } // Código como A001
        public string Descripcion { get; set; }

        public Anomalia(int numeroLinea, string tipoError, string descripcion)
        {
            NumeroLinea = numeroLinea;
            TipoError = tipoError;
            Descripcion = descripcion;
        }

        public override string ToString()
        {
            return $"Línea {NumeroLinea}: {TipoError} - {Descripcion}";
        }
    }
}