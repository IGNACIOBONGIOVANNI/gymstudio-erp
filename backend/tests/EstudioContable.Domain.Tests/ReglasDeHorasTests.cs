using System.Globalization;
using EstudioContable.Domain;
using EstudioContable.Domain.Entities;

namespace EstudioContable.Domain.Tests;

public class ReglasDeHorasTests
{
    private static decimal D(string numero) => decimal.Parse(numero, CultureInfo.InvariantCulture);

    // ---------- monto de un día ----------

    [Theory]
    [InlineData("8", "1500.00", "12000.00")]
    [InlineData("7.5", "1234.56", "9259.20")]
    [InlineData("0", "1500.00", "0")]
    [InlineData("0.25", "100.10", "25.03")]   // 25,025: el medio centavo sube
    [InlineData("0.25", "0.10", "0.03")]      // 0,025
    [InlineData("0.25", "0.30", "0.08")]      // 0,075
    [InlineData("0.25", "0.20", "0.05")]      // 0,05 justo: no hay nada que redondear
    public void El_monto_de_un_dia_es_horas_por_valor_redondeado_a_dos_decimales_con_el_medio_centavo_hacia_arriba(string horas, string valor, string esperado)
        => Assert.Equal(D(esperado), ReglasDeHoras.MontoDelDia(D(horas), D(valor)));

    [Fact]
    public void El_monto_se_redondea_por_dia_y_no_sobre_la_suma()
    {
        var porDia = ReglasDeHoras.MontoDelDia(0.25m, 100.10m);
        Assert.Equal(25.03m, porDia);
        Assert.Equal(50.06m, porDia + porDia);                                  // dos días: 50,06
        Assert.Equal(50.05m, Math.Round(0.5m * 100.10m, 2, MidpointRounding.AwayFromZero));   // sumar antes de redondear daría otro número
    }

    // ---------- calendario ----------

    [Theory]
    [InlineData("2026-11-09", 1)]   // lunes
    [InlineData("2026-11-11", 3)]
    [InlineData("2026-11-13", 5)]   // viernes
    [InlineData("2026-11-14", 6)]   // sábado
    [InlineData("2026-11-08", 7)]   // domingo
    public void El_dia_de_la_semana_va_de_lunes_1_a_domingo_7(string fecha, int esperado)
        => Assert.Equal(esperado, ReglasDeHoras.DiaDeLaSemana(DateOnly.Parse(fecha, CultureInfo.InvariantCulture)));

    [Fact]
    public void Las_programadas_cuentan_solo_los_turnos_de_ese_entrenador_en_ese_dia_de_la_semana()
    {
        var turnos = new (long EntrenadorId, int DiaSemana)[] { (1, 1), (1, 1), (1, 1), (1, 3), (2, 1), (2, 2) };

        Assert.Equal(3, ReglasDeHoras.Programadas(turnos, 1, new DateOnly(2026, 11, 9)));    // lunes
        Assert.Equal(1, ReglasDeHoras.Programadas(turnos, 1, new DateOnly(2026, 11, 11)));   // miércoles
        Assert.Equal(1, ReglasDeHoras.Programadas(turnos, 2, new DateOnly(2026, 11, 9)));
        Assert.Equal(0, ReglasDeHoras.Programadas(turnos, 1, new DateOnly(2026, 11, 10)));   // martes sin turnos
        Assert.Equal(0, ReglasDeHoras.Programadas(turnos, 99, new DateOnly(2026, 11, 9)));   // otro entrenador
        Assert.Equal(0, ReglasDeHoras.Programadas(turnos, 1, new DateOnly(2026, 11, 14)));   // sábado
        Assert.Equal(0, ReglasDeHoras.Programadas(turnos, 1, new DateOnly(2026, 11, 15)));   // domingo
        Assert.Equal(0, ReglasDeHoras.Programadas(Array.Empty<(long, int)>(), 1, new DateOnly(2026, 11, 9)));
    }

