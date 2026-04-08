# PLANIFICACIÓN TÉCNICA: Arquitectura del Módulo de Telemetría TBM

## 1. Propuesta Arquitectónica (Clean Architecture)

El sistema se diseñará bajo los principios de **Clean Architecture** para garantizar alta testabilidad, bajo acoplamiento y un enfoque centrado en el dominio, algo vital para sistemas de misión crítica.

*   **Capa de Dominio (Domain Layer):**
    *   Contendrá las reglas de negocio puras y la esencia del problema espacial.
    *   **Componentes:** Entities (`TbmMachine`), Value Objects (`Coordenada3D`), Enums (`NivelSeveridad`), y el **Motor de Cálculo Geométrico** (`DeviationCalculator`).
    *   Cero dependencias externas.
*   **Capa de Aplicación (Application / Use Cases):**
    *   Orquesta el flujo de la información.
    *   **Componentes:** Casos de uso como `ProcesarTelemetriaEntranteUseCase`. Aquí reside la lógica principal: toma la coordenada entrante, solicita al puerto de origen la coordenada teórica, llama al Dominio para calcular los deltas, evalúa el estado y, si hay una brecha o anomalía, notifica al exterior.
    *   Define interfaces de puertos de salida (Outbound Ports) como `ITelemetryNotificationService`.
*   **Capa de Infraestructura (Infrastructure):**
    *   Implementaciones tecnológicas concretas.
    *   **Servicios de Notificación de Alertas:** Adapta `ITelemetryNotificationService` para comunicarse vía OPC-UA, MQTT a SCADA, gRPC o WebHooks.
    *   **Adaptadores de Entrada:** Controladores API REST o Workers de background escuchando TCP Sockets directamente desde el PLC.

La clave de este diseño es que el **Motor de Cálculo Geométrico** (Dominio) nunca conoce sobre cómo enviar una alarma acústica o grabar en base de datos. Solo recibe coordenadas y devuelve distancias puras y estados matemáticos objetivos.

---

## 2. Architecture Decision Record (ADR-001)

### Título: Uso de Value Objects para Coordenadas espaciales y Patrón Observador para la Emisión de Alertas Críticas.
**Fecha:** 2026-04-08
**Estado:** Aprobado

**Contexto:**
El procesamiento de coordenadas $(X, Y, Z)$ requiere validaciones intrínsecas (ej. no ser valores nulos o infinitos) y será invocado miles de veces por segundo en un entorno industrial. A su vez, los cambios de umbral (pasar de *En Ruta* a *Crítico*) disparan múltiples acciones asíncronas (encender luces, frenar empuje, grabar logs detallados).

