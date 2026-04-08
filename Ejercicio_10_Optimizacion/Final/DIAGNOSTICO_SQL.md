# DIAGNOSTICO SQL - Consulta de Reporte (SQL Server)

## 1) Contexto del problema
La consulta de reporte analiza datos de:
- `Maquinas` (filtradas por `Estado` y `FechaAlta`)
- `Proyectos` (join por `ProyectoId`)
- `Movimientos` (tabla de alto volumen, ~50 millones de filas)

Patron actual observado (segun consulta base):
- Dos subconsultas correlacionadas en el `SELECT`:
  - `TotalConsumo`: `SUM(Consumo)` por maquina
  - `UltimoMovimiento`: `MAX(Fecha)` por maquina

Este enfoque es funcional, pero en escenarios de alto volumen produce degradacion severa de rendimiento por repeticion de trabajo sobre `Movimientos`.

## 2) Analisis de anti-patrones

### 2.1 Subconsultas correlacionadas en `SELECT`
Cada fila de `Maquinas` que sobrevive al `WHERE` dispara dos consultas adicionales sobre `Movimientos`.

Si llamamos:
- $N$ = maquinas activas con `FechaAlta > '2020-01-01'`
- $M$ = filas de `Movimientos` (50,000,000)

Sin un indice util por `MaquinaId`, el costo aproximado es:
- `SUM(Consumo)`: hasta recorrer gran parte de `Movimientos` por cada maquina
- `MAX(Fecha)`: otro recorrido similar por cada maquina

Complejidad efectiva aproximada:
$$
O(N \cdot M) + O(N \cdot M) \approx O(2NM)
$$

En terminos practicos: se multiplica el costo de explorar `Movimientos` por cada maquina candidata.

### 2.2 Repeticion de acceso al mismo conjunto
`TotalConsumo` y `UltimoMovimiento` consultan la misma tabla con el mismo predicado (`MaquinaId = m.Id`), pero como subconsultas independientes fuerzan dos agregaciones separadas.

Consecuencia:
- Doble lectura logica/fisica potencial de `Movimientos`
- Mayor CPU por agregaciones repetidas
- Mayor presion en memoria y tempdb si hay spills

### 2.3 Riesgo de serializacion por costo alto del plan
Cuando el optimizador estima un costo muy elevado por operadores de agregacion y escaneo repetitivo, puede elegir un plan poco favorable (o un paralelismo ineficiente con gran intercambio de filas), agravando esperas por `CXPACKET/CXCONSUMER`, `PAGEIOLATCH` o `SOS_SCHEDULER_YIELD` segun hardware y carga.

## 3) Prediccion del plan de ejecucion no optimizado

Para este patron y sin indices adecuados, el plan esperado suele incluir:

### 3.1 Acceso a `Maquinas`
- `Table Scan` o `Clustered Index Scan` sobre `Maquinas` para evaluar:
  - `Estado = 'Activa'`
  - `FechaAlta > '2020-01-01'`

Si no hay indice sobre `(Estado, FechaAlta)` (o combinacion compatible), el motor no puede hacer `Seek` eficiente y debe escanear.

### 3.2 Join con `Proyectos`
- `Hash Match (Inner Join)` o `Nested Loops` segun cardinalidad estimada.
- Si `Proyectos.Id` es clave y pequena, su costo no suele ser el cuello principal.

### 3.3 Subconsultas correlacionadas sobre `Movimientos`
Operadores probables por cada subconsulta:
- `Nested Loops (Correlated)`
- `Table Scan` o `Index Scan` de `Movimientos` (si no existe indice util por `MaquinaId`)
- `Stream Aggregate`/`Hash Aggregate` para `SUM` y `MAX`

Al ser dos subconsultas, el patron se repite dos veces.

### 3.4 `RID Lookup` / `Key Lookup` potenciales
Si existiera un indice no cluster sobre `Movimientos.MaquinaId` pero no cubriera columnas requeridas (`Consumo`, `Fecha`):
- El plan puede mostrar `RID Lookup` (heap) o `Key Lookup` (clustered)
- Estos lookups se disparan por cada fila localizada en el indice
- En volumen alto, el lookup masivo puede ser tan costoso como un scan completo

### 3.5 Ordenacion final
- `Sort` por `TotalConsumo DESC`.
- Si el conjunto intermedio es grande y no hay memoria concedida suficiente, puede haber spill a tempdb.

## 4) Impacto del filtrado por `Estado` y `FechaAlta` sin indices

### 4.1 Selectividad y costo de lectura
Sin indice alineado al filtro:
- SQL Server lee gran parte (o toda) la tabla `Maquinas`
- El predicado se aplica tarde como filtro residual
- Aumenta la cardinalidad de entrada a las subconsultas

Efecto cascada:
- Mas maquinas candidatas ($N$ mas alto)
- Mas ejecuciones correlacionadas sobre `Movimientos`
- Costo total multiplicado

### 4.2 Degradacion de estimaciones
Si estadisticas de `Estado` y `FechaAlta` no reflejan distribucion real:
- Puede infraestimar o sobreestimar filas
- Selecciona mal estrategias de join/agregacion
- Amplifica riesgos de scans, lookups y spills

### 4.3 Impacto en concurrencia
Un escaneo prolongado en tablas grandes:
- Incrementa tiempo de retencion de recursos
- Aumenta contencion de I/O en buffer pool
- Empeora latencia de otras consultas concurrentes

## 5) Objetivos de optimizacion (metas cuantitativas)

Estas metas son de diagnostico para validar mejora posterior:

1. Reducir complejidad de acceso a `Movimientos` desde esquema correlacionado $O(N \cdot M)$ a acceso indexado cercano a $O(N \cdot \log M)$ para localizacion por maquina.
2. Disminuir lecturas logicas sobre `Movimientos` en al menos 90% respecto al baseline actual.
3. Eliminar scans repetitivos por fila de maquina, consolidando agregacion en una sola pasada por clave.
4. Reducir tiempo total de ejecucion de minutos a rango objetivo menor a 10-20 segundos (dependiendo de hardware, concurrencia y cardinalidad final).
5. Minimizar o eliminar `RID/Key Lookups` masivos mediante estrategias de cobertura de columnas.
6. Reducir spills en `Sort/Aggregate` a 0 en el plan objetivo (o mantenerlos marginales).

## 6) Indicadores tecnicos a medir en la validacion
Para comprobar que el problema diagnostico queda resuelto en la fase de optimizacion:

- `SET STATISTICS IO, TIME ON` (lecturas logicas, CPU, elapsed time)
- Plan de ejecucion real (actual execution plan), revisando:
  - presencia de `Table/Index Scan` en tablas grandes
  - costo acumulado en subconsultas correlacionadas
  - `RID Lookup`/`Key Lookup`
  - spills (warnings de memoria)
- Wait stats durante ejecucion (I/O, CPU, paralelismo)

## 7) Conclusiones del diagnostico
La lentitud no responde a un unico operador, sino a un anti-patron estructural: subconsultas correlacionadas sobre una tabla masiva, ejecutadas por cada fila candidata de `Maquinas` y duplicadas para dos metricas. Sin indices adecuados para filtros y correlacion, el plan esperado tendera a scans y posibles lookups costosos, escalando de forma deficiente con el volumen.

En la siguiente fase, la optimizacion debera orientarse a:
- reducir trabajo repetido,
- convertir accesos de escaneo a busqueda,
- y estabilizar cardinalidades/estimaciones para un plan consistente.
