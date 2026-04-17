from __future__ import annotations

import unittest
from dataclasses import dataclass
from typing import List

from models.maquinaria import Maquina
from services.config import ReglasIneficiencia
from services.cost_service import ServicioCostesFinancieros
from services.ports import ClockPort, FiscalPolicyPort, FlotaRepositoryPort, FuelPricingPort


class FakeRepositorio(FlotaRepositoryPort):
    def __init__(self, maquinas: List[Maquina]) -> None:
        self._maquinas = list(maquinas)

    def obtener_maquinas(self) -> List[Maquina]:
        return list(self._maquinas)


@dataclass
class FakePoliticaFiscal(FiscalPolicyPort):
    tasa_impuesto_general: float
    tasa_amortizacion_anual: float

    def obtener_tasa_impuesto_general(self) -> float:
        return self.tasa_impuesto_general

    def obtener_tasa_amortizacion_anual(self) -> float:
        return self.tasa_amortizacion_anual


@dataclass
class FakePrecioCombustible(FuelPricingPort):
    precio_litro: float

    def obtener_precio_combustible_litro(self) -> float:
        return self.precio_litro


@dataclass
class FakeReloj(ClockPort):
    ano_actual: int

    def obtener_ano_actual(self) -> int:
        return self.ano_actual


class ServicioCostesFinancierosTests(unittest.TestCase):
    def test_calculo_incluye_amortizacion_en_maquina_propiedad(self) -> None:
        maquina = Maquina("M-01", "Tunel A", "Tuneladora", 100.0, 10, 5.0, 2020, es_alquilada=False)
        servicio = ServicioCostesFinancieros(
            repository=FakeRepositorio([maquina]),
            fiscal_policy=FakePoliticaFiscal(tasa_impuesto_general=0.20, tasa_amortizacion_anual=0.10),
            fuel_pricing=FakePrecioCombustible(precio_litro=2.0),
            clock=FakeReloj(ano_actual=2025),
            reglas_ineficiencia=ReglasIneficiencia(umbral_consumo_hora=40, antiguedad_minima_anos=3),
        )

        total, detalle = servicio.calcular_analisis_economico_completo()

        # Base=1000, combustible=100, amortizacion=500 -> subtotal=1600, impuesto=20%
        self.assertAlmostEqual(total, 1920.0)
        self.assertEqual(detalle[0]["id"], "M-01")
        self.assertEqual(detalle[0]["coste_final"], 1920.0)

    def test_calculo_omite_amortizacion_en_maquina_alquilada(self) -> None:
        maquina = Maquina("M-02", "Grua X", "Grua", 100.0, 10, 5.0, 2020, es_alquilada=True)
        servicio = ServicioCostesFinancieros(
            repository=FakeRepositorio([maquina]),
            fiscal_policy=FakePoliticaFiscal(tasa_impuesto_general=0.10, tasa_amortizacion_anual=0.50),
            fuel_pricing=FakePrecioCombustible(precio_litro=2.0),
            clock=FakeReloj(ano_actual=2025),
            reglas_ineficiencia=ReglasIneficiencia(umbral_consumo_hora=40, antiguedad_minima_anos=3),
        )

        total, _ = servicio.calcular_analisis_economico_completo()

        # Base=1000, combustible=100, amortizacion=0 -> subtotal=1100, impuesto=10%
        self.assertAlmostEqual(total, 1210.0)

    def test_identifica_ineficientes_por_regla_parametrica(self) -> None:
        eficiente = Maquina("M-03", "Excavadora A", "Excavadora", 120.0, 8, 20.0, 2024)
        ineficiente = Maquina("M-04", "Tuneladora B", "Tuneladora", 130.0, 8, 45.0, 2020)
        servicio = ServicioCostesFinancieros(
            repository=FakeRepositorio([eficiente, ineficiente]),
            fiscal_policy=FakePoliticaFiscal(tasa_impuesto_general=0.19, tasa_amortizacion_anual=0.09),
            fuel_pricing=FakePrecioCombustible(precio_litro=1.26),
            clock=FakeReloj(ano_actual=2026),
            reglas_ineficiencia=ReglasIneficiencia(umbral_consumo_hora=40, antiguedad_minima_anos=3),
        )

        resultado = servicio.identificar_maquinas_ineficientes()

        self.assertEqual(len(resultado), 1)
        self.assertEqual(resultado[0].id_maquina, "M-04")

    def test_escenario_chile_vs_australia_por_politica_inyectada(self) -> None:
        maquina = Maquina("M-05", "Equipo C", "Tuneladora", 200.0, 5, 10.0, 2022)

        servicio_chile = ServicioCostesFinancieros(
            repository=FakeRepositorio([maquina]),
            fiscal_policy=FakePoliticaFiscal(tasa_impuesto_general=0.19, tasa_amortizacion_anual=0.09),
            fuel_pricing=FakePrecioCombustible(precio_litro=1.26),
            clock=FakeReloj(ano_actual=2026),
            reglas_ineficiencia=ReglasIneficiencia(umbral_consumo_hora=40, antiguedad_minima_anos=3),
        )

        servicio_australia = ServicioCostesFinancieros(
            repository=FakeRepositorio([maquina]),
            fiscal_policy=FakePoliticaFiscal(tasa_impuesto_general=0.10, tasa_amortizacion_anual=0.08),
            fuel_pricing=FakePrecioCombustible(precio_litro=1.72),
            clock=FakeReloj(ano_actual=2026),
            reglas_ineficiencia=ReglasIneficiencia(umbral_consumo_hora=40, antiguedad_minima_anos=3),
        )

        total_chile, _ = servicio_chile.calcular_analisis_economico_completo()
        total_australia, _ = servicio_australia.calcular_analisis_economico_completo()

        self.assertNotEqual(total_chile, total_australia)


if __name__ == "__main__":
    unittest.main()
