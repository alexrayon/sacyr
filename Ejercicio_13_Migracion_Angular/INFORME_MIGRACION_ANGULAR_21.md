# Informe de migracion a Angular 21

## Objetivo
Se ha montado una aplicacion Angular 21 ejecutable en la carpeta angular21-migracion-demo para visualizar de forma directa los cambios aplicados durante la migracion del panel de sensores del middleware de Sacyr.

## Donde esta la demo
- Proyecto Angular: Ejercicio_13_Migracion_Angular/angular21-migracion-demo
- Componente legado simulado: Ejercicio_13_Migracion_Angular/angular21-migracion-demo/src/app/legacy-middleware-status.component.ts
- Componente migrado: Ejercicio_13_Migracion_Angular/angular21-migracion-demo/src/app/middleware-status.component.ts
- Pantalla comparativa: Ejercicio_13_Migracion_Angular/angular21-migracion-demo/src/app/app.html

## Como estaba antes
La version anterior seguia el patron habitual de Angular 15 para este tipo de pantallas:

1. El estado local del componente se gestionaba con BehaviorSubject.
2. La plantilla consumia ese estado mediante async pipe.
3. El flujo visual dependia de directivas estructurales legacy como *ngIf y *ngFor.
4. La composicion estaba mas cerca del modelo centrado en NgModule y del arranque clasico con deteccion global.
5. La validacion de datos y el tratamiento de errores quedaban menos visibles dentro del flujo del componente.

Ese enfoque funciona, pero introduce mas ruido tecnico para un estado local sencillo: streams, plantillas auxiliares y mayor acoplamiento a la deteccion de cambios global.

## Que se ha mejorado

### 1. Componente standalone real
El componente migrado ya no depende de NgModule para declararse. Esto reduce el cableado y facilita una migracion incremental por feature.

### 2. Estado local con Signals
Se han sustituido los BehaviorSubject por signal y computed.

Mejoras obtenidas:
1. Menos boilerplate.
2. Lectura directa del estado en template.
3. Derivados mas simples y expresivos.
4. Actualizaciones mas finas sobre la UI.

### 3. Plantilla con control flow moderno
La plantilla usa @if, @for y @empty en lugar de *ngIf, *ngFor y bloques auxiliares.

Mejoras obtenidas:
1. El flujo de carga, error y exito se lee de arriba abajo sin saltos.
2. La lista vacia tiene un bloque explicito.
3. El tracking por id queda visible en el propio bucle.

### 4. Preparacion zoneless
La aplicacion arranca con provideZonelessChangeDetection, alineando el ejercicio con el objetivo de eliminar dependencias innecesarias de Zone.js.

Mejoras obtenidas:
1. Menor dependencia del ciclo global de deteccion de cambios.
2. Mejor encaje con Signals.
3. Menor complejidad accidental para dashboards interactivos.

### 5. Robustez funcional añadida
Durante el montaje de la demo se ha reforzado el componente migrado con varias mejoras de calidad:

1. Se corrige la carga inicial para que el refresh se ejecute al abrir la pantalla.
2. Se valida la estructura del payload antes de pintar datos.
3. Se detectan ids duplicados y unidades vacias.
4. Los errores de infraestructura se traducen a mensajes de usuario controlados.

## Comparativa resumida
| Aspecto | Antes | Ahora |
|---|---|---|
| Composicion | Mas ligada al ensamblado clasico | Standalone-first |
| Estado local | BehaviorSubject | Signals y computed |
| Template | *ngIf, *ngFor, async pipe | @if, @for, @empty |
| Render | Dependencia mas fuerte del ciclo global | Preparado para zoneless |
| Robustez | Menos validacion visible | Normalizacion y reglas de dominio explicitas |

## Como ejecutar la demo
Desde Ejercicio_13_Migracion_Angular/angular21-migracion-demo:

```bash
npm install
npm start
```

Para validar compilacion:

```bash
npm run build
```

## Resultado final
El ejercicio ya no se queda en un archivo TypeScript aislado. Ahora existe una aplicacion Angular 21 funcional que permite ver:

1. Como era la implementacion anterior.
2. Como queda el componente tras la migracion.
3. Que mejoras tecnicas y funcionales aporta el cambio de version.