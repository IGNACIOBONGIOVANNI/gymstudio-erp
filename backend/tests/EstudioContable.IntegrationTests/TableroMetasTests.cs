using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Metas del Tablero de Gestión: las carga solo el Administrador, se validan contra el catálogo de indicadores
/// y quedan en la auditoría. Las metas son de todo el sistema: cada prueba borra la que puso.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class TableroMetasTests(ApiFixture api)
{
    private static Task<HttpResponseMessage> Guardar(HttpClient c, string clave, decimal? valor)
        => c.PutAsJsonAsync($"/api/tablero/metas/{clave}", new { valor });

    private static async Task<JsonObject?> Meta(HttpClient c, string clave)
        => (await c.GetJson("/api/tablero/metas")).AsArray().Select(x => x!.AsObject()).FirstOrDefault(x => (string?)x["clave"] == clave);

    [Fact]
    public async Task El_Administrador_guarda_una_meta_y_la_puede_borrar()
    {
        var jefe = await api.Jefe();
        try
        {
            var r = await Guardar(jefe, "socios-activos", 120);
            Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
            var meta = await Meta(jefe, "socios-activos");
            Assert.NotNull(meta);
            Assert.Equal(120m, (decimal)meta["valor"]!);
            Assert.Equal("jefe@gym.com", (string?)meta["actualizadoPor"]);

            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, "socios-activos", 150)).StatusCode);
            Assert.Equal(150m, (decimal)(await Meta(jefe, "socios-activos"))!["valor"]!);

            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, "socios-activos", null)).StatusCode);
            Assert.Null(await Meta(jefe, "socios-activos"));
        }
        finally { await Guardar(jefe, "socios-activos", null); }
    }

    [Theory]
    [InlineData("nada", 10)]
    [InlineData("socios-activos", -1)]
    [InlineData("retencion", 1001)]
    [InlineData("liquidez", 1000)]   // la liquidez es una foto: no lleva meta
    public async Task Una_meta_invalida_se_rechaza_con_400(string clave, int valor)
    {
        var jefe = await api.Jefe();
        var r = await Guardar(jefe, clave, valor);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Null(await Meta(jefe, clave));
    }

    [Fact]
    public async Task El_Contador_ve_las_metas_pero_no_las_cambia_y_Recepcion_no_las_ve()
    {
        var contador = await api.Contador();
        Assert.Equal(HttpStatusCode.Forbidden, (await Guardar(contador, "ingresos", 1000)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await contador.GetAsync("/api/tablero/metas")).StatusCode);

        var recepcion = await api.Recepcion();
        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.GetAsync("/api/tablero/metas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Guardar(recepcion, "ingresos", 1000)).StatusCode);
    }

    [Fact]
    public async Task Cambiar_una_meta_queda_en_la_auditoria()
    {
        var jefe = await api.Jefe();
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, "margen", 35)).StatusCode);
            var registros = (await jefe.GetJson("/api/auditoria?tabla=contabilidad.MetasDeIndicadores&cantidad=50")).AsArray();
            Assert.Contains(registros, x => (string?)x!["operacion"] == "INSERT" && (string?)x["usuario"] == "jefe@gym.com");
        }
        finally { await Guardar(jefe, "margen", null); }
    }
}
