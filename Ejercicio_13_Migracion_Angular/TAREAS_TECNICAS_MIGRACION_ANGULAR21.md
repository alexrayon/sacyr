# Tareas Tecnicas Granulares - Migracion a Angular 21

## Objetivo

Definir un backlog tecnico atomico y ordenado para migrar el componente de middleware a Angular 21, alineado con MIGRACION_MIDDLEWARE.md y el enfoque de Software Aumentado.

## Orden Obligatorio de Ejecucion

1. Modernizacion del Decorador
2. Transicion a Signals
3. Refactorizacion de la Plantilla (Control Flow)
4. Limpieza de Reactividad
5. Verificacion de Ciclo de Vida

## 1) Modernizacion del Decorador

### Tarea A21-001 - Consolidar metadata standalone
- Tipo: Refactor de definicion de componente
- Accion: Verificar y consolidar standalone: true como requisito inmutable del componente.
- Resultado esperado: El componente queda desacoplado de NgModule para este alcance.

### Tarea A21-002 - Inventario de imports de template
- Tipo: Analisis de dependencias
- Accion: Listar imports actuales y mapear su uso real en template y clase.
- Resultado esperado: Matriz importado vs usado para habilitar limpieza segura.

### Tarea A21-003 - Tree-shaking de CommonModule
- Tipo: Optimizacion de imports
- Accion: Eliminar CommonModule como dependencia generalista y sustituir por imports granulares estrictamente necesarios.
- Resultado esperado: Reduccion de superficie importada y mejor potencial de tree-shaking.

### Criterio de cierre del bloque
- standalone: true activo y validado.
- CommonModule eliminado del componente.
- No hay imports no usados.

## 2) Transicion a Signals

### Tarea A21-004 - Modelar estado local tipado
- Tipo: Rediseno reactivo
- Accion: Definir slices de estado local (loading, error, items) en formato signal tipado.
- Resultado esperado: Contrato de estado explicito y consistente.

### Tarea A21-005 - Sustituir BehaviorSubject por signal
- Tipo: Migracion de estado
- Accion: Reemplazar almacenamiento local basado en BehaviorSubject por signal correspondiente.
- Resultado esperado: Estado local sin dependencia de stream para consumo de vista.

### Tarea A21-006 - Migrar escrituras de estado a set/update
- Tipo: Ajuste de mutaciones
- Accion: Reescribir puntos de escritura para usar set o update segun el caso.
- Resultado esperado: Mutaciones declarativas y trazables.

### Tarea A21-007 - Introducir estado derivado con computed
- Tipo: Derivacion reactiva
- Accion: Declarar computed para agregados o proyecciones de presentacion cuando aplique.
- Resultado esperado: Eliminacion de logica derivada dispersa en metodos imperativos.

### Criterio de cierre del bloque
- No existe BehaviorSubject en estado local de UI.
- Todos los writes usan set/update.
- Estado derivado resuelto con computed cuando corresponde.

## 3) Refactorizacion de la Plantilla (Control Flow)

### Tarea A21-008 - Migrar condiciones de render a bloques if
- Tipo: Refactor de template
- Accion: Reescribir todas las ramas condicionales estructurales usando bloques if.
- Resultado esperado: Eliminacion total de sintaxis estructural obsoleta en condiciones.

### Tarea A21-009 - Migrar iteraciones a bloques for
- Tipo: Refactor de template
- Accion: Reescribir listas usando bloques for con lectura directa del estado signal-based.
- Resultado esperado: Iteracion alineada con control flow moderno.

### Tarea A21-010 - Definir clausula track obligatoria
- Tipo: Optimizacion de render
- Accion: Incorporar clausula track en cada bloque for de conectores para estabilidad de identidad en DOM.
- Resultado esperado: Menor recreacion de nodos y renderizado mas eficiente.

### Tarea A21-011 - Incorporar estado vacio con empty
- Tipo: Robustez de UX
- Accion: Agregar rama empty en iteraciones donde la coleccion pueda estar vacia.
- Resultado esperado: Comportamiento explicito de lista sin elementos.

### Criterio de cierre del bloque
- Sin uso de ngIf ni ngFor.
- Todos los bloques for tienen clausula track.
- Estado vacio cubierto con empty donde aplica.

## 4) Limpieza de Reactividad

### Tarea A21-012 - Eliminar AsyncPipe del template local
- Tipo: Simplificacion reactiva
- Accion: Retirar AsyncPipe para estado local y sustituir por acceso directo a la ejecucion de la senal items().
- Resultado esperado: Template sin dependencias de async para estado local migrado.

### Tarea A21-013 - Retirar suscripciones manuales residuales
- Tipo: Higiene de componente
- Accion: Eliminar subscribe/unsubscribe de la capa de vista cuando solo modelaban estado local.
- Resultado esperado: Componente sin gestion manual de suscripciones para UI state.

### Tarea A21-014 - Revisar imports reactivos sobrantes
- Tipo: Limpieza de dependencias
- Accion: Eliminar tipos y operadores RxJS no utilizados en componente de vista.
- Resultado esperado: Menor peso y menor complejidad de mantenimiento.

### Criterio de cierre del bloque
- Sin AsyncPipe para estado local.
- Acceso a datos mediante items().
- Sin suscripciones manuales de UI.

## 5) Verificacion de Ciclo de Vida

### Tarea A21-015 - Validar inicializacion en ngOnInit
- Tipo: Verificacion funcional
- Accion: Confirmar que ngOnInit inicializa el flujo de carga y que las senales reflejan loading/error/data en el mismo orden funcional actual.
- Resultado esperado: Equivalencia de comportamiento con la base.

### Tarea A21-016 - Comprobar invariantes de negocio
- Tipo: Control de integridad
- Accion: Verificar que setTimeout, secuencia de carga y dataset no fueron alterados por la migracion tecnica.
- Resultado esperado: Cero cambios de logica de negocio.

### Tarea A21-017 - Evaluar readiness zoneless
- Tipo: Preparacion arquitectonica
- Accion: Confirmar ausencia de dependencias implicitas a ciclos globales de deteccion y validar que la reactividad local queda gobernada por signals.
- Resultado esperado: Componente clasificado como zoneless-ready.

### Tarea A21-018 - Ejecutar checklist final de aceptacion
- Tipo: Gobernanza de entrega
- Accion: Validar cierre de bloques 1 a 5 con evidencia tecnica.
- Resultado esperado: Refactorizacion habilitada para fase de implementacion.

### Criterio de cierre del bloque
- ngOnInit inicia correctamente el flujo signal-based.
- Invariantes funcionales preservados.
- Componente marcado como zoneless-ready.

## Definicion de Terminado Global

- Orden de Software Aumentado completado sin saltos.
- Sin cambios en logica de negocio.
- Sin directivas estructurales obsoletas.
- Sin AsyncPipe ni suscripciones manuales para estado local.
- Plan listo para ejecucion de implementacion y validacion de rendimiento.