from services.cost_service import ServicioCostesFinancieros

def ejecutar_sistema_gestion():
    print("************************************************")
    print("* SACYR - GESTIÓN DE COSTES MAQUINARIA PESADA  *")
    print("************************************************\n")

    analista = ServicioCostesFinancieros()
    
    # 1. Ejecutar proceso de cálculo
    total_eur, detalle = analista.calcular_analisis_economico_completo()
    
    print("\n" + "="*48)
    print(f"BALANCE FINAL PROYECTO: {total_eur:,.2f} EUR")
    print("="*48)

    # 2. Revisar eficiencia de la flota
    maquinas_viejas = analista.identificar_maquinas_ineficientes()
    if maquinas_viejas:
        print("\n[AVISO] MAQUINARIA PARA RENOVAR (ALTO CONSUMO):")
        for mv in maquinas_viejas:
            print(f" - {mv.nombre} (ID: {mv.id_maquina}) | Antigüedad: {mv.obtener_antigüedad()} años")

if __name__ == "__main__":
    ejecutar_sistema_gestion()