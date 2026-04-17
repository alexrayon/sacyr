"""Modelos de dominio para el monitor climatico."""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from typing import List


@dataclass(frozen=True)
class WeatherSample:
    """Muestra meteorologica validada y normalizada."""

    station_id: str
    wind_kmh: float
    direction: str
    updated_at: datetime


@dataclass(frozen=True)
class SafetyDecision:
    """Decision operacional emitida por el motor de seguridad."""

    timestamp_utc: datetime
    wind_kmh: float
    alert_level: str
    command: str
    source_station: str
    data_freshness_seconds: float
    flags: List[str] = field(default_factory=list)
