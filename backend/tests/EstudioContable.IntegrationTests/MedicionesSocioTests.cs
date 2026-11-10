using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>Seguimiento de peso y medidas de los socios, contra la API y la base reales.</summary>
[Collection(ColeccionApi.Nombre)]
public class MedicionesSocioTests(ApiFixture api)
{
    private async Task<long> Socio(HttpClient cliente)
    {
        var dni = Random.Shared.Next(20_000_000, 49_999_999).ToString();
        var r = await cliente.PostAsJsonAsync("/api/socios", new { nombre = "Socio", apellido = $"Medido {Nombres.Aleatorio(6)}", dni, telefono = "" });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private static object Medicion(string fecha, decimal peso, decimal? grasa = null, decimal? cintura = null)
        => new { fecha, pesoKg = peso, grasaCorporalPct = grasa, cinturaCm = cintura, notas = "en ayunas" };

    [Fact]
    public async Task Las_mediciones_se_listan_ordenadas_por_fecha_con_sus_valores()
    {
        var jefe = await api.Jefe();
        var socio = await Socio(jefe);
        Assert.True((await jefe.PostAsJsonAsync($"/api/socios/{socio}/mediciones", Medicion("2026-09-15", 82.5m, 24.5m, 94m))).IsSuccessStatusCode);
        Assert.True((await jefe.PostAsJsonAsync($"/api/socios/{socio}/mediciones", Medicion("2026-08-01", 85m))).IsSuccessStatusCode);
        Assert.True((await jefe.PostAsJsonAsync($"/api/socios/{socio}/mediciones", Medicion("2026-10-10", 80.2m, cintura: 90m))).IsSuccessStatusCode);

        var lista = (await jefe.GetJson($"/api/socios/{socio}/mediciones")).AsArray();

        Assert.Equal(new[] { "2026-08-01", "2026-09-15", "2026-10-10" }, lista.Select(m => (string)m!["fecha"]!));
        Assert.Equal(new[] { 85m, 82.5m, 80.2m }, lista.Select(m => (decimal)m!["pesoKg"]!));
        Assert.Equal(24.5m, (decimal)lista[1]!["grasaCorporalPct"]!);
        Assert.Null(lista[0]!["grasaCorporalPct"]);                // lo que no se midió queda vacío
        Assert.Equal("en ayunas", (string)lista[0]!["notas"]!);
    }

    [Fact]
    public async Task No_se_puede_cargar_dos_veces_el_mismo_dia_y_editar_corrige_la_existente()
    {
        var jefe = await api.Jefe();
        var socio = await Socio(jefe);
        var primera = await jefe.PostAsJsonAsync($"/api/socios/{socio}/mediciones", Medicion("2026-10-01", 90m));
        var id = (long)(await primera.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;

        var repetida = await jefe.PostAsJsonAsync($"/api/socios/{socio}/mediciones", Medicion("2026-10-01", 91m));
        Assert.Equal(HttpStatusCode.BadRequest, repetida.StatusCode);
        Assert.Contains("editala", await repetida.Detalle());

        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PutAsJsonAsync($"/api/mediciones/{id}", Medicion("2026-10-01", 88.4m, 22m))).StatusCode);
        var lista = (await jefe.GetJson($"/api/socios/{socio}/mediciones")).AsArray();
        Assert.Single(lista);
        Assert.Equal(88.4m, (decimal)lista[0]!["pesoKg"]!);
    }

    [Fact]
    public async Task Los_valores_fuera_de_rango_y_las_fechas_futuras_se_rechazan()
    {
        var jefe = await api.Jefe();
        var socio = await Socio(jefe);

        foreach (var invalida in new object[]
        {
            Medicion("2026-10-01", 10m),                       // peso imposible
            Medicion("2026-10-01", 80m, grasa: 90m),           // grasa fuera de rango
            Medicion("2026-10-01", 80m, cintura: 5m),          // perímetro imposible
            Medicion("2099-01-01", 80m),                       // futura
            new { pesoKg = 80m },                              // sin fecha
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsJsonAsync($"/api/socios/{socio}/mediciones", invalida)).StatusCode);

        Assert.Empty((await jefe.GetJson($"/api/socios/{socio}/mediciones")).AsArray());
    }

    [Fact]
    public async Task Recepcion_registra_y_corrige_pero_solo_gestion_borra()
    {
        var jefe = await api.Jefe();
        var recepcion = await api.Recepcion();
        var socio = await Socio(recepcion);

        var creada = await recepcion.PostAsJsonAsync($"/api/socios/{socio}/mediciones", Medicion("2026-10-02", 70m));
        Assert.True(creada.IsSuccessStatusCode, await creada.Detalle());
        var id = (long)(await creada.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        Assert.Equal(HttpStatusCode.NoContent, (await recepcion.PutAsJsonAsync($"/api/mediciones/{id}", Medicion("2026-10-02", 69.5m))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.DeleteAsync($"/api/mediciones/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await jefe.DeleteAsync($"/api/mediciones/{id}")).StatusCode);
        Assert.Empty((await jefe.GetJson($"/api/socios/{socio}/mediciones")).AsArray());
    }

    [Fact]
    public async Task Un_socio_con_mediciones_no_se_puede_eliminar_y_uno_inexistente_da_404()
    {
        var jefe = await api.Jefe();
        var socio = await Socio(jefe);
        Assert.True((await jefe.PostAsJsonAsync($"/api/socios/{socio}/mediciones", Medicion("2026-10-03", 75m))).IsSuccessStatusCode);

        var borrar = await jefe.DeleteAsync($"/api/socios/{socio}");
        Assert.Equal(HttpStatusCode.BadRequest, borrar.StatusCode);
        Assert.Contains("mediciones", await borrar.Detalle());

        Assert.Equal(HttpStatusCode.NotFound, (await jefe.GetAsync("/api/socios/99999999/mediciones")).StatusCode);
    }
}
