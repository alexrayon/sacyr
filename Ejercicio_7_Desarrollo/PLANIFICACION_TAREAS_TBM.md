# LISTADO DE TAREAS: Implementación Módulo de Telemetría TBM

El siguiente documento detalla la descomposición estructurada de tareas técnicas (Work Breakdown Structure) necesarias para programar el Módulo de Telemetría bajo los estándares de Clean Architecture y alta precisión requeridos.

---

## FASE 1: Modelado Geométrico (Domain Layer)

*   **Tarea 1.1: Estructura Inmutable de Coordenadas**
    *   **Acción:** Implementar `readonly struct Point3D` (Value Object). Debe implementar las interfaces `IEquatable<Point3D>` para garantizar integridad por valor e inmutabilidad en memoria Stack.
    *   **Satisface Requisito:** Funcional #1 (Recepción de Datos Puros - Representación espacial).

*   **Tarea 1.2: Extensiones Geométricas de Alta Precisión**
    *   **Acción:** Desarrollar los métodos internos `CalcularDistanciaEuclidiana(Point3D target)` y `CalcularDistanciaCuadratica(Point3D target)`. El segundo será de uso prioritario para evitar la raíz cuadrada (`Math.Sqrt`) en cálculos de umbrales rígidos.
    *   **Satisface Requisito:** Funcional #3 (Cálculo de Desviación Geométrica).

---

## FASE 2: Motor de Análisis de Desviación (Domain Layer)

*   **Tarea 2.1: Definición de Estados de la Máquina**
    *   **Acción:** Crear el Enum de dominio `NivelSeveridad` definiendo explícitamente los valores base (`EnRuta = 0, Precaucion = 1, Critico = 2, FalloSensor = 99`).
    *   **Satisface Requisito:** Funcional #4 (Sistema Discreto de Estados de Alerta).

*   **Tarea 2.2: Implementación de la Calculadora de Desviaciones (`DeviationCalculator`)**
    *   **Acción:** Programar el módulo puro y determinista que tomará dos `Point3D` (actual teórica y medida en progreso) e indicará el nivel de severidad correspondiente operando las comparaciones sobre constantes calculadas al cuadrado ($2.0^2$ y $5.0^2$ cm). No requiere inyección de dependencias externas.
    *   **Satisface Requisito:** Funcional #4 (Categorización Lógica de Alertas).

---

## FASE 3: Servicio de Monitorización (Application Layer)

*   **Tarea 3.1: Definición de Contrato de Comunicación de Datos**
    *   **Acción:** Definir la clase inmutable `EstadoTrayectoria` (como `record` de C#) que contendrá las coordenadas encapsuladas, la distancia traducida y la enumeración final de Severidad junto con marca de tiempo UTC.
    *   **Satisface Requisito:** Escenarios Dinámicos - Definición de respuesta robusta.

*   **Tarea 3.2: Orquestador Principal (`TbmTelemetryService`)**
    *   **Acción:** Implementar el caso de uso y el servicio `ITbmTelemetryService` asociando el flujo: Recepción externa de `Point3D` $\rightarrow$ Consulta asíncrona de Coordenada Teórica (a un mock service provisional) $\rightarrow$ Invocación de motor geómetrico $\rightarrow$ Retorno del encapsulado `EstadoTrayectoria`.
    *   **Satisface Requisito:** Funcional #1 y #2 (Recepción PLC y Obtención de metadatos de Eje Teórico).

*   **Tarea 3.3: Implementación del Sistema Notificador Reactivo**
    *   **Acción:** Conectar un EventHandler o emisor de MediatR para publicar el evento `OnCriticSeverityReached` solo al momento en que la respuesta cambia a rojo.
    *   **Satisface Requisito:** Funcional #4 - Operaciones SCADA - Alarma dependiente de los Estados del motor de desviación.

---

## FASE 4: Manejo de Excepciones y Resiliencia (Edge Cases)

*   **Tarea 4.1: Intercepción de Anomalías Cinemáticas (Out-of-Bounds)**
    *   **Acción:** Introducir en la Application Layer una validación en caché (`PreviousCoord`). Si el delta con el frame previo es imposible (e.g. salto superior a 30cm en un solo milisegundo), descartar paquete y lanzar alarma tipo `SensorOutOfBoundsException`.
    *   **Satisface Requisito:** Casos de Borde #1 (Datos Anómalos - Parada estructural prevenida).

*   **Tarea 4.2: Filtro Anti-Ruido y Rebote (Smoothing)**
    *   **Acción:** Aplicar una validación por histórico breve. Si la calculadora emite "Crítico", la alerta se retiene a menos que $n$ frames logren confirmar sólidamente que la posición ha derivado, esquivando lecturas espurias momentáneas.
    *   **Satisface Requisito:** Casos de Borde #3 (Ruido Blanco de Señal / Vibraciones Lásers).

---

## FASE 5: Suite de Pruebas Unitarias (Test Layer)

*   **Tarea 5.1: Comprobación Máquina Ideal**
    *   **Acción:** Crear método test `GivenZeroDeviation_WhenCalculated_StateIsEnRuta()`.
    *   **Satisface Requisito:** Escenario BDD #1.

*   **Tarea 5.2: Comprobación Deriva Leve**
    *   **Acción:** Crear método test `Given4CmDeviation_WhenCalculated_StateIsPrecaucion()`.
    *   **Satisface Requisito:** Escenario BDD #2.

*   **Tarea 5.3: Test de Precisión para Errores de Redondeo Float**
    *   **Acción:** Insertar coordenada manipulada expresamente tal que la distancia arroje virtualmente **5.0001cm**, y testear que el sistema responda inequívocamente con el Enum `Critico` (superando la barrera exacta limitante de $5^2$ internamente).
    *   **Satisface Requisito:** Arquitectura de Alta Precisión (Revisión IEEE-754/ADR).

*   **Tarea 5.4: Test Out-of-Bounds**
    *   **Acción:** Introducir dos lecturas falsamente extremas seguidas (+50 metros). Confirmar que el motor geómetrico jamás sea invocado garantizando excepción temprana manejada.
    *   **Satisface Requisito:** Casos de Borde #1.
