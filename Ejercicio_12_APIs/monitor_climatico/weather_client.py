"""Cliente HTTP resiliente para datos meteorologicos."""

from __future__ import annotations

from typing import Any, Dict

import requests

from api_simulador import obtener_datos_clima_simulados

from .config import MonitorConfig
from .exceptions import WeatherUnavailableError


class WeatherApiClient:
    """Encapsula acceso a API externa usando requests.Session."""

    def __init__(self, config: MonitorConfig) -> None:
        self._config = config
        self._session = requests.Session()

    def fetch_payload(self) -> Dict[str, Any]:
        """Obtiene payload crudo de API o simulador."""
        if self._config.use_simulator:
            return obtener_datos_clima_simulados()

        headers = {
            "Accept": "application/json",
            "X-API-Key": self._config.weather_api_key,
        }

        try:
            response = self._session.get(
                self._config.weather_api_base_url,
                headers=headers,
                timeout=self._config.weather_timeout_seconds,
            )
            response.raise_for_status()
            payload = response.json()
            if not isinstance(payload, dict):
                raise WeatherUnavailableError("Respuesta JSON invalida")
            return payload
        except requests.exceptions.Timeout as exc:
            raise WeatherUnavailableError("Timeout de API meteorologica") from exc
        except requests.exceptions.ConnectionError as exc:
            raise WeatherUnavailableError("Fallo de conexion con API meteorologica") from exc
        except requests.exceptions.HTTPError as exc:
            raise WeatherUnavailableError("Respuesta HTTP no exitosa de API meteorologica") from exc
        except requests.exceptions.RequestException as exc:
            raise WeatherUnavailableError("Error no recuperable en cliente requests") from exc
        except ValueError as exc:
            raise WeatherUnavailableError("No se pudo parsear JSON de respuesta") from exc
