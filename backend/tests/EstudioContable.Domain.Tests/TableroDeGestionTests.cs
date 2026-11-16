using EstudioContable.Domain;

namespace EstudioContable.Domain.Tests;

public class TableroDeGestionTests
{
    [Theory]
    [InlineData(100, 100, SentidoDelIndicador.MasEsMejor, "verde")]
    [InlineData(90, 100, SentidoDelIndicador.MasEsMejor, "amarillo")]
    [InlineData(89.99, 100, SentidoDelIndicador.MasEsMejor, "rojo")]
    [InlineData(100, 100, SentidoDelIndicador.MenosEsMejor, "verde")]
    [InlineData(110, 100, SentidoDelIndicador.MenosEsMejor, "amarillo")]
    [InlineData(110.01, 100, SentidoDelIndicador.MenosEsMejor, "rojo")]
    [InlineData(0, 0, SentidoDelIndicador.MenosEsMejor, "verde")]
    [InlineData(0, 0, SentidoDelIndicador.MasEsMejor, "verde")]
    [InlineData(1, 0, SentidoDelIndicador.MenosEsMejor, "rojo")]
    public void El_semaforo_compara_el_valor_con_la_meta(double valor, double meta, SentidoDelIndicador sentido, string esperado)
        => Assert.Equal(esperado, TableroDeGestion.Semaforo((decimal)valor, (decimal)meta, sentido));

    [Fact]
    public void Sin_meta_el_semaforo_queda_gris_y_sin_valor_dice_sin_datos()
    {
        Assert.Equal("sin-meta", TableroDeGestion.Semaforo(5, null, SentidoDelIndicador.MasEsMejor));
        Assert.Equal("sin-datos", TableroDeGestion.Semaforo(null, 100, SentidoDelIndicador.MasEsMejor));
    }

    [Fact]
    public void La_variacion_es_en_porcentaje_o_en_puntos_si_el_indicador_ya_es_un_porcentaje()
    {
        Assert.Equal(20m, TableroDeGestion.Variacion(120, 100, esPorcentaje: false));
        Assert.Equal(-25m, TableroDeGestion.Variacion(75, 100, esPorcentaje: false));
        Assert.Equal(5m, TableroDeGestion.Variacion(30, 25, esPorcentaje: true));
        Assert.Null(TableroDeGestion.Variacion(5, 0, esPorcentaje: false));
        Assert.Null(TableroDeGestion.Variacion(null, 10, esPorcentaje: false));
        Assert.Null(TableroDeGestion.Variacion(10, null, esPorcentaje: true));
    }

    [Fact]
    public void El_catalogo_tiene_quince_indicadores_en_cuatro_perspectivas()
    {
        Assert.Equal(15, TableroDeGestion.Indicadores.Count);
        Assert.Equal(15, TableroDeGestion.Indicadores.Select(i => i.Clave).Distinct().Count());
        Assert.Equal(new[] { "Financiera", "Socios", "Procesos", "Personas" }, TableroDeGestion.Indicadores.Select(i => i.Perspectiva).Distinct());
        Assert.True(TableroDeGestion.Buscar("retencion")!.EsPorcentaje);
        Assert.Equal(SentidoDelIndicador.MenosEsMejor, TableroDeGestion.Buscar("costo-sobre-cuotas")!.Sentido);
        Assert.False(TableroDeGestion.Buscar("liquidez")!.AdmiteMeta);
        Assert.Null(TableroDeGestion.Buscar("nada"));
    }
}
