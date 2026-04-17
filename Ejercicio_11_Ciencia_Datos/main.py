from __future__ import annotations

import argparse
import json
from pathlib import Path

import pandas as pd

from predictive_maintenance.application import AppConfig
from predictive_maintenance.domain import SensorRecord
from predictive_maintenance.drift import build_baseline, detect_data_drift, detect_sensor_miscalibration
from predictive_maintenance.infrastructure import CSVSensorDataLoader, JsonAlertDispatcher, LocalModelStore
from predictive_maintenance.use_cases import FEATURES, PredictiveMaintenanceService


def build_service(base_dir: Path) -> PredictiveMaintenanceService:
    model_dir = base_dir / "artifacts"
    config = AppConfig(model_dir=model_dir)
    loader = CSVSensorDataLoader(config=config)
    store = LocalModelStore(model_dir=model_dir)
    dispatcher = JsonAlertDispatcher(alerts_path=model_dir / "alertas.jsonl")
    return PredictiveMaintenanceService(
        config=config,
        data_loader=loader,
        model_store=store,
        alert_dispatcher=dispatcher,
    )


def cmd_train(base_dir: Path, csv_path: Path) -> None:
    service = build_service(base_dir)
    metadata = service.entrenar(csv_path)
    print("Modelo entrenado con exito.")
    print(json.dumps(metadata, indent=2, ensure_ascii=True))


def cmd_score(base_dir: Path, csv_path: Path) -> None:
    service = build_service(base_dir)

    df = pd.read_csv(csv_path)
    if df.empty:
        raise ValueError("El CSV no contiene filas para inferencia")

    row = df.iloc[-1]
    record = SensorRecord(
        timestamp=pd.to_datetime(row["timestamp"], utc=True).to_pydatetime(),
        maquina_id=str(row["maquina_id"]),
        temperatura_motor=float(row["temperatura_motor"]),
        vibracion_eje=float(row["vibracion_eje"]),
    )
    riesgo = service.evaluar_riesgo(record)
    print("Resultado de evaluar_riesgo:")
    print(json.dumps(riesgo.__dict__, indent=2, ensure_ascii=True))


def cmd_drift(base_dir: Path, csv_path: Path) -> None:
    _ = base_dir
    df = pd.read_csv(csv_path)
    if len(df) < 100:
        raise ValueError("Se requieren al menos 100 registros para evaluar drift")

    df["timestamp"] = pd.to_datetime(df["timestamp"], utc=True)
    df = df.sort_values("timestamp").reset_index(drop=True)

    split = int(len(df) * 0.7)
    reference = df.iloc[:split].copy()
    current = df.iloc[split:].copy()

    report = detect_data_drift(reference_df=reference, current_df=current, features=FEATURES)
    baseline = build_baseline(reference, FEATURES)

    print("Reporte de drift:")
    print(json.dumps([item.__dict__ for item in report.reportes], indent=2, ensure_ascii=True))
    print(f"drift_global_detectado: {report.drift_global_detectado}")

    print("Posible descalibracion:")
    for feature in FEATURES:
        status = detect_sensor_miscalibration(current, baseline, feature)
        print(f"- {feature}: {json.dumps(status, ensure_ascii=True)}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Mantenimiento predictivo para maquinaria pesada")
    parser.add_argument(
        "command",
        choices=["train", "score", "drift"],
        help="Comando a ejecutar",
    )
    parser.add_argument(
        "--csv",
        default="sensores_maquinaria.csv",
        help="Ruta al CSV de telemetria",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    base_dir = Path(__file__).resolve().parent
    csv_path = Path(args.csv)
    if not csv_path.is_absolute():
        csv_path = base_dir / csv_path

    if args.command == "train":
        cmd_train(base_dir, csv_path)
    elif args.command == "score":
        cmd_score(base_dir, csv_path)
    elif args.command == "drift":
        cmd_drift(base_dir, csv_path)
    else:
        raise ValueError("Comando no soportado")


if __name__ == "__main__":
    main()
