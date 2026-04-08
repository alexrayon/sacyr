using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValidacionCertificaciones.Application.Services;
using ValidacionCertificaciones.Domain.Enums;
using ValidacionCertificaciones.Domain.Interfaces;
using ValidacionCertificaciones.Domain.Models;
using ValidacionCertificaciones.Infrastructure.Providers;

// ── Composición raíz con DI ──────────────────────────────────────────────────
var services = new ServiceCollection()
    .AddLogging(b => b
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information))
    .AddSingleton<IProveedorRuleSet, ProveedorRuleSetDefault>()
    .AddSingleton<MotorAuditoriaCertificaciones>()
    .BuildServiceProvider();

var motor = services.GetRequiredService<MotorAuditoriaCertificaciones>();

// ── Solicitudes de demostración ───────────────────────────────────────────────
var solicitudes = new[]
{
    // Escenario 1: Validación exitosa (Gherkin)
    ("CERT-2026-001", new SolicitudCertificacion(
        IdCertificacion:     "CERT-2026-001",
        IdPartida:           "PART-A1",
        PartidaProyectada:   100_000.00m,
        AcumuladoHistorico:  95_000.00m,
        ImporteActual:       8_000.00m,
        FechaActaReplanteo:  new DateOnly(2026, 1, 10),
        FechaTrabajos:       new DateOnly(2026, 2, 15),
        FechaEmision:        new DateOnly(2026, 2, 28),
        EstadoPartida:       EstadoPartida.Activa)),

    // Escenario 2: Rechazo por exceso presupuestario (Gherkin)
    ("CERT-2026-002", new SolicitudCertificacion(
        IdCertificacion:     "CERT-2026-002",
        IdPartida:           "PART-A1",
        PartidaProyectada:   100_000.00m,
        AcumuladoHistorico:  102_000.00m,
        ImporteActual:       4_000.00m,
        FechaActaReplanteo:  new DateOnly(2026, 1, 10),
        FechaTrabajos:       new DateOnly(2026, 2, 15),
        FechaEmision:        new DateOnly(2026, 2, 28),
        EstadoPartida:       EstadoPartida.Activa)),

    // Escenario 3: Rechazo por incoherencia de fechas (Gherkin)
    ("CERT-2026-003", new SolicitudCertificacion(
        IdCertificacion:     "CERT-2026-003",
        IdPartida:           "PART-A1",
        PartidaProyectada:   100_000.00m,
        AcumuladoHistorico:  90_000.00m,
        ImporteActual:       5_000.00m,
        FechaActaReplanteo:  new DateOnly(2026, 3,  1),
        FechaTrabajos:       new DateOnly(2026, 2, 15),
        FechaEmision:        new DateOnly(2026, 3, 20),
        EstadoPartida:       EstadoPartida.Activa)),
};

foreach (var (etiqueta, solicitud) in solicitudes)
{
    Console.WriteLine(new string('─', 60));
    Console.WriteLine($"  CERTIFICACIÓN: {etiqueta}");
    Console.WriteLine(new string('─', 60));

    var resultado = await motor.ValidarAsync(solicitud);

    var color = resultado.Apta ? ConsoleColor.Green : ConsoleColor.Red;
    Console.ForegroundColor = color;
    Console.WriteLine($"  DICTAMEN: {(resultado.Apta ? "✓ APTA" : "✗ RECHAZADA")}");
    Console.ResetColor();

    Console.WriteLine($"  ID Ejecución  : {resultado.IdEjecucion}");
    Console.WriteLine($"  Evaluado UTC  : {resultado.FechaEvaluacionUtc:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"  Versión Reglas: {resultado.VersionReglas}");
    Console.WriteLine();

    foreach (var regla in resultado.ResultadosReglas)
    {
        var icono = regla.Cumple ? "  ✓" : "  ✗";
        Console.WriteLine($"{icono} [{regla.CodigoRegla}] {regla.Mensaje}");
    }

    if (!resultado.Apta)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  Motivos de rechazo:");
        Console.ResetColor();
        foreach (var error in resultado.Errores)
            Console.WriteLine($"    · [{error.CodigoRegla}] {error.Mensaje}");
    }

    Console.WriteLine();
}

