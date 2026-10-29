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

    // Hoy (28/10/2026) es miércoles: la semana va del lunes 26/10 al domingo 01/11.

    [Fact]
    public void Ayer_es_el_dia_anterior()
    {
        var p = Lenguaje.InterpretarPeriodo(Lenguaje.Normalizar("¿quién vino ayer?"), Hoy);
        Assert.Equal(new DateOnly(2026, 10, 27), p.Desde);
        Assert.Equal(Hoy, p.Hasta);
        Assert.Equal("ayer", p.Etiqueta);
    }

    [Theory]
    [InlineData("cuánto vendí esta semana")]
    [InlineData("las visitas de la semana")]
    public void Esta_semana_va_del_lunes_al_domingo(string pregunta)
    {
        var p = Lenguaje.InterpretarPeriodo(Lenguaje.Normalizar(pregunta), Hoy);
        Assert.Equal(new DateOnly(2026, 10, 26), p.Desde);
        Assert.Equal(new DateOnly(2026, 11, 2), p.Hasta);
        Assert.Equal("esta semana", p.Etiqueta);
    }

    [Fact]
    public void En_domingo_la_semana_sigue_siendo_la_que_empezo_el_lunes()
    {
        var domingo = new DateOnly(2026, 11, 1);
        var p = Lenguaje.InterpretarPeriodo(Lenguaje.Normalizar("esta semana"), domingo);
        Assert.Equal(new DateOnly(2026, 10, 26), p.Desde);
        Assert.Equal(new DateOnly(2026, 11, 2), p.Hasta);
    }

    [Theory]
    [InlineData("vendí la semana pasada")]
    [InlineData("la semana anterior")]
    public void La_semana_pasada_es_la_anterior_completa(string pregunta)
    {
        var p = Lenguaje.InterpretarPeriodo(Lenguaje.Normalizar(pregunta), Hoy);
        Assert.Equal(new DateOnly(2026, 10, 19), p.Desde);
        Assert.Equal(new DateOnly(2026, 10, 26), p.Hasta);
        Assert.Equal("la semana pasada", p.Etiqueta);
    }

    [Fact]
    public void El_periodo_anterior_de_un_mes_es_el_mes_de_antes_incluso_cruzando_el_anio()
    {
        var octubre = Lenguaje.PeriodoAnterior(Lenguaje.InterpretarPeriodo("", Hoy));
        Assert.Equal(new DateOnly(2026, 9, 1), octubre.Desde);
        Assert.Equal(new DateOnly(2026, 10, 1), octubre.Hasta);
        Assert.Equal("septiembre de 2026", octubre.Etiqueta);

        var enero = Lenguaje.PeriodoAnterior(Lenguaje.InterpretarPeriodo("", new DateOnly(2026, 1, 20)));
        Assert.Equal(new DateOnly(2025, 12, 1), enero.Desde);
        Assert.Equal("diciembre de 2025", enero.Etiqueta);
    }

    [Fact]
    public void El_periodo_anterior_de_un_dia_una_semana_y_un_anio_son_los_de_antes()
    {
        var hoy = Lenguaje.PeriodoAnterior(Lenguaje.InterpretarPeriodo("hoy", Hoy));
        Assert.Equal(new DateOnly(2026, 10, 27), hoy.Desde);
        Assert.Equal("ayer", hoy.Etiqueta);

        var semana = Lenguaje.PeriodoAnterior(Lenguaje.InterpretarPeriodo("esta semana", Hoy));
        Assert.Equal(new DateOnly(2026, 10, 19), semana.Desde);
        Assert.Equal(new DateOnly(2026, 10, 26), semana.Hasta);
        Assert.Equal("la semana pasada", semana.Etiqueta);

        var anio = Lenguaje.PeriodoAnterior(Lenguaje.InterpretarPeriodo("este ano", Hoy));
        Assert.Equal(new DateOnly(2025, 1, 1), anio.Desde);
        Assert.Equal(new DateOnly(2026, 1, 1), anio.Hasta);
    }

    [Theory]
    [InlineData("vendi mas que el mes pasado", "2026-10-01", "2026-09-01")]       // "el mes pasado" es con lo que se compara, no lo que se mira
    [InlineData("subieron las ventas respecto del mes anterior", "2026-10-01", "2026-09-01")]
    [InlineData("vendi mas que la semana pasada", "2026-10-26", "2026-10-19")]
    [InlineData("vendi mas que ayer", "2026-10-28", "2026-10-27")]
    [InlineData("ventas de marzo comparadas con febrero", "2026-03-01", "2026-02-01")]
    [InlineData("compare las ventas de este mes", "2026-10-01", "2026-09-01")]
    public void Al_comparar_se_mira_el_periodo_actual_contra_el_anterior(string pregunta, string actualDesde, string anteriorDesde)
    {
        var (actual, anterior) = Lenguaje.InterpretarComparacion(Lenguaje.Normalizar(pregunta), Hoy);
        Assert.Equal(DateOnly.Parse(actualDesde), actual.Desde);
        Assert.Equal(DateOnly.Parse(anteriorDesde), anterior.Desde);
        Assert.Equal(actual.Desde, anterior.Hasta);       // el anterior termina donde empieza el actual
    }

    [Theory]
    [InlineData("quiénes no vienen hace 30 días", 30)]
    [InlineData("socios que faltan hace 2 semanas", 14)]
    [InlineData("no viene hace un mes", 30)]
    [InlineData("no vienen desde hace una semana", 7)]
    [InlineData("los que no vienen hace tiempo", 21)]       // sin cantidad: el valor por defecto que se pasa
    [InlineData("no vienen hace 99999 dias", 365)]          // nunca más de un año
    [InlineData("no vienen hace 0 dias", 21)]               // 0 no tiene sentido: se usa el valor por defecto
    public void Los_dias_de_una_pregunta_se_entienden_en_dias_semanas_y_meses(string pregunta, int esperado)
        => Assert.Equal(esperado, Lenguaje.DiasDeLaPregunta(Lenguaje.Normalizar(pregunta), 21));
}
