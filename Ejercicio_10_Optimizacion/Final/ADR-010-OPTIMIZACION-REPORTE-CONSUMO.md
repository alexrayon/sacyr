# ADR-010 - Optimizacion de Consulta de Reporte de Consumo en SQL Server

## Estado
Propuesto

## Fecha
2026-04-08

## Contexto
El diagnostico documentado en DIAGNOSTICO_SQL.md concluye que la consulta actual presenta:
- Anti-patron de subconsultas correlacionadas en el SELECT para `TotalConsumo` y `UltimoMovimiento`.
- Riesgo alto de `Table Scan`/`Index Scan` repetitivo sobre `Movimientos` (~50M filas).
- Posibles `RID Lookup`/`Key Lookup` masivos por falta de cobertura.
- Filtrado por `Estado` y `FechaAlta` sin indice alineado, elevando lecturas logicas y cardinalidad intermedia.

Impacto esperado en baseline:
- Complejidad cercana a $O(N \cdot M)$ con trabajo duplicado sobre la misma tabla.
- Ejecucion en minutos bajo carga.

## Decision
Se adopta una estrategia de optimizacion en dos pilares:

1. Refactorizacion del esquema de consulta para eliminar subconsultas correlacionadas.
2. Diseno de indices de cobertura para reducir lecturas logicas y evitar lookups.

### 1) Estrategia de refactorizacion (CTE/GROUP BY)
Se reemplaza el patron de dos subconsultas correlacionadas por una agregacion unica sobre `Movimientos` por `MaquinaId`, materializada logicamente mediante CTE (o subconsulta derivada equivalente con GROUP BY).

Esquema objetivo (logico):
1. Filtrar `Maquinas` por `Estado` y `FechaAlta` en una etapa temprana.
2. Agregar `Movimientos` una sola vez por `MaquinaId` para obtener simultaneamente:
   - SUM(Consumo) como TotalConsumo
   - MAX(Fecha) como UltimoMovimiento
3. Unir resultados agregados con `Maquinas` filtradas y `Proyectos`.
4. Ordenar por TotalConsumo en la capa final.

Razon tecnica:
- Se elimina el doble recorrido correlacionado por fila de maquina.
- Se pasa de ejecuciones repetitivas por fila a una agregacion set-based.
- Se reduce el riesgo de planes con nested loops correlacionados costosos.

### 2) Diseno de indices (Covering Index)
Se define un indice NONCLUSTERED de cobertura sobre la tabla de filtrado principal:

Indice propuesto (principal):
- Tabla: `Maquinas`
- Key columns: (`Estado`, `FechaAlta`)
- INCLUDE: (`Id`, `ProyectoId`, `Nombre`)

Objetivo del diseno:
- Permitir `Index Seek` por predicados del WHERE.
- Evitar acceso adicional a la tabla base para columnas de join y salida.
- Reducir o eliminar `Key Lookup` en la rama de `Maquinas`.

Indice complementario recomendado (misma decision, siguiente ola):
- Tabla: `Movimientos`
- Key columns: (`MaquinaId`)
- INCLUDE: (`Consumo`, `Fecha`)

Objetivo complementario:
- Habilitar agregacion por maquina con alta localidad de datos.
- Reducir lecturas logicas para SUM/MAX y minimizar scans completos.

## Justificacion arquitectonica
Esta decision reduce lecturas logicas por tres mecanismos acumulativos:

1. Menos pasadas sobre `Movimientos`:
- Antes: dos agregaciones correlacionadas por fila candidata de `Maquinas`.
- Despues: una agregacion consolidada por `MaquinaId`.

2. Mayor selectividad temprana:
- El indice en (`Estado`, `FechaAlta`) reduce filas candidatas antes de joins/agregaciones.

3. Cobertura de columnas de retorno:
- INCLUDE evita lookups para `Id`, `ProyectoId`, `Nombre` (y en fase complementaria, `Consumo`, `Fecha`).

Resultado esperado frente al baseline del diagnostico:
- Transicion de un comportamiento cercano a $O(N \cdot M)$ hacia un patron set-based con acceso indexado, cercano a $O(N \cdot \log M)$ en busqueda/localizacion.
- Reduccion de Logical Reads >= 90% en `Movimientos` en escenarios representativos.
- Disminucion del tiempo de respuesta desde minutos a objetivo operativo de 10-20 s (segun concurrencia y hardware).

## Consecuencias
### Positivas
- Menor I/O logico y fisico en tablas grandes.
- Menor CPU por eliminar agregaciones redundantes.
- Planes de ejecucion mas estables y predecibles.
- Menor probabilidad de spills y wait states por I/O.

### Costes y trade-offs
- Mayor uso de almacenamiento por nuevos indices.
- Incremento moderado de costo en operaciones DML (INSERT/UPDATE/DELETE).
- Necesidad de mantenimiento de estadisticas e indices para sostener beneficios.

## Plan de despliegue en produccion sin downtime (Sacyr, 50M filas)

### Fase 0 - Preparacion
1. Congelar ventana de cambio con aprobacion CAB y rollback definido.
2. Capturar baseline:
   - Query Store (duracion, CPU, logical reads)
   - `SET STATISTICS IO, TIME ON` en preproduccion con datos realistas
   - Plan de ejecucion real de referencia
3. Verificar edicion/version de SQL Server para capacidades ONLINE/RESUMABLE.

### Fase 1 - Creacion de indices online
1. Crear indice principal de `Maquinas` con:
   - `ONLINE = ON`
   - `SORT_IN_TEMPDB = ON`
   - `MAXDOP` controlado
   - `WAIT_AT_LOW_PRIORITY` para reducir bloqueos a cargas OLTP
2. Crear indice complementario de `Movimientos` tambien online y, si aplica, resumable:
   - `RESUMABLE = ON` para pausar/reanudar sin abortar toda la operacion
3. Monitorear durante la construccion:
   - Bloqueos
   - Crecimiento de log
   - Tempdb
   - Latencia de workload critico

### Fase 2 - Activacion controlada de la consulta refactorizada
1. Desplegar consulta refactorizada detras de feature flag o ruta canary.
2. Ejecutar validacion funcional de resultados (paridad con consulta anterior).
3. Forzar/estabilizar plan solo si hay regresion puntual (plan guide / query store hints segun politica).

### Fase 3 - Verificacion post-deploy
1. Comparar KPI vs baseline en 24-72 horas:
   - Logical Reads
   - Duracion p95/p99
   - CPU total
   - Wait stats
2. Confirmar ausencia de bloqueo severo y spills relevantes.
3. Cerrar cambio y documentar resultados en runbook.

### Fase 4 - Rollback (si aplica)
1. Revertir feature flag a consulta previa.
2. Mantener indices creados si no impactan negativamente, o eliminarlos en ventana controlada.
3. Registrar causa raiz y plan de remediacion.

## Criterios de aceptacion
1. Reduccion de Logical Reads total >= 70% y en `Movimientos` >= 90% respecto a baseline.
2. Duracion de la consulta por debajo de 20 s en percentil p95.
3. Sin bloqueos prolongados durante despliegue de indices online.
4. Sin diferencias funcionales en resultados del reporte.

## Relacion con documentos
- Entrada: DIAGNOSTICO_SQL.md
- Salida esperada siguiente fase: script de implementacion y validacion de performance (fuera del alcance de este ADR)
