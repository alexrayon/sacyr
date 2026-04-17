from __future__ import annotations

from datetime import datetime, timezone

import pytest

from monitor_climatico.exceptions import WeatherContractError
from monitor_climatico.validators import validate_weather_payload


def test_validate_payload_valido() -> None:
    payload = {
        "status": "success",
        "data": {
            "estacion_id": "Sacyr-Madrid-Norte",
            "viento_kmh": 42.4,
            "direccion": "NE",
            "ultima_actualizacion": datetime.now(timezone.utc).isoformat(),
        },
    }

    sample = validate_weather_payload(payload)
    assert sample.wind_kmh == 42.4
    assert sample.direction == "NE"


def test_validate_payload_invalido_campos_faltantes() -> None:
    payload = {
        "status": "success",
        "data": {
            "estacion_id": "Sacyr-Madrid-Norte",
            "direccion": "NE",
            "ultima_actualizacion": datetime.now(timezone.utc).isoformat(),
        },
    }

    with pytest.raises(WeatherContractError):
        validate_weather_payload(payload)
