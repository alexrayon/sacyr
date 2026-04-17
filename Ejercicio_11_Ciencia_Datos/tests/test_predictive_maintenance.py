from __future__ import annotations

import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path

import pandas as pd

from predictive_maintenance.application import AppConfig
from predictive_maintenance.domain import SensorRecord
from predictive_maintenance.drift import detect_data_drift
from predictive_maintenance.infrastructure import (
    CSVSensorDataLoader,
    JsonAlertDispatcher,
    LocalModelStore,
)
from predictive_maintenance.use_cases import PredictiveMaintenanceService


class PredictiveMaintenanceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.base_dir = Path(__file__).resolve().parents[1]
        self.csv_path = self.base_dir / "sensores_maquinaria.csv"
        self.tmp_dir = Path(tempfile.mkdtemp(prefix="pm_test_"))
        config = AppConfig(model_dir=self.tmp_dir)
        loader = CSVSensorDataLoader(config=config)
        store = LocalModelStore(model_dir=self.tmp_dir)
        dispatcher = JsonAlertDispatcher(alerts_path=self.tmp_dir / "alertas.jsonl")
        self.service = PredictiveMaintenanceService(config, loader, store, dispatcher)
        self.service.entrenar(self.csv_path)

    def test_evaluar_riesgo_nivel_bajo_o_medio_o_alto(self) -> None:
        record = SensorRecord(
            timestamp=datetime.now(timezone.utc),
            maquina_id="TBM-01",
            temperatura_motor=100.0,
            vibracion_eje=5.0,
        )

        riesgo = self.service.evaluar_riesgo(record)

        self.assertIn(riesgo.nivel_riesgo, {"BAJO", "MEDIO", "ALTO"})
        self.assertGreaterEqual(riesgo.prob_fallo, 0.0)
        self.assertLessEqual(riesgo.prob_fallo, 1.0)
        self.assertTrue(riesgo.principales_factores)

    def test_detect_data_drift_sin_drift_con_misma_distribucion(self) -> None:
        df = pd.read_csv(self.csv_path)
        df["timestamp"] = pd.to_datetime(df["timestamp"], utc=True)
        reference = df.iloc[:500].copy()
        current = df.iloc[500:].copy()
        report = detect_data_drift(reference, current, ["temperatura_motor", "vibracion_eje"])

        self.assertIsNotNone(report)
        self.assertEqual(len(report.reportes), 2)


if __name__ == "__main__":
    unittest.main()
