from __future__ import annotations

from datetime import datetime, timedelta, timezone

from monitor_climatico.config import MonitorConfig
from monitor_climatico.models import SafetyDecision
from monitor_climatico.monitor_runner import MonitorRunner


def _config() -> MonitorConfig:
    return MonitorConfig(
        weather_api_base_url="SIMULATED",
        weather_api_key="SIMULATED_KEY",
        weather_timeout_seconds=5.0,
        sampling_interval_seconds=2.0,
        stale_data_threshold_seconds=10.0,
        stale_cycles_to_fail_safe=2,
        ghost_gust_delta_threshold=20.0,
        use_simulator=True,
    )


def _base_decision(freshness: float) -> SafetyDecision:
    return SafetyDecision(
        timestamp_utc=datetime.now(timezone.utc),
        wind_kmh=30.0,
        alert_level="VERDE",
        command="OPERACION_PERMITIDA",
        source_station="Sacyr-Madrid-Norte",
        data_freshness_seconds=freshness,
        flags=[],
    )


def test_stale_data_escala_a_fail_safe_en_segundo_ciclo() -> None:
    runner = MonitorRunner(_config(), sleep_fn=lambda _: None)

    first = runner._apply_data_freshness_policy(_base_decision(12.0))
    second = runner._apply_data_freshness_policy(_base_decision(13.0))

    assert first.alert_level == "AMBAR"
    assert first.command == "ALERTA_OPERADOR"
    assert second.alert_level == "FAIL_SAFE_SIN_DATOS"


def test_stale_data_se_resetea_con_lectura_fresca() -> None:
    runner = MonitorRunner(_config(), sleep_fn=lambda _: None)

    _ = runner._apply_data_freshness_policy(_base_decision(12.0))
    fresh = runner._apply_data_freshness_policy(_base_decision(1.0))

    assert fresh.alert_level == "VERDE"
