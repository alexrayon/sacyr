namespace ValidacionCertificaciones.Domain.Models;

/// <summary>
/// Error de validación con trazabilidad completa para auditoría legal.
/// </summary>
public sealed record ErrorValidacion(
    string CodigoRegla,
    string Mensaje,
    string Severidad,
    IReadOnlyDictionary<string, string> Evidencia);
