using EstudioContable.Domain.Entities;

namespace EstudioContable.Domain.Tests;

public class EntrenadorTests
{
    [Fact]
    public void El_nombre_se_limpia_de_espacios_sobrantes_y_repetidos()
    {
        Assert.Equal("María José", Entrenador.LimpiarNombre("  María   José  "));
        Assert.Equal("Ana López", new Entrenador("  Ana    López ", null, null).NombreCompleto);
    }

    [Theory]
    [InlineData('\u00A0')]   // espacio duro: el que trae pegar desde WhatsApp o Word
    [InlineData('\u202F')]   // espacio duro angosto
    [InlineData('\u2007')]
    [InlineData('\u2003')]   // espacio largo
    [InlineData('\u3000')]   // espacio ideográfico
    [InlineData('\u1680')]
    public void Un_espacio_de_otro_tipo_cuenta_como_un_espacio_comun_para_que_sea_la_misma_ficha(char espacio)
    {
        Assert.Equal("Ana López", Entrenador.LimpiarNombre($"Ana{espacio}López"));
        Assert.Equal("Ana López", Entrenador.LimpiarNombre($"{espacio}Ana {espacio}{espacio} López{espacio}"));
        Assert.Equal(Entrenador.LimpiarNombre("Ana López"), new Entrenador($"Ana{espacio}López", null, null).NombreCompleto);
    }

    [Fact]
    public void Las_mayusculas_y_los_acentos_se_conservan_como_se_escribieron()
        => Assert.Equal("ANA López-Núñez", Entrenador.LimpiarNombre("ANA López-Núñez"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ana\nBeto")]      // salto de línea dentro del nombre
    [InlineData("Ana\u0000")]      // carácter nulo
    [InlineData("Ana\tBeto")]      // tabulación dentro del nombre
    public void Un_nombre_vacio_o_con_caracteres_de_control_se_rechaza(string? nombre)
    {
        Assert.Throws<ArgumentException>(() => Entrenador.LimpiarNombre(nombre));
        Assert.Throws<ArgumentException>(() => new Entrenador(nombre, null, null));
    }

    [Fact]
    public void El_nombre_tiene_un_tope_de_80_letras()
    {
        Assert.Equal(80, Entrenador.LargoMaximoDelNombre);
        Assert.Equal(new string('x', 80), Entrenador.LimpiarNombre(new string('x', 80)));
        Assert.Throws<ArgumentException>(() => Entrenador.LimpiarNombre(new string('x', 81)));
    }

    [Fact]
    public void El_mensaje_del_nombre_vacio_dice_que_es_requerido()
    {
        var error = Assert.Throws<ArgumentException>(() => Entrenador.LimpiarNombre("  "));
        Assert.Contains("El nombre del entrenador es requerido.", error.Message);
    }

    [Fact]
    public void Telefono_y_especialidad_son_opcionales_y_vacio_es_null()
    {
        var entrenador = new Entrenador("Ana López", "   ", "");
        Assert.Null(entrenador.Telefono);
        Assert.Null(entrenador.Especialidad);

        var conDatos = new Entrenador("Ana López", "  +54 11 1234-5678 ", " Musculación ");
        Assert.Equal("+54 11 1234-5678", conDatos.Telefono);
        Assert.Equal("Musculación", conDatos.Especialidad);
    }

    [Fact]
    public void Telefono_y_especialidad_tienen_su_tope()
    {
        Assert.Equal(30, Entrenador.LargoMaximoDelTelefono);
        Assert.Equal(60, Entrenador.LargoMaximoDeLaEspecialidad);
        Assert.Equal(new string('1', 30), new Entrenador("Ana", new string('1', 30), null).Telefono);
        Assert.Throws<ArgumentException>(() => new Entrenador("Ana", new string('1', 31), null));
        Assert.Equal(new string('e', 60), new Entrenador("Ana", null, new string('e', 60)).Especialidad);
        Assert.Throws<ArgumentException>(() => new Entrenador("Ana", null, new string('e', 61)));
    }

    [Fact]
    public void Se_crea_activo_y_se_puede_desactivar_y_activar()
    {
        var antes = DateTimeOffset.UtcNow;
        var entrenador = new Entrenador("Ana López", null, null);
        Assert.True(entrenador.Activo);
        Assert.True(entrenador.FechaCreacion >= antes && entrenador.FechaCreacion <= DateTimeOffset.UtcNow);

        entrenador.Desactivar();
        Assert.False(entrenador.Activo);
        entrenador.Activar();
        Assert.True(entrenador.Activo);
    }

    [Fact]
    public void ActualizarDatos_aplica_las_mismas_reglas()
    {
        var entrenador = new Entrenador("Ana López", "111", "Yoga");

        entrenador.ActualizarDatos("  Ana   M.  López ", "  ", " Pilates ");
        Assert.Equal("Ana M. López", entrenador.NombreCompleto);
        Assert.Null(entrenador.Telefono);
        Assert.Equal("Pilates", entrenador.Especialidad);

        Assert.Throws<ArgumentException>(() => entrenador.ActualizarDatos("", null, null));
        Assert.Throws<ArgumentException>(() => entrenador.ActualizarDatos("Ana", new string('1', 31), null));
        Assert.Equal("Ana M. López", entrenador.NombreCompleto);   // un pedido inválido no cambia nada
    }
}
