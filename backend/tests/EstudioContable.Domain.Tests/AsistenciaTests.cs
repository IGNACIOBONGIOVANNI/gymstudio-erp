using EstudioContable.Domain;

namespace EstudioContable.Domain.Tests;

public class AsistenciaTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 28);
    private static DateTime Vence(int diasDesdeHoy, int hora = 15) => Hoy.AddDays(diasDesdeHoy).ToDateTime(new TimeOnly(hora, 0));

    [Fact]
    public void Sin_vencimiento_el_socio_no_tiene_plan()
        => Assert.Equal(EstadoDeCuota.SinPlan, ReglasDeAsistencia.CalcularEstado(null, Hoy));

    [Theory]
    [InlineData(-1, EstadoDeCuota.Vencida)]      // venció ayer
    [InlineData(0, EstadoDeCuota.PorVencer)]     // vence hoy: todavía está vigente, aunque sea a las 00:00 o a las 23:59
    [InlineData(7, EstadoDeCuota.PorVencer)]     // el límite de "por vencer" entra
    [InlineData(8, EstadoDeCuota.AlDia)]
    [InlineData(90, EstadoDeCuota.AlDia)]
    public void El_estado_de_la_cuota_se_decide_por_el_dia_del_vencimiento(int dias, EstadoDeCuota esperado)
    {
        Assert.Equal(esperado, ReglasDeAsistencia.CalcularEstado(Vence(dias, 0), Hoy));
        Assert.Equal(esperado, ReglasDeAsistencia.CalcularEstado(Vence(dias, 23), Hoy));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 1)]    // da la vuelta
    [InlineData(10, 2)]
    public void Los_dias_de_la_rutina_rotan_en_orden_una_por_visita(int visitasPrevias, int esperado)
        => Assert.Equal(esperado, ReglasDeAsistencia.DiaDeRutina(new[] { 1, 2, 3 }, visitasPrevias));

    [Fact]
    public void Los_dias_no_tienen_que_ser_consecutivos_ni_venir_ordenados()
    {
        var dias = new[] { 5, 1, 3, 3 };    // desordenados y con un renglón repetido del día 3
        Assert.Equal(new int?[] { 1, 3, 5, 1 }, new[] { 0, 1, 2, 3 }.Select(v => ReglasDeAsistencia.DiaDeRutina(dias, v)).ToArray());
    }

    [Fact]
    public void Una_rutina_sin_dias_o_con_visitas_negativas_no_rompe()
    {
        Assert.Null(ReglasDeAsistencia.DiaDeRutina(Array.Empty<int>(), 4));
        Assert.Equal(1, ReglasDeAsistencia.DiaDeRutina(new[] { 1, 2 }, -5));
    }

    [Fact]
    public void Los_dias_sin_venir_cuentan_desde_la_ultima_visita()
        => Assert.Equal(20, ReglasDeAsistencia.DiasSinVenir(Hoy.AddDays(-20), Hoy.AddDays(-60), Hoy));

    [Fact]
    public void Si_nunca_vino_cuentan_desde_que_empezo_la_cuota()
    {
        Assert.Equal(3, ReglasDeAsistencia.DiasSinVenir(null, Hoy.AddDays(-3), Hoy));
        Assert.Equal(0, ReglasDeAsistencia.DiasSinVenir(null, Hoy, Hoy));
        Assert.Equal(0, ReglasDeAsistencia.DiasSinVenir(null, Hoy.AddDays(2), Hoy));    // un inicio futuro nunca da negativo
    }

    [Theory]
    [InlineData(EstadoDeCuota.AlDia, 14, true)]
    [InlineData(EstadoDeCuota.AlDia, 13, false)]
    [InlineData(EstadoDeCuota.PorVencer, 30, true)]
    [InlineData(EstadoDeCuota.Vencida, 90, false)]     // ya se ve en las cuotas vencidas
    [InlineData(EstadoDeCuota.SinPlan, 90, false)]
    public void En_riesgo_es_estar_al_dia_y_no_venir(EstadoDeCuota estado, int dias, bool esperado)
        => Assert.Equal(esperado, ReglasDeAsistencia.EstaEnRiesgo(estado, dias));

    [Fact]
    public void El_umbral_de_dias_se_puede_cambiar()
    {
        Assert.True(ReglasDeAsistencia.EstaEnRiesgo(EstadoDeCuota.AlDia, 7, 7));
        Assert.False(ReglasDeAsistencia.EstaEnRiesgo(EstadoDeCuota.AlDia, 6, 7));
    }
}
