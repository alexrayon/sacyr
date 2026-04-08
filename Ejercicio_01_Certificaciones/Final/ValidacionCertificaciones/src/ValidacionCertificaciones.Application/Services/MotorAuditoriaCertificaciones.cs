using Microsoft.Extensions.Logging;
using ValidacionCertificaciones.Domain.Interfaces;
using ValidacionCertificaciones.Domain.Models;

namespace ValidacionCertificaciones.Application.Services;

/// <summary>
/// Motor orquestador de validación de certificaciones de obra.
/// 
/// Primary constructor (C# 12+): IProveedorRuleSet e ILogger inyectados por DI.
/// Ejecuta todas las reglas de forma asíncrona concurrente (Task.WhenAll):
///   - nunca interrumpe en el primer fallo → dictamen de auditoría completo.
///   - emite logs de nivel Warning por cada rechazo para trazabilidad legal.
/// </summary>
public sealed class MotorAuditoriaCertificaciones(
    IProveedorRuleSet proveedorRuleSet,
    ILogger<MotorAuditoriaCertificaciones> logger)
{
    private const string VersionReglas = "1.0.0";

    /// <summary>
    /// Valida una propuesta de certificación y emite un <see cref="ResultadoAuditoria"/> completo.
    /// </summary>
    public async Task<ResultadoAuditoria> ValidarAsync(
        SolicitudCertificacion solicitud,
        string pais = "ES",
        string tipoContrato = "Default",
        CancellationToken cancellationToken = default)
    {
        // ── Cláusulas de guarda ──────────────────────────────────────────────
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentException.ThrowIfNullOrWhiteSpace(pais);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoContrato);

        var idEjecucion    = Guid.NewGuid().ToString("D");
        var fechaEvaluacion = DateTimeOffset.UtcNow;

        // ── Validación de entrada (antes de ejecutar reglas de negocio) ──────
        var erroresEntrada = ValidarEntrada(solicitud);
        if (erroresEntrada.Count > 0)
        {
            foreach (var e in erroresEntrada)
                logger.LogError(
                    "Datos de entrada inválidos [{IdEjecucion}] Campo={Codigo} Detalle={Mensaje}",
                    idEjecucion, e.CodigoRegla, e.Mensaje);

            return new ResultadoAuditoria(
                Apta: false,
                Errores: erroresEntrada,
                ResultadosReglas: [],
                VersionReglas: VersionReglas,
                FechaEvaluacionUtc: fechaEvaluacion,
                IdEjecucion: idEjecucion);
        }

        // ── Log de inicio ────────────────────────────────────────────────────
        logger.LogInformation(
            "Inicio validación [{IdEjecucion}] Certificacion={IdCertificacion} " +
            "Partida={IdPartida} Pais={Pais} Contrato={TipoContrato} Version={Version}",
            idEjecucion, solicitud.IdCertificacion, solicitud.IdPartida,
            pais, tipoContrato, VersionReglas);

        // ── Resolución y ejecución concurrente de reglas ─────────────────────
        var reglas = proveedorRuleSet
            .ObtenerReglas(pais, tipoContrato)
            .OrderBy(r => r.Prioridad)
            .ToList();

        // Task.WhenAll garantiza que TODAS las reglas se evalúan aunque alguna falle
        var tareas         = reglas.Select(r => r.EvaluarAsync(solicitud, cancellationToken));
        var resultadosArr  = await Task.WhenAll(tareas).ConfigureAwait(false);

        // ── Consolidación de dictamen ─────────────────────────────────────────
        List<ResultadoRegla>   resultadosReglas = [.. resultadosArr];
        List<ErrorValidacion>  errores = resultadosReglas
            .Where(r => !r.Cumple)
            .Select(r => new ErrorValidacion(r.CodigoRegla, r.Mensaje, "Error", r.Evidencia))
            .ToList();

        // ── Log de auditoría legal por cada rechazo ───────────────────────────
        foreach (var error in errores)
        {
            logger.LogWarning(
                "Rechazo [{IdEjecucion}] Regla={Codigo} Mensaje={Mensaje} Evidencia={Evidencia}",
                idEjecucion, error.CodigoRegla, error.Mensaje, error.Evidencia);
        }

        var apta = errores.Count == 0;

        // ── Log de cierre ─────────────────────────────────────────────────────
        logger.LogInformation(
            "Fin validación [{IdEjecucion}] Resultado={Resultado} ReglasEvaluadas={Count} Rechazos={Rechazos}",
            idEjecucion, apta ? "Apta" : "Rechazada", resultadosReglas.Count, errores.Count);

        return new ResultadoAuditoria(
            Apta: apta,
            Errores: errores,
            ResultadosReglas: resultadosReglas,
            VersionReglas: VersionReglas,
            FechaEvaluacionUtc: fechaEvaluacion,
            IdEjecucion: idEjecucion);
    }

    // ── Validación de entrada ────────────────────────────────────────────────
    private static List<ErrorValidacion> ValidarEntrada(SolicitudCertificacion s)
    {
        var errores = new List<ErrorValidacion>();
        IReadOnlyDictionary<string, string> sinEvidencia = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(s.IdCertificacion))
            errores.Add(new("ENTRADA", "IdCertificacion es obligatorio.", "Critical", sinEvidencia));

        if (string.IsNullOrWhiteSpace(s.IdPartida))
            errores.Add(new("ENTRADA", "IdPartida es obligatorio.", "Critical", sinEvidencia));

        if (s.PartidaProyectada <= 0m)
            errores.Add(new("ENTRADA",
                $"PartidaProyectada debe ser mayor que cero (recibido: {s.PartidaProyectada}).",
                "Critical", sinEvidencia));

        if (s.AcumuladoHistorico < 0m)
            errores.Add(new("ENTRADA",
                $"AcumuladoHistorico no puede ser negativo (recibido: {s.AcumuladoHistorico}).",
                "Critical", sinEvidencia));

        if (s.ImporteActual < 0m)
            errores.Add(new("ENTRADA",
                $"ImporteActual no puede ser negativo (recibido: {s.ImporteActual}).",
                "Critical", sinEvidencia));

        if (s.FechaActaReplanteo == DateOnly.MinValue)
            errores.Add(new("ENTRADA", "FechaActaReplanteo no es válida.", "Critical", sinEvidencia));

        if (s.FechaTrabajos == DateOnly.MinValue)
            errores.Add(new("ENTRADA", "FechaTrabajos no es válida.", "Critical", sinEvidencia));

        if (s.FechaEmision == DateOnly.MinValue)
            errores.Add(new("ENTRADA", "FechaEmision no es válida.", "Critical", sinEvidencia));

        return errores;
    }
}
