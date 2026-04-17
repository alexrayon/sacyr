from __future__ import annotations

from dataclasses import asdict
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import confusion_matrix, fbeta_score, precision_score, recall_score
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

from predictive_maintenance.application import AppConfig, IAlertDispatcher, IDataLoader, IModelStore
from predictive_maintenance.domain import ResultadoEvaluacion, RiesgoMantenimiento, SensorRecord

FEATURES = ["temperatura_motor", "vibracion_eje"]


def temporal_split(
    df: pd.DataFrame,
    train_ratio: float = 0.70,
    val_ratio: float = 0.15,
) -> tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame]:
    if df.empty:
        raise ValueError("No se puede particionar un dataset vacio")
    if not 0.0 < train_ratio < 1.0:
        raise ValueError("train_ratio debe estar en (0, 1)")
    if not 0.0 < val_ratio < 1.0:
        raise ValueError("val_ratio debe estar en (0, 1)")
    if train_ratio + val_ratio >= 1.0:
        raise ValueError("train_ratio + val_ratio debe ser menor que 1")

    sorted_df = df.sort_values("timestamp").reset_index(drop=True)
    n_rows = len(sorted_df)
    train_end = int(n_rows * train_ratio)
    val_end = int(n_rows * (train_ratio + val_ratio))

    train_df = sorted_df.iloc[:train_end].copy()
    val_df = sorted_df.iloc[train_end:val_end].copy()
    test_df = sorted_df.iloc[val_end:].copy()
    return train_df, val_df, test_df


def build_pipeline(config: AppConfig) -> Pipeline:
    config.validate()
    model = RandomForestClassifier(
        n_estimators=config.n_estimators,
        max_depth=config.max_depth,
        min_samples_leaf=config.min_samples_leaf,
        class_weight="balanced_subsample",
        random_state=config.random_state,
        n_jobs=-1,
    )
    return Pipeline(
        steps=[
            ("scaler", StandardScaler()),
            ("model", model),
        ]
    )


class PredictiveMaintenanceService:
    def __init__(
        self,
        config: AppConfig,
        data_loader: IDataLoader,
        model_store: IModelStore,
        alert_dispatcher: IAlertDispatcher,
    ) -> None:
        self._config = config
        self._data_loader = data_loader
        self._model_store = model_store
        self._alert_dispatcher = alert_dispatcher
        self._pipeline: Pipeline | None = None

    def entrenar(self, csv_path: Path) -> dict[str, object]:
        df, audit = self._data_loader.load_and_clean(csv_path)

        train_df, val_df, test_df = temporal_split(df)
        pipeline = build_pipeline(self._config)
        pipeline.fit(train_df[FEATURES], train_df["fallo"])
        self._pipeline = pipeline

        val_eval = self._evaluar_dataset(val_df)
        test_eval = self._evaluar_dataset(test_df)

        feature_importance = self._extract_feature_importance()
        metadata = {
            "trained_at_utc": datetime.now(timezone.utc).isoformat(),
            "features": FEATURES,
            "audit": audit,
            "val_metrics": asdict(val_eval),
            "test_metrics": asdict(test_eval),
            "feature_importance": feature_importance,
        }
        self._model_store.save(pipeline, metadata)
        return metadata

    def cargar_modelo(self) -> None:
        model = self._model_store.load()
        if not isinstance(model, Pipeline):
            raise TypeError("El artefacto cargado no es un Pipeline de Scikit-Learn")
        self._pipeline = model

    def evaluar_riesgo(self, record: SensorRecord) -> RiesgoMantenimiento:
        if self._pipeline is None:
            self.cargar_modelo()
        if self._pipeline is None:
            raise RuntimeError("No hay modelo disponible para evaluar riesgo")

        features = pd.DataFrame(
            [
                {
                    "temperatura_motor": float(record.temperatura_motor),
                    "vibracion_eje": float(record.vibracion_eje),
                }
            ]
        )
        probabilities = self._pipeline.predict_proba(features)
        prob_fallo = float(probabilities[0][1])

        thresholds = self._config.thresholds
        if prob_fallo < thresholds.low_to_medium:
            nivel = "BAJO"
            recomendacion = "Operacion normal. Mantener monitorizacion." 
        elif prob_fallo < thresholds.medium_to_high:
            nivel = "MEDIO"
            recomendacion = "Programar inspeccion preventiva en la siguiente ventana operativa."
        else:
            nivel = "ALTO"
            recomendacion = "Inspeccion inmediata y evaluar parada preventiva de la maquina."

        riesgo = RiesgoMantenimiento(
            prob_fallo=prob_fallo,
            nivel_riesgo=nivel,
            recomendacion=recomendacion,
            principales_factores=self._top_feature_importance(),
        )
        self._alert_dispatcher.dispatch(riesgo, record)
        return riesgo

    def _evaluar_dataset(self, df: pd.DataFrame) -> ResultadoEvaluacion:
        if df.empty:
            raise ValueError("No se puede evaluar un dataset vacio")
        if self._pipeline is None:
            raise RuntimeError("El modelo no esta entrenado")

        y_true = df["fallo"].to_numpy()
        y_pred = self._pipeline.predict(df[FEATURES])

        return ResultadoEvaluacion(
            recall_fallo=float(recall_score(y_true, y_pred, pos_label=1, zero_division=0)),
            precision_fallo=float(precision_score(y_true, y_pred, pos_label=1, zero_division=0)),
            f2_fallo=float(fbeta_score(y_true, y_pred, beta=2, pos_label=1, zero_division=0)),
            matriz_confusion=confusion_matrix(y_true, y_pred, labels=[0, 1]).tolist(),
        )

    def _extract_feature_importance(self) -> dict[str, float]:
        if self._pipeline is None:
            raise RuntimeError("No hay pipeline entrenado")
        model = self._pipeline.named_steps["model"]
        importances = getattr(model, "feature_importances_", None)
        if importances is None:
            raise RuntimeError("El modelo no expone feature_importances_")
        return {feature: float(value) for feature, value in zip(FEATURES, importances)}

    def _top_feature_importance(self, max_features: int = 2) -> list[tuple[str, float]]:
        importance = self._extract_feature_importance()
        ordered = sorted(importance.items(), key=lambda item: item[1], reverse=True)
        return ordered[:max_features]
