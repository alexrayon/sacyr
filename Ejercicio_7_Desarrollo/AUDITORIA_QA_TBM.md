# REPORTE DE AUDITORÍA QA Y CONTROL DE RIESGOS: Módulo de Telemetría TBM

**Elaborado por:** QA Senior & Ingeniería de Riesgos
**Fecha:** Abril 2026

---

## 1. Análisis de Fidelidad (Validación de Umbrales)

Tras la revisión algorítmica y la ejecución formal de la suite de pruebas automatizadas (xUnit), se **certifica** que el sistema cumple estrictamente con las directrices geométricas impuestas en la documentación original. 

Las pruebas demostraron una fidelidad micrométrica sobre los umbrales de transición de fase:
*   El cálculo está desacoplado del error nativo de las variables de punto flotante de 64 bits (`double`). Al redondear a 6 decimales antes de la computación euclidiana pura al cuadrado, la distancia es fidedignamente absoluta.
*   Se evidencia que una lectura de $5.0$ centímetros exactos no genera un falso salto a alarma inmovilizante, mientras que $5.001$ cm acciona el `NivelSeveridad.Critico` inmediatamente. 

**Conclusión QA:** Fidelidad Geométrica Aprobada.

---

## 2. Identificación de Desviaciones Técnicas y Carencias de Contexto Físico

Actualmente, el módulo computa únicamente una matriz posicional lineal reducida a la distancia de un punto A (teórico) a un punto B (real en cabezal) mediante Distancia Euclidiana en 3 dimensiones ($X, Y, Z$). 

**Evaluación de Riesgo a Futuro:**
*El cálculo 3D Euclidiano es **insuficiente** para túneles con curvas dinámicas de gran curvatura.* 

1.  **Omisión de Actitud Geométrica (Pitch, Yaw, Roll):** Una tuneladora puede tener el centro del anillo de corte situado exactamente en el Punto Teórico (`Distancia Euclidiana = 0`), pero estar **"apuntando" (guiñada o cabeceo)** con una divergencia de 3 grados respecto a la tangente de entrada geológica. En el siguiente metro de avance, la máquina se empotrará irremediablemente contra el frente de roca desviado.
2.  **Solución Propuesta para la v2.0:** Integrar al motor de cálculo una matriz de cuaterniones (Quaternions) o Ángulos de Euler recibidos de los giróscopos de abordo. El umbral crítico debe evaluar tanto el *Vector Posicional* como el *Vector Direccional* cruzando el error de alineación estructural.

---

## 3. Propuesta de Iteración: Filtro de Media Móvil (Moving Average)

El mayor riesgo funcional detectado es el "Parpadeo de Alarmas" (Alarm Flapping). En tunelación de frente rocoso cerrado, el traqueteo de la cabeza de perforación genera un enorme ruido blanco sensorial provocando que las coordenadas devueltas cambien violentamente milímetro a milímetro. Si una TBM va operando en límite de desviación ($4.9 cm$), durante la percusión puede mandar un frame de $5.1 cm$ deteniendo paralizando toda la obra automáticamente debido a un pico fantasma de un milisegundo.

Para blindar la fase 1 frente a este riesgo operativo, proponemos la adopción formal del filtro **SMA**.

### Actualización a la Especificación Técnica (Feature Definition)

A continuación, se detalla la actualización de la Sección 4.3 de la especificación técnica actual (la cual se actualizará en el documento matriz) para asentar este control contra la paralización injustificada de obra:

> **4.3 Mitigación de Falsos Positivos por Traqueteo (Filtro de Media Móvil)**
> El Módulo integrará un procesador *Simple Moving Average (SMA)* de ventana fija de **5 muestras**. Antes de calcular la posición actual, el valor de $X, Y$ y $Z$ será igual a la media aritmética de dichas magnitudes durante los últimos 5 ciclos de PLC válidos. 
> Gracias a este suavizado temporal, un pico sónico que registre el sensor como desviación anómala mayor a $5 cm$ se licuará internamente. Solo si la máquina retiene y persiste físicamente una desviación continuada en 5 lecturas consecutivas, la Media del Vector cruzará el umbral, garantizando al piloto de manera incontestable que el chasis de la máquina ha desviado formalmente, disparándose el Paro de Avance.
