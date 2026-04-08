using Moq;

namespace Ejercicio8_Final;

public sealed class GeofencingSecurityTests
{
	private const string OperatorId = "operario-qa-001";
	private static readonly GeoCoordinate ZoneCenter = new(40.416775, -3.70379);
	private const double RadiusMeters = 50.0;

	[Theory]
	[InlineData(51.0, 50.1, 50.0, 49.9, 49.8, 1)]
	[InlineData(50.2, 50.05, 50.01, 50.0, 49.99, 1)]
	[InlineData(52.0, 51.0, 50.1, 50.05, 50.01, 0)]
	public void BurstDistances_AroundThreshold_ShouldTriggerExpectedEntryAlerts(
		double d1,
		double d2,
		double d3,
		double d4,
		double d5,
		int expectedEntryAlerts)
	{
		var alertServiceMock = new Mock<IAlertService>(MockBehavior.Strict);
		alertServiceMock
			.Setup(service => service.SendAlert(It.IsAny<AlertEvent>()));

		var engine = CreateEngine(alertServiceMock.Object);
		var burst = new[] { d1, d2, d3, d4, d5 };

		foreach (var distanceMeters in burst)
		{
			var coordinate = BuildCoordinateAtEastDistance(ZoneCenter, distanceMeters);
			var result = engine.ProcessSample(OperatorId, coordinate, DateTime.UtcNow);

			Assert.True(
				result.IsValidSample,
				$"La muestra de la rafaga a {distanceMeters:F2}m deberia ser valida. Mensaje del motor: {result.Message}");
		}

		alertServiceMock.Verify(
			service => service.SendAlert(It.Is<AlertEvent>(evt => evt.Type == AlertType.Entry)),
			Times.Exactly(expectedEntryAlerts),
			$"Se esperaban {expectedEntryAlerts} alertas de entrada para la rafaga [{d1}, {d2}, {d3}, {d4}, {d5}] y el conteo no coincide.");
	}

	[Theory]
	[InlineData(50.0, GeofenceState.Inside)]
	[InlineData(49.9, GeofenceState.Inside)]
	[InlineData(50.1, GeofenceState.Outside)]
	public void BoundaryDistances_ShouldClassifyInsideOutsideCorrectly(double distanceMeters, GeofenceState expectedState)
	{
		var alertServiceMock = new Mock<IAlertService>(MockBehavior.Strict);
		alertServiceMock
			.Setup(service => service.SendAlert(It.IsAny<AlertEvent>()));

		var engine = CreateEngine(alertServiceMock.Object);

		var coordinate = BuildCoordinateAtEastDistance(ZoneCenter, distanceMeters);
		var result = engine.ProcessSample(OperatorId, coordinate, DateTime.UtcNow);

		Assert.Equal(
			expectedState,
			result.CurrentState);

		Assert.True(
			result.DistanceMeters.HasValue,
			$"La distancia para {distanceMeters:F1}m deberia existir para poder auditar la clasificacion de seguridad.");

		Assert.InRange(
			result.DistanceMeters!.Value,
			distanceMeters - 0.25,
			distanceMeters + 0.25);
	}

	[Fact]
	public void NullCoordinates_ShouldBeRejected_WithoutAlertAndWithoutStateMutation()
	{
		var alertServiceMock = new Mock<IAlertService>(MockBehavior.Strict);
		var engine = CreateEngine(alertServiceMock.Object);

		var initialState = engine.State;
		var result = engine.ProcessSample(OperatorId, new GeoCoordinate(null, null), DateTime.UtcNow);

		Assert.False(
			result.IsValidSample,
			"Una muestra con coordenadas nulas debe marcarse invalida para evitar decisiones de seguridad no confiables.");

		Assert.Equal(
			initialState,
			engine.State);

		Assert.Contains(
			"nulas",
			result.Message,
			StringComparison.OrdinalIgnoreCase);

		alertServiceMock.Verify(
			service => service.SendAlert(It.IsAny<AlertEvent>()),
			Times.Never,
			"No deben emitirse alertas cuando la muestra GPS es invalida por coordenadas nulas.");
	}