    [Theory]
    [InlineData("2026-11-13T01:30:00+00:00", "2026-11-12")]   // 22:30 en Argentina: todavía es el 12
    [InlineData("2026-11-12T03:00:00+00:00", "2026-11-12")]   // 00:00 en Argentina: ya es el 12
    [InlineData("2026-11-12T02:59:00+00:00", "2026-11-11")]   // 23:59 del día anterior
    [InlineData("2026-11-12T12:00:00-03:00", "2026-11-12")]   // ya viene con la hora de Argentina
    [InlineData("2026-12-31T23:30:00-03:00", "2026-12-31")]   // fin de año
    [InlineData("2027-01-01T02:00:00+00:00", "2026-12-31")]   // 23:00 del 31 en Argentina
    public void Hoy_es_el_dia_en_Argentina_aunque_en_UTC_ya_sea_el_siguiente(string ahora, string esperado)
        => Assert.Equal(DateOnly.Parse(esperado, CultureInfo.InvariantCulture),
            ReglasDeHoras.HoyEnArgentina(DateTimeOffset.Parse(ahora, CultureInfo.InvariantCulture)));

    // ---------- horas, valor, fecha, período, nota ----------

    [Theory]
    [InlineData("0")]
    [InlineData("0.25")]
    [InlineData("7.5")]
    [InlineData("8")]
    [InlineData("24")]
    public void Las_horas_validas_van_de_0_a_24_de_a_cuarto_de_hora(string horas)
        => Assert.Equal(D(horas), ReglasDeHoras.ValidarHoras(D(horas)));

    [Theory]
    [InlineData("-0.25")]
    [InlineData("24.25")]
    [InlineData("0.1")]
    [InlineData("0.3")]
    [InlineData("7.51")]
    [InlineData("100")]
    public void Las_horas_fuera_de_rango_o_que_no_son_cuarto_de_hora_se_rechazan(string horas)
    {
        var error = Assert.Throws<ArgumentException>(() => ReglasDeHoras.ValidarHoras(D(horas)));
        Assert.Contains("horas", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("1234.56")]
    [InlineData("1500")]
    [InlineData("999999999999")]
    public void El_valor_de_la_hora_vale_si_esta_vacio_o_entre_0_y_el_maximo_con_hasta_dos_decimales(string? valor)
    {
        decimal? esperado = valor is null ? null : D(valor);
        Assert.Equal(esperado, ReglasDeHoras.ValidarValorHora(esperado));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-0.01")]
    [InlineData("1000000000000")]
    [InlineData("1.234")]
    [InlineData("0.001")]
    public void El_valor_de_la_hora_negativo_enorme_o_con_mas_de_dos_decimales_se_rechaza(string valor)
        => Assert.Throws<ArgumentException>(() => ReglasDeHoras.ValidarValorHora(D(valor)));

    [Fact]
    public void Una_fecha_de_hoy_o_de_ayer_vale_y_una_de_mañana_se_rechaza()
    {
        var hoy = new DateOnly(2026, 11, 12);
        ReglasDeHoras.ValidarFecha(hoy, hoy);
        ReglasDeHoras.ValidarFecha(hoy.AddDays(-1), hoy);
        ReglasDeHoras.ValidarFecha(new DateOnly(2020, 1, 1), hoy);
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.ValidarFecha(hoy.AddDays(1), hoy));
    }

    [Fact]
    public void Una_fecha_omitida_o_absurda_se_rechaza_aunque_no_sea_futura()
    {
        var hoy = new DateOnly(2026, 11, 12);
        ReglasDeHoras.ValidarFecha(new DateOnly(2000, 1, 1), hoy);                          // el primer día que vale
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.ValidarFecha(default, hoy));  // 0001-01-01: la que toma un campo que falta
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.ValidarFecha(new DateOnly(1999, 12, 31), hoy));
    }

    [Fact]
    public void Un_periodo_con_fechas_omitidas_o_fuera_de_rango_se_rechaza()
    {
        ReglasDeHoras.ValidarPeriodo(new DateOnly(2000, 1, 1), new DateOnly(2000, 1, 31));
        ReglasDeHoras.ValidarPeriodo(new DateOnly(2100, 12, 1), new DateOnly(2100, 12, 31));
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.ValidarPeriodo(default, default));   // desde y hasta que faltan
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.ValidarPeriodo(default, new DateOnly(2026, 11, 12)));
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.ValidarPeriodo(new DateOnly(1999, 12, 1), new DateOnly(1999, 12, 31)));
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.ValidarPeriodo(new DateOnly(2101, 1, 1), new DateOnly(2101, 1, 5)));
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.ValidarPeriodo(new DateOnly(9999, 12, 1), DateOnly.MaxValue));   // recorrerlo se saldría del calendario
    }

    [Fact]
    public void El_periodo_va_de_un_dia_a_62_dias_contando_los_dos_extremos()
    {
        ReglasDeHoras.ValidarPeriodo(new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 5));          // un solo día
        ReglasDeHoras.ValidarPeriodo(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));          // 62 días justos
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.ValidarPeriodo(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 2)));   // 63
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.ValidarPeriodo(new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 5)));   // invertido
        Assert.Equal(62, ReglasDeHoras.DiasMaximosDelPeriodo);
    }

    [Fact]
    public void La_nota_se_recorta_vacia_es_null_y_tiene_su_tope_y_sin_caracteres_de_control()
    {
        Assert.Equal("Cubrió a Beto", ReglasDeHoras.LimpiarNota("  Cubrió a Beto "));
        Assert.Null(ReglasDeHoras.LimpiarNota(""));
        Assert.Null(ReglasDeHoras.LimpiarNota("   "));
        Assert.Null(ReglasDeHoras.LimpiarNota(null));
        Assert.Equal(new string('n', 200), ReglasDeHoras.LimpiarNota(new string('n', 200)));
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.LimpiarNota(new string('n', 201)));
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.LimpiarNota("a\nb"));
        Assert.Throws<ArgumentException>(() => ReglasDeHoras.LimpiarNota("a\tb"));
        Assert.Equal(200, ReglasDeHoras.LargoMaximoDeLaNota);
        Assert.Equal(500, ReglasDeHoras.RenglonesMaximos);
    }
}

