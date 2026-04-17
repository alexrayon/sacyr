# TASK_ROADMAP.md

## Secuencia determinista para @developer-agent
Orden obligatorio: 1. Metadatos -> 2. Estado (Signals) -> 3. Template (Control Flow) -> 4. Limpieza de pipes

## Bloque 1: Metadatos

### T1.1 Verificar prerequisitos Angular 21
- Objetivo: confirmar compatibilidad de sintaxis de bloques y signals.
- DoD:
  - Versión objetivo documentada.
  - Dependencias críticas compatibles.

### T1.2 Convertir componente a standalone-first real
- Objetivo: asegurar que el componente no depende de NgModule para declararse.
- DoD:
  - Metadata standalone válida y autosuficiente.
  - Imports del componente explícitos y mínimos.
  - Sin declaración duplicada en módulos legacy.

### T1.3 Preparar bootstrap zoneless
- Objetivo: habilitar ejecución sin Zone.js.
- DoD:
  - Configuración de arranque sin dependencia de Zone.js.
  - Aplicación inicia y renderiza el componente.

## Bloque 2: Estado con Signals

### T2.1 Sustituir BehaviorSubject por signal
- Objetivo: migrar estado primario de sensores.
- DoD:
  - No existe BehaviorSubject para estado local del componente.
  - Estado principal expuesto mediante signal writable.

### T2.2 Introducir computed para derivados de UI
- Objetivo: evitar cálculos imperativos repetidos en template.
- DoD:
  - Total de dispositivos derivado con computed.
  - Cualquier métrica adicional de UI derivada con computed.

### T2.3 Ajustar flujo de carga y error a signals
- Objetivo: modelar loading/error en el grafo reactivo.
- DoD:
  - loading y error son signals.
  - Transiciones de estado verificadas en pruebas unitarias.

## Bloque 3: Template con nuevo Control Flow

### T3.1 Migrar condicionales de estado
- Objetivo: reemplazar condicionales legacy.
- DoD:
  - Uso exclusivo de @if para loading/error/success.
  - Sin directivas estructurales legacy en el template.

### T3.2 Migrar iteración de lista a @for
- Objetivo: optimizar render de colección.
- DoD:
  - Lista renderizada con @for.
  - track por identificador estable de sensor.

### T3.3 Definir estado vacío con bloque dedicado
- Objetivo: cubrir ausencia de datos explícitamente.
- DoD:
  - Existe render de lista vacía con bloque dedicado.
  - Sin parpadeos ni estados ambiguos.

## Bloque 4: Limpieza de pipes innecesarios

### T4.1 Eliminar async pipe asociado a estado local
- Objetivo: consumir signals directamente.
- DoD:
  - No hay async pipe aplicado a estado local signal.
  - Lectura directa de signal en template.

### T4.2 Retirar imports y artefactos RxJS no usados
- Objetivo: reducir peso y deuda técnica.
- DoD:
  - Sin imports RxJS huérfanos en componente.
  - Linter sin advertencias por símbolos sin uso.

### T4.3 Revisión final Zoneless-ready
- Objetivo: validar criterios de auditoría.
- DoD:
  - Checklist Zoneless-ready completo.
  - Evidencia de pruebas funcionales mínimas adjunta.

## Criterios de éxito global
1. Componente funcional en Angular 21 con estrategia standalone.
2. Estado local íntegramente basado en signal/computed.
3. Template migrado a @if y @for con tracking estable.
4. Sin async pipe para estado local.
5. Ejecución confirmada sin Zone.js en el flujo objetivo.
6. Sin imports circulares y sin acoplamientos entre capas.
