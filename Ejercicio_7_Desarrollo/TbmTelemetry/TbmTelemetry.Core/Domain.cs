using System;

namespace TbmTelemetry.Core.Domain
{
    public enum NivelSeveridad
    {
        EnRuta,         // Desviacion < 2cm
        Precaucion,     // 2cm <= Desviacion <= 5cm
        Critico,        // Desviacion > 5cm
        FalloSensor     // Anomalía de datos
    }

    public record EstadoTrayectoria(
        Point3D CoordenadaActual,
        Point3D CoordenadaTeorica,
        double DistanciaDesviacionCm,
        NivelSeveridad Severidad,
        DateTime TimestampCalculo,
        bool EsPosicionValida,
        string MensajeError = null
    );

    /// <summary>
    /// Value Object inmutable para representar coordenadas en el espacio tridimensional.
    /// </summary>
    public readonly struct Point3D : IEquatable<Point3D>
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Point3D(double x, double y, double z)
        {
            X = x; Y = y; Z = z;
        }

        public bool Equals(Point3D other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Point3D other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    }

    public static class DeviationCalculator
    {
        /// <summary>
        /// Calcula la distancia de la desviación espacial y asigna el nivel de alerta correspondiente.
        /// La determinación de alerta es matemáticamente estricta, evitando errores de punto flotante de Math.Sqrt.
        /// </summary>
        public static (double DistanciaLinealCm, NivelSeveridad SeveridadAsignada) CalcularDesviacion(Point3D actual, Point3D teorica)
        {
            // FÓRMULA EUCLIDIANA TRIDIMENSIONAL EXPLICADA:
            // Obtenemos los Catetos al restar los valores espaciales reales vs los objetivos
            double dxEnMetros = actual.X - teorica.X;
            double dyEnMetros = actual.Y - teorica.Y;
            double dzEnMetros = actual.Z - teorica.Z;

            // Escalamiento al Dominio (centímetros) y limpieza de ruido IEEE-754 (punto flotante de la resta)
            // Limitamos a 6 decimales (sub-micrométrico) para que 10.05 - 10.0 = 0.05 y no 0.0500000000000007
            double dxCm = Math.Round(dxEnMetros * 100.0, 6);
            double dyCm = Math.Round(dyEnMetros * 100.0, 6);
            double dzCm = Math.Round(dzEnMetros * 100.0, 6);

            // D^2 = (ΔX)^2 + (ΔY)^2 + (ΔZ)^2
            // No extraemos la raíz cuadrada aquí para poder someter la medición a una regla determinista 
            // no susceptible a aproximaciones decimales, garantizando perfecta evaluación en los umbrales de seguridad.
            double distanciaCuadraticaCm = (dxCm * dxCm) + (dyCm * dyCm) + (dzCm * dzCm);

            // Expresión Switch: Árbol de decisión inmutable para la severidad comparando "Al cuadrado"
            // Umbral Critico (5.0cm)  -> 5.0^2 = 25.0 cm²
            // Umbral Precaucion(2.0cm)-> 2.0^2 = 4.0 cm²
            NivelSeveridad severidad = distanciaCuadraticaCm switch
            {
                > 25.0 => NivelSeveridad.Critico,
                >= 4.0 => NivelSeveridad.Precaucion,
                _ => NivelSeveridad.EnRuta
            };

            // Recién ahora extraemos D lineal para los metadatos visuales o de registro SCADA
            double distanciaLineal = Math.Sqrt(distanciaCuadraticaCm);

            return (distanciaLineal, severidad);
        }
    }
}
