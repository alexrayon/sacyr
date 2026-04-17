from __future__ import annotations

from datetime import datetime, timezone

from monitor_climatico.models import WeatherSample
from monitor_climatico.safety_engine import SafetyEngine


def build_sample(wind: float) -> WeatherSample:
    return WeatherSample(
        station_id="Sacyr-Madrid-Norte",
        wind_kmh=wind,
        direction="N",
        updated_at=datetime.now(timezone.utc),
    )


def test_alerta_verde_hasta_35() -> None:
    engine = SafetyEngine(ghost_gust_delta_threshold=20.0)
    decision = engine.decide(build_sample(35.0), 1.0, previous_wind_kmh=34.0)

    assert decision.alert_level == "VERDE"
    assert decision.command == "OPERACION_PERMITIDA"


def test_alerta_ambar_mayor_35_hasta_45() -> None:
    engine = SafetyEngine(ghost_gust_delta_threshold=20.0)
    decision = engine.decide(build_sample(40.0), 1.0, previous_wind_kmh=36.0)

    assert decision.alert_level == "AMBAR"
    assert decision.command == "VIGILANCIA_REFORZADA"


def test_alerta_roja_parada_inmediata_mayor_45() -> None:
    engine = SafetyEngine(ghost_gust_delta_threshold=20.0)
    decision = engine.decide(build_sample(45.1), 1.0, previous_wind_kmh=40.0)

    assert decision.alert_level == "ROJO"
    assert decision.command == "PARADA_INMEDIATA"
