namespace Sacyr.Engineering.Auditoria
{
    // Modelos de Dominio: Representación fiel de la verdad contractual
    public record PropuestaCertificacion(Guid PartidaId, decimal Cantidad, decimal PrecioUnitario, DateTime FechaEjecucion);
    public record ContextoContrato(decimal PresupuestoAsignado, decimal AcumuladoAnterior, DateTime InicioObra, bool PartidaCerrada);
    
    public record DetalleFallo(string Codigo, string Descripcion);
    public record InformeAuditoria(bool EsValida, List<DetalleFallo> Errores);

    // Contrato para reglas de auditoría extensibles (Fase 2: Arquitectura)
    public interface IReglaValidacion
    {
        Task<(bool Cumple, DetalleFallo? Error)> EvaluarAsync(PropuestaCertificacion propuesta, ContextoContrato contexto);
    }

    // Implementación de la Regla: Control de Techo de Gasto (Límite 105%)
    public class ReglaLimitePresupuestario : IReglaValidacion
    {
        private const decimal FactorMargenLegal = 1.05m;

        public Task<(bool Cumple, DetalleFallo? Error)> EvaluarAsync(PropuestaCertificacion prop, ContextoContrato ctx)
        {
            // Cláusula de Guarda: Validación de estado
            if (ctx.PartidaCerrada)
                return Task.FromResult((false, new DetalleFallo("VAL-01", "La partida está liquidada administrativamente.")));

            decimal totalPermitido = ctx.PresupuestoAsignado * FactorMargenLegal;
            decimal nuevoAcumulado = ctx.AcumuladoAnterior + prop.Cantidad;

            if (nuevoAcumulado > totalPermitido)
                return Task.FromResult((false, new DetalleFallo("VAL-02", $"Exceso presupuestario: {nuevoAcumulado} supera el límite legal de {totalPermitido}.")));

            return Task.FromResult((true, (DetalleFallo?)null));
        }
    }

    // Motor Orquestador: Auditoría Determinista Basada en Reglas
    public class MotorAuditoria(IEnumerable<IReglaValidacion> reglas, ILogger<MotorAuditoria> logger)
    {
        public async Task<InformeAuditoria> AuditarAsync(PropuestaCertificacion prop, ContextoContrato ctx)
        {
            // 1. Cláusulas de Guarda: Validación de integridad de datos (Fase 4)
            if (prop == null || ctx == null) throw new ArgumentNullException("Contexto de auditoría incompleto.");

            var errores = new List<DetalleFallo>();

            // 2. Ejecución de la colección de reglas (Paso 5 de la teoría)
            foreach (var regla in reglas)
            {
                var (cumple, fallo) = await regla.EvaluarAsync(prop, ctx);
                if (!cumple && fallo != null)
                {
                    logger.LogWarning("Fallo detectado por {Regla}: {Msg}", regla.GetType().Name, fallo.Descripcion);
                    errores.Add(fallo);
                }
            }

            return new InformeAuditoria(!errores.Any(), errores);
        }
    }
}
