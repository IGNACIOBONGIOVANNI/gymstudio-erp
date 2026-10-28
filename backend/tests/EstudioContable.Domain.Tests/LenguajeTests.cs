using EstudioContable.Domain.Asistente;

namespace EstudioContable.Domain.Tests;

public class LenguajeTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 28);

    [Theory]
    [InlineData("¿Cuánto VENDÍ este mes?", "cuanto vendi este mes")]
    [InlineData("  ¡Hola!!!   ¿Qué tal?  ", "hola que tal")]
    [InlineData("Cuántas CUOTAS vencen (esta semana)", "cuantas cuotas vencen esta semana")]
    [InlineData("niño/año — pingüino", "nino ano pinguino")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("?!¿¡", "")]
    public void Normalizar_quita_tildes_mayusculas_y_signos(string entrada, string esperado)
        => Assert.Equal(esperado, Lenguaje.Normalizar(entrada));

    [Fact]
    public void Normalizar_acepta_null()
        => Assert.Equal(string.Empty, Lenguaje.Normalizar(null));

    [Fact]
    public void Tiene_busca_por_raiz_al_comienzo_de_una_palabra()
    {
        var q = Lenguaje.Normalizar("¿Cuánto vendimos y facturamos?");
        Assert.True(Lenguaje.Tiene(q, "vend"));
        Assert.True(Lenguaje.Tiene(q, "factur"));
        Assert.False(Lenguaje.Tiene(q, "compr"));
        Assert.False(Lenguaje.Tiene(q, "endimos"));      // no busca en el medio de la palabra
    }

    [Fact]
    public void TieneFrase_empieza_en_el_comienzo_de_una_palabra()
    {
        var q = Lenguaje.Normalizar("el producto más vendido");
        Assert.True(Lenguaje.TieneFrase(q, "mas vend"));
        Assert.False(Lenguaje.TieneFrase(q, "s vendido"));
    }

    [Fact]
    public void Sin_aclaracion_el_periodo_es_el_mes_actual()
    {
        var p = Lenguaje.InterpretarPeriodo(Lenguaje.Normalizar("cuánto vendí"), Hoy);
        Assert.Equal(new DateOnly(2026, 10, 1), p.Desde);
        Assert.Equal(new DateOnly(2026, 11, 1), p.Hasta);
        Assert.Contains("octubre de 2026", p.Etiqueta);
    }

    [Theory]
    [InlineData("vendí el mes pasado")]
    [InlineData("ventas del mes anterior")]
    [InlineData("el último mes")]
    public void El_mes_pasado_cruza_correctamente_el_limite_de_mes(string pregunta)
    {
        var p = Lenguaje.InterpretarPeriodo(Lenguaje.Normalizar(pregunta), Hoy);
        Assert.Equal(new DateOnly(2026, 9, 1), p.Desde);
        Assert.Equal(new DateOnly(2026, 10, 1), p.Hasta);
        Assert.Equal("septiembre de 2026", p.Etiqueta);
    }

    [Fact]
    public void El_mes_pasado_de_enero_es_diciembre_del_anio_anterior()
    {
        var p = Lenguaje.InterpretarPeriodo("ventas del mes pasado", new DateOnly(2026, 1, 15));
        Assert.Equal(new DateOnly(2025, 12, 1), p.Desde);
        Assert.Equal(new DateOnly(2026, 1, 1), p.Hasta);
    }

    [Theory]
    [InlineData("cuánto vendí en marzo", 2026, 3)]
    [InlineData("ventas de Marzo de 2025", 2025, 3)]
    [InlineData("compras diciembre 2024", 2024, 12)]
    [InlineData("vendí en setiembre", 2026, 9)]
    [InlineData("ventas de enero del 2023", 2023, 1)]
    public void Un_mes_con_nombre_y_anio_opcional(string pregunta, int anio, int mes)
    {
        var p = Lenguaje.InterpretarPeriodo(Lenguaje.Normalizar(pregunta), Hoy);
        Assert.Equal(new DateOnly(anio, mes, 1), p.Desde);
        Assert.Equal(new DateOnly(anio, mes, 1).AddMonths(1), p.Hasta);
    }

    [Fact]
    public void Un_anio_fuera_de_rango_se_ignora_y_se_usa_el_actual()
    {
        var p = Lenguaje.InterpretarPeriodo(Lenguaje.Normalizar("ventas de marzo de 1850"), Hoy);
        Assert.Equal(new DateOnly(2026, 3, 1), p.Desde);
    }

    [Theory]
    [InlineData("cuánto vendí este año")]
    [InlineData("ventas anual")]
    [InlineData("vendí en lo que va del año")]
    public void Este_anio_abarca_de_enero_a_diciembre(string pregunta)
    {
        var p = Lenguaje.InterpretarPeriodo(Lenguaje.Normalizar(pregunta), Hoy);
        Assert.Equal(new DateOnly(2026, 1, 1), p.Desde);
        Assert.Equal(new DateOnly(2027, 1, 1), p.Hasta);
    }

    [Fact]
    public void Hoy_es_un_solo_dia()
    {
        var p = Lenguaje.InterpretarPeriodo(Lenguaje.Normalizar("¿cuánto vendí hoy?"), Hoy);
        Assert.Equal(Hoy, p.Desde);
        Assert.Equal(Hoy.AddDays(1), p.Hasta);
        Assert.Equal("hoy", p.Etiqueta);
    }
}
