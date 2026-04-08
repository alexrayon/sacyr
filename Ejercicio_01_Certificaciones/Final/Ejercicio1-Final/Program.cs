namespace Sacyr.Engineering.Auditoria
{
    // Modelos de Dominio: Estructura de la verdad contractual
    public record PropuestaCertificacion(Guid PartidaId, decimal Cantidad, decimal PrecioUnitario, DateTime FechaEjecucion);
    public record ContextoContrato(decimal PresupuestoAsignado, decimal AcumuladoAnterior, DateTime InicioObra, bool PartidaCerrada);
    
    public record DetalleFallo(string Codigo, string Descripcion);
    public record InformeAuditoria(bool EsValida, List<DetalleFallo> Errores);

    // Contrato para reglas de auditoría (Fase 2)
    public interface IReglaValidacion
    {
        Task<(bool Cumple, DetalleFallo? Error)> EvaluarAsync(PropuestaCertificacion propuesta, ContextoContrato contexto);
    }

    // Regla: Control de Techo de Gasto (Límite 105%)
    public class ReglaLimitePresupuestario : IReglaValidacion
    {
        private const decimal FactorMargenLegal = 1.05m;

        public Task<(bool Cumple, DetalleFallo? Error)> EvaluarAsync(PropuestaCertificacion prop, ContextoContrato ctx)
        {
            if (ctx.PartidaCerrada)
                return Task.FromResult<(bool Cumple, DetalleFallo? Error)>((false, new DetalleFallo("VAL-01", "La partida está liquidada administrativamente.")));

            decimal totalPermitido = ctx.PresupuestoAsignado * FactorMargenLegal;
            decimal nuevoAcumulado = ctx.AcumuladoAnterior + prop.Cantidad;

            if (nuevoAcumulado > totalPermitido)
                return Task.FromResult<(bool Cumple, DetalleFallo? Error)>((false, new DetalleFallo("VAL-02", $"Exceso presupuestario: {nuevoAcumulado} supera el límite legal de {totalPermitido}.")));

            return Task.FromResult((true, (DetalleFallo?)null));
        }
    }

    // Motor Orquestador: Auditoría Determinista
    public class MotorAuditoria(IEnumerable<IReglaValidacion> reglas)
    {
        public async Task<InformeAuditoria> AuditarAsync(PropuestaCertificacion prop, ContextoContrato ctx)
        {
            // Cláusulas de Guarda: Validación temprana (Fase 4)
            if (prop == null || ctx == null) throw new ArgumentNullException("Contexto incompleto.");

            var errores = new List<DetalleFallo>();

            foreach (var regla in reglas)
            {
                var (cumple, fallo) = await regla.EvaluarAsync(prop, ctx);
                if (!cumple && fallo != null)
                {
                    Console.WriteLine($"[WARN] Fallo detectado por {regla.GetType().Name}: {fallo.Descripcion}");
                    errores.Add(fallo);
                }
            }
            return new InformeAuditoria(!errores.Any(), errores);
        }
    }

    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Motor de auditoria preparado.");
        }
    }
}
