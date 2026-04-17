"""Validadores de contrato y calidad de datos."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any, Dict

from .exceptions import WeatherContractError
from .models import WeatherSample

_ALLOWED_DIRECTIONS = {"N", "NE", "E", "SE", "S", "SW", "W", "NW"}


def validate_weather_payload(payload: Dict[str, Any]) -> WeatherSample:
    """Valida y normaliza el payload meteorologico recibido."""
    if not isinstance(payload, dict):
        raise WeatherContractError("Payload invalido: no es un objeto JSON")

    status = payload.get("status")
    if status != "success":
        raise WeatherContractError("Payload invalido: status debe ser 'success'")

    data = payload.get("data")
    if not isinstance(data, dict):
        raise WeatherContractError("Payload invalido: falta objeto data")

    station_id = data.get("estacion_id")
    if not isinstance(station_id, str) or not station_id.strip():
        raise WeatherContractError("Payload invalido: estacion_id obligatorio")

    wind_kmh = _parse_wind(data.get("viento_kmh"))

    direction = data.get("direccion")
    if direction not in _ALLOWED_DIRECTIONS:
        raise WeatherContractError("Payload invalido: direccion fuera de contrato")

    updated_at_raw = data.get("ultima_actualizacion")
    updated_at = _parse_datetime(updated_at_raw)

    return WeatherSample(
        station_id=station_id.strip(),
        wind_kmh=wind_kmh,
        direction=direction,
        updated_at=updated_at,
    )


def compute_data_freshness_seconds(sample: WeatherSample, now_utc: datetime) -> float:
    if now_utc.tzinfo is None:
        raise WeatherContractError("now_utc debe incluir zona horaria")
    return max((now_utc - sample.updated_at).total_seconds(), 0.0)


def _parse_wind(value: Any) -> float:
    try:
        parsed = float(value)
    except (TypeError, ValueError) as exc:
        raise WeatherContractError("Payload invalido: viento_kmh no numerico") from exc

    if parsed < 0 or parsed > 120:
        raise WeatherContractError("Payload invalido: viento_kmh fuera de rango fisico")
    return parsed


def _parse_datetime(value: Any) -> datetime:
    if not isinstance(value, str) or not value.strip():
        raise WeatherContractError("Payload invalido: ultima_actualizacion obligatoria")

    iso = value.strip().replace("Z", "+00:00")
    try:
        parsed = datetime.fromisoformat(iso)
    except ValueError as exc:
        raise WeatherContractError("Payload invalido: ultima_actualizacion no ISO-8601") from exc

    if parsed.tzinfo is None:
        raise WeatherContractError("Payload invalido: ultima_actualizacion sin zona horaria")

    return parsed.astimezone(timezone.utc)
