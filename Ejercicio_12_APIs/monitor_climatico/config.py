"""Carga y validacion de configuracion del monitor."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

from .exceptions import ConfigurationError


@dataclass(frozen=True)
class MonitorConfig:
    """Configuracion runtime del monitor."""

    weather_api_base_url: str
    weather_api_key: str
    weather_timeout_seconds: float = 5.0
    sampling_interval_seconds: float = 2.0
    stale_data_threshold_seconds: float = 10.0
    stale_cycles_to_fail_safe: int = 2
    ghost_gust_delta_threshold: float = 20.0
    use_simulator: bool = False

    @classmethod
    def from_env(cls, env_file: str = ".env") -> "MonitorConfig":
        _load_dotenv_if_exists(env_file)

        use_simulator = os.getenv("WEATHER_USE_SIMULATOR", "false").strip().lower() == "true"
        base_url = os.getenv("WEATHER_API_BASE_URL", "").strip()
        api_key = os.getenv("WEATHER_API_KEY", "").strip()

        # Fallback seguro: sin API configurada se usa simulador automaticamente.
        if use_simulator or not base_url or not api_key:
            return cls(
                weather_api_base_url=base_url or "SIMULATED",
                weather_api_key=api_key or "SIMULATED_KEY",
                weather_timeout_seconds=_to_float("WEATHER_TIMEOUT_SECONDS", 5.0),
                sampling_interval_seconds=_to_float("SAMPLING_INTERVAL_SECONDS", 2.0),
                stale_data_threshold_seconds=_to_float("STALE_DATA_THRESHOLD_SECONDS", 10.0),
                stale_cycles_to_fail_safe=_to_int("STALE_CYCLES_TO_FAIL_SAFE", 2),
                ghost_gust_delta_threshold=_to_float("GHOST_GUST_DELTA_THRESHOLD", 20.0),
                use_simulator=True,
            )

        return cls(
            weather_api_base_url=base_url,
            weather_api_key=api_key,
            weather_timeout_seconds=_to_float("WEATHER_TIMEOUT_SECONDS", 5.0),
            sampling_interval_seconds=_to_float("SAMPLING_INTERVAL_SECONDS", 2.0),
            stale_data_threshold_seconds=_to_float("STALE_DATA_THRESHOLD_SECONDS", 10.0),
            stale_cycles_to_fail_safe=_to_int("STALE_CYCLES_TO_FAIL_SAFE", 2),
            ghost_gust_delta_threshold=_to_float("GHOST_GUST_DELTA_THRESHOLD", 20.0),
            use_simulator=False,
        )


def _load_dotenv_if_exists(env_file: str) -> None:
    try:
        from dotenv import load_dotenv
    except ImportError:
        return

    env_path = Path(env_file)
    if env_path.exists():
        load_dotenv(dotenv_path=env_path, override=False)


def _to_float(name: str, default: float) -> float:
    value = os.getenv(name)
    if value is None or value.strip() == "":
        return default
    try:
        parsed = float(value)
    except ValueError as exc:
        raise ConfigurationError(f"Valor invalido para {name}: {value}") from exc

    if parsed <= 0:
        raise ConfigurationError(f"{name} debe ser > 0")
    return parsed


def _to_int(name: str, default: int) -> int:
    value = os.getenv(name)
    if value is None or value.strip() == "":
        return default
    try:
        parsed = int(value)
    except ValueError as exc:
        raise ConfigurationError(f"Valor invalido para {name}: {value}") from exc

    if parsed <= 0:
        raise ConfigurationError(f"{name} debe ser > 0")
    return parsed
