from datetime import datetime

from data.flota_repository import RepositorioFlotaEnMemoria
from data.providers import PoliticaFiscalFija, PrecioCombustibleFijo, RelojSistema
from services.config import ReglasIneficiencia
from services.cost_service import ServicioCostesFinancieros


CONFIGURACION_POR_PAIS = {
    "ESPANA": {
        "tasa_impuesto_general": 0.21,
        "tasa_amortizacion_anual": 0.10,
        "precio_combustible_litro": 1.48,
    },
    "CHILE": {
        "tasa_impuesto_general": 0.19,
        "tasa_amortizacion_anual": 0.09,
        "precio_combustible_litro": 1.26,
    },
    "AUSTRALIA": {
        "tasa_impuesto_general": 0.10,
        "tasa_amortizacion_anual": 0.08,
        "precio_combustible_litro": 1.72,
    },
}


def construir_servicio_costes(pais: str = "ESPANA") -> ServicioCostesFinancieros:
    contexto = CONFIGURACION_POR_PAIS.get(pais.upper())
    if contexto is None:
        raise ValueError(f"No existe configuracion para el pais '{pais}'.")

    repositorio = RepositorioFlotaEnMemoria()
    politica_fiscal = PoliticaFiscalFija(
        tasa_impuesto_general=contexto["tasa_impuesto_general"],
        tasa_amortizacion_anual=contexto["tasa_amortizacion_anual"],
    )
    precio_combustible = PrecioCombustibleFijo(precio_litro=contexto["precio_combustible_litro"])
    reloj = RelojSistema()
    reglas_ineficiencia = ReglasIneficiencia(umbral_consumo_hora=40, antiguedad_minima_anos=3)

    return ServicioCostesFinancieros(
        repository=repositorio,
        fiscal_policy=politica_fiscal,
        fuel_pricing=precio_combustible,
        clock=reloj,
        reglas_ineficiencia=reglas_ineficiencia,
    )

def ejecutar_sistema_gestion() -> None:
    print("************************************************")
    print("* SACYR - GESTIÓN DE COSTES MAQUINARIA PESADA  *")
    print("************************************************\n")

    analista = construir_servicio_costes(pais="ESPANA")
    
    # 1. Ejecutar proceso de cálculo
    total_eur, detalle = analista.calcular_analisis_economico_completo()
    
    print("\n" + "="*48)
    print(f"BALANCE FINAL PROYECTO: {total_eur:,.2f} EUR")
    print("="*48)

    # 2. Revisar eficiencia de la flota
    maquinas_viejas = analista.identificar_maquinas_ineficientes()
    if maquinas_viejas:
        print("\n[AVISO] MAQUINARIA PARA RENOVAR (ALTO CONSUMO):")
        ano_actual = datetime.now().year
        for mv in maquinas_viejas:
            print(f" - {mv.nombre} (ID: {mv.id_maquina}) | Antiguedad: {mv.obtener_antiguedad(ano_actual)} anos")

if __name__ == "__main__":
    ejecutar_sistema_gestion()