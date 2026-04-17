from __future__ import annotations

from typing import List

from models.maquinaria import Maquina
from services.ports import FlotaRepositoryPort


INVENTARIO_MAQUINARIA: List[Maquina] = [
    Maquina("TBM-01", "Dulcinea", "Tuneladora", 1500.0, 120, 45.5, 2020),
    Maquina("EXC-45", "Cat 390F", "Excavadora", 250.0, 450, 12.8, 2018),
    Maquina("CRN-12", "Liebherr LTM", "Grua", 400.0, 85, 8.2, 2022),
    Maquina("TBM-02", "Libertad", "Tuneladora", 1650.0, 45, 52.0, 2023, es_alquilada=True),
    Maquina("EXC-46", "Komatsu PC800", "Excavadora", 280.0, 310, 14.5, 2021),
]


class RepositorioFlotaEnMemoria(FlotaRepositoryPort):
    def __init__(self, inventario: List[Maquina] | None = None) -> None:
        self._inventario = list(inventario) if inventario is not None else list(INVENTARIO_MAQUINARIA)

    def obtener_maquinas(self) -> List[Maquina]:
        return list(self._inventario)

    def obtener_maquinas_por_tipo(self, tipo: str) -> List[Maquina]:
        return [maquina for maquina in self._inventario if maquina.tipo == tipo]
