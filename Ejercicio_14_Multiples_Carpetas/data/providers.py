from __future__ import annotations

from datetime import datetime

from services.ports import ClockPort, FiscalPolicyPort, FuelPricingPort


class PoliticaFiscalFija(FiscalPolicyPort):
    def __init__(self, tasa_impuesto_general: float, tasa_amortizacion_anual: float) -> None:
        if not 0 <= tasa_impuesto_general <= 1:
            raise ValueError("La tasa de impuesto general debe estar en el rango [0, 1].")
        if not 0 <= tasa_amortizacion_anual <= 1:
            raise ValueError("La tasa de amortizacion anual debe estar en el rango [0, 1].")

        self._tasa_impuesto_general = tasa_impuesto_general
        self._tasa_amortizacion_anual = tasa_amortizacion_anual

    def obtener_tasa_impuesto_general(self) -> float:
        return self._tasa_impuesto_general

    def obtener_tasa_amortizacion_anual(self) -> float:
        return self._tasa_amortizacion_anual


class PrecioCombustibleFijo(FuelPricingPort):
    def __init__(self, precio_litro: float) -> None:
        if precio_litro <= 0:
            raise ValueError("El precio de combustible por litro debe ser mayor que cero.")
        self._precio_litro = precio_litro

    def obtener_precio_combustible_litro(self) -> float:
        return self._precio_litro


class RelojSistema(ClockPort):
    def obtener_ano_actual(self) -> int:
        return datetime.now().year
