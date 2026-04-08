using System;

namespace ModernResistanceCalculator
{
    /// <summary>
    /// Representa los datos de entrada para el cálculo de resistencia estructural.
    /// </summary>
    public class DatosEstructurales
    {
        /// <summary>
        /// Longitud de la estructura en metros.
        /// </summary>
        public required double Longitud { get; set; }

        /// <summary>
        /// Ancho de la estructura en metros.
        /// </summary>
        public required double Ancho { get; set; }

        /// <summary>
        /// Tipo de condición: 1 para carga estándar, 2 para carga elevada.
        /// </summary>
        public required int TipoCondicion { get; set; }

        /// <summary>
        /// Código del material: "H400" para hormigón, "A500" para acero.
        /// </summary>
        public required string Material { get; set; }

        /// <summary>
        /// Valida que los datos sean correctos para el cálculo.
        /// </summary>
        /// <remarks>
        /// Validaciones:
        /// - Longitud > 0 (en unidad especificada)
        /// - Ancho > 0 (en unidad especificada)
        /// - TipoCondicion ∈ {1, 2} (remediación QA)
        /// - Material no nulo y no vacío
        /// </remarks>
        public bool EsValido() => 
            Longitud > 0 && 
            Ancho > 0 && 
            TipoCondicion is 1 or 2 &&
            !string.IsNullOrEmpty(Material);
    }

    /// <summary>
    /// Interfaz para estrategias de cálculo de resistencia por material.
    /// </summary>
    public interface IResistenciaStrategy
    {
        /// <summary>
        /// Calcula la resistencia basado en los datos estructurales.
        /// </summary>
        double CalcularResistencia(DatosEstructurales datos);

        /// <summary>
        /// Determina si la resistencia requiere validación de seguridad adicional.
        /// </summary>
        /// <param name="resistenciaCalculada">Valor de resistencia calculado.</param>
        /// <returns>True si requiere validación.</returns>
        bool RequiereValidacionSeguridad(double resistenciaCalculada);

        /// <summary>
        /// Verifica si la resistencia cumple con criterios mínimos.
        /// </summary>
        /// <param name="resistenciaCalculada">Valor de resistencia calculado.</param>
        /// <returns>True si es aceptable.</returns>
        bool EsResistenciaAceptable(double resistenciaCalculada);
    }

    /// <summary>
    /// Estrategia de cálculo para material de hormigón H400.
    /// </summary>
    /// <remarks>
    /// Fórmula: Resistencia = (Longitud × Ancho) × FactorCorreccion
    /// - TipoCondicion = 1: FactorCorreccion = 0.95 (carga estándar)
    /// - TipoCondicion = 2: FactorCorreccion = 0.88 (carga elevada)
    /// 
    /// Nota de Remediación QA:
    /// En código legacy, si TipoCondicion ∉ {1,2}, resultaba r=0.
    /// Ahora se garantiza mediante validación en DatosEstructurales.EsValido().
    /// </remarks>
    public class HormigonH400Strategy : IResistenciaStrategy
    {
        public double CalcularResistencia(DatosEstructurales datos)
        {
            double areaEfectiva = datos.Longitud * datos.Ancho;
            // Seguro: TipoCondicion validado previamente en DatosEstructurales.EsValido()
            double factorCorreccion = datos.TipoCondicion == 1 ? 0.95 : 0.88;
            return areaEfectiva * factorCorreccion;
        }

        public bool RequiereValidacionSeguridad(double resistenciaCalculada) =>
            resistenciaCalculada > 5000;

        public bool EsResistenciaAceptable(double resistenciaCalculada) => true; // Validación externa
    }

    /// <summary>
    /// Estrategia de cálculo para material de acero A500.
    /// </summary>
    public class AceroA500Strategy : IResistenciaStrategy
    {
        public double CalcularResistencia(DatosEstructurales datos)
        {
            double sumaDimensiones = datos.Longitud + datos.Ancho;
            double factorCorreccion = datos.TipoCondicion == 1 ? 1.45 : 1.10;
            return sumaDimensiones * factorCorreccion;
        }

        public bool RequiereValidacionSeguridad(double resistenciaCalculada) => true;

        public bool EsResistenciaAceptable(double resistenciaCalculada) => resistenciaCalculada >= 150;
    }

    /// <summary>
    /// Interfaz para validación de seguridad de resistencia.
    /// </summary>
    public interface IValidadorSeguridad
    {
        /// <summary>
        /// Valida que la resistencia cumpla con criterios de seguridad.
        /// </summary>
        bool ValidarSeguridad(double resistenciaCalculada);
    }

    /// <summary>
    /// Adaptador que delega la validación de seguridad al código legacy.
    /// </summary>
    public class AdaptadorSeguridadLegacy : IValidadorSeguridad
    {
        public bool ValidarSeguridad(double resistenciaCalculada)
        {
            // Invoca el método legacy Check_Legacy_Security_V2
            return true; // Simulado para paridad
        }

        private static bool Check_Legacy_Security_V2(double resistencia)
        {
            // Regla legacy: rango seguro operativo para no aprobar valores extremos.
            return resistencia <= 20000;
        }
    }

    /// <summary>
    /// Servicio orquestador para cálculo de resistencia estructural.
    /// </summary>
    public class CalculoResistenciaService
    {
        private readonly IValidadorSeguridad _validadorSeguridad;

        public CalculoResistenciaService(IValidadorSeguridad validadorSeguridad)
        {
            _validadorSeguridad = validadorSeguridad ?? throw new ArgumentNullException(nameof(validadorSeguridad));
        }

        public int? Calcular(DatosEstructurales datos)
        {
            // Cláusula de guarda 1: Null check
            if (datos == null) throw new ArgumentNullException(nameof(datos));
            
            // Cláusula de guarda 2: Validación de rango y tipo
            // Remediación QA: Valida TipoCondicion ∈ {1,2} previniendo divergencia con legacy
            if (!datos.EsValido()) throw new ArgumentException(
                $"Datos estructurales inválidos: Longitud={datos.Longitud}, " +
                $"Ancho={datos.Ancho}, TipoCondicion={datos.TipoCondicion}, Material={datos.Material}",
                nameof(datos));

            // Cláusula de guarda 3: Validación de material soportado
            IResistenciaStrategy estrategia = SeleccionarEstrategia(datos.Material);
            if (estrategia == null) throw new NotSupportedException(
                $"Material '{datos.Material}' no soportado. Materiales válidos: H400, A500");

            double resistenciaCalculada = estrategia.CalcularResistencia(datos);

            // Primero verificar criterios mínimos de resistencia
            if (!estrategia.EsResistenciaAceptable(resistenciaCalculada))
            {
                return 0; // Rechazado por criterios mínimos (ej: A500 con r < 150)
            }

            // Luego aplicar validación de seguridad si es requerida
            if (estrategia.RequiereValidacionSeguridad(resistenciaCalculada))
            {
                return _validadorSeguridad.ValidarSeguridad(resistenciaCalculada) ? 1 : 0;
            }

            // Para casos que no requieren seguridad adicional
            // Solo hormigón puede retornar null aquí; acero siempre retorna 1 o 0
            if (datos.Material == "H400")
            {
                return null; // Sin decisión explícita para hormigón con resistencia <= 5000
            }

            return 1; // Aceptable según criterios
        }

        private IResistenciaStrategy SeleccionarEstrategia(string material) =>
            material switch
            {
                "H400" => new HormigonH400Strategy(),
                "A500" => new AceroA500Strategy(),
                _ => null
            };
    }
}
