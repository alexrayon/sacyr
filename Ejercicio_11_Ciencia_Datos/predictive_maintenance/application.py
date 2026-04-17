from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Protocol

import pandas as pd

from predictive_maintenance.domain import RiesgoMantenimiento, SensorRecord


class IDataLoader(Protocol):
    def load_and_clean(self, csv_path: Path) -> tuple[pd.DataFrame, dict[str, int]]:
        ...


class IModelStore(Protocol):
    def save(self, model: object, metadata: dict[str, object]) -> None:
        ...

    def load(self) -> object:
        ...

    def load_metadata(self) -> dict[str, object]:
        ...


class IAlertDispatcher(Protocol):
    def dispatch(self, riesgo: RiesgoMantenimiento, record: SensorRecord) -> None:
        ...


@dataclass(frozen=True)
class RiskThresholds:
    low_to_medium: float = 0.35
    medium_to_high: float = 0.65

    def validate(self) -> None:
        if not 0.0 < self.low_to_medium < 1.0:
            raise ValueError("El umbral low_to_medium debe estar en (0, 1)")
        if not 0.0 < self.medium_to_high < 1.0:
            raise ValueError("El umbral medium_to_high debe estar en (0, 1)")
        if self.low_to_medium >= self.medium_to_high:
            raise ValueError("El umbral low_to_medium debe ser menor que medium_to_high")


@dataclass(frozen=True)
class DataLimits:
    min_temp: float = 20.0
    max_temp: float = 130.0
    min_vibration: float = 0.0
    max_vibration: float = 10.0


@dataclass(frozen=True)
class AppConfig:
    model_dir: Path
    thresholds: RiskThresholds = RiskThresholds()
    limits: DataLimits = DataLimits()
    random_state: int = 42
    n_estimators: int = 300
    max_depth: int = 10
    min_samples_leaf: int = 4

    def validate(self) -> None:
        self.thresholds.validate()
        if self.n_estimators < 50:
            raise ValueError("n_estimators demasiado bajo para un baseline robusto")
        if self.max_depth < 2:
            raise ValueError("max_depth debe ser al menos 2")
        if self.min_samples_leaf < 1:
            raise ValueError("min_samples_leaf debe ser al menos 1")
