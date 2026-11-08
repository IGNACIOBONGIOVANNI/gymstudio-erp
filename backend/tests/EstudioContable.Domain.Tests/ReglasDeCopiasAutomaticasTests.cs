using EstudioContable.Domain;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Domain.Tests;

public class ReglasDeCopiasAutomaticasTests
{
    // Todas las horas son de Argentina (UTC-3, sin horario de verano).
    private static DateTimeOffset Fecha(int anio, int mes, int dia, int hora = 0, int minuto = 0, int segundo = 0)
        => new(anio, mes, dia, hora, minuto, segundo, TimeSpan.FromHours(-3));

    private static ConfiguracionDeCopias Config(FrecuenciaDeCopia frecuencia, int hora, int minuto = 0, int? diaDeLaSemana = null, int? diaDelMes = null,
        int conservar = 14, DateTimeOffset? activaDesde = null)
    {
        var config = new ConfiguracionDeCopias();
        config.Programar(true, frecuencia, hora, minuto, diaDeLaSemana, diaDelMes, conservar, activaDesde ?? Fecha(2026, 1, 1));
        return config;
    }

    private static DateTimeOffset Proxima(ConfiguracionDeCopias config, DateTimeOffset referencia)
        => ReglasDeCopiasAutomaticas.ProximaEjecucion(config, referencia);

    // ---------- el próximo momento programado ----------

    [Fact]
    public void Diaria_es_el_mismo_dia_si_todavia_no_llego_la_hora_y_el_siguiente_si_ya_llego()
    {
        var config = Config(FrecuenciaDeCopia.Diaria, 22);
        Assert.Equal(Fecha(2026, 11, 7, 22), Proxima(config, Fecha(2026, 11, 7, 21, 59)));
        Assert.Equal(Fecha(2026, 11, 8, 22), Proxima(config, Fecha(2026, 11, 7, 22)));        // estrictamente posterior
        Assert.Equal(Fecha(2026, 11, 8, 22), Proxima(config, Fecha(2026, 11, 7, 22, 0, 1)));
        Assert.Equal(Fecha(2026, 12, 1, 22), Proxima(config, Fecha(2026, 11, 30, 23)));       // cambio de mes
        Assert.Equal(Fecha(2027, 1, 1, 22), Proxima(config, Fecha(2026, 12, 31, 22, 30)));    // cambio de año
    }

    [Fact]
    public void El_resultado_es_de_hora_argentina_aunque_la_referencia_venga_en_otra_zona()
    {
        var config = Config(FrecuenciaDeCopia.Diaria, 22);
        // 00:59 UTC del 8/11 son las 21:59 del 7/11 en Argentina.
        var proxima = Proxima(config, new DateTimeOffset(2026, 11, 8, 0, 59, 0, TimeSpan.Zero));
        Assert.Equal(Fecha(2026, 11, 7, 22), proxima);
        Assert.Equal(TimeSpan.FromHours(-3), proxima.Offset);
    }

    [Fact]
    public void Semanal_es_el_proximo_dia_de_la_semana_elegido()
    {
        var lunes = Config(FrecuenciaDeCopia.Semanal, 8, 30, diaDeLaSemana: 1);
        Assert.Equal(Fecha(2026, 11, 9, 8, 30), Proxima(lunes, Fecha(2026, 11, 6, 10)));      // viernes -> lunes
        Assert.Equal(Fecha(2026, 11, 9, 8, 30), Proxima(lunes, Fecha(2026, 11, 9, 8, 29)));   // el mismo lunes, antes de la hora
        Assert.Equal(Fecha(2026, 11, 16, 8, 30), Proxima(lunes, Fecha(2026, 11, 9, 8, 30)));  // el lunes siguiente

        var domingo = Config(FrecuenciaDeCopia.Semanal, 10, diaDeLaSemana: 7);
        Assert.Equal(Fecha(2026, 11, 8, 10), Proxima(domingo, Fecha(2026, 11, 7, 12)));       // sábado -> domingo
    }

