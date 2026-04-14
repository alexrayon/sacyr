"""Script de mantenimiento predictivo para maquinaria industrial.

Este módulo implementa un flujo completo de Machine Learning para predecir
fallos de maquinaria a partir de sensores de temperatura y vibración.
"""

from __future__ import annotations

from pathlib import Path

import matplotlib.pyplot as plt
import pandas as pd
import seaborn as sns
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import classification_report, confusion_matrix, recall_score
from sklearn.model_selection import cross_val_predict, train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

# Umbral de decisión para priorizar Recall (detección de fallo).
THRESHOLD_FALLO = 0.35

# Variables globales para reutilizar el pipeline entrenado en predicciones manuales.
PIPELINE_ENTRENADO: Pipeline | None = None
COLUMNAS_MODELO = ["temperatura_motor", "vibracion_eje"]


def resolver_ruta_dataset() -> Path:
    """Resuelve la ruta del CSV en ubicaciones esperadas del repositorio."""
    rutas_candidatas = [
        Path(__file__).resolve().parent / "sensores_maquinaria.csv",
        Path(__file__).resolve().parent.parent / "sensores_maquinaria.csv",
        Path.cwd() / "sensores_maquinaria.csv",
    ]

    for ruta in rutas_candidatas:
        if ruta.exists():
            return ruta

    raise FileNotFoundError(
        "No se encontro sensores_maquinaria.csv en rutas esperadas."
    )


def cargar_datos(ruta_csv: Path) -> pd.DataFrame:
    """Carga el dataset desde CSV y devuelve un DataFrame."""
    df = pd.read_csv(ruta_csv)
    return df


def limpiar_datos(df: pd.DataFrame) -> pd.DataFrame:
    """Aplica reglas de limpieza y saneamiento antes de cualquier calculo.

    Reglas aplicadas:
    1. Eliminar temperaturas fuera de rango de seguridad [-10, 150].
    2. Eliminar filas con nulos en variables criticas del modelo.
    """
    # Copia defensiva para no mutar el DataFrame original.
    df_limpio = df.copy()

    # Filtro de seguridad para errores de lectura en temperatura.
    filtro_temperatura = (
        (df_limpio["temperatura_motor"] >= -10)
        & (df_limpio["temperatura_motor"] <= 150)
    )
    df_limpio = df_limpio.loc[filtro_temperatura].copy()

    # Eliminar registros con nulos en columnas necesarias para modelado.
    columnas_criticas = ["temperatura_motor", "vibracion_eje", "fallo"]
    df_limpio = df_limpio.dropna(subset=columnas_criticas)

    # Verificación explícita para detener el flujo si quedan nulos críticos.
    if df_limpio[columnas_criticas].isnull().sum().sum() > 0:
        raise ValueError("Existen valores nulos criticos tras la limpieza.")

    return df_limpio


def guardar_visualizacion_correlacion(df_limpio: pd.DataFrame, ruta_salida: Path) -> None:
    """Genera y guarda una visualizacion critica de la relacion entre sensores.

    Se guardan dos vistas en una sola imagen:
    - Heatmap de correlación entre temperatura, vibración y fallo.
    - Dispersión temperatura-vibración coloreada por clase de fallo.
    """
    sns.set_theme(style="whitegrid")

    fig, axes = plt.subplots(1, 2, figsize=(14, 6))

    # Subgráfico 1: matriz de correlación térmica.
    matriz = df_limpio[["temperatura_motor", "vibracion_eje", "fallo"]].corr(
        numeric_only=True
    )
    sns.heatmap(
        matriz,
        annot=True,
        fmt=".2f",
        cmap="YlOrRd",
        linewidths=0.5,
        ax=axes[0],
    )
    axes[0].set_title("Correlacion entre sensores y fallo")

    # Subgráfico 2: nube de puntos para identificar cluster de fallo.
    sns.scatterplot(
        data=df_limpio,
        x="temperatura_motor",
        y="vibracion_eje",
        hue="fallo",
        palette={0: "#1f77b4", 1: "#d62728"},
        alpha=0.75,
        ax=axes[1],
    )
    axes[1].set_title("Cluster de fallo: temperatura vs vibracion")
    axes[1].set_xlabel("Temperatura motor (°C)")
    axes[1].set_ylabel("Vibracion eje")

    fig.tight_layout()
    fig.savefig(ruta_salida, dpi=150)
    plt.close(fig)


def construir_pipeline() -> Pipeline:
    """Construye el pipeline de IA con escalado y Random Forest."""
    pipeline = Pipeline(
        steps=[
            ("scaler", StandardScaler()),
            (
                "modelo",
                RandomForestClassifier(
                    n_estimators=300,
                    random_state=42,
                    class_weight="balanced",
                ),
            ),
        ]
    )
    return pipeline


