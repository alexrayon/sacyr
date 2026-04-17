from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime


@dataclass(frozen=True)
class SensorRecord:
    timestamp: datetime
    maquina_id: str
    temperatura_motor: float
    vibracion_eje: float


@dataclass(frozen=True)
class ResultadoEvaluacion:
    recall_fallo: float
    precision_fallo: float
    f2_fallo: float
    matriz_confusion: list[list[int]]


@dataclass(frozen=True)
class RiesgoMantenimiento:
    prob_fallo: float
    nivel_riesgo: str
    recomendacion: str
    principales_factores: list[tuple[str, float]]


@dataclass(frozen=True)
class DriftFeatureReport:
    feature: str
    psi: float
    ks_pvalue: float | None
    drift_detectado: bool


@dataclass(frozen=True)
class DriftReport:
    reportes: list[DriftFeatureReport]
    drift_global_detectado: bool
