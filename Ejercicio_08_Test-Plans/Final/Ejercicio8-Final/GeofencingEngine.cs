using System.Globalization;

namespace Ejercicio8_Final;

public enum GeofenceState
{
	Outside,
	Inside,
	Unknown
}

public enum AlertType
{
	Entry,
	Exit
}

public sealed record GeoCoordinate(double? Latitude, double? Longitude);

public sealed record DangerZone(string ZoneId, GeoCoordinate Center, double RadiusMeters);

public sealed record AlertEvent(
	string OperatorId,
	string ZoneId,
	AlertType Type,
	double DistanceMeters,
	DateTime TimestampUtc);

public interface IAlertService
{
	void SendAlert(AlertEvent alertEvent);
}

public sealed record GeofencingResult(
	bool IsValidSample,
	GeofenceState CurrentState,
	double? DistanceMeters,
	string Message);

public sealed class GeofencingEngine
{
	private readonly DangerZone _zone;
	private readonly IAlertService _alertService;

	public GeofencingEngine(DangerZone zone, IAlertService alertService)
	{
		_zone = zone ?? throw new ArgumentNullException(nameof(zone));
		_alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));

		if (_zone.RadiusMeters <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(zone), "El radio de la zona debe ser mayor que 0 metros.");
		}
	}

	public GeofenceState State { get; private set; } = GeofenceState.Outside;

	public double? LastDistanceMeters { get; private set; }

	public GeofencingResult ProcessSample(string operatorId, GeoCoordinate coordinate, DateTime timestampUtc)
	{
		if (string.IsNullOrWhiteSpace(operatorId))
		{
			throw new ArgumentException("El identificador del operario es obligatorio.", nameof(operatorId));
		}

		var validationError = ValidateCoordinate(coordinate);
		if (validationError is not null)
		{
			return new GeofencingResult(
				IsValidSample: false,
				CurrentState: State,
				DistanceMeters: LastDistanceMeters,
				Message: validationError);
		}

		var distance = CalculateDistanceMeters(coordinate, _zone.Center);
		LastDistanceMeters = distance;
		var isInside = distance <= _zone.RadiusMeters;

		if (State == GeofenceState.Outside && isInside)
		{
			State = GeofenceState.Inside;
			_alertService.SendAlert(new AlertEvent(
				operatorId,
				_zone.ZoneId,
				AlertType.Entry,
				distance,
				timestampUtc));

			return new GeofencingResult(true, State, distance, "Entrada detectada en zona de peligro.");
		}

		if (State == GeofenceState.Inside && !isInside)
		{
			State = GeofenceState.Outside;
			_alertService.SendAlert(new AlertEvent(
				operatorId,
				_zone.ZoneId,
				AlertType.Exit,
				distance,
				timestampUtc));

			return new GeofencingResult(true, State, distance, "Salida detectada de zona de peligro.");
		}

		State = isInside ? GeofenceState.Inside : GeofenceState.Outside;
		var zoneLabel = isInside ? "dentro" : "fuera";
		return new GeofencingResult(
			IsValidSample: true,
			CurrentState: State,
			DistanceMeters: distance,
			Message: string.Create(
				CultureInfo.InvariantCulture,
				$"Muestra valida procesada. Operario {zoneLabel} del perimetro."));
	}

	public static double CalculateDistanceMeters(GeoCoordinate point, GeoCoordinate center)
	{
		var validationErrorPoint = ValidateCoordinate(point);
		if (validationErrorPoint is not null)
		{
			throw new ArgumentException(validationErrorPoint, nameof(point));
		}

		var validationErrorCenter = ValidateCoordinate(center);
		if (validationErrorCenter is not null)
		{
			throw new ArgumentException(validationErrorCenter, nameof(center));
		}

		var pointLat = point.Latitude.GetValueOrDefault();
		var pointLon = point.Longitude.GetValueOrDefault();
		var centerLat = center.Latitude.GetValueOrDefault();
		var centerLon = center.Longitude.GetValueOrDefault();

		// Aproximacion euclidiana local para pruebas unitarias de umbral.
		const double metersPerDegreeLat = 111_320d;
		var centerLatRadians = DegreesToRadians(centerLat);
		var metersPerDegreeLon = metersPerDegreeLat * Math.Cos(centerLatRadians);

		var deltaLatMeters = (pointLat - centerLat) * metersPerDegreeLat;
		var deltaLonMeters = (pointLon - centerLon) * metersPerDegreeLon;

		return Math.Sqrt((deltaLatMeters * deltaLatMeters) + (deltaLonMeters * deltaLonMeters));
	}

	private static double DegreesToRadians(double degrees)
	{
		return degrees * Math.PI / 180d;
	}

	private static string? ValidateCoordinate(GeoCoordinate? coordinate)
	{
		if (coordinate is null)
		{
			return "La muestra GPS es nula.";
		}

		if (!coordinate.Latitude.HasValue || !coordinate.Longitude.HasValue)
		{
			return "La muestra GPS contiene coordenadas nulas.";
		}

		if (coordinate.Latitude.Value < -90d || coordinate.Latitude.Value > 90d)
		{
			return "La latitud esta fuera de rango valido [-90, 90].";
		}

		if (coordinate.Longitude.Value < -180d || coordinate.Longitude.Value > 180d)
		{
			return "La longitud esta fuera de rango valido [-180, 180].";
		}

		return null;
	}
}
