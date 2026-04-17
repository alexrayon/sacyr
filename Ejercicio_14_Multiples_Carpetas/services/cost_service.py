from data.flota_repository import inventario_maquinaria

class ServicioCostesFinancieros:
    def __init__(self):
        # FALLO: IVA y Precio Gasoil "a fuego" (Hardcoded)
        # Esto debería venir de una configuración o parámetro por país.
        self.PRECIO_GASOIL_LITRO = 1.48 
        self.IVA_GENERAL = 0.21
        self.TASA_AMORTIZACION_ANUAL = 0.10

    def calcular_analisis_economico_completo(self):
        resumen = []
        total_proyecto = 0

        print(f"--- INICIANDO AUDITORÍA DE COSTES: {len(inventario_maquinaria)} ACTIVOS ---")

        for m in inventario_maquinaria:
            # 1. Coste Operativo (Horas x Precio)
            coste_base = m.horas_uso * m.coste_hora
            
            # 2. Coste Energético
            coste_combustible = (m.horas_uso * m.consumo_hora) * self.PRECIO_GASOIL_LITRO
            
            # 3. Amortización (Si es propiedad de Sacyr)
            amortizacion = 0
            if not m.es_alquilada:
                años = m.obtener_antigüedad()
                amortizacion = (coste_base * self.TASA_AMORTIZACION_ANUAL) * años

            # 4. Cálculo de Impuestos
            subtotal = coste_base + coste_combustible + amortizacion
            total_con_iva = subtotal * (1 + self.IVA_GENERAL)
            
            total_proyecto += total_con_iva
            resumen.append({
                "id": m.id_maquina,
                "nombre": m.nombre,
                "coste_final": round(total_con_iva, 2)
            })
            
            print(f"OK -> {m.id_maquina}: {total_con_iva:,.2f} €")

        return total_proyecto, resumen

    def identificar_maquinas_ineficientes(self):
        # Lógica: Si el consumo por hora es > 40 y tiene > 3 años, es ineficiente
        return [m for m in inventario_maquinaria 
                if m.consumo_hora > 40 and m.obtener_antigüedad() > 3]