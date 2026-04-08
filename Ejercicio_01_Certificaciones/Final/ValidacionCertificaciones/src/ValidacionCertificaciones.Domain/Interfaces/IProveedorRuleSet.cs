namespace ValidacionCertificaciones.Domain.Interfaces;

/// <summary>
/// Resuelve el conjunto de reglas activo según el contexto contractual.
/// Punto de extensión para variabilidad por país y tipo de contrato.
/// </summary>
public interface IProveedorRuleSet
{
    /// <summary>
    /// Retorna las reglas aplicables, ordenadas por prioridad.
    /// </summary>
    IReadOnlyList<IReglaValidacion> ObtenerReglas(string pais, string tipoContrato);
}