public class HoraDeEntrenadorTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 12);

    [Fact]
    public void Se_crea_con_sus_valores_y_la_nota_limpia()
    {
        var dia = new HoraDeEntrenador(7, new DateOnly(2026, 11, 10), 7.5m, 1500m, "  Cubrió a Beto ", Hoy);

        Assert.Equal((7L, new DateOnly(2026, 11, 10), 7.5m, 1500m), (dia.EntrenadorId, dia.Fecha, dia.Horas, dia.ValorHora));
        Assert.Equal("Cubrió a Beto", dia.Nota);
    }

    [Fact]
    public void Cambiar_solo_cambia_las_horas_y_la_nota_el_valor_y_la_fecha_no()
    {
        var dia = new HoraDeEntrenador(7, new DateOnly(2026, 11, 10), 8m, 1000m, null, Hoy);

        dia.Cambiar(0m, "Faltó");

        Assert.Equal((0m, "Faltó"), (dia.Horas, dia.Nota));
        Assert.Equal((new DateOnly(2026, 11, 10), 1000m), (dia.Fecha, dia.ValorHora));

        dia.Cambiar(9.5m, "  ");
        Assert.Equal((9.5m, (string?)null), (dia.Horas, dia.Nota));
    }

    [Fact]
    public void Un_cambio_invalido_no_cambia_nada()
    {
        var dia = new HoraDeEntrenador(7, new DateOnly(2026, 11, 10), 8m, 1000m, "Nota", Hoy);

        Assert.Throws<ArgumentException>(() => dia.Cambiar(24.25m, "Otra"));
        Assert.Throws<ArgumentException>(() => dia.Cambiar(0.1m, "Otra"));
        Assert.Throws<ArgumentException>(() => dia.Cambiar(8m, new string('x', 201)));   // las horas son válidas pero la nota no

        Assert.Equal((8m, "Nota"), (dia.Horas, dia.Nota));
    }

    [Fact]
    public void Una_fecha_futura_lanza_y_hoy_vale()
    {
        Assert.Throws<ArgumentException>(() => new HoraDeEntrenador(7, Hoy.AddDays(1), 8m, 1000m, null, Hoy));
        Assert.Equal(Hoy, new HoraDeEntrenador(7, Hoy, 8m, 1000m, null, Hoy).Fecha);
    }

    [Theory]
    [InlineData(0, "8", "1000")]       // sin entrenador
    [InlineData(7, "24.25", "1000")]   // horas fuera de rango
    [InlineData(7, "0.1", "1000")]     // no es cuarto de hora
    [InlineData(7, "8", "-1")]         // valor negativo
    [InlineData(7, "8", "1.234")]      // más de dos decimales
    public void Los_datos_invalidos_no_crean_el_dia(long entrenadorId, string horas, string valor)
        => Assert.Throws<ArgumentException>(() => new HoraDeEntrenador(
            entrenadorId, new DateOnly(2026, 11, 10), decimal.Parse(horas, CultureInfo.InvariantCulture), decimal.Parse(valor, CultureInfo.InvariantCulture), null, Hoy));
}
