from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class ReglasIneficiencia:
    umbral_consumo_hora: float
    antiguedad_minima_anos: int

    def __post_init__(self) -> None:
        if self.umbral_consumo_hora <= 0:
            raise ValueError("El umbral de consumo por hora debe ser mayor que cero.")
        if self.antiguedad_minima_anos < 0:
            raise ValueError("La antiguedad minima no puede ser negativa.")
