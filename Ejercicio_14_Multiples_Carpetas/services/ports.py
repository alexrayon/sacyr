from __future__ import annotations

from abc import ABC, abstractmethod
from typing import Sequence

from models.maquinaria import Maquina


class FlotaRepositoryPort(ABC):
    @abstractmethod
    def obtener_maquinas(self) -> Sequence[Maquina]:
        """Devuelve la flota que participa en el calculo economico."""


class FiscalPolicyPort(ABC):
    @abstractmethod
    def obtener_tasa_impuesto_general(self) -> float:
        """Devuelve la tasa fiscal general en formato decimal (0.21 = 21%)."""

    @abstractmethod
    def obtener_tasa_amortizacion_anual(self) -> float:
        """Devuelve la tasa de amortizacion anual en formato decimal."""


class FuelPricingPort(ABC):
    @abstractmethod
    def obtener_precio_combustible_litro(self) -> float:
        """Devuelve el precio del combustible por litro en moneda local."""


class ClockPort(ABC):
    @abstractmethod
    def obtener_ano_actual(self) -> int:
        """Devuelve el ano actual de referencia para calculos de antiguedad."""
