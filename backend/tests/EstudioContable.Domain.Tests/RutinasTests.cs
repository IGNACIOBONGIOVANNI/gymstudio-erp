using EstudioContable.Domain.Entities;

namespace EstudioContable.Domain.Tests;

public class RutinaTests
{
    private static RutinaEjercicio Linea(long ejercicio = 1, int dia = 1, int series = 3, string reps = "10-12", int descanso = 60)
        => new(ejercicio, dia, series, reps, descanso, null);

    [Fact]
    public void El_orden_se_numera_por_dia_segun_la_lista()
    {
        var rutina = new Rutina("Full body", null, "Principiante",
            new[] { Linea(1, dia: 1), Linea(2, dia: 1), Linea(3, dia: 2), Linea(4, dia: 1), Linea(5, dia: 2) });

        var porDia = rutina.Ejercicios.GroupBy(e => e.Dia).ToDictionary(g => g.Key, g => g.Select(e => (e.EjercicioId, e.Orden)).ToArray());
        Assert.Equal(new[] { (1L, 1), (2L, 2), (4L, 3) }, porDia[1]);
        Assert.Equal(new[] { (3L, 1), (5L, 2) }, porDia[2]);
    }

    [Fact]
    public void Una_rutina_sin_ejercicios_es_rechazada()
        => Assert.Throws<ArgumentException>(() => new Rutina("Vacía", null, "Principiante", Array.Empty<RutinaEjercicio>()));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void El_nombre_es_obligatorio(string nombre)
        => Assert.Throws<ArgumentException>(() => new Rutina(nombre, null, "Principiante", new[] { Linea() }));

    [Fact]
    public void El_nivel_debe_ser_uno_de_la_lista()
        => Assert.Throws<ArgumentException>(() => new Rutina("X", null, "Experto", new[] { Linea() }));

    [Fact]
    public void Actualizar_reemplaza_todos_los_ejercicios_y_normaliza_el_nivel()
    {
        var rutina = new Rutina("A", null, "Principiante", new[] { Linea(1), Linea(2) });
        rutina.Actualizar("B", "Fuerza", "avanzado", new[] { Linea(9) });

        Assert.Equal("B", rutina.Nombre);
        Assert.Equal("Avanzado", rutina.Nivel);
        Assert.Equal(new long[] { 9 }, rutina.Ejercicios.Select(e => e.EjercicioId));
    }

    [Theory]
    [InlineData(0, 3, "10", 60)]     // día 0
    [InlineData(8, 3, "10", 60)]     // día 8
    [InlineData(1, 0, "10", 60)]     // sin series
    [InlineData(1, 21, "10", 60)]    // demasiadas series
    [InlineData(1, 3, "", 60)]       // sin repeticiones
    [InlineData(1, 3, "10", -1)]     // descanso negativo
    [InlineData(1, 3, "10", 601)]    // descanso de más de 10 minutos
    public void Los_renglones_invalidos_son_rechazados(int dia, int series, string reps, int descanso)
        => Assert.Throws<ArgumentException>(() => new RutinaEjercicio(1, dia, series, reps, descanso, null));

    [Theory]
    [InlineData("12", "12")]
    [InlineData("8-10", "8-10")]
    [InlineData(" 10 - 12 ", "10-12")]
    [InlineData("1", "1")]
    [InlineData("100-200", "100-200")]
    public void Las_repeticiones_son_un_numero_o_un_rango(string escrito, string guardado)
        => Assert.Equal(guardado, new RutinaEjercicio(1, 1, 3, escrito, 60, null).Repeticiones);

    [Theory]
    [InlineData("30 seg")]        // texto: el tiempo va en las indicaciones
    [InlineData("10 por pierna")]
    [InlineData("doce")]
    [InlineData("0")]             // cero repeticiones
    [InlineData("10-")]           // rango incompleto
    [InlineData("-10")]
    [InlineData("12-8")]          // rango al revés
    [InlineData("10-10")]
    [InlineData("8-10-12")]
    [InlineData("1000")]          // más de 3 cifras
    [InlineData("10.5")]
    public void Las_repeticiones_que_no_son_un_numero_o_un_rango_se_rechazan(string reps)
    {
        var error = Assert.Throws<ArgumentException>(() => new RutinaEjercicio(1, 1, 3, reps, 60, null));
        Assert.Contains("número", error.Message);
    }

    [Fact]
    public void Un_ejercicio_acepta_solo_los_grupos_musculares_conocidos()
    {
        Assert.Equal("Pecho", new Ejercicio("Press", "pecho").GrupoMuscular);
        Assert.Throws<ArgumentException>(() => new Ejercicio("Press", "Orejas"));
    }
}

public class AsignacionRutinaTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 27);

    [Fact]
    public void El_vencimiento_no_puede_ser_anterior_al_inicio()
        => Assert.Throws<ArgumentException>(() => new AsignacionRutina(1, 1, Hoy, Hoy.AddDays(-1), null));

    [Fact]
    public void Esta_vigente_hasta_su_vencimiento_inclusive()
    {
        var a = new AsignacionRutina(1, 1, Hoy, Hoy.AddDays(30), null);
        Assert.True(a.EstaVigenteEl(Hoy.AddDays(30)));
        Assert.False(a.EstaVigenteEl(Hoy.AddDays(31)));
    }

    [Fact]
    public void Sin_vencimiento_sigue_vigente()
        => Assert.True(new AsignacionRutina(1, 1, Hoy, null, null).EstaVigenteEl(Hoy.AddYears(2)));

    [Fact]
    public void Finalizar_la_deja_inactiva_y_nunca_la_cierra_antes_de_empezar()
    {
        var a = new AsignacionRutina(1, 1, Hoy, null, null);
        a.Finalizar(Hoy.AddDays(-5));

        Assert.False(a.Activa);
        Assert.Equal(Hoy, a.FechaFin);
        Assert.False(a.EstaVigenteEl(Hoy));
        Assert.Throws<InvalidOperationException>(() => a.Finalizar(Hoy));
    }
}

public class RutinaTopeTests
{
    private static RutinaEjercicio Linea() => new(1, 1, 3, "10", 60, null);

    [Fact]
    public void Una_rutina_acepta_hasta_el_tope_de_renglones_y_rechaza_uno_mas()
    {
        Assert.Equal(Rutina.MaximoDeRenglones, new Rutina("Justa", null, "Principiante", Enumerable.Range(0, Rutina.MaximoDeRenglones).Select(_ => Linea())).Ejercicios.Count);
        Assert.Throws<ArgumentException>(() => new Rutina("Enorme", null, "Principiante", Enumerable.Range(0, Rutina.MaximoDeRenglones + 1).Select(_ => Linea())));
    }
}
