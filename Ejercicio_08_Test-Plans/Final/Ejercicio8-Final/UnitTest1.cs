using Moq;

namespace Sacyr.Safety.Geofencing.Tests
{
    public record Coordenada(double Lat, double Lon);
    public record ZonaPeligro(Coordenada Centro, double RadioMetros);

    public class GeofencingSecurityTests
    {
        private readonly IAlertService _mockAlerts;
        private readonly GeofencingEngine _engine;
        private readonly ZonaPeligro _zonaCritica;

        public GeofencingSecurityTests()
        {
            _mockAlerts = Mock.Of<IAlertService>();
            _engine = new GeofencingEngine(_mockAlerts);
            _zonaCritica = new ZonaPeligro(new Coordenada(0, 0), 50.0);
        }

        [Theory]
        [InlineData(0, 0, true)]    // Centro: Peligro
        [InlineData(0, 49.9, true)] // Límite interno: Peligro
        [InlineData(0, 50.1, false)]// Límite externo: Seguro
        [InlineData(0, 100, false)] // Lejos: Seguro
        public void Validar_Entrada_En_Zona_Peligro(double lat, double lon, bool debeAlertar)
        {
            // Arrange
            var posicionOperario = new Coordenada(lat, lon);

            // Act
            var resultado = _engine.EvaluarPosicion(posicionOperario, _zonaCritica);

            // Assert
            Assert.Equal(debeAlertar, resultado.AlertaActiva);
            if (debeAlertar)
            {
                Mock.Get(_mockAlerts).Verify(x => x.NotifyDanger(It.IsAny<string>()), Times.AtLeastOnce);
            }
        }

        [Fact]
        public void Sistema_Debe_Ser_Resiliente_A_Coordenadas_Nulas()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => _engine.EvaluarPosicion(null, _zonaCritica));
            Assert.Contains("posicion", ex.Message);
        }
    }
}
