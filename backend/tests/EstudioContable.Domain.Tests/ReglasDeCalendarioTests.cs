using EstudioContable.Domain;
using EstudioContable.Domain.Entities;

namespace EstudioContable.Domain.Tests;

public class ReglasDeCalendarioTests
{
    [Theory]
    [InlineData(1, 6)]    // lunes de 06 a 07: la primera celda
    [InlineData(5, 21)]   // viernes de 21 a 22: la última
    [InlineData(3, 13)]
    public void Las_celdas_de_la_semana_son_validas(int dia, int hora) => ReglasDeCalendario.ValidarCelda(dia, hora);

    [Theory]
    [InlineData(0, 8)]     // no hay día 0
    [InlineData(6, 8)]     // sábado: el calendario es de lunes a viernes
    [InlineData(7, 8)]
    [InlineData(-1, 8)]
    [InlineData(1, 5)]     // antes de abrir
    [InlineData(1, 22)]    // el gimnasio cierra a las 22: ese turno no existe
    [InlineData(1, 23)]
    [InlineData(1, -1)]
    [InlineData(int.MaxValue, 8)]
    [InlineData(1, int.MinValue)]
    public void Las_celdas_fuera_de_la_semana_se_rechazan(int dia, int hora)
        => Assert.Throws<ArgumentException>(() => ReglasDeCalendario.ValidarCelda(dia, hora));

    [Fact]
    public void La_semana_tiene_80_celdas_de_5_dias_por_16_horas()
    {
        Assert.Equal(80, ReglasDeCalendario.CeldasMaximas);
        Assert.Equal(16, ReglasDeCalendario.UltimaHora - ReglasDeCalendario.PrimeraHora + 1);
    }

    [Fact]
    public void Un_turno_se_crea_y_cambia_de_entrenador_con_las_mismas_reglas()
    {
        var turno = new TurnoEncargado(2, 9, 7);
        Assert.Equal((2, 9, 7L), (turno.DiaSemana, turno.Hora, turno.EntrenadorId));

        turno.CambiarEntrenador(8);
        Assert.Equal(8, turno.EntrenadorId);
        Assert.Throws<ArgumentException>(() => turno.CambiarEntrenador(0));
        Assert.Throws<ArgumentException>(() => turno.CambiarEntrenador(-3));
        Assert.Equal(8, turno.EntrenadorId);   // un cambio inválido no cambia nada

        Assert.Throws<ArgumentException>(() => new TurnoEncargado(6, 9, 7));    // sábado
        Assert.Throws<ArgumentException>(() => new TurnoEncargado(2, 22, 7));   // el gimnasio ya cerró
        Assert.Throws<ArgumentException>(() => new TurnoEncargado(2, 9, 0));    // sin entrenador
    }
}
