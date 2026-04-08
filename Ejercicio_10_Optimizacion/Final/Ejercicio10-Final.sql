/*
  Ejercicio 10 - Optimizacion SQL Server (implementacion ADR-010)
  Objetivo:
  1) Reducir lecturas logicas sobre Movimientos con indices de cobertura.
  2) Eliminar subconsultas correlacionadas y consolidar agregaciones con CTE.
*/

SET NOCOUNT ON;
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

/*
  Parametros de reporte.
  Ajustar en ejecucion segun ventana de negocio.
*/
DECLARE @EstadoObjetivo NVARCHAR(20) = N'Activa';
DECLARE @FechaAltaMinima DATE = '20200101';

/*
  1) INDEXING - Movimientos
  Se crean dos indices complementarios:
  - Cobertura para agregacion por MaquinaId (SUM/MAX sin lookups).
  - Soporte opcional para busquedas por ultimo movimiento por maquina.
*/

IF NOT EXISTS (
	SELECT 1
	FROM sys.indexes
	WHERE object_id = OBJECT_ID(N'dbo.Movimientos')
	  AND name = N'IX_Movimientos_MaquinaId_Covering'
)
BEGIN
	CREATE NONCLUSTERED INDEX IX_Movimientos_MaquinaId_Covering
		ON dbo.Movimientos (MaquinaId)
		INCLUDE (Consumo, Fecha)
		WITH (
			ONLINE = ON,
			SORT_IN_TEMPDB = ON,
			DATA_COMPRESSION = PAGE
		);
END;

IF NOT EXISTS (
	SELECT 1
	FROM sys.indexes
	WHERE object_id = OBJECT_ID(N'dbo.Movimientos')
	  AND name = N'IX_Movimientos_MaquinaId_FechaDesc'
)
BEGIN
	CREATE NONCLUSTERED INDEX IX_Movimientos_MaquinaId_FechaDesc
		ON dbo.Movimientos (MaquinaId, Fecha DESC)
		INCLUDE (Consumo)
		WITH (
			ONLINE = ON,
			SORT_IN_TEMPDB = ON,
			DATA_COMPRESSION = PAGE
		);
END;

/*
  2) QUERY REWRITE - Reporte optimizado
  Principios:
  - Filtrar Maquinas primero para reducir cardinalidad temprana.
  - Agregar Movimientos una sola vez por MaquinaId.
  - Evitar calculos en predicates de JOIN.
*/
WITH MaquinasFiltradas AS (
	SELECT
		m.Id,
		m.Nombre,
		m.ProyectoId
	FROM dbo.Maquinas AS m
	WHERE m.Estado = @EstadoObjetivo
	  AND m.FechaAlta > @FechaAltaMinima
),
MovimientosAgregados AS (
	SELECT
		mv.MaquinaId,
		SUM(mv.Consumo) AS TotalConsumo,
		MAX(mv.Fecha) AS UltimoMovimiento
	FROM dbo.Movimientos AS mv
	INNER JOIN MaquinasFiltradas AS mf
		ON mf.Id = mv.MaquinaId
	GROUP BY
		mv.MaquinaId
)
SELECT
	mf.Nombre AS Maquina,
	p.Nombre AS Proyecto,
	ma.TotalConsumo,
	ma.UltimoMovimiento
FROM MaquinasFiltradas AS mf
INNER JOIN dbo.Proyectos AS p
	ON p.Id = mf.ProyectoId
LEFT JOIN MovimientosAgregados AS ma
	ON ma.MaquinaId = mf.Id
ORDER BY
	ma.TotalConsumo DESC,
	mf.Id ASC;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
