using ValidacionCertificaciones.Domain.Enums;

namespace ValidacionCertificaciones.Domain.Models;

/// <summary>
/// Dato de entrada inmutable que describe una propuesta de certificación de obra.
/// Todos los campos monetarios son decimal (ADR-001).
/// </summary>
public sealed record SolicitudCertificacion(
    string IdCertificacion,
    string IdPartida,
    decimal PartidaProyectada,
    decimal AcumuladoHistorico,
    decimal ImporteActual,
    DateOnly FechaActaReplanteo,
    DateOnly FechaTrabajos,
    DateOnly FechaEmision,
    EstadoPartida EstadoPartida);