    [Fact]
    public void Mensual_es_el_proximo_dia_del_mes_elegido_y_nunca_se_salta_un_mes_corto()
    {
        var dia28 = Config(FrecuenciaDeCopia.Mensual, 22, diaDelMes: 28);
        Assert.Equal(Fecha(2026, 3, 28, 22), Proxima(dia28, Fecha(2026, 2, 28, 22)));         // pasa de febrero a marzo
        Assert.Equal(Fecha(2026, 2, 28, 22), Proxima(dia28, Fecha(2026, 2, 1)));              // febrero sí tiene día 28
        Assert.Equal(Fecha(2027, 1, 28, 22), Proxima(dia28, Fecha(2026, 12, 28, 22, 1)));     // cambio de año

        var dia1 = Config(FrecuenciaDeCopia.Mensual, 22, diaDelMes: 1);
        Assert.Equal(Fecha(2026, 12, 1, 22), Proxima(dia1, Fecha(2026, 11, 1, 22)));
        Assert.Equal(Fecha(2026, 11, 1, 22), Proxima(dia1, Fecha(2026, 10, 31, 23)));
    }

    [Fact]
    public void Cada_6_horas_cuenta_desde_la_hora_elegida()
    {
        var config = Config(FrecuenciaDeCopia.CadaSeisHoras, 2);   // 02, 08, 14 y 20
        Assert.Equal(Fecha(2026, 11, 7, 8), Proxima(config, Fecha(2026, 11, 7, 2)));
        Assert.Equal(Fecha(2026, 11, 8, 2), Proxima(config, Fecha(2026, 11, 7, 20)));
        Assert.Equal(Fecha(2026, 11, 7, 2), Proxima(config, Fecha(2026, 11, 7, 1, 59)));
        Assert.Equal(Fecha(2026, 11, 7, 8), Proxima(config, Fecha(2026, 11, 7, 7, 59)));
        Assert.Equal(Fecha(2026, 11, 7, 2), Proxima(config, Fecha(2026, 11, 7, 0)));

        var tarde = Config(FrecuenciaDeCopia.CadaSeisHoras, 22);   // 04, 10, 16 y 22
        Assert.Equal(Fecha(2026, 11, 7, 10), Proxima(tarde, Fecha(2026, 11, 7, 5)));
        Assert.Equal(Fecha(2026, 11, 8, 4), Proxima(tarde, Fecha(2026, 11, 7, 22)));
    }

    [Fact]
    public void Cada_12_horas_cuenta_desde_la_hora_elegida()
    {
        var config = Config(FrecuenciaDeCopia.CadaDoceHoras, 6, 30);   // 06:30 y 18:30
        Assert.Equal(Fecha(2026, 11, 7, 18, 30), Proxima(config, Fecha(2026, 11, 7, 6, 30)));
        Assert.Equal(Fecha(2026, 11, 8, 6, 30), Proxima(config, Fecha(2026, 11, 7, 18, 30)));
        Assert.Equal(Fecha(2026, 11, 7, 6, 30), Proxima(config, Fecha(2026, 11, 7, 0)));
    }

    // ---------- cuándo toca ----------

    [Fact]
    public void Una_configuracion_inactiva_nunca_toca_ni_tiene_proxima_copia()
    {
        var config = new ConfiguracionDeCopias();
        Assert.False(config.Activa);
        Assert.False(ReglasDeCopiasAutomaticas.Toca(config, Fecha(2030, 1, 1)));
        Assert.Null(ReglasDeCopiasAutomaticas.ProximaCopia(config));
    }

    [Fact]
    public void Toca_a_la_hora_programada_y_no_antes()
    {
        var config = Config(FrecuenciaDeCopia.Diaria, 22, activaDesde: Fecha(2026, 11, 6, 23));
        Assert.False(ReglasDeCopiasAutomaticas.Toca(config, Fecha(2026, 11, 7, 21, 59)));
        Assert.True(ReglasDeCopiasAutomaticas.Toca(config, Fecha(2026, 11, 7, 22)));
    }

