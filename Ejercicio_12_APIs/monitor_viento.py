"""Monitor de viento para gruas con resiliencia de red y alertas operativas."""

from __future__ import annotations

import os
import time
from dataclasses import dataclass
from datetime import datetime
from typing import Any, Dict, Optional

import requests

from api_simulador import obtener_datos_clima_simulados


# Constantes contractuales de seguridad.
WIND_ALERT_RED_THRESHOLD = 45.0
WIND_ALERT_AMBER_THRESHOLD = 35.0
REQUEST_TIMEOUT_SECONDS = 5
POLL_INTERVAL_SECONDS = 10


class WeatherDataUnavailableError(Exception):
    """Error de dominio para representar indisponibilidad de datos meteorologicos."""


class ContractValidationError(Exception):
    """Error de dominio para representar incumplimientos del contrato JSON."""


@dataclass
class WindReading:
    """Lectura normalizada de viento obtenida desde la API o simulador."""

    station_id: str
    wind_kmh: float
    direction: str
    updated_at: str


@dataclass
class AlertResult:
    """Resultado de evaluacion operacional de seguridad."""

    level: str
    symbol: str
    message: str
    action: str


class CraneWeatherClient:
    """Cliente HTTP resiliente para consultar la API de viento de gruas."""

    def __init__(
        self,
        base_url: Optional[str],
        api_key: Optional[str],
        timeout_seconds: int = REQUEST_TIMEOUT_SECONDS,
    ) -> None:
        # Si no existe URL, se activa modo simulacion para pruebas seguras.
        self.base_url = (base_url or "").strip()
        self.api_key = (api_key or "").strip()
        self.timeout_seconds = timeout_seconds
        self.use_simulator = not bool(self.base_url)

    def fetch_wind_data(self) -> WindReading:
        """Obtiene una lectura de viento validada desde API real o simulador local."""
        payload = self._fetch_payload()
        return self._parse_payload(payload)

    def _fetch_payload(self) -> Dict[str, Any]:
        # Ruta de simulacion para entornos sin endpoint real.
        if self.use_simulator:
            return obtener_datos_clima_simulados()

        headers = {
            "Accept": "application/json",
            # Se envia token en cabecera Authorization cuando existe.
            "Authorization": f"Bearer {self.api_key}",
        }

        try:
            response = requests.get(
                self.base_url,
                headers=headers,
                timeout=self.timeout_seconds,
            )
            # Convierte codigos HTTP 4xx/5xx en excepcion gestionable.
            response.raise_for_status()
            return response.json()
        except requests.exceptions.Timeout as exc:
            raise WeatherDataUnavailableError(
                "ALERTA DE SISTEMA: DATOS NO DISPONIBLES (Timeout > 5s)"
            ) from exc
        except requests.exceptions.ConnectionError as exc:
            raise WeatherDataUnavailableError(
                "ALERTA DE SISTEMA: DATOS NO DISPONIBLES (Fallo de conexion)"
            ) from exc
        except requests.exceptions.RequestException as exc:
            raise WeatherDataUnavailableError(
                "ALERTA DE SISTEMA: DATOS NO DISPONIBLES"
            ) from exc

    def _parse_payload(self, payload: Dict[str, Any]) -> WindReading:
        # Validacion estricta del contrato: data debe existir y ser diccionario.
        data = payload.get("data")
        if not isinstance(data, dict):
            raise ContractValidationError("Contrato invalido: falta clave 'data'.")

        # Validacion estricta del atributo critico de seguridad.
        if "viento_kmh" not in data:
            raise ContractValidationError(
                "Contrato invalido: falta clave 'data.viento_kmh'."
            )

        try:
            wind_kmh = float(data["viento_kmh"])
        except (TypeError, ValueError) as exc:
            raise ContractValidationError(
                "Contrato invalido: 'data.viento_kmh' no es numerico."
            ) from exc

        return WindReading(
            station_id=str(data.get("estacion_id", "N/D")),
            wind_kmh=wind_kmh,
            direction=str(data.get("direccion", "N/D")),
            updated_at=str(data.get("ultima_actualizacion", "N/D")),
        )


class WindSafetyEvaluator:
    """Evalua severidad del viento y define accion operativa."""

    @staticmethod
    def evaluate(wind_kmh: float) -> AlertResult:
        # Regla contractual: viento superior a 45 km/h.
        if wind_kmh > WIND_ALERT_RED_THRESHOLD:
            return AlertResult(
                level="ALERTA ROJA",
                symbol="\033[91m[!!!]\033[0m",
                message="Parada inmediata de maniobras.",
                action="DETENER_OPERACION",
            )

        # Regla contractual: franja ambar entre 35 y 45 km/h inclusive.
        if WIND_ALERT_AMBER_THRESHOLD <= wind_kmh <= WIND_ALERT_RED_THRESHOLD:
            return AlertResult(
                level="ALERTA AMBAR",
                symbol="\033[93m[!! ]\033[0m",
                message="Operacion restringida y vigilancia reforzada.",
                action="RESTRINGIR_OPERACION",
            )

        return AlertResult(
            level="ESTADO NORMAL",
            symbol="\033[92m[ OK]\033[0m",
            message="Operacion permitida segun procedimiento estandar.",
            action="OPERACION_NORMAL",
        )


