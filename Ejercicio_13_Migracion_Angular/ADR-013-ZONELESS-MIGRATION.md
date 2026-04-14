# ADR-013 - Migración Angular 21 con enfoque Zoneless para mejorar TTI

- Estado: Propuesto
- Fecha: 2026-04-14
- Decisores: Arquitectura Frontend / Equipo Middleware
- Alcance: Componente crítico de middleware en Ejercicio_13_Migracion_Angular

## 1. Contexto

El componente de middleware se encuentra en un proceso de migración técnica desde Angular 15 hacia Angular 21, con una especificación inmutable que exige:

- Modernización de sintaxis de template con Control Flow.
- Migración de estado local hacia signals y computed.
- Definición standalone e imports granulares.
- Preservación estricta de la lógica de negocio (setTimeout, carga y manejo de estados).

El objetivo de arquitectura es mejorar Time to Interactive (TTI) del frontend del middleware, disminuyendo trabajo innecesario de detección de cambios y simplificando el runtime reactivo en capa de vista.

## 2. Problema

El modelo tradicional apoyado en Zone.js y patrones de estado con suscripciones manuales puede provocar:

- Activaciones de Change Detection más amplias de lo necesario.
- Mayor coste de parse/execute por código de orquestación reactiva de vista.
- Complejidad operativa en lifecycle (suscripciones, limpieza, acoplamiento).

Esto afecta negativamente la rapidez con la que la interfaz alcanza un estado interactivo estable, especialmente en dashboards con estados de carga y refresco.

## 3. Decisión

Adoptar Angular 21 con estrategia de migración orientada a un modelo zoneless, apoyado en señales como mecanismo primario de reactividad local de UI.

Elementos de la decisión:

- Priorizar un flujo Signal-based para estado local y derivado del componente.
- Reducir dependencia de patrones de suscripción manual en la capa de vista.
- Migrar control flow de template a sintaxis moderna para minimizar directivas estructurales heredadas.
- Mantener inalterada la lógica de negocio durante la transición técnica.

## 4. Justificación

La combinación Angular 21 + Signals + enfoque zoneless aporta:

- Menor superficie de Change Detection global al favorecer actualizaciones dirigidas por dependencias reactivas explícitas.
- Menor overhead de coordinación en lifecycle por eliminar suscripciones manuales de estado local.
- Mejor predictibilidad de render y menor trabajo por interacción en vistas de monitoreo.
- Alineación con roadmap moderno del core Angular para rendimiento y mantenibilidad.

En términos de TTI, la hipótesis de mejora se basa en reducir:

- Tiempo de inicialización de código reactivo de vista.
- Trabajo de detección innecesario en estados que no cambian.
- Carga de utilidades no críticas en la capa de presentación.

## 5. Consecuencias

### 5.1 Positivas

- Mejoras esperadas en TTI percibido y estabilidad de render inicial.
- Reducción de complejidad accidental en componente.
- Menor coste de mantenimiento por modelo reactivo uniforme.
- Mejor tree-shaking al racionalizar dependencias de vista.

### 5.2 Costes y trade-offs

- Curva de adaptación del equipo a patrones Signal-first.
- Necesidad de revisar pautas de testing para estado basado en signals.
- Coexistencia temporal con RxJS en capas de integración/servicios hasta completar transición.

## 6. Opciones consideradas

1. Mantener Angular 15 y optimizar internamente.
- Rechazada por limitar adopción de capacidades modernas del core y mantener deuda técnica estructural.

2. Migrar a Angular 21 sin enfoque zoneless ni strategy Signal-first.
- Rechazada por reducir impacto potencial sobre TTI y preservar complejidad de Change Detection heredada.

3. Migrar a Angular 21 con enfoque zoneless y estado Signal-based.
- Aprobada por mejor equilibrio entre rendimiento, mantenibilidad y evolución futura.

## 7. Plan de implementación asociado

1. Ejecutar secuencia lineal: Metadata -> State -> Lifecycle -> Template.
2. Validar preservación funcional estricta (setTimeout, carga de datos, estados).
3. Medir baseline y post-migración de TTI y bundle.
4. Cerrar ADR con resultados observados y ajustes de hardening.

## 8. Métricas de verificación

- TTI del middleware antes/después.
- Tamaño de bundle JS (raw y gzip) del alcance intervenido.
- Tiempo de evaluación JS del chunk de vista.
- Número de suscripciones manuales en componente (objetivo: 0 para estado local).

## 9. Cumplimiento y gobernanza

Este ADR queda subordinado a MIGRACION_MIDDLEWARE.md como marco normativo de integridad funcional.

Cualquier desviación de lógica de negocio invalida la implementación técnica, aunque mejore métricas de rendimiento.