    [Fact]
    public void Si_estuvo_apagado_varios_periodos_toca_una_sola_vez()
    {
        var config = Config(FrecuenciaDeCopia.Diaria, 22, activaDesde: Fecha(2026, 11, 1));
        config.RegistrarCopia(Fecha(2026, 11, 4, 22));   // la última copia, hace tres días

        Assert.True(ReglasDeCopiasAutomaticas.Toca(config, Fecha(2026, 11, 7, 23)));
        config.RegistrarCopia(Fecha(2026, 11, 7, 23));
        Assert.False(ReglasDeCopiasAutomaticas.Toca(config, Fecha(2026, 11, 7, 23, 0, 30)));
        Assert.False(ReglasDeCopiasAutomaticas.Toca(config, Fecha(2026, 11, 8, 21, 59)));
        Assert.True(ReglasDeCopiasAutomaticas.Toca(config, Fecha(2026, 11, 8, 22)));
    }

    [Fact]
    public void Tras_una_falla_se_reintenta_a_los_30_minutos_y_no_antes()
    {
        var config = Config(FrecuenciaDeCopia.Diaria, 22, activaDesde: Fecha(2026, 11, 6, 23));
        var intento = Fecha(2026, 11, 7, 22, 5);
        Assert.True(ReglasDeCopiasAutomaticas.Toca(config, intento));

        config.RegistrarFalla(intento, "No se pudo guardar.");

        Assert.False(ReglasDeCopiasAutomaticas.Toca(config, intento.AddMinutes(29)));
        Assert.False(ReglasDeCopiasAutomaticas.Toca(config, intento.AddMinutes(29).AddSeconds(59)));
        Assert.True(ReglasDeCopiasAutomaticas.Toca(config, intento.AddMinutes(30)));
        Assert.Equal("No se pudo guardar.", config.UltimoError);

        config.RegistrarCopia(intento.AddMinutes(30));
        Assert.Null(config.UltimoError);
        Assert.Equal(intento.AddMinutes(30), config.UltimaCopia);
        Assert.Equal(intento.AddMinutes(30), config.UltimoIntento);
    }

    [Fact]
    public void Reprogramar_reinicia_la_referencia_y_no_dispara_una_copia_atrasada()
    {
        var config = Config(FrecuenciaDeCopia.Diaria, 22, activaDesde: Fecha(2026, 9, 1));
        config.RegistrarCopia(Fecha(2026, 10, 1, 22));   // hace 40 días

        config.Programar(true, FrecuenciaDeCopia.Mensual, 22, 0, null, 1, 14, Fecha(2026, 11, 10, 12));

        Assert.False(ReglasDeCopiasAutomaticas.Toca(config, Fecha(2026, 11, 10, 12)));
        Assert.False(ReglasDeCopiasAutomaticas.Toca(config, Fecha(2026, 11, 30, 23)));
        Assert.True(ReglasDeCopiasAutomaticas.Toca(config, Fecha(2026, 12, 1, 22)));
        Assert.Equal(Fecha(2026, 12, 1, 22), ReglasDeCopiasAutomaticas.ProximaCopia(config));
    }

    [Fact]
    public void La_proxima_copia_sale_de_la_ultima_copia_o_de_la_activacion()
    {
        var config = Config(FrecuenciaDeCopia.Diaria, 22, activaDesde: Fecha(2026, 11, 6, 23));
        Assert.Equal(Fecha(2026, 11, 7, 22), ReglasDeCopiasAutomaticas.ProximaCopia(config));

        config.RegistrarCopia(Fecha(2026, 11, 7, 22, 0, 5));
        Assert.Equal(Fecha(2026, 11, 8, 22), ReglasDeCopiasAutomaticas.ProximaCopia(config));
    }

