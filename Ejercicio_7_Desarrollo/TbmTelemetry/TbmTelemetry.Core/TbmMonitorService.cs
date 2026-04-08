using System;
using TbmTelemetry.Core.Domain;

namespace TbmTelemetry.Core.Services
{
    public interface ITbmMonitorService
    {
        EstadoTrayectoria AnalizarLecturaSincrona(Point3D actual, Point3D teorica);
        event EventHandler<EstadoTrayectoria> OnAlarmaCriticaActivada;
    }

    public class TbmMonitorService : ITbmMonitorService
    {
        public event EventHandler<EstadoTrayectoria> OnAlarmaCriticaActivada;

        // Limites operacionales de seguridad estructural en tuneladora
        private const double LIMITE_OBRA_METROS = 10000.0;
        private const double MAX_SALTO_PREDICCION_CM = 30.0; 

        private Point3D? _ultimaLecturaAceptada = null;

        public EstadoTrayectoria AnalizarLecturaSincrona(Point3D actual, Point3D teorica)
        {
            // --- Cláusula de Guarda Extrema 1: Coordenadas irracionales fuera de la franja territorial ---
            if (Math.Abs(actual.X) > LIMITE_OBRA_METROS || 
                Math.Abs(actual.Y) > LIMITE_OBRA_METROS || 
                Math.Abs(actual.Z) > LIMITE_OBRA_METROS)
            {
                return new EstadoTrayectoria(
                    actual, teorica, 0, NivelSeveridad.FalloSensor, 
                    DateTime.UtcNow, false, "COORDENADAS_FUERA_DE_RANGO_OBRA");
            }

            // --- Cláusula de Guarda de Continuidad 2: Validación Cinemática (Out-of-Bounds relativo) ---
            // Asegura que en un ciclo de PLC (milisegundos) la máquina no haya "saltado" metros mágicamente
            if (_ultimaLecturaAceptada.HasValue)
            {
                var (distanciaSaltoFantasma, _) = DeviationCalculator.CalcularDesviacion(actual, _ultimaLecturaAceptada.Value);
                if (distanciaSaltoFantasma > MAX_SALTO_PREDICCION_CM) 
                {
                    return new EstadoTrayectoria(
                        actual, teorica, 0, NivelSeveridad.FalloSensor, 
                        DateTime.UtcNow, false, "SALTO_CINEMATICO_IMPOSIBLE");
                }
            }

            // 1. Proceder al cálculo matemático inmutable
            var (distanciaLineal, severidadAsignada) = DeviationCalculator.CalcularDesviacion(actual, teorica);

            // 2. Componer la respuesta definitiva
            var estado = new EstadoTrayectoria(
                CoordenadaActual: actual,
                CoordenadaTeorica: teorica,
                DistanciaDesviacionCm: Math.Round(distanciaLineal, 4), // Truncado seguro representativo
                Severidad: severidadAsignada,
                TimestampCalculo: DateTime.UtcNow,
                EsPosicionValida: true
            );

            // 3. Cachear coordenada como segura para el próximo frame
            _ultimaLecturaAceptada = actual;

            // 4. Implementación del Patrón Observador - Gatillar alerta a subsistemas mecánicos de ser necesario
            if (estado.Severidad == NivelSeveridad.Critico)
            {
                // El Invoke (?) chequea si hay suscriptores vivos escuchando, evitando NullReference.
                OnAlarmaCriticaActivada?.Invoke(this, estado);
            }

            return estado;
        }
    }
}
