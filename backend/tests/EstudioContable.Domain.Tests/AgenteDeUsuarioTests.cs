using EstudioContable.Domain;

namespace EstudioContable.Domain.Tests;

public class AgenteDeUsuarioTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36", "Windows 10/11", "Chrome 141")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36 Edg/141.0.3537.57", "Windows 10/11", "Edge 141")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0", "Windows 10/11", "Firefox 132")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 OPR/115.0.0.0", "Windows 10/11", "Opera 115")]
    [InlineData("Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36", "Windows 7", "Chrome 109")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15", "macOS", "Safari 17")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64; rv:132.0) Gecko/20100101 Firefox/132.0", "Linux", "Firefox 132")]
    [InlineData("Mozilla/5.0 (X11; CrOS x86_64 14541.0.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36", "ChromeOS", "Chrome 120")]
    public void Reconoce_el_sistema_y_el_navegador_de_los_equipos_comunes(string agente, string sistema, string navegador)
    {
        var (so, nav) = AgenteDeUsuario.Analizar(agente);
        Assert.Equal(sistema, so);
        Assert.Equal(navegador, nav);
    }

    [Theory]
    // Estos agentes también dicen "Linux" o "Mac OS X": gana el sistema móvil.
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Mobile Safari/537.36", "Android 14", "Chrome 141")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1", "iOS 17", "Safari 17")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/141.0.0.0 Mobile/15E148 Safari/604.1", "iOS 17", "Chrome 141")]
    public void En_los_celulares_gana_el_sistema_movil(string agente, string sistema, string navegador)
    {
        var (so, nav) = AgenteDeUsuario.Analizar(agente);
        Assert.Equal(sistema, so);
        Assert.Equal(navegador, nav);
    }

    [Theory]
    [InlineData("curl/8.4.0", "curl 8")]
    [InlineData("PostmanRuntime/7.39.0", "Postman")]
    [InlineData("python-requests/2.32.3", "python-requests")]
    public void Las_herramientas_que_no_son_navegadores_se_nombran_como_tales(string agente, string navegador)
    {
        var (so, nav) = AgenteDeUsuario.Analizar(agente);
        Assert.Equal(AgenteDeUsuario.Desconocido, so);
        Assert.Equal(navegador, nav);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("algo que no es un agente")]
    public void Un_agente_vacio_o_irreconocible_es_desconocido_y_nunca_null(string? agente)
    {
        var (so, nav) = AgenteDeUsuario.Analizar(agente);
        Assert.Equal(AgenteDeUsuario.Desconocido, so);
        Assert.Equal(AgenteDeUsuario.Desconocido, nav);
    }

    [Fact]
    public void Un_agente_hostil_no_rompe_ni_cuela_caracteres_de_control_ni_texto_enorme()
    {
        var hostil = "Mozilla/5.0 (Windows NT 10.0)\r\nSet-Cookie: x=1\u0000" + new string('A', 100_000) + " Chrome/141.0";
        var (so, nav) = AgenteDeUsuario.Analizar(hostil);
        Assert.Equal("Windows 10/11", so);
        Assert.True(nav.Length <= AgenteDeUsuario.LargoMaximoDelTexto);
        Assert.DoesNotContain(so + nav, "\r\n");

        // Un agente que se arma con texto del atacante no puede producir un nombre largo ni con control.
        var (so2, nav2) = AgenteDeUsuario.Analizar("Chrome/" + new string('9', 5000));
        Assert.True(so2.Length <= AgenteDeUsuario.LargoMaximoDelTexto && nav2.Length <= AgenteDeUsuario.LargoMaximoDelTexto);
        Assert.Equal("Chrome 9999", nav2);   // la versión se corta en cuatro cifras
    }

    [Theory]
    [InlineData("192.168.1.50", "192.168.1.50")]
    [InlineData("  10.0.0.7 ", "10.0.0.7")]
    [InlineData("::1", "::1")]
    [InlineData("::ffff:172.18.0.1", "172.18.0.1")]            // IPv4 dentro de IPv6: se guarda como IPv4
    [InlineData("2001:db8::ff00:42:8329", "2001:db8::ff00:42:8329")]
    [InlineData("fe80::1%eth0", "fe80::1")]                      // sin la zona del equipo local
    public void La_ip_se_guarda_en_su_forma_canonica(string entrada, string esperada)
        => Assert.Equal(esperada, AgenteDeUsuario.NormalizarIp(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no es una ip")]
    [InlineData("1.2.3.4:8080")]
    [InlineData("1.2.3.4, 5.6.7.8")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("1.2.3.4'; DROP TABLE auditoria;--")]
    [InlineData("999.1.1.1")]
    public void Lo_que_no_es_una_ip_no_se_guarda(string? entrada)
        => Assert.Null(AgenteDeUsuario.NormalizarIp(entrada));
}
