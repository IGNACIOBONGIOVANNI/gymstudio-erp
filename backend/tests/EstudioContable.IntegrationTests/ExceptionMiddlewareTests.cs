using System.Text.Json;
using EstudioContable.API.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Los pedidos que el servidor corta antes de llegar al controlador (un cuerpo más grande que el tope de la ruta, por ejemplo la foto de
/// un entrenador) no son un error del sistema: se contestan con su propio código y un mensaje claro, no con un 500. Kestrel lanza
/// <see cref="BadHttpRequestException"/> al leer el cuerpo; el servidor de pruebas (TestServer) no aplica ese tope, por eso se prueba
/// el middleware directamente.
/// </summary>
public class ExceptionMiddlewareTests
{
    private static async Task<(int Estado, string Titulo, string Detalle)> Contestar(Exception excepcion)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(_ => throw excepcion, NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);

        contexto.Response.Body.Position = 0;
        using var json = JsonDocument.Parse(await new StreamReader(contexto.Response.Body).ReadToEndAsync());
        return (contexto.Response.StatusCode, json.RootElement.GetProperty("title").GetString()!, json.RootElement.GetProperty("detail").GetString()!);
    }

    [Fact]
    public async Task Un_cuerpo_mas_grande_que_el_tope_da_413_con_un_mensaje_claro_y_no_un_error_interno()
    {
        var (estado, titulo, detalle) = await Contestar(new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, estado);
        Assert.Contains("demasiado grande", titulo + " " + detalle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base de datos", detalle);   // el texto del error interno
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status408RequestTimeout)]
    public async Task Otro_pedido_mal_formado_conserva_su_codigo_en_vez_de_un_500(int codigo)
    {
        var (estado, _, detalle) = await Contestar(new BadHttpRequestException("Unexpected end of request content.", codigo));

        Assert.Equal(codigo, estado);
        Assert.False(string.IsNullOrWhiteSpace(detalle));
        Assert.DoesNotContain("Unexpected end", detalle);   // no se muestra el texto interno de Kestrel
    }

    [Fact]
    public async Task Un_error_cualquiera_sigue_dando_500_sin_mostrar_detalles()
    {
        var (estado, _, detalle) = await Contestar(new Exception("detalle secreto del servidor"));

        Assert.Equal(StatusCodes.Status500InternalServerError, estado);
        Assert.DoesNotContain("secreto", detalle);
    }
}
