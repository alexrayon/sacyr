using ValidacionCertificaciones.Domain.Interfaces;
using ValidacionCertificaciones.Infrastructure.Rules;

namespace ValidacionCertificaciones.Infrastructure.Providers;

/// <summary>
/// Proveedor por defecto del conjunto de reglas contractuales.
/// Extensión: implementar nuevos IProveedorRuleSet para otros países o tipos de contrato
/// y registrarlos en el contenedor DI por perfil (Strategy + Factory).
/// </summary>
public sealed class ProveedorRuleSetDefault : IProveedorRuleSet
{
    // Reglas inmutables compartidas entre todas las evaluaciones (sin estado mutable)
    private static readonly IReadOnlyList<IReglaValidacion> ReglasPorDefecto =
    [
        new ReglaLimitePresupuestario(),
        new ReglaTemporalidad(),
        new ReglaEstadoPartida()
    ];

    public IReadOnlyList<IReglaValidacion> ObtenerReglas(string pais, string tipoContrato)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pais);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoContrato);

        // Punto de extensión: futuros proveedores devuelven sets específicos por pais/contrato
        return ReglasPorDefecto;
    }
}
