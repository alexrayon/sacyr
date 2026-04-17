from __future__ import annotations

from typing import List

from models.maquinaria import Maquina
from services.ports import FlotaRepositoryPort


class RepositorioFlotaSQL(FlotaRepositoryPort):
    """
    Adaptador de infraestructura para almacenamiento SQL.
    Este esqueleto preserva el contrato para que la logica de calculo no cambie.
    """

    def __init__(self, connection_string: str) -> None:
        if not connection_string:
            raise ValueError("La cadena de conexion SQL es obligatoria.")
        self._connection_string = connection_string

    def obtener_maquinas(self) -> List[Maquina]:
        raise NotImplementedError(
            "Pendiente: implementar consulta SQL y mapeo a entidades Maquina."
        )
