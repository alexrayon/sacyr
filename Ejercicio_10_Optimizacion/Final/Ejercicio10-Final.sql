-- 1. Optimización Estructural: Índice Cubriente
CREATE NONCLUSTERED INDEX IX_Movimientos_MaquinaId_Includes 
ON Movimientos (MaquinaId) 
INCLUDE (Consumo, Fecha) 
WITH (ONLINE = ON);

-- 2. Optimización Lógica: Consulta Refactorizada (Fase 4)
SET STATISTICS IO, TIME ON;

WITH ResumenConsumo AS (
    -- Agregamos los 50M de filas primero, de forma eficiente sobre el índice
    SELECT 
        MaquinaId, 
        SUM(Consumo) as TotalConsumo, 
        MAX(Fecha) as UltimoMovimiento
    FROM Movimientos
    GROUP BY MaquinaId
)
SELECT 
    m.Nombre AS Maquina,
    p.Nombre AS Proyecto,
    rc.TotalConsumo,
    rc.UltimoMovimiento
FROM Maquinas m
INNER JOIN Proyectos p ON m.ProyectoId = p.Id
LEFT JOIN ResumenConsumo rc ON rc.MaquinaId = m.Id
WHERE m.Estado = 'Activa' 
  AND m.FechaAlta > '2020-01-01'
ORDER BY rc.TotalConsumo DESC;
