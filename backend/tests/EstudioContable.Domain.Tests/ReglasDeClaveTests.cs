using EstudioContable.Domain;

namespace EstudioContable.Domain.Tests;

public class ReglasDeClaveTests
{
    [Theory]
    [InlineData("Clave-De-Prueba-17!")]
    [InlineData("Abcdef1!")]            // justo 8
    [InlineData("Zeta#19Zeta")]         // números separados por letras
    [InlineData("Hola.Mundo.2")]
    [InlineData("Mi clave 7 segura!")]  // con espacios
    [InlineData("Ñandú#4Ñandú")]        // mayúscula con tilde y eñe
    [InlineData("Aa1!1111")]            // repetidos no son consecutivos
    [InlineData("Ab!1x3y5z")]           // 1, 3 y 5 no son consecutivos
    public void Las_claves_que_cumplen_la_politica_se_aceptan(string clave)
    {
        Assert.Empty(ReglasDeClave.Problemas(clave));
        ReglasDeClave.Validar(clave);
    }

    [Theory]
    [InlineData(null, "al menos 8 caracteres")]
    [InlineData("", "al menos 8 caracteres")]
    [InlineData("Ab1!", "al menos 8 caracteres")]
    [InlineData("abcdef1!", "mayúscula")]
    [InlineData("ABCDEF1!", null)]      // sin minúscula: la política no la pide
    [InlineData("Abcdefg!", "un número")]
    [InlineData("Abcdefg1", "carácter especial")]
    [InlineData("Abc defg 1", "carácter especial")]   // el espacio no cuenta como especial
    [InlineData("Abcd12!x", "consecutivos")]
    [InlineData("Abcd21!x", "consecutivos")]
    [InlineData("Abcd98!x", "consecutivos")]
    [InlineData("Abcd7890!", "consecutivos")]          // 7-8 y 8-9
    [InlineData("Abcd\u0001ef1!", "control")]
    public void Las_claves_que_no_cumplen_dicen_que_les_falta(string? clave, string? falta)
    {
        var problemas = ReglasDeClave.Problemas(clave);
        if (falta == null)
        {
            Assert.Empty(problemas);
            return;
        }
        Assert.Contains(problemas, p => p.Contains(falta));
        var ex = Assert.Throws<ArgumentException>(() => ReglasDeClave.Validar(clave));
        Assert.Contains(falta, ex.Message);
    }

    [Fact]
    public void Un_largo_que_el_inicio_de_sesion_no_aceptaria_se_rechaza()
    {
        var clave = "A1!x" + new string('x', ReglasDeClave.LargoMaximo);
        Assert.Contains(ReglasDeClave.Problemas(clave), p => p.Contains("como máximo"));
        Assert.Empty(ReglasDeClave.Problemas("A1!x" + new string('x', ReglasDeClave.LargoMaximo - 4)));
    }

    [Fact]
    public void Numeros_de_otros_alfabetos_no_cuentan_como_numero()
    {
        // "٣" (tres arábigo-índico) es un dígito Unicode, pero no sirve como el número que exige la política.
        Assert.Contains(ReglasDeClave.Problemas("Abcdef٣!"), p => p.Contains("un número"));
    }

    [Fact]
    public void Todos_los_problemas_se_informan_juntos()
    {
        // Corta, sin mayúscula, sin número y sin carácter especial: la persona ve todo lo que le falta de una vez.
        Assert.Equal(4, ReglasDeClave.Problemas("abc").Count);
    }
}
