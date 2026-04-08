namespace Sacyr.Safety.Geofencing.Tests;

public interface IAlertService
{
    void NotifyDanger(string message);
}

public sealed class GeofencingEngine
{
    private readonly IAlertService _alertService;

    public GeofencingEngine(IAlertService alertService)
    {
        _alertService = alertService;
    }

    public GeofencingResult EvaluarPosicion(Coordenada? posicion, ZonaPeligro? zona)
    {
        if (posicion is null)
        {
            throw new ArgumentNullException(nameof(posicion), "La posicion no puede ser nula.");
        }

        if (zona is null)
        {
            throw new ArgumentNullException(nameof(zona), "La zona no puede ser nula.");
        }

        double distancia = Math.Sqrt(
            Math.Pow(posicion.Lat - zona.Centro.Lat, 2) +
            Math.Pow(posicion.Lon - zona.Centro.Lon, 2));

        bool alertaActiva = distancia <= zona.RadioMetros;
        if (alertaActiva)
        {
            _alertService.NotifyDanger($"Operario dentro de zona de peligro a {distancia:F2} metros.");
        }

        return new GeofencingResult(alertaActiva, distancia);
    }
}

public sealed record GeofencingResult(bool AlertaActiva, double DistanciaMetros);