class WindMonitor:
    """Orquesta consulta periodica, evaluacion y salida en consola."""

    def __init__(self, client: CraneWeatherClient, poll_interval_seconds: int) -> None:
        self.client = client
        self.poll_interval_seconds = poll_interval_seconds

    def run(self) -> None:
        """Ejecuta monitor continuo con ciclo de 10 segundos."""
        while True:
            self._clear_console()
            now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

            try:
                reading = self.client.fetch_wind_data()
                result = WindSafetyEvaluator.evaluate(reading.wind_kmh)
                self._print_reading(now, reading, result)
            except WeatherDataUnavailableError as exc:
                self._print_system_alert(now, str(exc))
            except ContractValidationError as exc:
                self._print_system_alert(now, f"ALERTA DE SISTEMA: {exc}")
            except Exception as exc:
                # Clausula defensiva para errores no previstos.
                self._print_system_alert(now, f"ALERTA DE SISTEMA: Error inesperado ({exc})")

            time.sleep(self.poll_interval_seconds)

    @staticmethod
    def _clear_console() -> None:
        # Limpieza compatible con Linux/Mac y Windows.
        os.system("cls" if os.name == "nt" else "clear")

    @staticmethod
    def _print_reading(now: str, reading: WindReading, result: AlertResult) -> None:
        print("=" * 62)
        print("MONITOR DE VIENTO - SEGURIDAD DE GRUAS SACYR")
        print("=" * 62)
        print(f"Hora local            : {now}")
        print(f"Estacion              : {reading.station_id}")
        print(f"Ultima actualizacion  : {reading.updated_at}")
        print(f"Direccion del viento  : {reading.direction}")
        print(f"Velocidad viento (km/h): {reading.wind_kmh:.1f}")
        print("-" * 62)
        print(f"Estado                : {result.symbol} {result.level}")
        print(f"Accion operativa      : {result.message}")
        print(f"Codigo de accion      : {result.action}")
        print("=" * 62)
        print(f"Proxima lectura en {POLL_INTERVAL_SECONDS} segundos...")

    @staticmethod
    def _print_system_alert(now: str, message: str) -> None:
        print("=" * 62)
        print("MONITOR DE VIENTO - SEGURIDAD DE GRUAS SACYR")
        print("=" * 62)
        print(f"Hora local            : {now}")
        print("-" * 62)
        print(f"Estado                : \033[95m[SYS]\033[0m ALERTA DE SISTEMA")
        print(f"Detalle               : {message}")
        print("Accion operativa      : Conmutar a modo seguro y notificar.")
        print("=" * 62)
        print(f"Reintento en {POLL_INTERVAL_SECONDS} segundos...")


def load_env_file(path: str = ".env") -> None:
    """Carga archivo .env de forma simple para desarrollo local.

    Nota: en produccion se recomienda inyectar variables desde el entorno.
    """
    if not os.path.exists(path):
        return

    with open(path, "r", encoding="utf-8") as env_file:
        for raw_line in env_file:
            line = raw_line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue

            key, value = line.split("=", 1)
            key = key.strip()
            value = value.strip().strip('"').strip("'")

            # Solo define variable si no venia previamente del entorno.
            if key and key not in os.environ:
                os.environ[key] = value


def build_client_from_env() -> CraneWeatherClient:
    """Construye el cliente leyendo configuracion y credenciales de entorno."""
    load_env_file()

    base_url = os.getenv("WEATHER_API_BASE_URL", "").strip()
    api_key = os.getenv("WEATHER_API_KEY", "").strip()

    timeout_seconds = int(os.getenv("WEATHER_TIMEOUT_SECONDS", str(REQUEST_TIMEOUT_SECONDS)))

    return CraneWeatherClient(
        base_url=base_url,
        api_key=api_key,
        timeout_seconds=timeout_seconds,
    )


def main() -> None:
    """Punto de entrada del monitor de viento."""
    poll_interval = int(os.getenv("WEATHER_POLL_INTERVAL_SECONDS", str(POLL_INTERVAL_SECONDS)))

    client = build_client_from_env()
    monitor = WindMonitor(client=client, poll_interval_seconds=poll_interval)

    try:
        monitor.run()
    except KeyboardInterrupt:
        print("\nMonitor detenido por el operador.")


if __name__ == "__main__":
    main()
