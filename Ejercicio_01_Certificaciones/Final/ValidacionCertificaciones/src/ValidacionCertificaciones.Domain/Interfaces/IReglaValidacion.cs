using ValidacionCertificaciones.Domain.Models;

namespace ValidacionCertificaciones.Domain.Interfaces;

/// <summary>
/// Contrato atómico para una regla contractual de validación.
/// Cada implementación evalúa una única condición de negocio.
/// EvaluarAsync es una función pura: misma entrada → mismo resultado.
/// </summary>
public interface IReglaValidacion
{
    /// <summary>Código contractual de la regla (R1, R2, R3...).</summary>
    string Codigo { get; }

    /// <summary>Nombre funcional legible para reportes.</summary>
    string Nombre { get; }

    /// <summary>Orden de presentación en el dictamen (no afecta la lógica).</summary>
    int Prioridad { get; }

    /// <summary>
    /// Evalúa la regla sobre la solicitud y devuelve un resultado con evidencia completa.
    /// </summary>
    Task<ResultadoRegla> EvaluarAsync(
        SolicitudCertificacion solicitud,
        CancellationToken cancellationToken = default);
}
