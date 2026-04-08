using Microsoft.Extensions.Logging.Abstractions;
using ValidacionCertificaciones.Application.Services;
using ValidacionCertificaciones.Domain.Enums;
using ValidacionCertificaciones.Domain.Models;
using ValidacionCertificaciones.Infrastructure.Providers;

namespace ValidacionCertificaciones.Tests;

/// <summary>
/// Tests de integración del MotorAuditoriaCertificaciones.
/// Traduce punto a punto los escenarios Gherkin de VALIDACION_FUNCIONAL.md
/// y todos los edge cases definidos en la sección 11.
/// </summary>
public sealed class MotorAuditoriaTests
{
    // Helper: motor real con ProveedorRuleSetDefault y logger nulo (sin I/O en tests)
    private static MotorAuditoriaCertificaciones CrearMotor() =>
        new(new ProveedorRuleSetDefault(), NullLogger<MotorAuditoriaCertificaciones>.Instance);

    // ═══════════════════════════════════════════════════════════════════════════
    // ESCENARIOS GHERKIN
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gherkin Escenario 1 — Validación exitosa.
    /// PartidaProyectada=100000 | Acumulado=95000 | Importe=8000
    /// Acta=2026-01-10 | Trabajos=2026-02-15 | Emision=2026-02-28
    /// Estado=Activa  →  Apta, 3 reglas Cumple, 0 errores
    /// </summary>
    [Fact]
    public async Task Validar_GherkinEscenario1_ValidacionExitosa_DevuelveApta()
    {
        var motor     = CrearMotor();
        var solicitud = new SolicitudCertificacion(
            IdCertificacion:    "CERT-2026-001",
            IdPartida:          "PART-A1",
            PartidaProyectada:  100_000.00m,
            AcumuladoHistorico: 95_000.00m,
            ImporteActual:      8_000.00m,
            FechaActaReplanteo: new DateOnly(2026, 1, 10),
            FechaTrabajos:      new DateOnly(2026, 2, 15),
            FechaEmision:       new DateOnly(2026, 2, 28),
            EstadoPartida:      EstadoPartida.Activa);

        var resultado = await motor.ValidarAsync(solicitud);

        Assert.True(resultado.Apta);
        Assert.Empty(resultado.Errores);
        Assert.Equal(3, resultado.ResultadosReglas.Count);
        Assert.All(resultado.ResultadosReglas, r => Assert.True(r.Cumple));
        Assert.NotEmpty(resultado.IdEjecucion);
        Assert.NotEmpty(resultado.VersionReglas);
    }

    /// <summary>
    /// Gherkin Escenario 2 — Rechazo por exceso de presupuesto.
    /// Acumulado=102000 + Importe=4000 = 106000 > 105000  →  fallo R1
    /// </summary>
    [Fact]
    public async Task Validar_GherkinEscenario2_ExcesoPresupuesto_DevuelveRechazadaConErrorR1()
    {
        var motor     = CrearMotor();
        var solicitud = new SolicitudCertificacion(
            IdCertificacion:    "CERT-2026-002",
            IdPartida:          "PART-A1",
            PartidaProyectada:  100_000.00m,
            AcumuladoHistorico: 102_000.00m,
            ImporteActual:      4_000.00m,
            FechaActaReplanteo: new DateOnly(2026, 1, 10),
            FechaTrabajos:      new DateOnly(2026, 2, 15),
            FechaEmision:       new DateOnly(2026, 2, 28),
            EstadoPartida:      EstadoPartida.Activa);

        var resultado = await motor.ValidarAsync(solicitud);

        Assert.False(resultado.Apta);
        Assert.Contains(resultado.Errores,        e => e.CodigoRegla == "R1");
        Assert.Contains(resultado.ResultadosReglas, r => r.CodigoRegla == "R1" && !r.Cumple);
        // R2 y R3 deben haberse evaluado igualmente (motor completo, sin corte)
        Assert.Equal(3, resultado.ResultadosReglas.Count);
    }