def entrenar_y_evaluar(df_limpio: pd.DataFrame) -> Pipeline:
    """Entrena el modelo y reporta resultados de evaluación por consola."""
    X = df_limpio[COLUMNAS_MODELO]
    y = df_limpio["fallo"].astype(int)

    # División 80/20 estratificada para conservar proporción de fallos.
    X_train, X_test, y_train, y_test = train_test_split(
        X,
        y,
        test_size=0.20,
        random_state=42,
        stratify=y,
    )

    pipeline = construir_pipeline()
    pipeline.fit(X_train, y_train)

    # Predicción probabilística para priorizar Recall con umbral ajustado.
    y_prob_test = pipeline.predict_proba(X_test)[:, 1]
    y_pred_test = (y_prob_test >= THRESHOLD_FALLO).astype(int)

    recall = recall_score(y_test, y_pred_test)

    print("\n=== RESULTADOS HOLD-OUT (80/20) ===")
    print(f"Recall (fallo=1): {recall:.4f}")
    print("\nClassification Report:")
    print(classification_report(y_test, y_pred_test, digits=4))
    print("Matriz de Confusion:")
    print(confusion_matrix(y_test, y_pred_test))

    # Validación cruzada para robustez y reporte adicional.
    y_prob_cv = cross_val_predict(
        pipeline,
        X,
        y,
        cv=5,
        method="predict_proba",
        n_jobs=-1,
    )[:, 1]
    y_pred_cv = (y_prob_cv >= THRESHOLD_FALLO).astype(int)

    print("\n=== VALIDACION CRUZADA (5-FOLD) ===")
    print("Classification Report CV:")
    print(classification_report(y, y_pred_cv, digits=4))
    print("Matriz de Confusion CV:")
    print(confusion_matrix(y, y_pred_cv))

    # Importancia de variables: porcentaje relativo Temperatura vs Vibración.
    importancias = pipeline.named_steps["modelo"].feature_importances_
    imp_temp = float(importancias[0])
    imp_vib = float(importancias[1])

    total_dos = imp_temp + imp_vib
    pct_temp = (imp_temp / total_dos) * 100 if total_dos > 0 else 0.0
    pct_vib = (imp_vib / total_dos) * 100 if total_dos > 0 else 0.0

    print("\n=== IMPORTANCIA DE SENSORES ===")
    print(f"Temperatura: {pct_temp:.2f}%")
    print(f"Vibración: {pct_vib:.2f}%")

    return pipeline


def evaluar_riesgo_maquina(temp: float, vib: float) -> str:
    """Evalua manualmente el riesgo de fallo de una maquina.

    Args:
        temp: Temperatura del motor en grados Celsius.
        vib: Vibración del eje de la maquina.

    Returns:
        Informe textual detallado con estado operativo y probabilidad de fallo.
    """
    if PIPELINE_ENTRENADO is None:
        raise RuntimeError(
            "El modelo no esta entrenado. Ejecuta main() antes de usar esta funcion."
        )

    # Construcción del registro de entrada respetando el orden de columnas.
    entrada = pd.DataFrame(
        [{"temperatura_motor": float(temp), "vibracion_eje": float(vib)}]
    )

    prob_fallo = float(PIPELINE_ENTRENADO.predict_proba(entrada)[0, 1])

    if prob_fallo >= THRESHOLD_FALLO:
        estado = "ESTADO: CRÍTICO - Probabilidad de fallo alta"
    else:
        estado = "ESTADO: NORMAL"

    informe = (
        "\n=== INFORME DE RIESGO MAQUINA ===\n"
        f"Temperatura ingresada: {temp:.2f} °C\n"
        f"Vibracion ingresada: {vib:.2f}\n"
        f"Probabilidad estimada de fallo: {prob_fallo * 100:.2f}%\n"
        f"Umbral operativo de alerta: {THRESHOLD_FALLO * 100:.2f}%\n"
        f"{estado}"
    )

    return informe


def main() -> None:
    """Orquesta el flujo completo de mantenimiento predictivo."""
    global PIPELINE_ENTRENADO

    ruta_csv = resolver_ruta_dataset()
    ruta_img = Path(__file__).resolve().parent / "correlacion_fallos.png"

    print("=== INICIO FLUJO PREDICTIVO ===")
    print(f"Dataset localizado en: {ruta_csv}")

    # 1) Carga de datos.
    df = cargar_datos(ruta_csv)
    print(f"Filas originales: {len(df)}")

    # 2) Limpieza obligatoria antes de cualquier cálculo adicional.
    df_limpio = limpiar_datos(df)
    print(f"Filas tras limpieza: {len(df_limpio)}")

    # 3) Visualización crítica para análisis de relación entre sensores y fallo.
    guardar_visualizacion_correlacion(df_limpio, ruta_img)
    print(f"Grafico guardado en: {ruta_img}")

    # 4) Entrenamiento y evaluación del pipeline de IA.
    PIPELINE_ENTRENADO = entrenar_y_evaluar(df_limpio)

    # 5) Ejemplo de uso de la interfaz final de predicción.
    ejemplo = evaluar_riesgo_maquina(temp=95.0, vib=4.8)
    print(ejemplo)


if __name__ == "__main__":
    main()
