namespace Sacyr.Tbm.Telemetry
{
    // Representación inmutable de una posición en el espacio 3D
    public record Point3D(double X, double Y, double Z);

    // Resultado de la evaluación de trayectoria
    public enum SeverityLevel { EnRuta, Precaucion, Critico }
    public record TelemetryResult(double DeviationDistance, SeverityLevel Status);

    public class TbmMonitorService
    {
        private const double ThresholdWarning = 0.02; // 2 cm
        private const double ThresholdCritical = 0.05; // 5 cm

        public TelemetryResult EvaluatePosition(Point3D current, Point3D theoretical)
        {
            // 1. Cláusulas de Guarda (Falla Rápido)
            if (current == null || theoretical == null) 
                throw new ArgumentNullException("Los puntos de posición no pueden ser nulos.");

            // 2. Cálculo de Distancia Euclidiana 3D
            // Formula: sqrt((x2-x1)^2 + (y2-y1)^2 + (z2-z1)^2)
            double distance = Math.Sqrt(
                Math.Pow(current.X - theoretical.X, 2) +
                Math.Pow(current.Y - theoretical.Y, 2) +
                Math.Pow(current.Z - theoretical.Z, 2)
            );

            // 3. Clasificación de Severidad (Switch Expression)
            SeverityLevel status = distance switch
            {
                < ThresholdWarning => SeverityLevel.EnRuta,
                < ThresholdCritical => SeverityLevel.Precaucion,
                _ => SeverityLevel.Critico
            };

            return new TelemetryResult(Math.Round(distance, 4), status);
        }
    }

    public static class Program
    {
        public static void Main(string[] args)
        {
            var service = new TbmMonitorService();
            var current = new Point3D(10.01, 5.00, 2.00);
            var theoretical = new Point3D(10.00, 5.00, 2.00);
            var result = service.EvaluatePosition(current, theoretical);

            Console.WriteLine($"Desviacion: {result.DeviationDistance} m");
            Console.WriteLine($"Estado: {result.Status}");
        }
    }
}
