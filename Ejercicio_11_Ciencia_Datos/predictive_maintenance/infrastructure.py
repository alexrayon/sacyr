from __future__ import annotations

import json
from dataclasses import asdict
from pathlib import Path

import joblib
import pandas as pd

from predictive_maintenance.application import AppConfig
from predictive_maintenance.domain import RiesgoMantenimiento, SensorRecord


class CSVSensorDataLoader:
    REQUIRED_COLUMNS = {
        "timestamp",
        "maquina_id",
        "temperatura_motor",
        "vibracion_eje",
        "fallo",
    }

    def __init__(self, config: AppConfig) -> None:
        self._config = config

    def load_and_clean(self, csv_path: Path) -> tuple[pd.DataFrame, dict[str, int]]:
        if not csv_path.exists():
            raise FileNotFoundError(f"No existe el CSV en la ruta: {csv_path}")

        raw = pd.read_csv(csv_path)
        missing = self.REQUIRED_COLUMNS - set(raw.columns)
        if missing:
            raise ValueError(f"Faltan columnas obligatorias: {sorted(missing)}")

        initial_rows = len(raw)

        df = raw.copy()
        df["timestamp"] = pd.to_datetime(df["timestamp"], errors="coerce", utc=True)
        df["maquina_id"] = df["maquina_id"].astype(str)
        df["temperatura_motor"] = pd.to_numeric(df["temperatura_motor"], errors="coerce")
        df["vibracion_eje"] = pd.to_numeric(df["vibracion_eje"], errors="coerce")
        df["fallo"] = pd.to_numeric(df["fallo"], errors="coerce")

        with_na = len(df)
        df = df.dropna(subset=["timestamp", "maquina_id", "temperatura_motor", "vibracion_eje", "fallo"])
        removed_na = with_na - len(df)

        before_duplicates = len(df)
        df = df.drop_duplicates(subset=["timestamp", "maquina_id"], keep="last")
        removed_duplicates = before_duplicates - len(df)

        limits = self._config.limits
        range_mask = (
            (df["temperatura_motor"] >= limits.min_temp)
            & (df["temperatura_motor"] <= limits.max_temp)
            & (df["vibracion_eje"] >= limits.min_vibration)
            & (df["vibracion_eje"] <= limits.max_vibration)
        )
        out_of_range = int((~range_mask).sum())
        df = df.loc[range_mask].copy()

        # Las clases deben permanecer binarias para no degradar la supervision.
        df = df[df["fallo"].isin([0, 1])].copy()
        df["fallo"] = df["fallo"].astype(int)

        df = df.sort_values(["maquina_id", "timestamp"]).reset_index(drop=True)
        df["delta_horas"] = (
            df.groupby("maquina_id")["timestamp"].diff().dt.total_seconds().div(3600.0)
        )
        temporal_gaps = int((df["delta_horas"] > 1.0).sum())
        df = df.drop(columns=["delta_horas"])

        audit = {
            "filas_iniciales": initial_rows,
            "filas_limpias": len(df),
            "eliminadas_nulos": removed_na,
            "eliminadas_duplicados": removed_duplicates,
            "eliminadas_fuera_rango": out_of_range,
            "huecos_temporales_detectados": temporal_gaps,
        }
        return df, audit


class LocalModelStore:
    def __init__(self, model_dir: Path) -> None:
        self._model_dir = model_dir
        self._model_path = self._model_dir / "modelo.joblib"
        self._metadata_path = self._model_dir / "metadata.json"

    def save(self, model: object, metadata: dict[str, object]) -> None:
        self._model_dir.mkdir(parents=True, exist_ok=True)
        joblib.dump(model, self._model_path)
        with self._metadata_path.open("w", encoding="utf-8") as handle:
            json.dump(metadata, handle, ensure_ascii=True, indent=2)

    def load(self) -> object:
        if not self._model_path.exists():
            raise FileNotFoundError("No existe un modelo entrenado para cargar")
        return joblib.load(self._model_path)

    def load_metadata(self) -> dict[str, object]:
        if not self._metadata_path.exists():
            raise FileNotFoundError("No existe metadata asociada al modelo")
        with self._metadata_path.open("r", encoding="utf-8") as handle:
            data = json.load(handle)
        return data


class JsonAlertDispatcher:
    def __init__(self, alerts_path: Path) -> None:
        self._alerts_path = alerts_path
        self._alerts_path.parent.mkdir(parents=True, exist_ok=True)

    def dispatch(self, riesgo: RiesgoMantenimiento, record: SensorRecord) -> None:
        payload = {
            "record": {
                "timestamp": record.timestamp.isoformat(),
                "maquina_id": record.maquina_id,
                "temperatura_motor": record.temperatura_motor,
                "vibracion_eje": record.vibracion_eje,
            },
            "riesgo": {
                "prob_fallo": riesgo.prob_fallo,
                "nivel_riesgo": riesgo.nivel_riesgo,
                "recomendacion": riesgo.recomendacion,
                "principales_factores": riesgo.principales_factores,
            },
        }
        with self._alerts_path.open("a", encoding="utf-8") as handle:
            handle.write(json.dumps(payload, ensure_ascii=True) + "\n")
