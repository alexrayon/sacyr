from __future__ import annotations

from typing import Dict, List


class Maquina:
    def __init__(
        self,
        id_maquina: str,
        nombre: str,
        tipo: str,
        coste_hora: float,
        horas_uso: float,
        consumo_hora: float,
        ano_fabricacion: int,
        es_alquilada: bool = False,
    ) -> None:
        self.id_maquina = id_maquina
        self.nombre = nombre
        self.tipo = tipo  # 'Tuneladora', 'Excavadora', 'Grua'
        self.coste_hora = coste_hora
        self.horas_uso = horas_uso
        self.consumo_hora = consumo_hora
        self.ano_fabricacion = ano_fabricacion
        # Compatibilidad temporal con codigo legado que usa atributo con tilde.
        self.año_fabricacion = ano_fabricacion
        self.es_alquilada = es_alquilada
        self.historial_mantenimiento: List[Dict[str, object]] = []

    def registrar_mantenimiento(self, fecha: str, descripcion: str, coste: float) -> None:
        self.historial_mantenimiento.append({"fecha": fecha, "desc": descripcion, "coste": coste})

    def obtener_antiguedad(self, ano_actual: int) -> int:
        return ano_actual - self.ano_fabricacion

    def obtener_antigüedad(self, año_actual: int = 2026) -> int:
        # Compatibilidad temporal con codigo legado que invoca el metodo con tilde.
        return self.obtener_antiguedad(año_actual)