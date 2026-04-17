from __future__ import annotations

from dataclasses import dataclass

import numpy as np
import pandas as pd

from predictive_maintenance.domain import DriftFeatureReport, DriftReport


def _psi(expected: np.ndarray, observed: np.ndarray, bins: int = 10) -> float:
    if expected.size == 0 or observed.size == 0:
        raise ValueError("No se puede calcular PSI con arrays vacios")

    quantiles = np.linspace(0, 1, bins + 1)
    breakpoints = np.quantile(expected, quantiles)
    breakpoints[0] = -np.inf
    breakpoints[-1] = np.inf

    expected_hist, _ = np.histogram(expected, bins=breakpoints)
    observed_hist, _ = np.histogram(observed, bins=breakpoints)

    expected_ratio = expected_hist / np.maximum(expected_hist.sum(), 1)
    observed_ratio = observed_hist / np.maximum(observed_hist.sum(), 1)

    epsilon = 1e-8
    expected_ratio = np.clip(expected_ratio, epsilon, None)
    observed_ratio = np.clip(observed_ratio, epsilon, None)

    return float(np.sum((observed_ratio - expected_ratio) * np.log(observed_ratio / expected_ratio)))


@dataclass(frozen=True)
class SensorBaseline:
    media: float
    desviacion: float
    p05: float
    p50: float
    p95: float


def build_baseline(df: pd.DataFrame, features: list[str]) -> dict[str, SensorBaseline]:
    if df.empty:
        raise ValueError("No se puede construir baseline con dataframe vacio")
    baselines: dict[str, SensorBaseline] = {}
    for feature in features:
        series = pd.to_numeric(df[feature], errors="coerce").dropna()
        if series.empty:
            raise ValueError(f"Feature sin datos para baseline: {feature}")
        baselines[feature] = SensorBaseline(
            media=float(series.mean()),
            desviacion=float(series.std(ddof=0)),
            p05=float(series.quantile(0.05)),
            p50=float(series.quantile(0.50)),
            p95=float(series.quantile(0.95)),
        )
    return baselines


def detect_data_drift(
    reference_df: pd.DataFrame,
    current_df: pd.DataFrame,
    features: list[str],
    psi_threshold: float = 0.2,
    ks_pvalue_threshold: float = 0.05,
) -> DriftReport:
    if reference_df.empty or current_df.empty:
        raise ValueError("No se puede detectar drift con datasets vacios")

    try:
        from scipy.stats import ks_2samp
    except ImportError:
        ks_2samp = None

    reports: list[DriftFeatureReport] = []
    for feature in features:
        expected = pd.to_numeric(reference_df[feature], errors="coerce").dropna().to_numpy()
        observed = pd.to_numeric(current_df[feature], errors="coerce").dropna().to_numpy()
        psi_value = _psi(expected, observed)
        ks_pvalue = None
        if ks_2samp is not None:
            _, ks_pvalue = ks_2samp(expected, observed)
            ks_pvalue = float(ks_pvalue)

        drift_flag = psi_value > psi_threshold
        if ks_pvalue is not None:
            drift_flag = drift_flag or ks_pvalue < ks_pvalue_threshold

        reports.append(
            DriftFeatureReport(
                feature=feature,
                psi=psi_value,
                ks_pvalue=ks_pvalue,
                drift_detectado=drift_flag,
            )
        )

    drift_global = any(item.drift_detectado for item in reports)
    return DriftReport(reportes=reports, drift_global_detectado=drift_global)


def detect_sensor_miscalibration(
    current_df: pd.DataFrame,
    baselines: dict[str, SensorBaseline],
    feature: str,
    flatline_std_threshold: float = 1e-3,
) -> dict[str, bool]:
    if current_df.empty:
        raise ValueError("No se puede evaluar descalibracion sin datos actuales")
    if feature not in baselines:
        raise ValueError(f"No existe baseline para la feature: {feature}")

    values = pd.to_numeric(current_df[feature], errors="coerce").dropna()
    if values.empty:
        raise ValueError(f"No hay valores validos para la feature: {feature}")

    baseline = baselines[feature]
    current_mean = float(values.mean())
    current_std = float(values.std(ddof=0))

    offset_persistente = abs(current_mean - baseline.media) > (3.0 * max(baseline.desviacion, 1e-6))
    varianza_anomala = current_std > (2.0 * max(baseline.desviacion, 1e-6))
    flatline = current_std < flatline_std_threshold

    return {
        "offset_persistente": offset_persistente,
        "varianza_anomala": varianza_anomala,
        "sensor_congelado": flatline,
        "posible_descalibracion": bool(offset_persistente or varianza_anomala or flatline),
    }
