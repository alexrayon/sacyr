# TELEMETRIA_TBM — Especificación Funcional
## Módulo de Telemetría para Tuneladoras (TBM)

> **Versión:** 1.0.0  
> **Fecha:** 2026-04-08  
> **Estado:** Borrador para revisión  
> **Metodología:** Spec-Driven Development (SDD)  
> **Dominio:** Ingeniería Geoespacial — Construcción de Túneles  

---

## Índice

1. [Contexto y Motivación](#1-contexto-y-motivación)
2. [Objetivo del Producto](#2-objetivo-del-producto)
3. [Glosario Técnico](#3-glosario-técnico)
4. [Arquitectura Funcional de Alto Nivel](#4-arquitectura-funcional-de-alto-nivel)
5. [Requisitos Funcionales](#5-requisitos-funcionales)
   - [RF-01 Recepción de Coordenadas desde PLC](#rf-01-recepción-de-coordenadas-desde-plc)
   - [RF-02 Consulta de Coordenadas Objetivo del Diseño](#rf-02-consulta-de-coordenadas-objetivo-del-diseño)
   - [RF-03 Cálculo de Desviación Tridimensional](#rf-03-cálculo-de-desviación-tridimensional)
   - [RF-04 Clasificación del Estado de Alerta](#rf-04-clasificación-del-estado-de-alerta)
   - [RF-05 Emisión del Telemetry Report](#rf-05-emisión-del-telemetry-report)
6. [Máquina de Estados del Sistema de Alerta](#6-máquina-de-estados-del-sistema-de-alerta)
7. [Escenarios de Aceptación BDD](#7-escenarios-de-aceptación-bdd)
8. [Gestión de Casos de Borde](#8-gestión-de-casos-de-borde)
9. [Restricciones y Atributos de Calidad](#9-restricciones-y-atributos-de-calidad)
10. [Criterios de Aceptación Global del Módulo](#10-criterios-de-aceptación-global-del-módulo)

---

## 1. Contexto y Motivación

Una Tuneladora (TBM, *Tunnel Boring Machine*) es una máquina de ingeniería de alta precisión que excava túneles avanzando por el subsuelo. El eje teórico del proyecto (también denominado *eje de trazado* o *alignment*) es la línea geométrica tridimensional definida por el proyecto de ingeniería que la máquina debe seguir con exactitud milimétrica.

Cualquier desviación sostenida respecto a ese eje puede provocar:

- **Errores geométricos acumulativos** que comprometan el trazado final del túnel.
- **Daños estructurales** en los anillos de dovelas ya colocados.
- **Colisiones** con estructuras subterráneas no previstas.
- **Sobrecostes** y retrasos por correcciones de trayectoria de emergencia.

El **Módulo de Telemetría para TBM** tiene como misión principal proporcionar en tiempo real al operador y a los sistemas de control un indicador cuantitativo y cualitativo (estado de alerta) de la desviación tridimensional de la cabeza de corte respecto al eje teórico del proyecto.

---

## 2. Objetivo del Producto

> Diseñar un **motor de cálculo geoespacial** que:
> 1. Ingiera coordenadas tridimensionales (X, Y, Z) procedentes del PLC (Controlador Lógico Programable) de la TBM.
> 2. Las compare con las coordenadas objetivo (X, Y, Z) definidas en el modelo de diseño del proyecto.
> 3. Calcule la **distancia euclidiana tridimensional** entre la posición real y la posición teórica.
> 4. Clasifique el resultado en un **estado de alerta** que permita a los operadores tomar decisiones en tiempo real.
> 5. Gestione con robustez los errores de comunicación y los datos anómalos del sensor.

El módulo **NO** incluye lógica de actuación sobre la máquina ni comunicación directa con sistemas SCADA externos. Su única responsabilidad es recibir, calcular, clasificar y reportar.

---

## 3. Glosario Técnico

| Término | Definición |
|---|---|
| **TBM** | *Tunnel Boring Machine*. Máquina perforadora de túneles. |
| **PLC** | *Programmable Logic Controller*. Controlador industrial que interfaza con los sensores de la TBM. |
| **Eje Teórico** | Línea de referencia 3D definida por el proyecto de ingeniería que la TBM debe seguir. |
| **Posición Real (PR)** | Coordenada (X, Y, Z) de la cabeza de corte de la TBM en un instante dado. |
| **Posición Objetivo (PO)** | Coordenada (X, Y, Z) del punto del eje teórico correspondiente al avance actual de la TBM. |
| **Desviación (δ)** | Distancia euclidiana tridimensional entre PR y PO. Expresada en metros. |
| **Telemetry Report** | Estructura de datos de salida que encapsula PR, PO, δ y el estado de alerta. |
| **Estado de Alerta** | Clasificación cualitativa del estado del avance: `EN_RUTA`, `PRECAUCION`, `CRITICO`. |
| **Coordenadas de Obra** | Rango de coordenadas XYZ válido para el ámbito geográfico específico del proyecto. Definido en la configuración de obra. |
| **Dato Intermitente** | Señal del sensor que no llega en el periodo de muestreo esperado o llega con valor nulo/inválido. |

---

## 4. Arquitectura Funcional de Alto Nivel

```
┌─────────────────────────────────────────────────────────────────────┐
│                   MÓDULO DE TELEMETRÍA TBM                          │
│                                                                     │
│  ┌──────────────┐    ┌─────────────────┐    ┌────────────────────┐  │
│  │  INPUT PORT  │    │  CALCULATION    │    │   OUTPUT PORT      │  │
│  │              │    │  ENGINE         │    │                    │  │
│  │  PLC Reader  │───>│  1. Validate    │───>│  Telemetry Report  │  │
│  │  (RF-01)     │    │  2. Fetch PO    │    │  (RF-05)           │  │
│  │              │    │  3. Compute δ   │    │                    │  │
│  └──────────────┘    │  4. Classify    │    └────────────────────┘  │
│                      │  (RF-02,03,04)  │                            │
│  ┌──────────────┐    └─────────────────┘                            │
│  │ Design Model │           │                                        │
│  │ Repository   │<──────────┘                                        │
│  │  (RF-02)     │    ┌─────────────────┐                            │
│  └──────────────┘    │  ERROR HANDLER  │                            │
│                      │  (Sección 8)    │                            │
│                      └─────────────────┘                            │
└─────────────────────────────────────────────────────────────────────┘
```

El flujo principal (happy path) es **unidireccional y síncrono** dentro de un ciclo de muestreo. No existe retroalimentación al PLC desde este módulo.

---

## 5. Requisitos Funcionales

---

### RF-01 Recepción de Coordenadas desde PLC

**Descripción:**  
El módulo debe ser capaz de recibir, en cada ciclo de muestreo, una estructura de coordenadas tridimensionales que representa la posición actual de la cabeza de corte de la TBM.

**Datos de entrada esperados:**

| Campo | Tipo | Unidad | Descripción |
|---|---|---|---|
| `tbm_id` | String | — | Identificador único de la TBM en obra. |
| `timestamp` | ISO 8601 UTC | — | Marca temporal exacta de la lectura del sensor. |
| `pos_real.x` | Float (64 bits) | metros | Coordenada X en el sistema de referencia de obra. |
| `pos_real.y` | Float (64 bits) | metros | Coordenada Y en el sistema de referencia de obra. |
| `pos_real.z` | Float (64 bits) | metros | Coordenada Z (cota) en el sistema de referencia de obra. |

**Precondiciones:**
- La conexión con el PLC debe estar establecida antes de iniciar el ciclo de muestreo.
- El módulo debe estar configurado con el `tbm_id` válido para la sesión activa.

**Postcondiciones:**
- Los valores de `pos_real` son accesibles para el motor de cálculo en el mismo ciclo.
- Si la recepción falla, se activa el protocolo de **Dato Intermitente** (ver Sección 8).

**Frecuencia de muestreo:** Configurable. Valor por defecto: **1 Hz** (1 lectura/segundo).

---

### RF-02 Consulta de Coordenadas Objetivo del Diseño

**Descripción:**  
Para cada lectura de posición real, el módulo debe recuperar la coordenada objetivo correspondiente del modelo de diseño del proyecto. La coordenada objetivo es el punto del eje teórico más cercano al avance longitudinal actual de la TBM.

**Datos recuperados:**

| Campo | Tipo | Unidad | Descripción |
|---|---|---|---|
| `pos_objetivo.x` | Float (64 bits) | metros | Coordenada X objetivo en el eje teórico. |
| `pos_objetivo.y` | Float (64 bits) | metros | Coordenada Y objetivo en el eje teórico. |
| `pos_objetivo.z` | Float (64 bits) | metros | Coordenada Z objetivo en el eje teórico. |
| `avance_pk` | Float (64 bits) | metros | Punto kilométrico (PK) del avance longitudinal. |

**Criterio de selección del punto objetivo:**  
La coordenada objetivo se determina mediante la **proyección ortogonal de la posición real sobre el eje teórico**. El punto proyectado es el punto objetivo. Esto garantiza que la desviación calculada es siempre perpendicular al eje de avance.

**Precondiciones:**
- El modelo de diseño (eje teórico) debe estar cargado y ser accesible.
- El `tbm_id` debe tener un eje teórico asociado en el modelo.

**Postcondiciones:**
- `pos_objetivo` queda disponible para el motor de cálculo en el mismo ciclo.
- Si el modelo no es accesible, se emite un error de tipo `DESIGN_MODEL_UNAVAILABLE` y el ciclo se aborta sin emitir `Telemetry Report`.

---

### RF-03 Cálculo de Desviación Tridimensional

**Descripción:**  
El motor de cálculo debe computar la distancia euclidiana tridimensional (δ) entre la posición real (PR) y la posición objetivo (PO).

**Fórmula:**

```
δ = √[ (x_real - x_objetivo)² + (y_real - y_objetivo)² + (z_real - z_objetivo)² ]
```

Donde:
- `x_real, y_real, z_real` = componentes de la Posición Real (`pos_real`).
- `x_objetivo, y_objetivo, z_objetivo` = componentes de la Posición Objetivo (`pos_objetivo`).
- `δ` se expresa en **metros**, con **precisión mínima de 4 decimales** (±0,0001 m = ±0,1 mm).

**Cálculo de componentes individuales (diagnóstico):**  
Además del escalar `δ`, el módulo debe calcular y reportar las desviaciones parciales por eje para facilitar el diagnóstico del operador:

| Campo | Fórmula | Unidad |
|---|---|---|
| `delta_x` | `x_real - x_objetivo` | metros |
| `delta_y` | `y_real - y_objetivo` | metros |
| `delta_z` | `z_real - z_objetivo` | metros |
| `delta_total` (δ) | `√(Δx² + Δy² + Δz²)` | metros |

**Nota sobre signo:** Las desviaciones parciales (`delta_x`, `delta_y`, `delta_z`) son valores con signo para indicar el sentido de la desviación (positivo = exceso hacia la derecha/arriba, negativo = defecto). El `delta_total` siempre es positivo (módulo del vector).

---

### RF-04 Clasificación del Estado de Alerta

**Descripción:**  
Una vez calculado `delta_total` (δ), el módulo debe clasificar el estado de la TBM en uno de los tres estados definidos.

**Tabla de clasificación:**

| Estado | Identificador | Condición | Color HMI |
|---|---|---|---|
| En Ruta | `EN_RUTA` | δ < 0,02 m (< 2 cm) | Verde |
| Precaución | `PRECAUCION` | 0,02 m ≤ δ ≤ 0,05 m (2–5 cm) | Ámbar |
| Crítico | `CRITICO` | δ > 0,05 m (> 5 cm) | Rojo |

**Reglas de transición:**

1. Las comparaciones usan la desviación total euclidiana `δ`. No se evalúan los ejes de forma independiente para la clasificación de estado.
2. Los umbrales son **inclusivos en el límite inferior** del rango `PRECAUCION` (2 cm) y **exclusivos en el límite superior** (5 cm):
   - `EN_RUTA`:    δ ∈ [0, 0.02)
   - `PRECAUCION`: δ ∈ [0.02, 0.05]
   - `CRITICO`:    δ ∈ (0.05, +∞)
3. El módulo **no implementa histéresis** en esta versión. La clasificación es directa según el valor instantáneo de δ en cada ciclo.

**Postcondiciones:**  
El estado clasificado queda disponible para su inclusión en el `Telemetry Report`.

---

### RF-05 Emisión del Telemetry Report

**Descripción:**  
Al finalizar cada ciclo de cálculo exitoso, el módulo debe emitir un `Telemetry Report` estructurado con todos los datos calculados.

**Estructura del Telemetry Report:**

```
TelemetryReport {
    tbm_id:          String           // Identificador de la TBM
    timestamp:       ISO 8601 UTC     // Timestamp de la lectura original del PLC
    processed_at:    ISO 8601 UTC     // Timestamp de finalización del cálculo

    pos_real {
        x: Float
        y: Float
        z: Float
    }

    pos_objetivo {
        x: Float
        y: Float
        z: Float
        avance_pk: Float
    }

    desviacion {
        delta_x:     Float    // metros, con signo
        delta_y:     Float    // metros, con signo
        delta_z:     Float    // metros, con signo
        delta_total: Float    // metros, siempre positivo, 4 decimales
    }

    estado_alerta:   Enum { EN_RUTA | PRECAUCION | CRITICO }

    ciclo_id:        UUID             // Identificador único del ciclo de muestreo
}
```

**Garantías del Telemetry Report:**
- Solo se emite si **todos los requisitos RF-01 a RF-04 se completaron sin error**.
- En caso de error parcial, se emite un `ErrorReport` en su lugar (ver Sección 8).

---

## 6. Máquina de Estados del Sistema de Alerta

La siguiente máquina de estados describe el ciclo de vida de un estado de alerta durante una sesión de avance activa:

```
                        ┌──────────────┐
                        │   INACTIVO   │ <── (Sistema apagado / sin sesión)
                        └──────┬───────┘
                               │ Inicio de sesión de avance
                               ▼
                        ┌──────────────┐
              ┌────────>│   EN_RUTA    │<────────────────────┐
              │         │  (δ < 2cm)   │                     │
              │         └──────┬───────┘                     │
              │                │ δ >= 2cm                    │ δ < 2cm
              │                ▼                             │
              │         ┌──────────────┐                     │
              │         │  PRECAUCION  │─────────────────────┘
              │         │(2cm ≤ δ ≤ 5) │
              │         └──────┬───────┘
              │                │ δ > 5cm          δ <= 5cm
              │                ▼                     │
              │         ┌──────────────┐             │
              │         │   CRITICO    │─────────────┘
              └─────────│  (δ > 5cm)   │
          δ < 2cm       └──────────────┘

          Desde cualquier estado:
          DATA_UNAVAILABLE ──> [ ErrorReport emitido, ciclo abortado ]
```

**Nota:** No existe una transición directa de `CRITICO` a `EN_RUTA` que omita `PRECAUCION`. Sin embargo, dado que no se implementa histéresis y la clasificación es instantánea, si la corrección es suficiente, la transición `CRITICO -> EN_RUTA` en un único ciclo es matemáticamente posible y válida.

---

## 7. Escenarios de Aceptación BDD

Los siguientes escenarios están redactados en lenguaje Gherkin y constituyen los criterios de aceptación formales del módulo. Todos los valores de coordenadas usan el sistema de referencia de obra del proyecto.

---

### Feature: Clasificación del estado de alerta de la TBM

```gherkin
Background:
  Given el sistema de telemetría TBM está activo
  And el modelo de diseño del proyecto está cargado con el eje teórico completo
  And la TBM con id "TBM-SACYR-01" tiene una sesión de avance activa
  And las coordenadas de obra válidas son:
    | Eje | Mínimo     | Máximo      |
    | X   | 100000.000 | 200000.000  |
    | Y   | 400000.000 | 500000.000  |
    | Z   | -100.000   | 0.000       |
```

---

#### Escenario 1 — Máquina Perfectamente Alineada (Estado: EN RUTA)

```gherkin
Scenario: La TBM está perfectamente alineada con el eje teórico
  Given la posición objetivo del eje teórico en el avance actual es:
    | x_objetivo | 150000.0000 |
    | y_objetivo | 450000.0000 |
    | z_objetivo |    -50.0000 |
  When el PLC reporta la posición real de la TBM como:
    | x_real | 150000.0050 |
    | y_real | 450000.0040 |
    | z_real |    -50.0030 |
  Then el motor de cálculo debe calcular:
    | delta_x     |  0.0050 m |
    | delta_y     |  0.0040 m |
    | delta_z     |  0.0030 m |
    | delta_total |  0.0071 m |
  And el estado de alerta emitido debe ser "EN_RUTA"
  And el Telemetry Report debe ser emitido con estado "EN_RUTA"

  # Verificación: δ = √(0.005² + 0.004² + 0.003²) = √0.000050 ≈ 0.0071 m < 0.02 m -> EN_RUTA
```

---

#### Escenario 2 — Desviación de ~4 cm (Estado: PRECAUCIÓN)

```gherkin
Scenario: La TBM presenta una desviación que activa el estado de Precaución
  Given la posición objetivo del eje teórico en el avance actual es:
    | x_objetivo | 150000.0000 |
    | y_objetivo | 450000.0000 |
    | z_objetivo |    -50.0000 |
  When el PLC reporta la posición real de la TBM como:
    | x_real | 150000.0300 |
    | y_real | 450000.0200 |
    | z_real |    -50.0100 |
  Then el motor de cálculo debe calcular:
    | delta_x     |  0.0300 m |
    | delta_y     |  0.0200 m |
    | delta_z     |  0.0100 m |
    | delta_total |  0.0374 m |
  And el estado de alerta emitido debe ser "PRECAUCION"
  And el Telemetry Report debe ser emitido con estado "PRECAUCION"
  And se debe registrar un evento de advertencia en el log de sesión

  # Verificación: δ = √(0.03² + 0.02² + 0.01²) = √0.0014 ≈ 0.0374 m
  # 0.02 m ≤ 0.0374 m ≤ 0.05 m -> PRECAUCION
```

---

#### Escenario 3 — Desviación de ~7 cm (Estado: CRÍTICO)

```gherkin
Scenario: La TBM presenta una desviación severa que activa la Alarma Crítica
  Given la posición objetivo del eje teórico en el avance actual es:
    | x_objetivo | 150000.0000 |
    | y_objetivo | 450000.0000 |
    | z_objetivo |    -50.0000 |
  When el PLC reporta la posición real de la TBM como:
    | x_real | 150000.0566 |
    | y_real | 450000.0350 |
    | z_real |    -50.0200 |
  Then el motor de cálculo debe calcular:
    | delta_x     |  0.0566 m |
    | delta_y     |  0.0350 m |
    | delta_z     |  0.0200 m |
    | delta_total |  0.0695 m |
  And el estado de alerta emitido debe ser "CRITICO"
  And el Telemetry Report debe ser emitido con estado "CRITICO"
  And se debe registrar un evento CRITICO en el log de sesión con timestamp
  And se debe notificar la alarma crítica al sistema de supervisión

  # Verificación: δ = √(0.0566² + 0.035² + 0.02²) ≈ 0.0695 m > 0.05 m -> CRITICO
  # Tolerancia de aceptación del test: ±0.0001 m
```

---

#### Escenario 4 — Transición Ascendente (PRECAUCIÓN -> CRÍTICO)

```gherkin
Scenario: La desviación de la TBM aumenta cruzando el umbral crítico
  Given el estado de alerta actual de la TBM es "PRECAUCION" con delta = 0.045 m
  When el PLC reporta una nueva posición real que produce delta = 0.0510 m
  Then el estado de alerta debe cambiar a "CRITICO"
  And el Telemetry Report del nuevo ciclo debe reflejar el estado "CRITICO"
```

---

#### Escenario 5 — Transición Descendente (CRÍTICO -> PRECAUCIÓN)

```gherkin
Scenario: La TBM corrige su trayectoria y reduce la desviación por debajo del umbral crítico
  Given el estado de alerta actual de la TBM es "CRITICO" con delta = 0.065 m
  When el PLC reporta una nueva posición real que produce delta = 0.0380 m
  Then el estado de alerta debe cambiar a "PRECAUCION"
  And el Telemetry Report del nuevo ciclo debe reflejar el estado "PRECAUCION"
```

---

## 8. Gestión de Casos de Borde

Esta sección especifica el comportamiento obligatorio del módulo ante situaciones anómalas. Todos los errores deben ser registrados en un log de auditoría no borrable.

---

### CB-01 Coordenadas Fuera del Rango Lógico de la Obra

**Descripción del caso:**  
El PLC envía coordenadas que, aunque numéricamente válidas, caen fuera del bounding box tridimensional definido para el proyecto de obra (configurado en los parámetros del módulo).

**Causa probable:**
- Error de configuración del PLC (offset incorrecto).
- Corrupción de la señal en el bus de comunicación industrial.
- Error de referenciación del sistema de coordenadas de obra.

**Comportamiento especificado:**

1. **Detección:** Antes de invocar el motor de cálculo, el módulo debe validar que `pos_real.x`, `pos_real.y` y `pos_real.z` se encuentran dentro de los límites `[min, max]` definidos por el parámetro `obra.bounding_box`.
2. **Acción:**
   - El ciclo de cálculo se **aborta** inmediatamente. No se produce `Telemetry Report`.
   - Se emite un `ErrorReport` con tipo `OUT_OF_BOUNDS_COORDINATES`.
   - Se registra el valor recibido, el rango válido y el timestamp en el log de auditoría.
3. **Escalada:** Después de **3 lecturas consecutivas** `OUT_OF_BOUNDS`, el módulo debe:
   - Emitir una alarma de nivel `CRITICAL_SENSOR_FAULT` al sistema supervisor.
   - Pausar la sesión de muestreo hasta que un operador confirme la revisión del PLC.
4. **Recuperación:** Un contador de lecturas consecutivas erróneas se resetea a cero en cuanto se recibe una lectura válida dentro del bounding box.

**Escenario BDD:**

```gherkin
Scenario: Las coordenadas recibidas del PLC están fuera del rango de la obra
  Given el bounding box de la obra está definido como:
    | Eje | Mínimo     | Máximo      |
    | X   | 100000.000 | 200000.000  |
    | Y   | 400000.000 | 500000.000  |
    | Z   | -100.000   | 0.000       |
  When el PLC reporta la posición real:
    | x_real |  50000.000 |
    | y_real | 450000.000 |
    | z_real |    -50.000 |
  Then el módulo detecta que x_real = 50000.000 está fuera de [100000.000, 200000.000]
  And el ciclo de cálculo se aborta sin emitir un Telemetry Report
  And se emite un ErrorReport con tipo "OUT_OF_BOUNDS_COORDINATES"
  And se registra en el log de auditoría el valor recibido y el rango válido
```

---

### CB-02 Datos del Sensor Intermitentes

**Descripción del caso:**  
El módulo no recibe datos del PLC en el periodo de muestreo esperado. Esto puede ocurrir por pérdida de conexión, timeout del socket, o envío de una estructura de datos vacía/nula.

**Causa probable:**
- Pérdida temporal de conectividad en la red industrial (bus CAN, Profibus, Ethernet industrial).
- Reinicio o fallo transitorio del PLC.
- Alta carga de cómputo en el PLC por otras tareas prioritarias.

**Comportamiento especificado:**

1. **Detección:** Si transcurrido el periodo de muestreo configurado (por defecto 1 segundo) no se ha recibido un paquete de datos válido, se considera un **fallo de ciclo** (`CYCLE_TIMEOUT`).
2. **Tolerancia:** El módulo implementa una política de **reintentos configurables**. Valor por defecto: **2 reintentos** con un intervalo de **500 ms** entre cada reintento antes de declarar el ciclo como fallido.
3. **Acción al agotar reintentos:**
   - Se emite un `ErrorReport` con tipo `SENSOR_DATA_UNAVAILABLE`.
   - El estado de alerta **no se actualiza**. El último estado válido conocido se conserva (*hold-last-known-state*).
   - Se registra en el log de auditoría: timestamp del fallo, número de reintentos agotados, último estado válido.
4. **Escalada:** Después de **5 ciclos consecutivos fallidos**, el módulo debe:
   - Marcar el estado de la sesión como `COMMS_DEGRADED`.
   - Emitir una alarma `COMMUNICATION_FAULT` al sistema supervisor.
   - El operador debe ser notificado visualmente en el HMI.
5. **Recuperación:** Al recibir un ciclo exitoso, el módulo resetea el contador de ciclos fallidos consecutivos y restaura el estado de operación normal.

**Escenario BDD:**

```gherkin
Scenario: El PLC deja de enviar datos durante varios ciclos consecutivos
  Given la TBM está en estado "EN_RUTA" con delta = 0.008 m en el último ciclo exitoso
  When el módulo no recibe datos del PLC durante 3 ciclos de muestreo consecutivos
    (cada ciclo con 2 reintentos agotados)
  Then se emiten 3 ErrorReports de tipo "SENSOR_DATA_UNAVAILABLE"
  And el estado de alerta mostrado al operador permanece en "EN_RUTA" (hold-last-known-state)
  And cada fallo queda registrado en el log de auditoría
  When el módulo no recibe datos durante 2 ciclos adicionales (total: 5 consecutivos)
  Then el módulo emite una alarma "COMMUNICATION_FAULT"
  And el estado de la sesión se marca como "COMMS_DEGRADED"
```

---

### CB-03 Modelo de Diseño No Disponible

**Descripción del caso:**  
El repositorio del modelo de diseño (eje teórico) no responde o devuelve un error al solicitar las coordenadas objetivo.

**Comportamiento especificado:**

1. El ciclo de cálculo se **aborta** inmediatamente.
2. Se emite un `ErrorReport` con tipo `DESIGN_MODEL_UNAVAILABLE`.
3. El estado de alerta se marca como `INDETERMINATE`. Este estado especial es visible en el HMI pero **no se clasifica** dentro de la máquina de estados normal.
4. No se emite `Telemetry Report` hasta que el modelo vuelva a estar accesible.

---

### CB-04 Valor de Coordenada No Numérico / NaN / Infinito

**Descripción del caso:**  
El paquete recibido del PLC contiene un campo de coordenada con valor NaN (*Not a Number*), Infinito, o un tipo de dato inesperado.

**Comportamiento especificado:**

1. El módulo detecta el valor inválido durante la **fase de validación de entrada** (antes del motor de cálculo).
2. El ciclo se aborta. Se emite un `ErrorReport` con tipo `INVALID_COORDINATE_VALUE`.
3. Este error se trata con la misma política de escalada que `OUT_OF_BOUNDS_COORDINATES` (CB-01): 3 consecutivos → alarma `CRITICAL_SENSOR_FAULT`.

---

### CB-05 Ruido Blanco de Señal y Falsos Positivos (Moving Average)

**Descripción del caso:**  
Los niveles de vibración de la cabeza de corte y tolerancias del láser generan que las coordenadas suban y bajen drásticamente. Un pico sónico podría registrar una desviación espuria de > 5cm activando la alarma de estado Crítico y ordenando un interlock innecesario sobre la obra.

**Comportamiento especificado:**
1. El Módulo integrará un procesador de Filtro de Media Móvil (Simple Moving Average - SMA) con una ventana fija de 5 muestras consecutivas provenientes del PLC. 
2. Antes de calcular la euclidiana al cuadrado, la posición evaluada será el promedio de los últimos 5 ciclos válidos. 
3. Solo si la máquina retiene y persiste físicamente una derivación mantenida durante 5 lecturas repetitivas, el cruce de umbral será activado lícitamente mitigando el "Alarm Flapping".

---

## 9. Restricciones y Atributos de Calidad

### Rendimiento
- **Latencia máxima de ciclo:** El tiempo total desde la recepción del paquete del PLC hasta la emisión del `Telemetry Report` no debe superar **200 ms** en condiciones normales de operación.
- **Throughput:** El módulo debe soportar una frecuencia de muestreo máxima de hasta **10 Hz** sin degradación de rendimiento.

### Precisión Numérica
- Todos los cálculos de coordenadas y distancias deben realizarse con aritmética de **punto flotante de 64 bits (IEEE 754 double precision)**.
- El resultado `delta_total` debe reportarse con un mínimo de **4 decimales** en metros (resolución de 0.1 mm).

### Fiabilidad
- El módulo debe alcanzar una disponibilidad de **>= 99.5%** durante las ventanas de avance activo.
- Los errores en el procesamiento de un ciclo **no deben afectar** al procesamiento del ciclo siguiente (aislamiento de fallos por ciclo).

### Trazabilidad y Auditoría
- Cada `Telemetry Report` y cada `ErrorReport` deben incluir un `ciclo_id` único (UUID v4).
- El log de auditoría debe ser **append-only** e **inmutable** durante la sesión de avance.
- Los logs deben conservarse un mínimo de **6 meses** tras la finalización de la obra.

### Seguridad de Datos
- Las coordenadas del eje teórico son datos sensibles del proyecto. El acceso al modelo de diseño debe estar protegido por autenticación de servicio.
- Los `Telemetry Reports` en tránsito deben viajar cifrados (mínimo TLS 1.3).

---

## 10. Criterios de Aceptación Global del Módulo

El Módulo de Telemetría TBM se considera **aceptado** cuando:

- [ ] Todos los escenarios BDD de la Sección 7 pasan exitosamente en el entorno de integración.
- [ ] Los casos de borde de la Sección 8 (CB-01 a CB-04) están cubiertos por tests de integración automatizados.
- [ ] La latencia de ciclo medida en el entorno de pre-producción es inferior a 200 ms en el percentil P99.
- [ ] El `delta_total` calculado para los escenarios BDD presenta un error numérico inferior a **0.0001 m** respecto al valor de referencia.
- [ ] La documentación de integración del módulo (interfaces de entrada/salida) está completa y aprobada por el equipo de instrumentación.
- [ ] Los logs de auditoría funcionan correctamente bajo condiciones de fallo simuladas (CB-01 a CB-04).

---

*Documento generado bajo metodología Spec-Driven Development (SDD). Ninguna línea de código debe ser escrita hasta que esta especificación sea revisada y firmada por el Responsable de Ingeniería del Proyecto y el Analista de Sistemas.*

---
**Fin del documento — TELEMETRIA_TBM v1.0.0**
