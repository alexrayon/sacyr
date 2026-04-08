using ValidacionCertificaciones.Domain.Enums;
using ValidacionCertificaciones.Domain.Interfaces;
using ValidacionCertificaciones.Domain.Models;

namespace ValidacionCertificaciones.Infrastructure.Rules;

/// <summary>
/// R3 - Estado de Partida:
/// Bloqueo administrativo automático si la partida está Finalizada o Liquidada.
/// Esta regla prevalece con independencia de R1 y R2.
/// </summary>
public sealed class ReglaEstadoPartida : IReglaValidacion
{
    public string Codigo => "R3";
    public string Nombre => "Estado de Partida";
    public int Prioridad => 3;

    public Task<ResultadoRegla> EvaluarAsync(
        SolicitudCertificacion solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var cumple = solicitud.EstadoPartida is not (EstadoPartida.Finalizada or EstadoPartida.Liquidada);

        IReadOnlyDictionary<string, string> evidencia = new Dictionary<string, string>
        {
            ["EstadoPartida"] = solicitud.EstadoPartida.ToString()
        };

        var mensaje = cumple
            ? $"La partida se encuentra en estado operativo ({solicitud.EstadoPartida})."
            : $"Rechazo R3: la partida se encuentra cerrada para nuevas certificaciones ({solicitud.EstadoPartida}).";

        return Task.FromResult(new ResultadoRegla(Codigo, cumple, mensaje, evidencia));
    }
}
