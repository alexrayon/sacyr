namespace ValidacionCertificaciones.Domain.Models;

/// <summary>
/// Dictamen consolidado de la validación: Apta o Rechazada.
/// Apta = true si y sólo si Errores está vacía.
/// </summary>
public sealed record ResultadoAuditoria(
    bool Apta,
    IReadOnlyList<ErrorValidacion> Errores,
    IReadOnlyList<ResultadoRegla> ResultadosReglas,
    string VersionReglas,
    DateTimeOffset FechaEvaluacionUtc,
    string IdEjecucion);
