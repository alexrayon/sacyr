# SPECIFICATION - Refactor Gestion de Costes de Maquinaria

## 1. Resumen del problema
El sistema actual calcula costes economicos de maquinaria pesada, pero presenta acoplamiento estrecho entre capa de servicios y capa de datos, y usa reglas fiscales/energeticas hardcodeadas.

Impacto operativo en Sacyr:
- Dificulta reutilizar el sistema en obras internacionales (Chile, Australia, etc.).
- Impide tests unitarios aislados por dependencia directa de datos reales.
- Aumenta el riesgo de errores de cumplimiento fiscal por configuracion fija.

## 2. Radiografia del fallo de diseno actual
### 2.1 Evidencia de acoplamiento
Fuente: services/cost_service.py
- El servicio importa directamente inventario_maquinaria desde data/flota_repository.py.
- La logica de negocio depende de una estructura global concreta de datos.
- No existe contrato abstracto de acceso a flota (repositorio/puerto).

Consecuencia tecnica:
- Violacion de inversion de dependencias.
- El servicio no puede operar con otras fuentes (mock, CSV alternativo, SQL) sin modificar codigo interno.

### 2.2 Evidencia de valores hardcodeados criticos
Fuente: services/cost_service.py
- PRECIO_GASOIL_LITRO = 1.48
- IVA_GENERAL = 0.21
- TASA_AMORTIZACION_ANUAL = 0.10

Riesgo de negocio:
- IVA no universal: Chile y Australia usan esquemas distintos (p. ej. IVA/GST), por lo que 0.21 fijo no aplica.
- Precio combustible varia por pais, divisa, fecha, region y tipo de contrato.
- La tasa de amortizacion puede variar por normativa contable local o politica financiera del proyecto.

### 2.3 Otras constantes de negocio a parametrizar
- Umbral de ineficiencia: consumo_hora > 40
- Antiguedad de ineficiencia: > 3 anos
- Ano actual para calculo de antiguedad: valor por defecto fijo en modelo (2026)

## 3. Analisis de contrato (entrada/salida)

### 3.1 Entradas del caso de uso: Analisis economico completo
| Campo | Tipo | Fuente | Restriccion |
|---|---|---|---|
| maquinas | list[Maquina] | Repositorio de flota | Lista no nula |
| precio_combustible_litro | float | Configuracion por pais/proyecto | > 0 |
| tasa_impuesto_general | float | Politica fiscal | 0 <= valor <= 1 |
| tasa_amortizacion_anual | float | Configuracion financiera | 0 <= valor <= 1 |
| ano_actual | int | Reloj/Contexto | >= ano_fabricacion |

### 3.2 Salidas del caso de uso
| Campo | Tipo | Descripcion |
|---|---|---|
| total_proyecto | float | Total final con impuesto |
| resumen | list[dict] | Detalle por maquina con coste final |
| maquinas_ineficientes | list[Maquina] | Maquinas candidatas a renovacion |

## 4. Regla de dependencia (Clean Architecture)
Capas objetivo:
- Domain (modelos y reglas puras)
- Application (casos de uso: calculo de costes)
- Ports (interfaces: repositorio, fiscalidad, combustible, reloj)
- Infrastructure (CSV, memoria, SQL, APIs externas)
- Composition Root (main.py)

Regla obligatoria:
- Domain no importa Application ni Infrastructure.
- Application solo conoce Ports (abstracciones), nunca implementaciones concretas.
- Infrastructure implementa Ports.
- main.py instancia implementaciones concretas e inyecta dependencias.

## 5. Limites fisicos y de negocio
### 5.1 Limites de validacion de datos
- coste_hora >= 0
- horas_uso >= 0
- consumo_hora >= 0
- ano_fabricacion <= ano_actual
- tasa_impuesto_general en [0, 1]
- tasa_amortizacion_anual en [0, 1]
- precio_combustible_litro > 0

### 5.2 Limites de seguridad operativa (Sacyr)
- Si consumo_hora supera umbral configurable, marcar riesgo de eficiencia.
- Si faltan datos de flota o configuracion fiscal, el sistema debe fallar de forma controlada con error de configuracion.

## 6. Requisitos no funcionales
- Testabilidad: servicio ejecutable con repositorio falso sin I/O.
- Portabilidad fiscal: reglas por pais/proyecto sin recompilar codigo.
- Escalabilidad de datos: cambio de CSV/memoria a SQL sin tocar logica de calculo.
- Trazabilidad: configuracion fiscal usada en cada ejecucion debe ser auditable.