    /// <summary>
    /// Gherkin Escenario 3 — Rechazo por incoherencia de fechas.
    /// Acta=2026-03-01, Trabajos=2026-02-15 (antes del acta)  →  fallo R2
    /// </summary>
    [Fact]
    public async Task Validar_GherkinEscenario3_IncoherenciaFechas_DevuelveRechazadaConErrorR2()
    {
        var motor     = CrearMotor();
        var solicitud = new SolicitudCertificacion(
            IdCertificacion:    "CERT-2026-003",
            IdPartida:          "PART-A1",
            PartidaProyectada:  100_000.00m,
            AcumuladoHistorico: 90_000.00m,
            ImporteActual:      5_000.00m,
            FechaActaReplanteo: new DateOnly(2026, 3,  1),
            FechaTrabajos:      new DateOnly(2026, 2, 15),
            FechaEmision:       new DateOnly(2026, 3, 20),
            EstadoPartida:      EstadoPartida.Activa);

        var resultado = await motor.ValidarAsync(solicitud);

        Assert.False(resultado.Apta);
        Assert.Contains(resultado.Errores, e => e.CodigoRegla == "R2");
        Assert.Equal(3, resultado.ResultadosReglas.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EDGE CASES — Sección 11 VALIDACION_FUNCIONAL.md
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Edge case 11.1 — Importe cero, partida dentro del margen.
    /// 95000 + 0 = 95000 ≤ 105000  →  Apta
    /// </summary>
    [Fact]
    public async Task Validar_ImporteCero_DentroDelMargen_DevuelveApta()
    {
        var motor     = CrearMotor();
        var solicitud = new SolicitudCertificacion(
            IdCertificacion:    "CERT-2026-004",
            IdPartida:          "PART-A1",
            PartidaProyectada:  100_000.00m,
            AcumuladoHistorico: 95_000.00m,
            ImporteActual:      0m,
            FechaActaReplanteo: new DateOnly(2026, 1, 10),
            FechaTrabajos:      new DateOnly(2026, 2, 15),
            FechaEmision:       new DateOnly(2026, 2, 28),
            EstadoPartida:      EstadoPartida.Activa);

        var resultado = await motor.ValidarAsync(solicitud);

        Assert.True(resultado.Apta);
    }

    /// <summary>
    /// Edge case 11.1 — Importe cero, acumulado ya excedido.
    /// 106000 + 0 = 106000 > 105000  →  Rechazada por R1
    /// </summary>
    [Fact]
    public async Task Validar_ImporteCero_AcumuladoExcedido_DevuelveRechazadaPorR1()
    {
        var motor     = CrearMotor();
        var solicitud = new SolicitudCertificacion(
            IdCertificacion:    "CERT-2026-005",
            IdPartida:          "PART-A1",
            PartidaProyectada:  100_000.00m,
            AcumuladoHistorico: 106_000.00m,
            ImporteActual:      0m,
            FechaActaReplanteo: new DateOnly(2026, 1, 10),
            FechaTrabajos:      new DateOnly(2026, 2, 15),
            FechaEmision:       new DateOnly(2026, 2, 28),
            EstadoPartida:      EstadoPartida.Activa);

        var resultado = await motor.ValidarAsync(solicitud);

        Assert.False(resultado.Apta);
        Assert.Contains(resultado.Errores, e => e.CodigoRegla == "R1");
    }

    /// <summary>
    /// Edge case 11.2 — Exactamente en el límite del 105 % (inclusivo).
    /// 100000 + 5000 = 105000 == 100000 * 1.05  →  Apta (sin margen de error decimal)
    /// </summary>
    [Fact]
    public async Task Validar_TotalExactamenteEnLimite_DevuelveApta()
    {
        var motor     = CrearMotor();
        var solicitud = new SolicitudCertificacion(
            IdCertificacion:    "CERT-2026-006",
            IdPartida:          "PART-A1",
            PartidaProyectada:  100_000.00m,
            AcumuladoHistorico: 100_000.00m,
            ImporteActual:      5_000.00m,
            FechaActaReplanteo: new DateOnly(2026, 1, 10),
            FechaTrabajos:      new DateOnly(2026, 2, 15),
            FechaEmision:       new DateOnly(2026, 2, 28),
            EstadoPartida:      EstadoPartida.Activa);

        var resultado = await motor.ValidarAsync(solicitud);

        Assert.True(resultado.Apta);
        Assert.Contains(resultado.ResultadosReglas, r => r.CodigoRegla == "R1" && r.Cumple);
    }

    /// <summary>
    /// Edge case 11.2 — Un céntimo por encima del límite (decimal exacto).
    /// 100000 + 5000.01 = 105000.01 > 105000  →  Rechazada (sin tolerancias)
    /// </summary>
    [Fact]
    public async Task Validar_UnCentimoSobreElLimite_DevuelveRechazada()
    {
        var motor     = CrearMotor();
        var solicitud = new SolicitudCertificacion(
            IdCertificacion:    "CERT-2026-007",
            IdPartida:          "PART-A1",
            PartidaProyectada:  100_000.00m,
            AcumuladoHistorico: 100_000.00m,
            ImporteActual:      5_000.01m,
            FechaActaReplanteo: new DateOnly(2026, 1, 10),
            FechaTrabajos:      new DateOnly(2026, 2, 15),
            FechaEmision:       new DateOnly(2026, 2, 28),
            EstadoPartida:      EstadoPartida.Activa);

        var resultado = await motor.ValidarAsync(solicitud);

        Assert.False(resultado.Apta);
        Assert.Contains(resultado.Errores, e => e.CodigoRegla == "R1");
    }

    /// <summary>
    /// Partida Finalizada bloquea incluso cuando R1 y R2 son válidas.
    /// </summary>
    [Fact]
    public async Task Validar_PartidaFinalizada_DevuelveRechazadaPorR3()
    {
        var motor     = CrearMotor();
        var solicitud = new SolicitudCertificacion(
            IdCertificacion:    "CERT-2026-008",
            IdPartida:          "PART-A1",
            PartidaProyectada:  100_000.00m,
            AcumuladoHistorico: 90_000.00m,
            ImporteActual:      5_000.00m,
            FechaActaReplanteo: new DateOnly(2026, 1, 10),
            FechaTrabajos:      new DateOnly(2026, 2, 15),
            FechaEmision:       new DateOnly(2026, 2, 28),
            EstadoPartida:      EstadoPartida.Finalizada);

        var resultado = await motor.ValidarAsync(solicitud);

        Assert.False(resultado.Apta);
        Assert.Contains(resultado.Errores, e => e.CodigoRegla == "R3");
        Assert.DoesNotContain(resultado.Errores, e => e.CodigoRegla == "R1");
        Assert.DoesNotContain(resultado.Errores, e => e.CodigoRegla == "R2");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // VALIDACIÓN DE ENTRADA
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// IdCertificacion vacío impide ejecutar reglas de negocio.
    /// </summary>
    [Fact]
    public async Task Validar_IdCertificacionVacio_RechazaSinEjecutarReglas()
    {
        var motor     = CrearMotor();
        var solicitud = new SolicitudCertificacion(
            IdCertificacion:    "",           // inválido
            IdPartida:          "PART-A1",
            PartidaProyectada:  100_000.00m,
            AcumuladoHistorico: 90_000.00m,
            ImporteActual:      5_000.00m,
            FechaActaReplanteo: new DateOnly(2026, 1, 10),
            FechaTrabajos:      new DateOnly(2026, 2, 15),
            FechaEmision:       new DateOnly(2026, 2, 28),
            EstadoPartida:      EstadoPartida.Activa);

        var resultado = await motor.ValidarAsync(solicitud);

        Assert.False(resultado.Apta);
        Assert.Empty(resultado.ResultadosReglas);                          // reglas no ejecutadas
        Assert.Contains(resultado.Errores, e => e.CodigoRegla == "ENTRADA");
    }

    /// <summary>
    /// PartidaProyectada cero o negativa impide la validación.
    /// </summary>
    [Fact]
    public async Task Validar_PartidaProyectadaCero_RechazaSinEjecutarReglas()
    {
        var motor     = CrearMotor();
        var solicitud = new SolicitudCertificacion(
            IdCertificacion:    "CERT-X",
            IdPartida:          "PART-X",
            PartidaProyectada:  0m,           // inválido
            AcumuladoHistorico: 0m,
            ImporteActual:      0m,
            FechaActaReplanteo: new DateOnly(2026, 1, 10),
            FechaTrabajos:      new DateOnly(2026, 2, 15),
            FechaEmision:       new DateOnly(2026, 2, 28),
            EstadoPartida:      EstadoPartida.Activa);

        var resultado = await motor.ValidarAsync(solicitud);

        Assert.False(resultado.Apta);
        Assert.Empty(resultado.ResultadosReglas);
        Assert.Contains(resultado.Errores, e => e.CodigoRegla == "ENTRADA");
    }

    /// <summary>
    /// Múltiples errores de entrada se acumulan todos en la lista.
    /// </summary>
    [Fact]
    public async Task Validar_MultiplesErroresDeEntrada_DevuelveTodosLosErrores()
    {
        var motor     = CrearMotor();
        var solicitud = new SolicitudCertificacion(
            IdCertificacion:    "",            // inválido
            IdPartida:          "",            // inválido
            PartidaProyectada:  -1m,           // inválido
            AcumuladoHistorico: 0m,
            ImporteActual:      0m,
            FechaActaReplanteo: DateOnly.MinValue,  // inválido
            FechaTrabajos:      DateOnly.MinValue,  // inválido
            FechaEmision:       DateOnly.MinValue,  // inválido
            EstadoPartida:      EstadoPartida.Activa);

        var resultado = await motor.ValidarAsync(solicitud);

        Assert.False(resultado.Apta);
        Assert.True(resultado.Errores.Count >= 4);
        Assert.All(resultado.Errores, e => Assert.Equal("ENTRADA", e.CodigoRegla));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TRAZABILIDAD
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dos evaluaciones de la misma solicitud generan IdEjecucion distintos
    /// pero producen el mismo dictamen (determinismo).
    /// </summary>
    [Fact]
    public async Task Validar_DosLlamadas_IdEjecucionUnicoYResultadoDeterministico()
    {
        var motor     = CrearMotor();
        var solicitud = new SolicitudCertificacion(
            IdCertificacion:    "CERT-DET",
            IdPartida:          "PART-DET",
            PartidaProyectada:  100_000m,
            AcumuladoHistorico: 50_000m,
            ImporteActual:      1_000m,
            FechaActaReplanteo: new DateOnly(2026, 1, 1),
            FechaTrabajos:      new DateOnly(2026, 2, 1),
            FechaEmision:       new DateOnly(2026, 3, 1),
            EstadoPartida:      EstadoPartida.Activa);

        var r1 = await motor.ValidarAsync(solicitud);
        var r2 = await motor.ValidarAsync(solicitud);

        Assert.NotEqual(r1.IdEjecucion, r2.IdEjecucion);   // IDs distintos
        Assert.Equal(r1.Apta, r2.Apta);                     // mismo resultado

        // FechaEvaluacionUtc siempre en UTC
        Assert.Equal(TimeSpan.Zero, r1.FechaEvaluacionUtc.Offset);
        Assert.Equal(TimeSpan.Zero, r2.FechaEvaluacionUtc.Offset);
    }
}
