using ValidacionCertificaciones.Domain.Enums;
using ValidacionCertificaciones.Domain.Models;
using ValidacionCertificaciones.Infrastructure.Rules;

namespace ValidacionCertificaciones.Tests;

public sealed class ReglaTemporalidadTests
{
    private readonly ReglaTemporalidad _sut = new();

    private static SolicitudCertificacion Crear(
        DateOnly fechaActa, DateOnly fechaTrabajos, DateOnly fechaEmision) =>
        new(
            IdCertificacion:    "CERT-R2-TEST",
            IdPartida:          "PART-TEST",
            PartidaProyectada:  100_000m,
            AcumuladoHistorico: 90_000m,
            ImporteActual:      5_000m,
            FechaActaReplanteo: fechaActa,
            FechaTrabajos:      fechaTrabajos,
            FechaEmision:       fechaEmision,
            EstadoPartida:      EstadoPartida.Activa);

    // ── Escenario Gherkin 1: fecha en rango válido ────────────────────────────
    [Fact]
    public async Task Evaluar_FechaEnRangoValido_DevuelveCumple()
    {
        // acta=2026-01-10 < trabajos=2026-02-15 < emision=2026-02-28
        var resultado = await _sut.EvaluarAsync(
            Crear(new(2026, 1, 10), new(2026, 2, 15), new(2026, 2, 28)));

        Assert.True(resultado.Cumple);
        Assert.Equal("R2", resultado.CodigoRegla);
    }

    // ── Escenario Gherkin 3: trabajos anteriores al acta ─────────────────────
    [Fact]
    public async Task Evaluar_FechaTrabajosAnteriorAlActa_DevuelveNoCumple()
    {
        // acta=2026-03-01, trabajos=2026-02-15 → trabajos NO posterior al acta
        var resultado = await _sut.EvaluarAsync(
            Crear(new(2026, 3, 1), new(2026, 2, 15), new(2026, 3, 20)));

        Assert.False(resultado.Cumple);
        Assert.Contains("R2", resultado.Mensaje);
    }

    // ── Comparación estricta: FechaTrabajos == FechaActa rechaza ─────────────
    [Fact]
    public async Task Evaluar_FechaTrabajosIgualAlActa_DevuelveNoCumple()
    {
        var mismaFecha = new DateOnly(2026, 1, 10);
        var resultado  = await _sut.EvaluarAsync(
            Crear(mismaFecha, mismaFecha, new(2026, 2, 28)));

        Assert.False(resultado.Cumple);
    }

    // ── Comparación estricta: FechaTrabajos == FechaEmision rechaza ───────────
    [Fact]
    public async Task Evaluar_FechaTrabajosIgualAEmision_DevuelveNoCumple()
    {
        var emision   = new DateOnly(2026, 2, 28);
        var resultado = await _sut.EvaluarAsync(
            Crear(new(2026, 1, 10), emision, emision));

        Assert.False(resultado.Cumple);
    }

    // ── FechaTrabajos posterior a la emisión ──────────────────────────────────
    [Fact]
    public async Task Evaluar_FechaTrabajosPosteriorrAEmision_DevuelveNoCumple()
    {
        var resultado = await _sut.EvaluarAsync(
            Crear(new(2026, 1, 10), new(2026, 3, 5), new(2026, 2, 28)));

        Assert.False(resultado.Cumple);
    }

    // ── Evidencia contiene la terna de fechas ─────────────────────────────────
    [Fact]
    public async Task Evaluar_Resultado_ContieneTernaDeFechasEnEvidencia()
    {
        var resultado = await _sut.EvaluarAsync(
            Crear(new(2026, 1, 10), new(2026, 2, 15), new(2026, 2, 28)));

        Assert.Contains("FechaActaReplanteo", resultado.Evidencia.Keys);
        Assert.Contains("FechaTrabajos",      resultado.Evidencia.Keys);
        Assert.Contains("FechaEmision",       resultado.Evidencia.Keys);
    }

    // ── Cláusula de guarda ────────────────────────────────────────────────────
    [Fact]
    public async Task Evaluar_SolicitudNula_LanzaArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.EvaluarAsync(null!));
    }
}
