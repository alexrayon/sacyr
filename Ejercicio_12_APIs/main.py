"""Punto de entrada del monitor de seguridad climatica."""

from __future__ import annotations

from monitor_climatico.config import MonitorConfig
from monitor_climatico.exceptions import ConfigurationError
from monitor_climatico.monitor_runner import MonitorRunner


def main() -> None:
    try:
        config = MonitorConfig.from_env()
    except ConfigurationError as exc:
        raise SystemExit(f"Configuracion invalida: {exc}") from exc

    runner = MonitorRunner(config=config)
    runner.run_forever()


if __name__ == "__main__":
    main()
