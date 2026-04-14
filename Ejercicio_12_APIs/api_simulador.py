"""Simulador local de API de viento para pruebas de seguridad operacional."""

from __future__ import annotations

import random
from datetime import datetime, timezone
from typing import Any, Dict


ESTACIONES = [
    "Sacyr-Madrid-Norte",
    "Sacyr-Valencia-Puerto",
    "Sacyr-Sevilla-Obra-07",
]

DIRECCIONES = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"]


def obtener_datos_clima_simulados() -> Dict[str, Any]:
    """Genera una respuesta con la misma forma que la API de meteorologia esperada."""
    # Valor en rango operativo ampliado para cubrir escenarios normal, ambar y rojo.
    viento = round(random.uniform(18.0, 55.0), 1)

    return {
        "status": "success",
        "data": {
            "estacion_id": random.choice(ESTACIONES),
            "viento_kmh": viento,
            "direccion": random.choice(DIRECCIONES),
            "ultima_actualizacion": datetime.now(timezone.utc).isoformat(),
        },
    }
