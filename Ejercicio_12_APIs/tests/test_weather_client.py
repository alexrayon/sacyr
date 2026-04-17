from __future__ import annotations

from unittest.mock import Mock, patch

import pytest
import requests

from monitor_climatico.config import MonitorConfig
from monitor_climatico.exceptions import WeatherUnavailableError
from monitor_climatico.weather_client import WeatherApiClient


def _config() -> MonitorConfig:
    return MonitorConfig(
        weather_api_base_url="https://api.example.local/v1/weather/wind/current",
        weather_api_key="token",
        weather_timeout_seconds=5.0,
        sampling_interval_seconds=2.0,
        stale_data_threshold_seconds=10.0,
        stale_cycles_to_fail_safe=2,
        ghost_gust_delta_threshold=20.0,
        use_simulator=False,
    )


def test_timeout_lanza_weather_unavailable() -> None:
    client = WeatherApiClient(_config())

    with patch.object(client, "_session") as mock_session:
        mock_session.get.side_effect = requests.exceptions.Timeout("timeout")
        with pytest.raises(WeatherUnavailableError):
            client.fetch_payload()


def test_http_ok_devuelve_payload() -> None:
    client = WeatherApiClient(_config())
    response = Mock()
    response.raise_for_status.return_value = None
    response.json.return_value = {"status": "success", "data": {}}

    with patch.object(client, "_session") as mock_session:
        mock_session.get.return_value = response
        payload = client.fetch_payload()

    assert payload["status"] == "success"
