-- Consulta de Reporte de Consumo: Lenta y costosa
SELECT 
    m.Nombre AS Maquina,
    p.Nombre AS Proyecto,
    (SELECT SUM(Consumo) FROM Movimientos WHERE MaquinaId = m.Id) AS TotalConsumo,
    (SELECT MAX(Fecha) FROM Movimientos WHERE MaquinaId = m.Id) AS UltimoMovimiento
FROM Maquinas m
JOIN Proyectos p ON m.ProyectoId = p.Id
WHERE m.Estado = 'Activa' 
  AND m.FechaAlta > '2020-01-01'
ORDER BY TotalConsumo DESC;