    // ---------- la programación ----------

    [Fact]
    public void Programar_guarda_los_valores_y_fija_desde_cuando_rige()
    {
        var config = new ConfiguracionDeCopias();
        var ahora = Fecha(2026, 11, 7, 15);

        config.Programar(true, FrecuenciaDeCopia.Semanal, 8, 30, 3, null, 20, ahora);

        Assert.True(config.Activa);
        Assert.Equal((FrecuenciaDeCopia.Semanal, 8, 30, 3, (int?)null, 20), (config.Frecuencia, config.Hora, config.Minuto, config.DiaDeLaSemana, config.DiaDelMes, config.Conservar));
        Assert.Equal(ahora, config.ActivaDesde);
    }

    [Fact]
    public void Los_valores_por_defecto_son_inactiva_diaria_a_las_22_y_14_copias()
    {
        var config = new ConfiguracionDeCopias();
        Assert.Equal(ConfiguracionDeCopias.IdUnico, config.Id);
        Assert.Equal((false, FrecuenciaDeCopia.Diaria, 22, 0, 14), (config.Activa, config.Frecuencia, config.Hora, config.Minuto, config.Conservar));
        Assert.Null(config.UltimaCopia);
        Assert.Null(config.UltimoError);
    }

    [Fact]
    public void Los_dias_que_no_corresponden_a_la_frecuencia_quedan_en_null()
    {
        var config = new ConfiguracionDeCopias();

        config.Programar(true, FrecuenciaDeCopia.Diaria, 22, 0, 3, 5, 14, Fecha(2026, 11, 7));
        Assert.Equal((null, null), (config.DiaDeLaSemana, config.DiaDelMes));

        config.Programar(true, FrecuenciaDeCopia.Semanal, 22, 0, 3, 5, 14, Fecha(2026, 11, 7));
        Assert.Equal((3, null), (config.DiaDeLaSemana, config.DiaDelMes));

        config.Programar(true, FrecuenciaDeCopia.Mensual, 22, 0, 3, 5, 14, Fecha(2026, 11, 7));
        Assert.Equal((null, 5), (config.DiaDeLaSemana, config.DiaDelMes));
    }

    public static IEnumerable<object?[]> Programaciones_imposibles()
    {
        yield return new object?[] { "hora 24", FrecuenciaDeCopia.Diaria, 24, 0, null, null, 14 };
        yield return new object?[] { "hora -1", FrecuenciaDeCopia.Diaria, -1, 0, null, null, 14 };
        yield return new object?[] { "minuto 60", FrecuenciaDeCopia.Diaria, 22, 60, null, null, 14 };
        yield return new object?[] { "minuto -1", FrecuenciaDeCopia.Diaria, 22, -1, null, null, 14 };
        yield return new object?[] { "conservar 0", FrecuenciaDeCopia.Diaria, 22, 0, null, null, 0 };
        yield return new object?[] { "conservar 61", FrecuenciaDeCopia.Diaria, 22, 0, null, null, 61 };
        yield return new object?[] { "semanal sin día", FrecuenciaDeCopia.Semanal, 22, 0, null, null, 14 };
        yield return new object?[] { "semanal día 0", FrecuenciaDeCopia.Semanal, 22, 0, 0, null, 14 };
        yield return new object?[] { "semanal día 8", FrecuenciaDeCopia.Semanal, 22, 0, 8, null, 14 };
        yield return new object?[] { "mensual sin día", FrecuenciaDeCopia.Mensual, 22, 0, null, null, 14 };
        yield return new object?[] { "mensual día 0", FrecuenciaDeCopia.Mensual, 22, 0, null, 0, 14 };
        yield return new object?[] { "mensual día 29", FrecuenciaDeCopia.Mensual, 22, 0, null, 29, 14 };
        yield return new object?[] { "mensual día 31", FrecuenciaDeCopia.Mensual, 22, 0, null, 31, 14 };
        yield return new object?[] { "frecuencia que no existe", (FrecuenciaDeCopia)99, 22, 0, null, null, 14 };
    }

