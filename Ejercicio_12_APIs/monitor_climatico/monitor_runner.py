"""Runner infinito del monitor de seguridad climatica."""

from __future__ import annotations

import time
import uuid
from datetime import datetime, timezone
from typing import Callable, Optional

from .config import MonitorConfig
from .exceptions import WeatherContractError, WeatherUnavailableError
from .logging_utils import build_logger
from .models import SafetyDecision
from .safety_engine import SafetyEngine
from .validators import compute_data_freshness_seconds, validate_weather_payload
from .weather_client import WeatherApiClient


class MonitorRunner:
    """Orquesta polling continuo, resiliencia y decisiones de seguridad."""

    def __init__(self, config: MonitorConfig, sleep_fn: Callable[[float], None] = time.sleep) -> None:
        self._config = config
        self._sleep_fn = sleep_fn
        self._logger = build_logger()
        self._client = WeatherApiClient(config)
        self._engine = SafetyEngine(ghost_gust_delta_threshold=config.ghost_gust_delta_threshold)
        self._previous_wind_kmh: Optional[float] = None
        self._stale_cycles = 0

    def run_forever(self) -> None:
        while True:
            self._run_cycle()
            self._sleep_fn(self._config.sampling_interval_seconds)

    def _run_cycle(self) -> None:
        correlation_id = str(uuid.uuid4())
        self._log_event("weather_poll_started", {"correlation_id": correlation_id})

        try:
            payload = self._client.fetch_payload()
            sample = validate_weather_payload(payload)
            now_utc = datetime.now(timezone.utc)
            freshness = compute_data_freshness_seconds(sample, now_utc)

            decision = self._engine.decide(
                sample=sample,
                data_freshness_seconds=freshness,
                previous_wind_kmh=self._previous_wind_kmh,
            )
            self._previous_wind_kmh = sample.wind_kmh

            decision = self._apply_data_freshness_policy(decision)
            self._emit_decision(decision, correlation_id)
        except WeatherUnavailableError:
            self._emit_fail_safe_decision(correlation_id, reason="weather_timeout_fail_safe")
        except WeatherContractError:
            self._emit_fail_safe_decision(correlation_id, reason="weather_contract_error")
        except Exception as exc:  # pragma: no cover - defensa operacional
            self._log_event(
                "monitor_cycle_error",
                {
                    "correlation_id": correlation_id,
                    "error": type(exc).__name__,
                    "detail": str(exc),
                },
                level="error",
            )
            self._emit_fail_safe_decision(correlation_id, reason="unexpected_cycle_error")

    def _apply_data_freshness_policy(self, decision: SafetyDecision) -> SafetyDecision:
        if decision.data_freshness_seconds <= self._config.stale_data_threshold_seconds:
            self._stale_cycles = 0
            return decision

        self._stale_cycles += 1

        flags = list(decision.flags)
        if "stale_weather_data" not in flags:
            flags.append("stale_weather_data")

        stale_decision = SafetyDecision(
            timestamp_utc=decision.timestamp_utc,
            wind_kmh=decision.wind_kmh,
            alert_level="AMBAR",
            command="ALERTA_OPERADOR",
            source_station=decision.source_station,
            data_freshness_seconds=decision.data_freshness_seconds,
            flags=flags,
        )

        if self._stale_cycles < self._config.stale_cycles_to_fail_safe:
            self._log_event(
                "stale_weather_data",
                {
                    "stale_cycles": self._stale_cycles,
                    "freshness_seconds": decision.data_freshness_seconds,
                },
                level="warning",
            )
            return stale_decision

        return SafetyDecision(
            timestamp_utc=decision.timestamp_utc,
            wind_kmh=decision.wind_kmh,
            alert_level="FAIL_SAFE_SIN_DATOS",
            command="ALERTA_OPERADOR",
            source_station=decision.source_station,
            data_freshness_seconds=decision.data_freshness_seconds,
            flags=flags,
        )

    def _emit_fail_safe_decision(self, correlation_id: str, reason: str) -> None:
        self._stale_cycles = min(self._stale_cycles + 1, self._config.stale_cycles_to_fail_safe)

        self._log_event(
            reason,
            {
                "correlation_id": correlation_id,
                "timeout_s": self._config.weather_timeout_seconds,
                "endpoint": self._config.weather_api_base_url,
            },
            level="warning",
        )

        decision = SafetyDecision(
            timestamp_utc=datetime.now(timezone.utc),
            wind_kmh=self._previous_wind_kmh or 0.0,
            alert_level="FAIL_SAFE_SIN_DATOS",
            command="ALERTA_OPERADOR",
            source_station="UNKNOWN",
            data_freshness_seconds=float("inf"),
            flags=["no_live_data"],
        )
        self._emit_decision(decision, correlation_id)

    def _emit_decision(self, decision: SafetyDecision, correlation_id: str) -> None:
        event_name = "safety_command_emitted"
        if decision.alert_level == "FAIL_SAFE_SIN_DATOS":
            event_name = "weather_timeout_fail_safe"

        self._log_event(
            event_name,
            {
                "correlation_id": correlation_id,
                "station_id": decision.source_station,
                "wind_kmh": decision.wind_kmh,
                "alert_level": decision.alert_level,
                "command": decision.command,
                "data_freshness_seconds": decision.data_freshness_seconds,
                "flags": decision.flags,
            },
            level="warning" if decision.alert_level in {"AMBAR", "ROJO", "FAIL_SAFE_SIN_DATOS"} else "info",
        )

    def _log_event(self, event: str, context: dict, level: str = "info") -> None:
        log_fn = self._logger.info
        if level == "warning":
            log_fn = self._logger.warning
        elif level == "error":
            log_fn = self._logger.error

        log_fn("monitor_event", extra={"event": event, "context": context})
