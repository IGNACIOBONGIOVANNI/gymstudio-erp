using System;
using EstudioContable.Domain;
using EstudioContable.Domain.Entities;
using Xunit;

namespace EstudioContable.Domain.Tests;

/// <summary>Nombre de persona con solo letras y teléfono con código de país: la regla y los tres lugares donde vale.</summary>
public class ReglasDePersonasTests
{
    [Theory]
    [InlineData("Ana")]
    [InlineData("María José")]
    [InlineData("Pérez-Gómez")]
    [InlineData("D'Angelo")]
    [InlineData("O’Connor")]
    [InlineData("Lic. Roberto Gómez")]
    [InlineData("Ñandú Güemes")]
    [InlineData("Åsa Öberg")]
    public void Un_nombre_con_letras_espacios_puntos_apostrofos_y_guiones_vale(string nombre)
        => Assert.Equal(nombre, ReglasDePersonas.NombreDePersona(nombre, "El nombre", 100));

    [Theory]
    [InlineData("Ana 2")]
    [InlineData("Ana1")]
    [InlineData("4na")]
    [InlineData("Ana@gym")]
    [InlineData("Ana_López")]
    [InlineData("Ana/López")]
    [InlineData("Ana (hija)")]
    [InlineData("<b>Ana</b>")]
    [InlineData("Ana; DROP TABLE")]
    [InlineData("-Ana")]
    [InlineData(".Ana")]
    [InlineData("'Ana")]
    [InlineData("Ana😀")]
    [InlineData("١٢٣")]            // dígitos árabes: son números aunque no sean 0-9
    public void Un_nombre_con_numeros_o_simbolos_se_rechaza(string nombre)
    {
        var error = Assert.Throws<ArgumentException>(() => ReglasDePersonas.NombreDePersona(nombre, "El nombre", 100));
        Assert.Equal("El nombre solo puede tener letras, espacios, puntos, apóstrofos y guiones.", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_nombre_vacio_se_rechaza_como_requerido(string? nombre)
    {
        var error = Assert.Throws<ArgumentException>(() => ReglasDePersonas.NombreDePersona(nombre, "El nombre", 100));
        Assert.Equal("El nombre es requerido.", error.Message);
    }

    [Fact]
    public void Un_nombre_se_guarda_sin_espacios_sobrantes_y_con_espacios_comunes()
    {
        Assert.Equal("Ana López", ReglasDePersonas.NombreDePersona("  Ana   López  ", "El nombre", 100));
        Assert.Equal("Ana López", ReglasDePersonas.NombreDePersona("Ana" + (char)0x00A0 + "López", "El nombre", 100));
        Assert.Equal("Ana López", ReglasDePersonas.NombreDePersona("Ana" + (char)0x3000 + "López", "El nombre", 100));
        Assert.Equal("Ana López", ReglasDePersonas.NombreDePersona("Ana" + (char)0x2028 + "López", "El nombre", 100));
    }

    [Fact]
    public void Un_nombre_con_tabulador_o_salto_de_linea_se_rechaza()
    {
        Assert.Throws<ArgumentException>(() => ReglasDePersonas.NombreDePersona("Ana\tLópez", "El nombre", 100));
        Assert.Throws<ArgumentException>(() => ReglasDePersonas.NombreDePersona("Ana\nLópez", "El nombre", 100));
    }

    [Fact]
    public void Un_nombre_tiene_su_tope_de_largo()
    {
        Assert.Equal(new string('a', 100), ReglasDePersonas.NombreDePersona(new string('a', 100), "El nombre", 100));
        Assert.Throws<ArgumentException>(() => ReglasDePersonas.NombreDePersona(new string('a', 101), "El nombre", 100));
    }

    [Fact]
    public void Un_nombre_con_letras_combinadas_vale()
    {
        // «é» como «e» + tilde suelta (lo que producen algunos teclados y copiar desde un Mac)
        var descompuesto = "Jose" + (char)0x0301 + " Pe" + (char)0x0301 + "rez";
        Assert.Equal(descompuesto, ReglasDePersonas.NombreDePersona(descompuesto, "El nombre", 100));
    }

    [Theory]
    [InlineData("+54 1155551234")]
    [InlineData("+54 91155551234")]
    [InlineData("+1 2125551234")]
    [InlineData("+598 91234567")]
    [InlineData("+34 612345678")]
    [InlineData("+971 501234567")]
    [InlineData("+54 1234")]                // el número más corto que se acepta: 4 dígitos
    [InlineData("+7 12345678901234")]       // 1 + 14 = 15 dígitos: el máximo internacional
    public void Un_telefono_con_codigo_de_pais_y_solo_digitos_vale(string telefono)
        => Assert.Equal(telefono, ReglasDePersonas.Telefono(telefono, "El teléfono"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_telefono_vacio_es_null(string? telefono)
        => Assert.Null(ReglasDePersonas.Telefono(telefono, "El teléfono"));

    [Theory]
    [InlineData("1155551234")]              // sin código de país
    [InlineData("+541155551234")]           // sin el espacio entre código y número
    [InlineData("+54 11 5555-1234")]        // con espacios y guion dentro del número
    [InlineData("+54 11 55551234")]
    [InlineData("+54 (11) 55551234")]
    [InlineData("+54 11.5555.1234")]
    [InlineData("54 1155551234")]           // sin el +
    [InlineData("+ 54 1155551234")]
    [InlineData("++54 1155551234")]
    [InlineData("+54 123")]                 // número demasiado corto
    [InlineData("+54 123456789012345")]     // número de 15 dígitos: con el código pasa los 15
    [InlineData("+7 123456789012345")]
    [InlineData("+598 12345678901234")]     // 3 + 14 = 17 dígitos: pasa el máximo internacional de 15
    [InlineData("+5444 1155551234")]        // código de 4 dígitos
    [InlineData("+ 1155551234")]
    [InlineData("+54 11a5551234")]
    [InlineData("+54 1155551234 int 5")]
    [InlineData("+54 ١٢٣٤٥٦٧٨")]            // dígitos árabes
    [InlineData("+54\n1155551234")]
    [InlineData("hola")]
    public void Un_telefono_mal_armado_se_rechaza(string telefono)
    {
        var error = Assert.Throws<ArgumentException>(() => ReglasDePersonas.Telefono(telefono, "El teléfono"));
        Assert.Equal("El teléfono tiene que llevar el código del país y solo números, por ejemplo +54 1155551234.", error.Message);
    }

    [Fact]
    public void Un_telefono_pegado_con_espacios_duros_se_guarda_con_un_espacio_comun()
    {
        Assert.Equal("+54 1155551234", ReglasDePersonas.Telefono("+54" + (char)0x00A0 + "1155551234", "El teléfono"));
        Assert.Equal("+54 1155551234", ReglasDePersonas.Telefono("  +54   1155551234 " + (char)0x00A0, "El teléfono"));
    }

    [Fact]
    public void El_mensaje_del_telefono_nombra_el_campo_que_se_pidio()
    {
        var error = Assert.Throws<ArgumentException>(() => ReglasDePersonas.Telefono("11", "El WhatsApp"));
        Assert.StartsWith("El WhatsApp tiene que llevar", error.Message);
    }

    // ---- Dónde vale: entrenadores, socios y usuarios ----

    [Fact]
    public void Entrenador_rechaza_nombre_con_numeros_y_telefono_o_whatsapp_sin_codigo()
    {
        Assert.Throws<ArgumentException>(() => new Entrenador("Ana 2", null, null));
        Assert.Throws<ArgumentException>(() => new Entrenador("Ana", "11 5555-1234", null));
        Assert.Throws<ArgumentException>(() => new Entrenador("Ana", null, null, null, "1155551234"));

        var ok = new Entrenador("Ana López", "+54 1155551234", null, null, "+54 91155551234");
        Assert.Equal("+54 1155551234", ok.Telefono);
        Assert.Equal("+54 91155551234", ok.WhatsApp);
    }

    [Fact]
    public void Entrenador_rechaza_nombre_con_simbolos_al_actualizar()
    {
        var entrenador = new Entrenador("Ana López", null, null);
        Assert.Throws<ArgumentException>(() => entrenador.ActualizarDatos("Ana López 3", null, null));
        Assert.Equal("Ana López", entrenador.NombreCompleto);
    }

    [Fact]
    public void Socio_rechaza_nombre_o_apellido_con_numeros_y_telefono_sin_codigo()
    {
        var alta = new DateTime(2026, 1, 1);
        Assert.Throws<ArgumentException>(() => new Socio("Ana1", "Gómez", "12345678", null, alta));
        Assert.Throws<ArgumentException>(() => new Socio("Ana", "Gómez 2", "12345678", null, alta));
        Assert.Throws<ArgumentException>(() => new Socio("Ana", "Gómez", "12345678", "11 5555-1234", alta));

        var sin = new Socio("Ana", "Gómez", "12345678", null, alta);
        Assert.Equal(string.Empty, sin.Telefono);
        var con = new Socio("Ana", "Gómez", "12345678", "+54 1155551234", alta);
        Assert.Equal("+54 1155551234", con.Telefono);
    }

    [Fact]
    public void Socio_rechaza_datos_invalidos_al_actualizar_y_no_queda_a_medias()
    {
        var socio = new Socio("Ana", "Gómez", "12345678", "+54 1155551234", new DateTime(2026, 1, 1));
        Assert.Throws<ArgumentException>(() => socio.ActualizarDatos("Ana", "Gómez!", "12345678", "+54 1155551234"));
        Assert.Throws<ArgumentException>(() => socio.ActualizarDatos("Ana", "Gómez", "12345678", "1155551234"));
        Assert.Equal(("Ana", "Gómez", "+54 1155551234"), (socio.Nombre, socio.Apellido, socio.Telefono));
    }

    [Fact]
    public void Usuario_rechaza_nombre_con_numeros_y_telefono_sin_codigo()
    {
        Assert.Throws<ArgumentException>(() => new Usuario("Jefe 2", "a@b.com", "-", RolUsuario.Recepcion));
        Assert.Throws<ArgumentException>(() => new Usuario("Jefe", "a@b.com", "-", RolUsuario.Recepcion, telefono: "11 5555"));

        var sin = new Usuario("Jefe", "a@b.com", "-", RolUsuario.Recepcion);
        Assert.Null(sin.Telefono);
        var con = new Usuario("Lic. Roberto Gómez", "a@b.com", "-", RolUsuario.Contador, telefono: "+54 1155550202");
        Assert.Equal("+54 1155550202", con.Telefono);
    }

    [Fact]
    public void Usuario_rechaza_datos_invalidos_al_actualizar_y_no_queda_a_medias()
    {
        var usuario = new Usuario("Jefe", "a@b.com", "-", RolUsuario.Recepcion, telefono: "+54 1155550202");
        Assert.Throws<ArgumentException>(() => usuario.ActualizarDatos("Jefe 9", "a@b.com", RolUsuario.Recepcion, "+54 1155550202"));
        Assert.Throws<ArgumentException>(() => usuario.ActualizarDatos("Jefe", "a@b.com", RolUsuario.Recepcion, "abc"));
        Assert.Equal(("Jefe", "+54 1155550202"), (usuario.NombreCompleto, usuario.Telefono));
    }
}
