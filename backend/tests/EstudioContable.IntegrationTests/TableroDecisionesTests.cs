using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static EstudioContable.IntegrationTests.BasesDePrueba;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Decisiones del Tablero de Gestión: socios para contactar (no vienen o les vence la cuota) y la base del simulador de precios.
/// Usa «hoy» = 10/06/2030 y socios con vencimientos puestos por SQL en esa fecha, que ningún otro dato alcanza.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class TableroDecisionesTests(ApiFixture api)
{
    private const string Hoy = "2030-06-10";

    private static async Task<long> Id(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private async Task<long> Socio(HttpClient jefe, long plan, string vence, string? ultimaVisita)
    {
        var id = await Id(await jefe.PostAsJsonAsync("/api/socios", new
        {
            nombre = "Dora", apellido = "Decision " + Nombres.Aleatorio(10),
            dni = Random.Shared.Next(20_000_000, 49_999_999).ToString(), telefono = "+54 1155550000"
        }));
        await Ejecutar(api.ConnectionString, $"""
            UPDATE contabilidad."Socios" SET "FechaVencimiento" = '{vence}T00:00:00Z', "PlanActualId" = {plan}, "Estado" = 'Activo' WHERE "Id" = {id};
            """);
        if (ultimaVisita is not null)
            await Ejecutar(api.ConnectionString, $"""
                INSERT INTO contabilidad."AsistenciasSocio" ("SocioId", "Fecha", "RegistradaEn", "RegistradaPor")
                VALUES ({id}, '{ultimaVisita}', '{ultimaVisita}T15:00:00Z', 'prueba');
                """);
        return id;
    }

    [Fact]
    public async Task Lista_a_quien_contactar_y_por_que_sin_el_dni()
    {
        var jefe = await api.Jefe();
        var plan = await Id(await jefe.PostAsJsonAsync("/api/planes", new { nombre = "Plan Decisiones " + Nombres.Aleatorio(8), precioMensual = 30000, duracionMeses = 3 }));
        var porVencer = await Socio(jefe, plan, "2030-06-13", ultimaVisita: "2030-06-09");
        var noViene = await Socio(jefe, plan, "2030-07-30", ultimaVisita: "2030-05-01");

        var respuesta = await jefe.GetAsync($"/api/tablero/decisiones?hoy={Hoy}");
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.DoesNotContain("\"dni\"", texto);
        var d = JsonNode.Parse(texto)!;
        var socios = d["socios"]!.AsArray();

        var a = socios.Single(x => (long)x!["id"]! == porVencer)!;
        Assert.Equal("por-vencer", (string?)a["motivo"]);
        Assert.Equal(3, (int)a["dias"]!);
        Assert.False(string.IsNullOrWhiteSpace((string?)a["accion"]));
        Assert.StartsWith("Plan Decisiones", (string?)a["plan"]);

        var b = socios.Single(x => (long)x!["id"]! == noViene)!;
        Assert.Equal("no-viene", (string?)b["motivo"]);
        Assert.Equal(40, (int)b["dias"]!);

        var p = d["planes"]!.AsArray().Single(x => (long)x!["id"]! == plan)!;
        Assert.Equal(2, (int)p["sociosVigentes"]!);
        Assert.Equal(20000m, (decimal)p["recaudacionMensual"]!);
    }

    [Theory]
    [InlineData("/api/tablero/indicadores")]
    [InlineData("/api/tablero/analisis")]
    [InlineData("/api/tablero/decisiones")]
    public async Task Recepcion_no_puede_ver_el_tablero(string ruta)
    {
        var r = await (await api.Recepcion()).GetAsync(ruta);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task El_Contador_puede_ver_el_tablero()
    {
        var contador = await api.Contador();
        Assert.Equal(HttpStatusCode.OK, (await contador.GetAsync($"/api/tablero/decisiones?hoy={Hoy}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await contador.GetAsync("/api/tablero/indicadores")).StatusCode);
    }
}
