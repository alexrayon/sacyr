"""Excepciones de dominio del monitor climatico."""

from __future__ import annotations


class MonitorError(Exception):
    """Error base para el monitor."""


class ConfigurationError(MonitorError):
    """Error de configuracion invalida o incompleta."""


class WeatherUnavailableError(MonitorError):
    """Error de disponibilidad de fuente meteorologica."""


class WeatherContractError(MonitorError):
    """Error de contrato JSON de la API de clima."""
