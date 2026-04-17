class Maquina:
    def __init__(self, id_maquina, nombre, tipo, coste_hora, horas_uso, 
                 consumo_hora, año_fabricacion, es_alquilada=False):
        self.id_maquina = id_maquina
        self.nombre = nombre
        self.tipo = tipo  # 'Tuneladora', 'Excavadora', 'Grua'
        self.coste_hora = coste_hora
        self.horas_uso = horas_uso
        self.consumo_hora = consumo_hora
        self.año_fabricacion = año_fabricacion
        self.es_alquilada = es_alquilada
        self.historial_mantenimiento = []

    def registrar_mantenimiento(self, fecha, descripcion, coste):
        self.historial_mantenimiento.append({"fecha": fecha, "desc": descripcion, "coste": coste})

    def obtener_antigüedad(self, año_actual=2026):
        return año_actual - self.año_fabricacion