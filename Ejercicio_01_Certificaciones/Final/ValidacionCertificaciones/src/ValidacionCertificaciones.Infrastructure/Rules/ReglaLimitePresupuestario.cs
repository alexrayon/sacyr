using ValidacionCertificaciones.Domain.Interfaces;
using ValidacionCertificaciones.Domain.Models;

namespace ValidacionCertificaciones.Infrastructure.Rules;

/// <summary>
/// R1 - Límite Presupuestario:
/// La suma (AcumuladoHistorico + ImporteActual) no debe superar el 105% de PartidaProyectada.
/// Todos los cálculos en decimal (ADR-001). El límite del 105 % es inclusivo.
/// </summary>
public sealed class ReglaLimitePresupuestario : IReglaValidacion
{
    private const decimal MargenLegal = 1.05m;

    public string Codigo => "R1";
    public string Nombre => "Límite Presupuestario";
    public int Prioridad => 1;

    public Task<ResultadoRegla> EvaluarAsync(
        SolicitudCertificacion solicitud,
        CancellationToken cancellationToken = default)
    {
        // Cláusula de guarda: entrada nunca debe ser nula en este punto
        ArgumentNullException.ThrowIfNull(solicitud);

        var totalCertificable = solicitud.AcumuladoHistorico + solicitud.ImporteActual;
        var techoPermitido    = solicitud.PartidaProyectada * MargenLegal;
        var cumple            = totalCertificable <= techoPermitido;

        IReadOnlyDictionary<string, string> evidencia = new Dictionary<string, string>
        {
            ["PartidaProyectada"]   = solicitud.PartidaProyectada.ToString("F2"),
            ["AcumuladoHistorico"]  = solicitud.AcumuladoHistorico.ToString("F2"),
            ["ImporteActual"]       = solicitud.ImporteActual.ToString("F2"),
            ["TotalCertificable"]   = totalCertificable.ToString("F2"),
            ["TechoPermitido"]      = techoPermitido.ToString("F2"),
            ["MargenAplicado"]      = "5%"
        };

        var mensaje = cumple
            ? $"El total certificable ({totalCertificable:F2}) está dentro del límite presupuestario ({techoPermitido:F2})."
            : $"Rechazo R1: el total certificable ({totalCertificable:F2}) excede el 105 % de la partida proyectada ({techoPermitido:F2}).";

        return Task.FromResult(new ResultadoRegla(Codigo, cumple, mensaje, evidencia));
    }
}
