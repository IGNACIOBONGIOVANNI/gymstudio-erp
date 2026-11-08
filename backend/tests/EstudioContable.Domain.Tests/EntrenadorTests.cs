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

    [Fact]
    public void Email_y_WhatsApp_son_opcionales_y_vacio_es_null()
    {
        var sinContacto = new Entrenador("Ana López", null, null);
        Assert.Null(sinContacto.Email);
        Assert.Null(sinContacto.WhatsApp);

        var vacios = new Entrenador("Ana López", null, null, "   ", "");
        Assert.Null(vacios.Email);
        Assert.Null(vacios.WhatsApp);

        Assert.Null(Entrenador.LimpiarEmail(null));
        Assert.Null(Entrenador.LimpiarWhatsApp(null));
        Assert.Null(Entrenador.LimpiarEmail("  "));
        Assert.Null(Entrenador.LimpiarWhatsApp("  "));

        var conContacto = new Entrenador("Ana López", null, null, "ana@gym.com", "+54 11 5555-1234");
        Assert.Equal("ana@gym.com", conContacto.Email);
        Assert.Equal("+54 11 5555-1234", conContacto.WhatsApp);
    }

    [Fact]
    public void El_email_se_recorta_y_tiene_que_tener_forma_de_email()
    {
        Assert.Equal("ana@gym.com", Entrenador.LimpiarEmail("  ana@gym.com "));
        Assert.Equal("ana.lopez+gym@mail.gym.com.ar", Entrenador.LimpiarEmail("ana.lopez+gym@mail.gym.com.ar"));
        Assert.Equal("Ana@Gym.com", Entrenador.LimpiarEmail("Ana@Gym.com"));   // las mayúsculas se conservan como se escribieron

        Assert.Equal(254, Entrenador.LargoMaximoDelEmail);
        var de254 = new string('a', 246) + "@gym.com";
        var de255 = new string('a', 247) + "@gym.com";
        Assert.Equal((254, 255), (de254.Length, de255.Length));
        Assert.Equal(de254, Entrenador.LimpiarEmail(de254));

        foreach (var invalido in new[]
                 {
                     "sin-arroba", "a b@c.com", "a@b", "@c.com", "ana@", "ana@@gym.com", "ana@gym..", "ana@.com.", "ana@gym..com",
                     "ana\t@gym.com", "ana@gym\n.com", "ana@gym\u00A0.com", "ana@gym.com ana@gym.com", de255,
                 })
        {
            Assert.Throws<ArgumentException>(() => Entrenador.LimpiarEmail(invalido));
            Assert.Throws<ArgumentException>(() => new Entrenador("Ana López", null, null, invalido, null));
        }
    }

    [Fact]
    public void El_mensaje_del_email_invalido_se_entiende()
    {
        var error = Assert.Throws<ArgumentException>(() => Entrenador.LimpiarEmail("sin-arroba"));
        Assert.Contains("email", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("\u00A0")]     // espacio duro: el que trae pegar desde WhatsApp
    [InlineData("\u202F")]     // espacio duro angosto: el que ponen algunos teléfonos al copiar un número
    [InlineData("\u00A0\u00A0")]
    [InlineData(" \u00A0 \u202F ")]
    public void El_WhatsApp_pegado_con_espacios_duros_se_guarda_con_espacios_comunes(string espacio)
    {
        Assert.Equal("+54 11 5555-1234", Entrenador.LimpiarWhatsApp($"+54{espacio}11{espacio}5555-1234"));
        Assert.Equal("+54 11 5555-1234", Entrenador.LimpiarWhatsApp($"{espacio}+54{espacio}11{espacio}5555-1234{espacio}"));
        Assert.Equal("+54 11 5555-1234", new Entrenador("Ana", null, null, null, $"+54{espacio}11{espacio}5555-1234").WhatsApp);
    }

    [Fact]
    public void El_WhatsApp_con_espacios_repetidos_queda_con_uno_solo()
        => Assert.Equal("+54 11 5555-1234", Entrenador.LimpiarWhatsApp("  +54   11    5555-1234  "));

    [Theory]
    [InlineData("abc")]
    [InlineData("11 5555-1234 int. 5")]
    [InlineData("11.5555.1234")]
    [InlineData("11 5555\n1234")]
    [InlineData("+54 11 5555-1234 ext")]
    [InlineData("+")]            // signos solos: no hay un número al que escribirle
    [InlineData("( ) - +")]
    public void El_WhatsApp_solo_admite_digitos_espacios_y_signos(string whatsApp)
    {
        Assert.Throws<ArgumentException>(() => Entrenador.LimpiarWhatsApp(whatsApp));
        Assert.Throws<ArgumentException>(() => new Entrenador("Ana", null, null, null, whatsApp));
    }

    [Fact]
    public void El_WhatsApp_admite_parentesis_guiones_y_el_signo_mas_y_tiene_su_tope()
    {
        Assert.Equal("(011) 15 5555-1234", Entrenador.LimpiarWhatsApp("(011) 15 5555-1234"));
        Assert.Equal("+5491155551234", Entrenador.LimpiarWhatsApp("+5491155551234"));

        Assert.Equal(30, Entrenador.LargoMaximoDelWhatsApp);
        Assert.Equal(new string('1', 30), Entrenador.LimpiarWhatsApp(new string('1', 30)));
        Assert.Throws<ArgumentException>(() => Entrenador.LimpiarWhatsApp(new string('1', 31)));
    }

    [Fact]
    public void ActualizarDatos_guarda_el_contacto_y_lo_puede_borrar()
    {
        var entrenador = new Entrenador("Ana López", "111", "Yoga", "ana@gym.com", "11 5555-1234");

        entrenador.ActualizarDatos("Ana López", "111", "Yoga", "  ana.lopez@gym.com ", "+54 9 11 5555-1234");
        Assert.Equal("ana.lopez@gym.com", entrenador.Email);
        Assert.Equal("+54 9 11 5555-1234", entrenador.WhatsApp);

        entrenador.ActualizarDatos("Ana López", "111", "Yoga", "", null);
        Assert.Null(entrenador.Email);
        Assert.Null(entrenador.WhatsApp);
    }

    [Fact]
    public void ActualizarDatos_sin_contacto_deja_el_contacto_en_blanco_como_lo_manda_el_pedido()
    {
        // Los parámetros nuevos son opcionales para que lo que ya llama a estos métodos siga compilando; omitirlos es mandarlos vacíos.
        var entrenador = new Entrenador("Ana López", null, null, "ana@gym.com", "11 5555-1234");
        entrenador.ActualizarDatos("Ana López", null, null);
        Assert.Null(entrenador.Email);
        Assert.Null(entrenador.WhatsApp);
    }

    [Fact]
    public void ActualizarDatos_con_un_email_invalido_no_cambia_nada()
    {
        var entrenador = new Entrenador("Ana López", "111", "Yoga", "ana@gym.com", "11 5555-1234");

        Assert.Throws<ArgumentException>(() => entrenador.ActualizarDatos("Beto Pérez", "222", "Pilates", "sin-arroba", "11 0000-0000"));
        Assert.Throws<ArgumentException>(() => entrenador.ActualizarDatos("Beto Pérez", "222", "Pilates", "beto@gym.com", "abc"));

        Assert.Equal("Ana López", entrenador.NombreCompleto);
        Assert.Equal("111", entrenador.Telefono);
        Assert.Equal("Yoga", entrenador.Especialidad);
        Assert.Equal("ana@gym.com", entrenador.Email);
        Assert.Equal("11 5555-1234", entrenador.WhatsApp);
    }

    // ---------- valor de la hora ----------

    [Fact]
    public void El_valor_de_la_hora_es_opcional_y_admite_cero_y_dos_decimales()
    {
        Assert.Null(new Entrenador("Ana López", null, null).ValorHora);
        Assert.Null(new Entrenador("Ana López", null, null, null, null, null).ValorHora);
        Assert.Equal(0m, new Entrenador("Ana López", null, null, null, null, 0m).ValorHora);
        Assert.Equal(1500.50m, new Entrenador("Ana López", null, null, null, null, 1500.50m).ValorHora);
        Assert.Equal(999_999_999_999m, new Entrenador("Ana López", null, null, null, null, 999_999_999_999m).ValorHora);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1000000000000")]
    [InlineData("1.234")]
    public void Un_valor_de_hora_negativo_enorme_o_con_tres_decimales_se_rechaza(string valor)
    {
        var numero = decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<ArgumentException>(() => new Entrenador("Ana López", null, null, null, null, numero));
    }

    [Fact]
    public void ActualizarDatos_cambia_el_valor_de_la_hora_y_sin_valor_lo_borra()
    {
        var entrenador = new Entrenador("Ana López", null, null, null, null, 1000m);

        entrenador.ActualizarDatos("Ana López", null, null, null, null, 1500m);
        Assert.Equal(1500m, entrenador.ValorHora);

        entrenador.ActualizarDatos("Ana López", null, null);   // el valor ausente es «sin valor»
        Assert.Null(entrenador.ValorHora);
    }

    [Fact]
    public void ActualizarDatos_con_un_valor_de_hora_invalido_no_cambia_nada()
    {
        var entrenador = new Entrenador("Ana López", "111", "Yoga", "ana@gym.com", "11 5555-1234", 1000m);

        Assert.Throws<ArgumentException>(() => entrenador.ActualizarDatos("Beto Pérez", "222", "Pilates", "beto@gym.com", "11 0000-0000", -5m));
        Assert.Throws<ArgumentException>(() => entrenador.ActualizarDatos("Beto Pérez", "222", "Pilates", "beto@gym.com", "11 0000-0000", 1.234m));

        Assert.Equal(("Ana López", "111", "Yoga", "ana@gym.com", "11 5555-1234", (decimal?)1000m),
            (entrenador.NombreCompleto, entrenador.Telefono, entrenador.Especialidad, entrenador.Email, entrenador.WhatsApp, entrenador.ValorHora));
    }
}
