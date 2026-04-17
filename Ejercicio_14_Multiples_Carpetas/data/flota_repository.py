inventario_maquinaria = [
    Maquina("TBM-01", "Dulcinea", "Tuneladora", 1500.0, 120, 45.5, 2020),
    Maquina("EXC-45", "Cat 390F", "Excavadora", 250.0, 450, 12.8, 2018),
    Maquina("CRN-12", "Liebherr LTM", "Grua", 400.0, 85, 8.2, 2022),
    Maquina("TBM-02", "Libertad", "Tuneladora", 1650.0, 45, 52.0, 2023, es_alquilada=True),
    Maquina("EXC-46", "Komatsu PC800", "Excavadora", 280.0, 310, 14.5, 2021)
]

def obtener_maquinas_por_tipo(tipo):
    return [m for m in inventario_maquinaria if m.tipo == tipo]