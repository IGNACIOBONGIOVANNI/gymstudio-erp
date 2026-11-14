using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>La pantalla de auditoría pide de a una página y recibe el total en el encabezado X-Total-Count.</summary>
[Collection(ColeccionApi.Nombre)]
public class AuditoriaPaginadaTests(ApiFixture api)
{
    /// <summary>Deja <paramref name="cantidad"/> registros de acceso con un email propio, para filtrarlos sin mezclarlos con los de otras pruebas.</summary>
    private async Task<string> RegistrosDePrueba(int cantidad)
    {
        var email = $"pag.{Guid.NewGuid():N}"[..16] + "@gym.com";
        for (var i = 0; i < cantidad; i++)
            await api.ClienteAnonimo().PostAsJsonAsync("/api/auth/login", new { email, password = "No-Existe-29?" });
        return email;
    }

    private static async Task<(List<long> Ids, int Total)> Pagina(HttpClient jefe, string email, int pagina, int tamano)
    {
        var respuesta = await jefe.GetAsync($"/api/auditoria?usuario={Uri.EscapeDataString(email)}&pagina={pagina}&tamano={tamano}");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var total = int.Parse(respuesta.Headers.GetValues("X-Total-Count").Single());
        var ids = (await respuesta.Content.ReadFromJsonAsync<JsonArray>())!.Select(r => (long)r!["id"]!).ToList();
        return (ids, total);
    }

    [Fact]
    public async Task Las_paginas_traen_de_a_pocos_sin_repetir_y_el_total_viene_en_el_encabezado()
    {
        var email = await RegistrosDePrueba(7);
        var jefe = await api.Jefe();

        var p1 = await Pagina(jefe, email, 1, 3);
        var p2 = await Pagina(jefe, email, 2, 3);
        var p3 = await Pagina(jefe, email, 3, 3);
        var p4 = await Pagina(jefe, email, 4, 3);

        Assert.All(new[] { p1, p2, p3, p4 }, p => Assert.Equal(7, p.Total));
        Assert.Equal(3, p1.Ids.Count);
        Assert.Equal(3, p2.Ids.Count);
        Assert.Single(p3.Ids);
        Assert.Empty(p4.Ids);

        var todos = p1.Ids.Concat(p2.Ids).Concat(p3.Ids).ToList();
        Assert.Equal(7, todos.Distinct().Count());
        Assert.Equal(todos.OrderByDescending(i => i), todos);   // de lo más nuevo a lo más viejo, también entre páginas
    }

    [Theory]
    [InlineData(0, 3)]       // página 0: se toma la 1
    [InlineData(-5, 3)]
    public async Task Una_pagina_invalida_se_toma_como_la_primera(int pagina, int tamano)
    {
        var email = await RegistrosDePrueba(4);
        var jefe = await api.Jefe();

        var pedida = await Pagina(jefe, email, pagina, tamano);
        var primera = await Pagina(jefe, email, 1, tamano);

        Assert.Equal(primera.Ids, pedida.Ids);
    }

    [Fact]
    public async Task El_tamano_de_pagina_tiene_un_tope()
    {
        var jefe = await api.Jefe();
        var respuesta = await jefe.GetAsync("/api/auditoria?pagina=1&tamano=100000");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True((await respuesta.Content.ReadFromJsonAsync<JsonArray>())!.Count <= 100);
    }

    [Fact]
    public async Task Sin_pagina_sigue_respondiendo_como_antes()
    {
        var email = await RegistrosDePrueba(2);
        var jefe = await api.Jefe();
        var registros = await jefe.GetFromJsonAsync<JsonArray>($"/api/auditoria?usuario={Uri.EscapeDataString(email)}");
        Assert.Equal(2, registros!.Count);
    }

    [Fact]
    public async Task Solo_el_administrador_ve_la_auditoria_paginada()
    {
        var contador = await api.Contador();
        Assert.Equal(HttpStatusCode.Forbidden, (await contador.GetAsync("/api/auditoria?pagina=1&tamano=10")).StatusCode);
    }
}
