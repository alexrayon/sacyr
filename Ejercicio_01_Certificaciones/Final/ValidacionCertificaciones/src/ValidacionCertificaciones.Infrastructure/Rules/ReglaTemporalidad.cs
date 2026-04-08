using ValidacionCertificaciones.Domain.Interfaces;
using ValidacionCertificaciones.Domain.Models;

namespace ValidacionCertificaciones.Infrastructure.Rules;

/// <summary>
/// R2 - Temporalidad:
/// FechaTrabajos debe ser estrictamente posterior al acta de replanteo
/// y estrictamente anterior a la fecha de emisión de la certificación.
/// No se admiten igualdades en ninguno de los dos límites.
/// </summary>
public sealed class ReglaTemporalidad : IReglaValidacion
{
    public string Codigo => "R2";
    public string Nombre => "Temporalidad";
    public int Prioridad => 2;

    public Task<ResultadoRegla> EvaluarAsync(
        SolicitudCertificacion solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var posteriorAlActa      = solicitud.FechaTrabajos > solicitud.FechaActaReplanteo;
        var anteriorALaEmision   = solicitud.FechaTrabajos < solicitud.FechaEmision;
        var cumple               = posteriorAlActa && anteriorALaEmision;

        IReadOnlyDictionary<string, string> evidencia = new Dictionary<string, string>
        {
            ["FechaActaReplanteo"]    = solicitud.FechaActaReplanteo.ToString("yyyy-MM-dd"),
            ["FechaTrabajos"]         = solicitud.FechaTrabajos.ToString("yyyy-MM-dd"),
            ["FechaEmision"]          = solicitud.FechaEmision.ToString("yyyy-MM-dd"),
            ["PosteriorAlActa"]       = posteriorAlActa.ToString(),
            ["AnteriorALaEmision"]    = anteriorALaEmision.ToString()
        };

        var mensaje = cumple
            ? "La fecha de trabajos es coherente con el acta de replanteo y la emisión de la certificación."
            : "Rechazo R2: incoherencia temporal entre acta de replanteo, fecha de trabajos y emisión de certificación.";

        return Task.FromResult(new ResultadoRegla(Codigo, cumple, mensaje, evidencia));
    }
}
