from __future__ import annotations

from typing import Dict, List, Tuple

from models.maquinaria import Maquina
from services.config import ReglasIneficiencia
from services.ports import ClockPort, FiscalPolicyPort, FlotaRepositoryPort, FuelPricingPort


class ServicioCostesFinancieros:
    def __init__(
        self,
        repository: FlotaRepositoryPort,
        fiscal_policy: FiscalPolicyPort,
        fuel_pricing: FuelPricingPort,
        clock: ClockPort,
        reglas_ineficiencia: ReglasIneficiencia,
    ) -> None:
        if repository is None:
            raise ValueError("El repositorio de flota es obligatorio.")
        if fiscal_policy is None:
            raise ValueError("La politica fiscal es obligatoria.")
        if fuel_pricing is None:
            raise ValueError("La configuracion de combustible es obligatoria.")
        if clock is None:
            raise ValueError("El reloj de sistema es obligatorio.")
        if reglas_ineficiencia is None:
            raise ValueError("Las reglas de ineficiencia son obligatorias.")

        self._repository = repository
        self._fiscal_policy = fiscal_policy
        self._fuel_pricing = fuel_pricing
        self._clock = clock
        self._reglas_ineficiencia = reglas_ineficiencia

    def calcular_analisis_economico_completo(self) -> Tuple[float, List[Dict[str, object]]]:
        resumen: List[Dict[str, object]] = []
        total_proyecto = 0.0
        maquinas = self._repository.obtener_maquinas()
        ano_actual = self._clock.obtener_ano_actual()
        precio_gasoil_litro = self._fuel_pricing.obtener_precio_combustible_litro()
        tasa_impuesto_general = self._fiscal_policy.obtener_tasa_impuesto_general()
        tasa_amortizacion_anual = self._fiscal_policy.obtener_tasa_amortizacion_anual()

        print(f"--- INICIANDO AUDITORIA DE COSTES: {len(maquinas)} ACTIVOS ---")

        for maquina in maquinas:
            coste_base = maquina.horas_uso * maquina.coste_hora
            coste_combustible = (maquina.horas_uso * maquina.consumo_hora) * precio_gasoil_litro

            amortizacion = 0.0
            if not maquina.es_alquilada:
                anos_antiguedad = maquina.obtener_antiguedad(ano_actual)
                amortizacion = (coste_base * tasa_amortizacion_anual) * anos_antiguedad

            subtotal = coste_base + coste_combustible + amortizacion
            total_con_impuesto = subtotal * (1 + tasa_impuesto_general)
            total_proyecto += total_con_impuesto

            resumen.append(
                {
                    "id": maquina.id_maquina,
                    "nombre": maquina.nombre,
                    "coste_final": round(total_con_impuesto, 2),
                }
            )

            print(f"OK -> {maquina.id_maquina}: {total_con_impuesto:,.2f} EUR")

        return total_proyecto, resumen

    def identificar_maquinas_ineficientes(self) -> List[Maquina]:
        maquinas = self._repository.obtener_maquinas()
        ano_actual = self._clock.obtener_ano_actual()
        return [
            maquina
            for maquina in maquinas
            if maquina.consumo_hora > self._reglas_ineficiencia.umbral_consumo_hora
            and maquina.obtener_antiguedad(ano_actual) > self._reglas_ineficiencia.antiguedad_minima_anos
        ]