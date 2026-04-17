# SPECIFICATION.md

## Resumen del alcance
Migración del núcleo de middleware de Sacyr desde Angular 15 a Angular 21 para habilitar una arquitectura zoneless, standalone-first y basada en reactividad fina con Signals.

Objetivo primario:
- Eliminar dependencia de Zone.js en el componente y su flujo de render.
- Sustituir patrón reactivo de estado local basado en BehaviorSubject por Signals.
- Migrar control flow de plantillas desde sintaxis estructural legacy a bloques modernos.

## Fronteras del sistema (Radiografía)

### Contrato de entrada
| Fuente | Tipo | Formato | Frecuencia | Restricciones |
|---|---|---|---|---|
| API Middleware sensores | Lista de sensores | JSON | Bajo demanda (refresh) | Latencia variable, posibles fallos temporales |
| Acción de usuario | Evento UI | Click | Manual | No bloqueante |

### Contrato de salida
| Consumidor | Tipo | Formato | SLA funcional |
|---|---|---|---|
| Template de estado | Estado de carga/error/datos | Estructuras tipadas en memoria | Cambio visible en < 1 frame tras actualización signal |
| Operador de obra | Vista de sensores | HTML renderizado | Coherencia fuerte: sin estados intermedios inválidos |

### Modelo de dominio mínimo
| Entidad | Campos obligatorios | Invariantes |
|---|---|---|
| SensorData | id, name, active, lastReading, unit | id único, unit no vacío, lastReading numérico |

## Regla de dependencia (Clean Frontend)
Capas permitidas:
1. Modelo: tipos de dominio puros.
2. Estado local: signals y computed.
3. Adaptación de datos: mapeo de respuesta API a modelo.
4. Presentación: template y estilos.

Regla:
- Presentación puede consumir estado y modelos.
- Estado puede consumir modelos.
- Adaptación puede consumir modelos.
- Ninguna capa puede depender de implementación de framework legacy (NgModule APIs o pipes async para estado local signal).

## Matriz de Migración Angular 15 -> Angular 21

### A. Arquitectura de composición
| Área | Antes (Angular 15) | Después (Angular 21) | Criterio de aceptación |
|---|---|---|---|
| Ensamblado | NgModules como unidad principal | Standalone Components como unidad principal | Sin declaraciones en NgModule para el componente migrado |
| Imports UI | imports de módulo agregado | imports directos por componente | Dependencias explícitas en metadata del componente |
| Bootstrap | Dependiente de configuración con Zone.js por defecto | Proveedor zoneless en arranque | App inicia sin Zone.js cargado |

### B. Control Flow en template
| Área | Antes | Después | Criterio de aceptación |
|---|---|---|---|
| Condicional | *ngIf | @if | No uso de directivas estructurales legacy |
| Iteración | *ngFor | @for (con track) | Iteración con estrategia track estable por id |
| Estado vacío | ng-template auxiliar | bloque @empty | Render explícito para lista vacía |

### C. Estado reactivo
| Área | Antes | Después | Criterio de aceptación |
|---|---|---|---|
| Fuente de estado | BehaviorSubject | signal | Estado local sin Subject |
| Derivados | operadores RxJS para cálculo simple | computed | Derivados deterministas sin subscripciones manuales |
| Consumo template | async pipe | lectura directa de signal | Sin async pipe para estado local |

## Límites físicos y de negocio
- Latencia objetivo de actualización visual: <= 16 ms tras commit de estado (escenario nominal).
- Lista de sensores soportada sin degradación perceptible: hasta 1.000 ítems con track por id.
- Debe preservarse semántica operativa: activo/inactivo y lecturas nunca pueden mostrarse cruzadas.
- Falla de red no puede bloquear UI ni dejar loading permanente.

## Seguridad por diseño
- Sin claves ni endpoints hardcodeados en componente.
- Configuración de API vía entorno/inyección de configuración.
- Mensajes de error UI no exponen detalle sensible de infraestructura.

## Criterios de éxito de auditoría previa (Zoneless-ready)
1. No existe dependencia runtime de Zone.js en el componente y su flujo de actualización.
2. Todo estado local de presentación se expresa con signal/computed.
3. No se usan async pipe ni suscripciones manuales para estado local del componente.
4. Template usa bloques @if y @for con track estable.
5. No hay mutaciones fuera del grafo reactivo (sin efectos colaterales silenciosos).
6. El componente funciona con estrategia de render sin detección global.
7. Pruebas de UI validan transiciones: loading -> success y loading -> error.
