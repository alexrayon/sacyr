using ValidacionCertificaciones.Domain.Enums;
using ValidacionCertificaciones.Domain.Models;
using ValidacionCertificaciones.Infrastructure.Rules;

namespace ValidacionCertificaciones.Tests;

public sealed class ReglaEstadoPartidaTests
{
    private readonly ReglaEstadoPartida _sut = new();

    private static SolicitudCertificacion Crear(EstadoPartida estado) =>
        new(
            IdCertificacion:    "CERT-R3-TEST",
            IdPartida:          "PART-TEST",
            PartidaProyectada:  100_000m,
            AcumuladoHistorico: 90_000m,
            ImporteActual:      5_000m,
            FechaActaReplanteo: new DateOnly(2026, 1, 10),
            FechaTrabajos:      new DateOnly(2026, 2, 15),
            FechaEmision:       new DateOnly(2026, 2, 28),
            EstadoPartida:      estado);

    // ── Estado Activa: operativo ──────────────────────────────────────────────
    [Fact]
    public async Task Evaluar_EstadoActiva_DevuelveCumple()
    {
        var resultado = await _sut.EvaluarAsync(Crear(EstadoPartida.Activa));

        Assert.True(resultado.Cumple);
        Assert.Equal("R3", resultado.CodigoRegla);
    }

    // ── Estado Finalizada: bloqueo administrativo ─────────────────────────────
    [Fact]
    public async Task Evaluar_EstadoFinalizada_DevuelveNoCumple()
    {
        var resultado = await _sut.EvaluarAsync(Crear(EstadoPartida.Finalizada));

        Assert.False(resultado.Cumple);
        Assert.Contains("R3", resultado.Mensaje);
    }

    // ── Estado Liquidada: bloqueo administrativo ──────────────────────────────
    [Fact]
    public async Task Evaluar_EstadoLiquidada_DevuelveNoCumple()
    {
        var resultado = await _sut.EvaluarAsync(Crear(EstadoPartida.Liquidada));

        Assert.False(resultado.Cumple);
        Assert.Contains("R3", resultado.Mensaje);
    }

    // ── Evidencia contiene el estado evaluado ─────────────────────────────────
    [Fact]
    public async Task Evaluar_Resultado_ContieneEstadoEnEvidencia()
    {
        var resultado = await _sut.EvaluarAsync(Crear(EstadoPartida.Finalizada));

        Assert.Contains("EstadoPartida", resultado.Evidencia.Keys);
        Assert.Equal("Finalizada", resultado.Evidencia["EstadoPartida"]);
    }

    // ── Cláusula de guarda ────────────────────────────────────────────────────
    [Fact]
    public async Task Evaluar_SolicitudNula_LanzaArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.EvaluarAsync(null!));
    }
}
