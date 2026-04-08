using ValidacionCertificaciones.Domain.Enums;
using ValidacionCertificaciones.Domain.Models;
using ValidacionCertificaciones.Infrastructure.Rules;

namespace ValidacionCertificaciones.Tests;

public sealed class ReglaLimitePresupuestarioTests
{
    private readonly ReglaLimitePresupuestario _sut = new();

    // Fechas base válidas para todos los tests de esta clase (R2 y R3 no se evalúan aquí)
    private static readonly DateOnly Acta    = new(2026, 1, 10);
    private static readonly DateOnly Trabajo = new(2026, 2, 15);
    private static readonly DateOnly Emision = new(2026, 2, 28);

    private static SolicitudCertificacion Crear(
        decimal proyectada, decimal acumulado, decimal importeActual) =>
        new(
            IdCertificacion:    "CERT-R1-TEST",
            IdPartida:          "PART-TEST",
            PartidaProyectada:  proyectada,
            AcumuladoHistorico: acumulado,
            ImporteActual:      importeActual,
            FechaActaReplanteo: Acta,
            FechaTrabajos:      Trabajo,
            FechaEmision:       Emision,
            EstadoPartida:      EstadoPartida.Activa);

    // ── Escenario Gherkin 1: dentro del margen ────────────────────────────────
    [Fact]
    public async Task Evaluar_TotalDentroDelMargen_DevuelveCumple()
    {
        // 95000 + 8000 = 103000 ≤ 100000 * 1.05 = 105000
        var resultado = await _sut.EvaluarAsync(Crear(100_000m, 95_000m, 8_000m));

        Assert.True(resultado.Cumple);
        Assert.Equal("R1", resultado.CodigoRegla);
    }

    // ── Escenario Gherkin 2: exceso presupuestario ────────────────────────────
    [Fact]
    public async Task Evaluar_ExcesoPresupuestario_DevuelveNoCumple()
    {
        // 102000 + 4000 = 106000 > 105000
        var resultado = await _sut.EvaluarAsync(Crear(100_000m, 102_000m, 4_000m));

        Assert.False(resultado.Cumple);
        Assert.Contains("R1", resultado.Mensaje);
    }

    // ── Edge case: exactamente en el límite del 105 % (inclusivo) ────────────
    [Fact]
    public async Task Evaluar_TotalExactamenteEnElLimite_DevuelveCumple()
    {
        // 100000 + 5000 = 105000 == 100000 * 1.05  →  Apta (límite inclusivo)
        var resultado = await _sut.EvaluarAsync(Crear(100_000m, 100_000m, 5_000m));

        Assert.True(resultado.Cumple);
    }

    // ── Edge case: un céntimo por encima del límite ────────────────────────────
    [Fact]
    public async Task Evaluar_UnCentimoSobreElLimite_DevuelveNoCumple()
    {
        // 100000 + 5000.01 = 105000.01 > 105000  →  Rechazada
        var resultado = await _sut.EvaluarAsync(Crear(100_000m, 100_000m, 5_000.01m));

        Assert.False(resultado.Cumple);
    }

    // ── Edge case: importe cero, acumulado dentro del margen ─────────────────
    [Fact]
    public async Task Evaluar_ImporteCero_AcumuladoDentroDelMargen_DevuelveCumple()
    {
        // 95000 + 0 = 95000 ≤ 105000
        var resultado = await _sut.EvaluarAsync(Crear(100_000m, 95_000m, 0m));

        Assert.True(resultado.Cumple);
    }

    // ── Edge case: importe cero, pero acumulado ya excedido ──────────────────
    [Fact]
    public async Task Evaluar_ImporteCero_AcumuladoExcedido_DevuelveNoCumple()
    {
        // 106000 + 0 = 106000 > 105000
        var resultado = await _sut.EvaluarAsync(Crear(100_000m, 106_000m, 0m));

        Assert.False(resultado.Cumple);
    }

    // ── Evidencia completa para auditoría ─────────────────────────────────────
    [Fact]
    public async Task Evaluar_Resultado_ContieneEvidenciaClave()
    {
        var resultado = await _sut.EvaluarAsync(Crear(100_000m, 95_000m, 5_000m));

        Assert.Contains("TotalCertificable", resultado.Evidencia.Keys);
        Assert.Contains("TechoPermitido",    resultado.Evidencia.Keys);
        Assert.Contains("MargenAplicado",    resultado.Evidencia.Keys);
        Assert.Equal("5%", resultado.Evidencia["MargenAplicado"]);
    }

    // ── Cláusula de guarda ────────────────────────────────────────────────────
    [Fact]
    public async Task Evaluar_SolicitudNula_LanzaArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.EvaluarAsync(null!));
    }
}
