namespace ValidacionCertificaciones.Domain.Models;

/// <summary>
/// Resultado parcial de la evaluación de una única regla contractual.
/// </summary>
public sealed record ResultadoRegla(
    string CodigoRegla,
    bool Cumple,
    string Mensaje,
    IReadOnlyDictionary<string, string> Evidencia);