**Decisión:**
1.  **Value Objects para Coordenadas:** Modelaremos `Coordenada3D` como un `readonly struct` (Value Object de C#).
    *   *Justificación:* Otorga inmutabilidad por defecto y semántica de valor. Dos coordenadas con los mismos valores de $(X, Y, Z)$ son estructuralmente iguales. Su asignación en la pila (Stack) reduce sustancialmente el trabajo del Garbage Collector (GC), optimizando el alto rendimiento que demanda un ciclo infinito de PLC.
2.  **Patrón Observador para Alertas:** La capa de aplicación expondrá notificaciones de cambio de estado a través de Eventos de C# (`IObservable<T>` o vía patrón Mediador como MediatR).
    *   *Justificación:* Desacopla la evaluación matemática de sus consecuencias. El caso de uso notifica un evento `OnCriticSeverityReached`. Los suscriptores (servicios de SCADA, Logging, UI) escuchan el evento de forma reactiva y actúan.

**Consecuencias Positivas:** Evita fallos de concurrencia al mutar variables espaciales y garantiza que si se añade un nuevo actuador de emergencia mañana, el núcleo lógico no se modifique en absoluto (Open/Closed Principle).

---

## 3. Diseño de Contratos e Interfaces (C# .NET)

A continuación, la abstracción mediante las entidades puras del dominio y el caso de uso central.

```csharp
namespace TbmTelemetry.Domain
{
    public enum NivelSeveridad
    {
        EnRuta = 0,
        Precaucion = 1,
        Critico = 2,
        PerdidaTelemetria = 99
    }

    /// <summary>
    /// Contract para exponer la información validada al exterior.
    /// </summary>
    public record EstadoTrayectoria
    {
        public Coordenada3D CoordenadaActual { get; init; }
        public Coordenada3D CoordenadaTeorica { get; init; }
        public double DistanciaDesviacionCm { get; init; }
        public NivelSeveridad Severidad { get; init; }
        public DateTime TimestampCalculo { get; init; }
        public bool EsPosicionValida { get; init; } // Gestor del Edge Case "Out of Bounds"
    }

    public readonly struct Coordenada3D : IEquatable<Coordenada3D>
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Coordenada3D(double x, double y, double z)
        {
            X = x; Y = y; Z = z;
        }

        public bool Equals(Coordenada3D other) => 
            X == other.X && Y == other.Y && Z == other.Z;
    }
}

namespace TbmTelemetry.Application
{
    using TbmTelemetry.Domain;
    
    /// <summary>
    /// Interfaz principal (Input Port) que consumirán los controladores/workers.
    /// </summary>
    public interface ITbmTelemetryService
    {
        /// <summary>
        /// Procesa una nueva trama entrante del PLC.
        /// </summary>
        Task<EstadoTrayectoria> ProcesarLecturaSincronaAsync(Coordenada3D posicionActual, string progressKey);
    }
}
```

---

## 4. Análisis de Precisión: Control del Error de Truncamiento IEEE-754

En procesadores modernos, los floats y doubles bajo el estándar IEEE-754 pueden representar valores como `4.99999999999991` cuando en realidad la distancia geométrica pura es exactamente de `5.0` centímetros. Si evaluamos llanamente `Math.Sqrt(suma_cuadrados) > 5.0m`, podríamos caer en el falso positivo y permanecer en estado `Precaución` perdiendo un nanómetro de exactitud causando un fallo sistémico.

### Estrategia de Mitigación en el Motor Geométrico

Para garantizar exactitud perfecta contra los umbrales estáticos y eludir la perdida originada por aproximaciones en la computación de `Math.Sqrt`, **alteraremos la fórmula de comparación evitando extraer la raíz cuadrada para la toma de decisión.**

En lugar de evaluar:
$$D = \sqrt{(\Delta X)^2 + (\Delta Y)^2 + (\Delta Z)^2} \ >\ \text{Umbral\_Alarma\_Crítica}\ (5.0\text{ cm})$$

**Elevaremos al cuadrado los umbrales lógicos del sistema de estados de antemano:**
- Umbral Precaución (Cuadrático) = $2.0^2 = 4.0$
- Umbral Crítico (Cuadrático) =   $5.0^2 = 25.0$

```csharp
// Dentro del Dominio (DeviationCalculator)
public static NivelSeveridad CalcularSeveridad(Coordenada3D c1, Coordenada3D c2)
{
    // Calculamos distancias relativas directas en CM
    double dx = (c1.X - c2.X) * 100.0;
    double dy = (c1.Y - c2.Y) * 100.0;
    double dz = (c1.Z - c2.Z) * 100.0;
    
    // Distancia Cuadrática. Cero uso de Math.Sqrt, la operación es entera/precisa.
    double distanciaCuadratica = (dx * dx) + (dy * dy) + (dz * dz);

    // Comparación sobre los umbrales al cuadrado (4.0 y 25.0 cm^2)
    if (distanciaCuadratica > 25.0) 
        return NivelSeveridad.Critico;
    
    if (distanciaCuadratica >= 4.0) 
        return NivelSeveridad.Precaucion;
        
    return NivelSeveridad.EnRuta;
}
```

**Ventaja Computacional y Lógica:**
1. **Rendimiento:** Evitar `Math.Sqrt` elimina un ciclo de Ticks considerable en el procesador (crucial para ciclos PLC de lata Frecuencia).
2. **Determinismo Absoluto:** Al comparar las sumas cuadráticas planas contra las constantes `4.0` y `25.0`, nos blindamos operativamente previniendo el error de truncamiento `float`. Extraeremos `Math.Sqrt()` solo para la emisión pública de la propiedad textual `DistanciaDesviacionCm` en la UI, pero nunca como detonador de la lógica de negocio subyacente.
