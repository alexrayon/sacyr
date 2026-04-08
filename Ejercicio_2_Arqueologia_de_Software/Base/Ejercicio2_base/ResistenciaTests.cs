using Xunit;
using ModernResistanceCalculator;

namespace ResistenciaTests
{
    public class ResistenciaParidadTests
    {
        private readonly CalculoResistenciaService _servicioModerno;
        private readonly AdaptadorSeguridadLegacy _validadorSeguridad;

        public ResistenciaParidadTests()
        {
            _validadorSeguridad = new AdaptadorSeguridadLegacy();
            _servicioModerno = new CalculoResistenciaService(_validadorSeguridad);
        }

        [Fact]
        public void Escenario_CalculoEstandarHormigon_DebeRetornarNull_ParidadConLegacy()
        {
            // Given
            var datos = new DatosEstructurales
            {
                Longitud = 5.0,
                Ancho = 0.3,
                TipoCondicion = 1,
                Material = "H400"
            };

            // When
            var resultadoModerno = _servicioModerno.Calcular(datos);
            var resultadoLegacy = CalcularLegacy(datos);

            // Then
            Assert.Equal(resultadoLegacy, resultadoModerno);
            Assert.Null(resultadoModerno); // Específicamente null para este caso
        }

        [Fact]
        public void Escenario_FalloResistenciaMinimaAcero_DebeRetornarCero_ParidadConLegacy()
        {
            // Given
            var datos = new DatosEstructurales
            {
                Longitud = 2.0,
                Ancho = 1.5,
                TipoCondicion = 2,
                Material = "A500"
            };

            // When
            var resultadoModerno = _servicioModerno.Calcular(datos);
            var resultadoLegacy = CalcularLegacy(datos);

            // Then
            Assert.Equal(resultadoLegacy, resultadoModerno);
            Assert.Equal(0, resultadoModerno);
        }

        [Fact]
        public void Escenario_ActivacionProtocoloSeguridad_DebeRetornarNull_NoActivacion()
        {
            // Given
            var datos = new DatosEstructurales
            {
                Longitud = 10.0,
                Ancho = 2.0,
                TipoCondicion = 1,
                Material = "H400"
            };

            // When
            var resultadoModerno = _servicioModerno.Calcular(datos);

            // Then
            // Protocolo de seguridad se activa si resistencia > 5000
            // Para este caso: 10*2*0.95 = 19, que es < 5000
            Assert.Null(resultadoModerno);
        }

        [Fact]
        public void Validacion_DatosInvalidos_DebeLanzarExcepcion()
        {
            // Given - datos con dimensión negativa
            var datosInvalidos = new DatosEstructurales
            {
                Longitud = -5.0, // Inválido
                Ancho = 1.0,
                TipoCondicion = 1,
                Material = "H400"
            };

            // When & Then
            Assert.Throws<ArgumentException>(() => _servicioModerno.Calcular(datosInvalidos));
        }

        [Fact]
        public void Validacion_MaterialNoSoportado_DebeLanzarExcepcion()
        {
            // Given
            var datosNoSoportado = new DatosEstructurales
            {
                Longitud = 5.0,
                Ancho = 1.0,
                TipoCondicion = 1,
                Material = "H600" // Material no soportado
            };

            // When & Then
            Assert.Throws<NotSupportedException>(() => _servicioModerno.Calcular(datosNoSoportado));
        }

        // Método auxiliar que simula el cálculo del código legacy
        private int? CalcularLegacy(DatosEstructurales datos)
        {
            if (datos.Material == "H400")
            {
                double r = datos.Longitud * datos.Ancho * (datos.TipoCondicion == 1 ? 0.95 : 0.88);
                return r <= 5000 ? null : (r <= 20000 ? 1 : 0);
            }

            if (datos.Material == "A500")
            {
                double r = (datos.Longitud + datos.Ancho) * (datos.TipoCondicion == 1 ? 1.45 : 1.10);
                return r < 150 ? 0 : (r <= 20000 ? 1 : 0);
            }

            return null; // Material no soportado
        }
    }
}