    [Theory]
    [MemberData(nameof(Programaciones_imposibles))]
    public void Una_programacion_imposible_se_rechaza_y_no_cambia_nada(string caso, FrecuenciaDeCopia frecuencia, int hora, int minuto,
        int? diaDeLaSemana, int? diaDelMes, int conservar)
    {
        var config = Config(FrecuenciaDeCopia.Semanal, 8, 30, diaDeLaSemana: 3, conservar: 20, activaDesde: Fecha(2026, 11, 1));

        Assert.True(Assert.Throws<ArgumentException>(() => config.Programar(false, frecuencia, hora, minuto, diaDeLaSemana, diaDelMes, conservar, Fecha(2026, 11, 7))) != null, caso);

        Assert.Equal((true, FrecuenciaDeCopia.Semanal, 8, 30, 3, 20), (config.Activa, config.Frecuencia, config.Hora, config.Minuto, config.DiaDeLaSemana, config.Conservar));
        Assert.Equal(Fecha(2026, 11, 1), config.ActivaDesde);
    }

    [Fact]
    public void El_mensaje_de_error_se_recorta_a_500_caracteres_y_no_puede_ser_vacio()
    {
        var config = Config(FrecuenciaDeCopia.Diaria, 22);
        config.RegistrarFalla(Fecha(2026, 11, 7, 22), new string('x', 800));
        Assert.Equal(500, config.UltimoError!.Length);
        Assert.Equal(500, ConfiguracionDeCopias.LargoMaximoDelError);

        Assert.Throws<ArgumentException>(() => config.RegistrarFalla(Fecha(2026, 11, 7, 23), "   "));
    }

    // ---------- los archivos ----------

    [Fact]
    public void El_nombre_del_archivo_lleva_la_fecha_y_hora_de_Argentina()
    {
        // 01:05:09 UTC del 8/11 son las 22:05:09 del 7/11 en Argentina.
        Assert.Equal("gymstudio_auto_20261107_220509.zip", ReglasDeCopiasAutomaticas.NombreDeArchivo(new DateTimeOffset(2026, 11, 8, 1, 5, 9, TimeSpan.Zero)));
        Assert.Equal("gymstudio_auto_20261107_220509.zip", ReglasDeCopiasAutomaticas.NombreDeArchivo(Fecha(2026, 11, 7, 22, 5, 9)));
    }

    [Theory]
    [InlineData("gymstudio_auto_20261107_220509.zip", true)]
    [InlineData("gymstudio_auto_19990101_000000.zip", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("../gymstudio_auto_20261107_220509.zip", false)]
    [InlineData("..\\gymstudio_auto_20261107_220509.zip", false)]
    [InlineData("a/gymstudio_auto_20261107_220509.zip", false)]
    [InlineData("/gymstudio_auto_20261107_220509.zip", false)]
    [InlineData("gymstudio_auto_20261107_220509.zip.tmp", false)]
    [InlineData("gymstudio_auto_20261107_220509.zip\n", false)]     // «$» aceptaría el salto de línea final: se usa «\z»
    [InlineData(" gymstudio_auto_20261107_220509.zip", false)]
    [InlineData("gymstudio_auto_x.zip", false)]
    [InlineData("gymstudio_auto_2026110_220509.zip", false)]
    [InlineData("GYMSTUDIO_AUTO_20261107_220509.ZIP", false)]
    [InlineData("gymstudio_auto_٢٠٢٦١١٠٧_٢٢٠٥٠٩.zip", false)]    // dígitos arábigos: «\d» los aceptaría, se aceptan solo 0-9
    public void Solo_se_aceptan_los_nombres_exactos_de_las_copias_automaticas(string? nombre, bool esperado)
        => Assert.Equal(esperado, ReglasDeCopiasAutomaticas.EsNombreDeCopiaAutomatica(nombre));
}
