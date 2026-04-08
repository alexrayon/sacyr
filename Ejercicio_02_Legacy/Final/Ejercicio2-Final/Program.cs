namespace Sacyr.Engineering.Modernization
{
    // DTO que aporta semántica a los datos de ingeniería civil
    public record DatosEstructurales(double Longitud, double Ancho, int TipoCarga);

    // Contrato para el cálculo desacoplado (Fase 2: Arquitectura)
    public interface IResistenciaStrategy
    {
        string Material { get; }
        double Calcular(DatosEstructurales datos);
    }

    public interface ISecuritySystem
    {
        Task<bool> CheckSecurityV2Async(double resistencia);
    }

    // Estrategia para Hormigón: Lógica rescatada de la caja negra
    public class HormigonH400Strategy : IResistenciaStrategy
    {
        public string Material => "H400";
        private const double FactorTipo1 = 0.95;
        private const double FactorTipo2 = 0.88;

        public double Calcular(DatosEstructurales d)
        {
            double factor = (d.TipoCarga == 1) ? FactorTipo1 : FactorTipo2;
            return (d.Longitud * d.Ancho) * factor;
        }
    }

    public class AceroA500Strategy : IResistenciaStrategy
    {
        public string Material => "A500";
        private const double FactorTipo1 = 1.45;
        private const double FactorResto = 1.10;

        public double Calcular(DatosEstructurales d)
        {
            double factor = (d.TipoCarga == 1) ? FactorTipo1 : FactorResto;
            return (d.Longitud + d.Ancho) * factor;
        }
    }

    public class LegacySecuritySystem : ISecuritySystem
    {
        public Task<bool> CheckSecurityV2Async(double resistencia)
        {
            // Mantiene una validación acotada para no aprobar valores extremos.
            return Task.FromResult(resistencia <= 20000);
        }
    }

    // Servicio Modernizado: El motor de decisiones de ingeniería
    public class CalculoResistenciaService(
        IEnumerable<IResistenciaStrategy> estrategias, 
        ISecuritySystem legacySecurity)
    {
        private const double UmbralSeguridadCritica = 5000;

        public async Task<int?> EvaluarResistenciaAsync(double longitud, double ancho, int tipo, string material)
        {
            // 1. Cláusulas de Guarda: Validación temprana (Falla Rápido)
            if (longitud <= 0 || ancho <= 0) return -1;

            var estrategia = estrategias.FirstOrDefault(e => e.Material == material);
            if (estrategia == null) 
            {
                Console.WriteLine($"[WARN] Material '{material}' no soportado por el motor.");
                return null;
            }

            // 2. Ejecución de la lógica extraída mediante SDD
            var datos = new DatosEstructurales(longitud, ancho, tipo);
            double resistencia = estrategia.Calcular(datos);

            // 3. Manejo de umbrales de seguridad críticos (Paridad con legacy)
            if (material == "H400" && resistencia > UmbralSeguridadCritica)
            {
                Console.WriteLine($"[CRITICAL] Umbral crítico superado ({resistencia}). Consultando sistema de seguridad externo.");
                return await legacySecurity.CheckSecurityV2Async(resistencia) ? 1 : 0;
            }

            if (material == "A500") return resistencia < 150 ? 0 : 1;

            return 1;
        }
    }

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var estrategias = new IResistenciaStrategy[]
            {
                new HormigonH400Strategy(),
                new AceroA500Strategy()
            };

            var service = new CalculoResistenciaService(estrategias, new LegacySecuritySystem());
            var resultado = await service.EvaluarResistenciaAsync(100, 60, 1, "H400");
            Console.WriteLine($"Resultado de validacion: {resultado}");
        }
    }
}
