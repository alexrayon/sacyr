using System;
using Xunit;
using TbmTelemetry.Core.Domain;
using TbmTelemetry.Core.Services;

namespace TbmTelemetry.Tests
{
    public class DeviationMonitorTests
    {
        [Fact]
        public void Scenario1_GivenZeroDeviation_WhenCalculated_StateIsEnRuta()
        {
            // Arrange
            var monitor = new TbmMonitorService();
            var teorica = new Point3D(1000.0, 2000.0, 50.0);
            var actual = new Point3D(1000.0, 2000.0+0.005, 50.0); // +0.5 cm en Y

            // Act
            var resultado = monitor.AnalizarLecturaSincrona(actual, teorica);

            // Assert
            Assert.True(resultado.EsPosicionValida);
            Assert.Equal(0.5, resultado.DistanciaDesviacionCm);
            Assert.Equal(NivelSeveridad.EnRuta, resultado.Severidad);
        }

        [Fact]
        public void Scenario2_Given4CmDeviation_WhenCalculated_StateIsPrecaucion()
        {
            // Arrange - Falla de 4cm en eje X
            var monitor = new TbmMonitorService();
            var teorica = new Point3D(1500.0, 2500.0, -20.0);
            var actual = new Point3D(1500.0 + 0.04, 2500.0, -20.0); 

            // Act
            var resultado = monitor.AnalizarLecturaSincrona(actual, teorica);

            // Assert
            Assert.True(resultado.EsPosicionValida);
            Assert.Equal(4.0, resultado.DistanciaDesviacionCm);
            Assert.Equal(NivelSeveridad.Precaucion, resultado.Severidad);
        }

        [Fact]
        public void Scenario3_GivenExact5CmDeviation_StateRemainsPrecaucion_AvoidingToleranceLost()
        {
            // Arrange - Demuestra que la solución es resiliente contra IEEE-754 porque usa la regla 25.0 en el core matemático.
            // Una desviación real de 5.0 centímetros NUNCA debe ser interpretada como CRÍTICA.
            var teorica = new Point3D(10.0, 10.0, 10.0);
            var actualEnElBorde = new Point3D(10.0 + 0.05, 10.0, 10.0); // +5.0 cm exactos.
            
            // Act
            var monitorA = new TbmMonitorService();
            var resultBorde = monitorA.AnalizarLecturaSincrona(actualEnElBorde, teorica);

            // Assert
            Assert.Equal(5.0, resultBorde.DistanciaDesviacionCm);
            Assert.Equal(NivelSeveridad.Precaucion, resultBorde.Severidad); 
        }

        [Fact]
        public void Scenario3_Given5Point001CmDeviation_StateEscalatesToCritico()
        {
            // Arrange - Un nanómetro por encima del 5.0 debe saltar (5.001 cm).
            var teorica = new Point3D(10.0, 10.0, 10.0);
            var actualExceso = new Point3D(10.0 + 0.05001, 10.0, 10.0);
            
            // Act
            var monitorB = new TbmMonitorService();
            var resultCritico = monitorB.AnalizarLecturaSincrona(actualExceso, teorica);

            // Assert
            Assert.Equal(NivelSeveridad.Critico, resultCritico.Severidad);
        }

        [Fact]
        public void EdgeCase1_GivenOutOfBoundsCoordinates_ReturnsSensorFailure()
        {
            // Arrange - Coordena que cruza la franja de 10km (Error físico de láser de prisma).
            var monitor = new TbmMonitorService();
            var teorica = new Point3D(10.0, 10.0, 10.0);
            var actualIrreal = new Point3D(99999.0, 0, 0); 

            // Act
            var resultado = monitor.AnalizarLecturaSincrona(actualIrreal, teorica);

            // Assert
            Assert.False(resultado.EsPosicionValida);
            Assert.StartsWith("COORDENADAS_FUERA_DE_RANGO", resultado.MensajeError);
            Assert.Equal(NivelSeveridad.FalloSensor, resultado.Severidad);
        }

        [Fact]
        public void EdgeCase2_GivenImpossibleKinematicJump_ReturnsSensorFailure()
        {
            // Arrange - Una lectura OK, seguida de una lectura a 2 metros de distancia instantánea.
            var monitor = new TbmMonitorService();
            var teorica = new Point3D(10.0, 10.0, 10.0);
            
            var lecturaOk = new Point3D(10.0, 10.0, 10.0);
            var lecturaSaltada = new Point3D(10.0 + 2.0, 10.0, 10.0); // +200cm saltados en el mismo milisegundo

            // Act
            monitor.AnalizarLecturaSincrona(lecturaOk, teorica); 
            var resultadoErrorSalto = monitor.AnalizarLecturaSincrona(lecturaSaltada, teorica);

            // Assert
            Assert.False(resultadoErrorSalto.EsPosicionValida);
            Assert.Equal("SALTO_CINEMATICO_IMPOSIBLE", resultadoErrorSalto.MensajeError);
        }

        [Fact]
        public void Pattern_GivenCriticalSeverity_ObserverEventEmitsData()
        {
            // Arrange
            var monitor = new TbmMonitorService();
            var teorica = new Point3D(5.0, 5.0, 5.0);
            var actualParaCritico = new Point3D(5.2, 5.0, 5.0); // +20cm (Critico)

            bool eventFired = false;
            monitor.OnAlarmaCriticaActivada += (sender, payload) => 
            {
                eventFired = true;
                Assert.Equal(NivelSeveridad.Critico, payload.Severidad);
            };

            // Act
            monitor.AnalizarLecturaSincrona(actualParaCritico, teorica);

            // Assert
            Assert.True(eventFired, "La alarma crítica debio gatillar el patrón observador.");
        }
    }
}
