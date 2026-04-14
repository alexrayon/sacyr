# Especificación Técnica Inmutable - Migración Middleware Angular 15 -> 21

## 1. Propósito

Este documento define las reglas técnicas obligatorias para ejecutar la migración lineal del componente crítico de middleware desde Angular 15 hacia Angular 21.

Su objetivo es establecer criterios inmutables para las fases de planificación, diseño y ejecución, evitando desviaciones funcionales durante la modernización técnica del frontend.

## 2. Alcance y Principios

- Alcance: componente de estado y visualización del middleware en el ejercicio de migración Angular.
- Tipo de migración: lineal, controlada y sin rediseño funcional.
- Naturaleza del cambio: exclusivamente técnica (sintaxis, modelo reactivo y definición de componente).
- Restricción principal: preservar comportamiento observable y resultados de negocio.

## 3. Matriz de Conversión de Sintaxis (Obligatoria)

Todas las directivas estructurales obsoletas deben ser sustituidas por Control Flow moderno de Angular 21.

| Estado Angular 15 | Estado Angular 21 (Objetivo) | Regla de Migración |
|---|---|---|
| `*ngIf` | `@if` | Reemplazo obligatorio en toda condición de renderizado. |
| `*ngFor` | `@for` | Reemplazo obligatorio en toda iteración de colecciones. |
| N/A o condiciones auxiliares para listas vacías | `@empty` | Obligatorio para expresar estado vacío junto a `@for` cuando aplique. |

### 3.1 Reglas Normativas de Conversión

- No se permite mantener `*ngIf` ni `*ngFor` en el resultado final.
- No se permiten migraciones parciales dentro del mismo componente.
- Toda iteración migrada a `@for` debe evaluar explícitamente la experiencia de estado vacío mediante `@empty` cuando exista riesgo de colección vacía.
- La migración de sintaxis no puede introducir cambios en textos de negocio, semántica de estado ni criterios de visibilidad.

## 4. Especificación de Estado Reactivo (Obligatoria)

Se define la transición del modelo basado en RxJS para estado de vista hacia Signals como estándar del core moderno.

### 4.1 Cambio de Paradigma

| Patrón Anterior | Patrón Objetivo | Restricción |
|---|---|---|
| `BehaviorSubject` para estado de UI | `signal()` | Sustitución obligatoria para estado local del componente. |
| `pipe async` en plantilla | lectura directa de signals y derivados | Eliminación obligatoria del uso de `async` para estado local migrado. |
| Composición derivada ad hoc | `computed()` | Uso obligatorio para estado derivado declarativo. |

### 4.2 Reglas Normativas de Estado

- `signal()` será el mecanismo base para estado mutable de interfaz.
- `computed()` será el mecanismo obligatorio para derivaciones de estado y agregados de presentación.
- No se admiten coexistencias redundantes de `BehaviorSubject` y `signal()` para el mismo dato de estado.
- Eliminar dependencia de `pipe async` en la plantilla para los estados migrados a signals.
- Cualquier interoperabilidad RxJS externa debe justificarse por frontera técnica, no por preferencia de implementación.

## 5. Definición de Componente en Angular 21 (Obligatoria)

### 5.1 Standalone

- Todo componente migrado deberá declararse con `standalone: true`.
- No se aceptan componentes acoplados a NgModule para este alcance.

### 5.2 Imports Granulares

- Se elimina el uso generalista de `CommonModule` como dependencia por defecto.
- Se adoptan imports granulares, explícitos y mínimos, alineados con las capacidades requeridas del componente.
- No se deben introducir imports no utilizados ni mantener dependencias heredadas sin uso efectivo.

## 6. Criterios de Integridad Funcional (No Negociables)

Durante esta refactorización técnica, no se debe alterar la lógica de negocio existente.

### 6.1 Invariantes de Comportamiento

- Debe preservarse la mecánica de temporización basada en `setTimeout`.
- Debe preservarse la secuencia de carga de datos (inicio, éxito, error) tal y como existe en la versión base.
- Deben mantenerse los datos de simulación, su estructura y el flujo de asignación al estado de UI.
- Debe preservarse la semántica de las banderas de estado funcional (`loading`, `error` o equivalentes).
- No se permiten cambios en reglas de negocio, umbrales, textos operativos ni decisiones de dominio.

### 6.2 Criterio de Aceptación de Integridad

Una migración se considera valida solo si:

- El comportamiento funcional observado antes y despues de la migración es equivalente.
- Las diferencias se limitan a infraestructura de renderizado y modelo reactivo.
- No existe regresión en estados de carga, error y visualización de dispositivos.

## 7. Secuencia de Ejecución para Planificación

1. Inventariar sintaxis estructural existente y mapearla a Control Flow (`@if`, `@for`, `@empty`).
2. Inventariar fuentes de estado local y clasificar migración a `signal()` y `computed()`.
3. Definir matriz de imports granulares requeridos para componente standalone.
4. Ejecutar refactorización técnica sin tocar lógica de negocio.
5. Validar equivalencia funcional con foco en temporización, carga de datos y estados visibles.

## 8. Entregables de Fase

- Documento de plan de migración aprobado contra esta especificación.
- Checklist de cumplimiento de sintaxis, estado reactivo y definición de componente.
- Evidencias de verificación de integridad funcional sin cambios de negocio.

## 9. Criterio de Cumplimiento

Este documento es normativo para el proceso de migración Angular 15 -> 21 del middleware.

Cualquier desviación debe registrarse como excepcion formal y aprobarse antes de implementación.
