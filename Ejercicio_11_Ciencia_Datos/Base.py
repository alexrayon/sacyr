import pandas as pd
import numpy as np
import os

# Configuración de aleatoriedad para resultados reproducibles
np.random.seed(42)
rows = 1000

# Generación de datos sintéticos
data = {
    'timestamp': pd.date_range(start='2026-01-01', periods=rows, freq='h'),
    'maquina_id': np.random.choice(['TBM-01', 'TBM-02', 'EXCAV-05'], rows),
    'temperatura_motor': np.random.normal(75, 12, rows),
    'vibracion_eje': np.random.normal(2.8, 0.9, rows),
}

df = pd.DataFrame(data)

# Lógica de fallo: si la temperatura es > 95 o la vibración > 4.5
fallo_logico = ((df['temperatura_motor'] > 95) | (df['vibracion_eje'] > 4.5))
df['fallo'] = fallo_logico.astype(int)

# Guardar a CSV
df.to_csv('sensores_maquinaria.csv', index=False)
print("Archivo 'sensores_maquinaria.csv' generado con éxito.")