	[Fact]
	public void GpsPrecisionLoss_JitterAroundThreshold_ShouldNotDuplicateEntryAlert()
	{
		var alertServiceMock = new Mock<IAlertService>(MockBehavior.Strict);
		alertServiceMock
			.Setup(service => service.SendAlert(It.IsAny<AlertEvent>()));

		var engine = CreateEngine(alertServiceMock.Object);

		// Secuencia con ruido GPS cerca del borde: cruza una vez y luego oscila dentro.
		var jitterBurst = new[] { 50.2, 50.05, 49.98, 49.99, 50.0, 49.97, 49.99, 49.96 };

		foreach (var distanceMeters in jitterBurst)
		{
			var coordinate = BuildCoordinateAtEastDistance(ZoneCenter, distanceMeters);
			var result = engine.ProcessSample(OperatorId, coordinate, DateTime.UtcNow);

			Assert.True(
				result.IsValidSample,
				$"La muestra con jitter a {distanceMeters:F2}m deberia ser valida para analizar perdida de precision GPS.");
		}

		alertServiceMock.Verify(
			service => service.SendAlert(It.Is<AlertEvent>(evt => evt.Type == AlertType.Entry)),
			Times.Once,
			"Con perdida de precision GPS alrededor del umbral, la alerta de entrada debe dispararse una sola vez por transicion real.");
	}

	[Fact]
	public void EntryTransition_ShouldNotifyExactlyOnce()
	{
		var alertServiceMock = new Mock<IAlertService>(MockBehavior.Strict);
		alertServiceMock
			.Setup(service => service.SendAlert(It.IsAny<AlertEvent>()));

		var engine = CreateEngine(alertServiceMock.Object);

		var outsideCoordinate = BuildCoordinateAtEastDistance(ZoneCenter, 51.0);
		var insideCoordinate = BuildCoordinateAtEastDistance(ZoneCenter, 49.9);

		var outsideResult = engine.ProcessSample(OperatorId, outsideCoordinate, DateTime.UtcNow);
		var entryResult = engine.ProcessSample(OperatorId, insideCoordinate, DateTime.UtcNow);
		var stillInsideResult = engine.ProcessSample(OperatorId, insideCoordinate, DateTime.UtcNow);

		Assert.Equal(
			GeofenceState.Outside,
			outsideResult.CurrentState);

		Assert.Equal(
			GeofenceState.Inside,
			entryResult.CurrentState);

		Assert.Equal(
			GeofenceState.Inside,
			stillInsideResult.CurrentState);

		alertServiceMock.Verify(
			service => service.SendAlert(It.Is<AlertEvent>(evt => evt.Type == AlertType.Entry)),
			Times.Once,
			"El servicio de alertas debe invocarse exactamente una vez cuando se produce una unica transicion Fuera->Dentro.");
	}

	[Fact]
	public void EuclideanDistance_ShouldMatchExpectedMetersOnLocalPlane()
	{
		var pointAt50 = BuildCoordinateAtEastDistance(ZoneCenter, 50.0);
		var measuredDistance = GeofencingEngine.CalculateDistanceMeters(pointAt50, ZoneCenter);

		Assert.InRange(
			measuredDistance,
			49.8,
			50.2);
	}

	private static GeofencingEngine CreateEngine(IAlertService alertService)
	{
		var zone = new DangerZone("zona-peligro-01", ZoneCenter, RadiusMeters);
		return new GeofencingEngine(zone, alertService);
	}

	private static GeoCoordinate BuildCoordinateAtEastDistance(GeoCoordinate center, double distanceMeters)
	{
		const double metersPerDegreeLat = 111_320d;
		var centerLatRadians = center.Latitude!.Value * Math.PI / 180d;
		var metersPerDegreeLon = metersPerDegreeLat * Math.Cos(centerLatRadians);

		var deltaLonDegrees = distanceMeters / metersPerDegreeLon;
		return new GeoCoordinate(center.Latitude, center.Longitude!.Value + deltaLonDegrees);
	}
}
