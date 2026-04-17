# ARCHITECTURE_PLAN.md

## Resumen Ejecutivo
La migración a Angular 21 se basa en tres ejes técnicos:
1. Composición standalone para desacoplar la funcionalidad del modelo NgModule.
2. Estado local con Signals para granularidad reactiva.
3. Ejecución zoneless para reducir trabajo de change detection global y mejorar estabilidad de frame time.

Resultado esperado:
- Menor coste de render en navegador.
- Menor complejidad accidental en plantillas.
- Mayor mantenibilidad para evolución del middleware de obra.

## Diseño de solución

### Patrón principal
- Inyección de Dependencias para servicios de datos y configuración.
- Principios SOLID:
  - SRP: componente solo orquesta estado y vista.
  - OCP: fuente de datos intercambiable por contrato.
  - DIP: componente depende de abstracción de repositorio de sensores.

### Flujo lógico de datos
1. Acción refresh invoca caso de uso de carga.
2. Caso de uso obtiene datos mediante puerto de repositorio inyectado.
3. Adaptador normaliza payload a SensorData.
4. Signals actualizan loading, error y colección.
5. Computed deriva métricas de presentación.
6. Template renderiza por bloques @if/@for.

## Estrategia BehaviorSubject -> Signals

### Estado propuesto
- Signal writable:
  - sensors
  - loading
  - error
- Computed:
  - totalDispositivos
  - sensoresActivos
  - haySensores

### Principios de migración
- Reemplazar next por set/update.
- Eliminar asObservable para estado local.
- Eliminar async pipe cuando la fuente sea signal.
- Mantener RxJS solo en fronteras externas si el backend/SDK lo exige.

## Justificación de rendimiento en navegador
Con Zone.js, cualquier evento asíncrono relevante puede disparar ciclos de detección de cambios de alcance amplio. En modo zoneless con Signals:
- Solo se reevalúan nodos afectados por señales leídas.
- Se evita traversing global del árbol de componentes en eventos no relacionados.
- Se reduce presión de CPU en vistas con listas y refrescos frecuentes.

Efecto esperado:
- Menor tiempo de scripting por interacción.
- Mejor estabilidad del frame budget para 60 fps.
- Menor variabilidad de latencia percibida en paneles operativos.

## Resiliencia y gestión de errores
- Timeout de petición en capa de servicio (configurable).
- Retry con backoff acotado para fallos transitorios de red.
- Circuit breaker simple para evitar tormentas de reintento en degradación sostenida.
- Estado de error explícito en signal y botón de recuperación manual.

## ADRs

### ADR-013-01: Adopción de Standalone Components
- Estado: Aprobado.
- Decisión: El componente se declara standalone y define imports explícitos.
- Motivo: Reducir acoplamiento estructural y facilitar migración incremental.
- Impacto: Menos complejidad de wiring, mayor portabilidad de feature.

### ADR-013-02: Sustitución de BehaviorSubject por Signals
- Estado: Aprobado.
- Decisión: Estado de UI local migrado a signal/computed.
- Motivo: Reactividad fina, menos boilerplate y mejor integración con zoneless.
- Impacto: Eliminar async pipe local y simplificar template.

### ADR-013-03: Control Flow moderno con @if y @for
- Estado: Aprobado.
- Decisión: Migrar directivas estructurales legacy a bloques de control flow.
- Motivo: Sintaxis más clara, menor fragmentación de template, mejor trazabilidad del estado.
- Impacto: Reescritura de plantilla y criterios de track explícitos.

### ADR-013-04: Estrategia Zoneless-first
- Estado: Aprobado.
- Decisión: Configurar arranque y componentes para operar sin Zone.js.
- Motivo: Evitar detección de cambios global y escalar mejor en dashboards industriales.
- Impacto: Revisión de patrones que dependan implícitamente de Zone.js.

## Riesgos y mitigaciones
| Riesgo | Probabilidad | Impacto | Mitigación |
|---|---|---|---|
| Dependencia indirecta de Zone.js en librerías | Media | Alta | Inventario de dependencias y pruebas de arranque zoneless |
| Regresiones visuales en control flow | Media | Media | Pruebas de snapshot y casos de transición de estados |
| Mal uso de signals (mutaciones no reactivas) | Media | Media | Guía de codificación y revisión técnica obligatoria |

## Checklist de salida de planificación
1. Contrato de repositorio definido por interfaz.
2. Estado local modelado con signals/computed.
3. Template migrado a bloques con track por id.
4. Evidencia de ejecución sin Zone.js.
5. Plan de pruebas funcionales y de rendimiento ligero completado.
