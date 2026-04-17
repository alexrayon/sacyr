"""Motor de decision de seguridad climatica."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import List, Optional

from .models import SafetyDecision, WeatherSample


class SafetyEngine:
    """Clasifica alertas y emite comandos operativos."""

    def __init__(self, ghost_gust_delta_threshold: float) -> None:
        self._ghost_gust_delta_threshold = ghost_gust_delta_threshold

    def decide(
        self,
        sample: WeatherSample,
        data_freshness_seconds: float,
        previous_wind_kmh: Optional[float],
    ) -> SafetyDecision:
        flags: List[str] = []

        if previous_wind_kmh is not None:
            delta = abs(sample.wind_kmh - previous_wind_kmh)
            if delta > self._ghost_gust_delta_threshold:
                flags.append("possible_ghost_gust")

        alert_level: str
        command: str

        if sample.wind_kmh > 45.0:
            alert_level = "ROJO"
            command = "PARADA_INMEDIATA"
        elif sample.wind_kmh > 35.0:
            alert_level = "AMBAR"
            command = "VIGILANCIA_REFORZADA"
        else:
            alert_level = "VERDE"
            command = "OPERACION_PERMITIDA"

        return SafetyDecision(
            timestamp_utc=datetime.now(timezone.utc),
            wind_kmh=sample.wind_kmh,
            alert_level=alert_level,
            command=command,
            source_station=sample.station_id,
            data_freshness_seconds=data_freshness_seconds,
            flags=flags,
        )
