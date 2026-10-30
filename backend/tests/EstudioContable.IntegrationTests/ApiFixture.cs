using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Levanta un PostgreSQL real y descartable (Testcontainers) y la API completa en
/// memoria. Al arrancar, la API aplica todas las migraciones (triggers incluidos) y
/// crea los usuarios iniciales, igual que en producción. Requiere Docker.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    // Contraseña de los usuarios iniciales (appsettings.Development.json).
    public const string Clave = "123456";

    // Contraseña de los usuarios que crean los tests (cumple la política: 8+ con letras y números).
    public const string ClaveNueva = "clave-de-prueba-123";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
            builder.UseSetting("Auth:IntentosLoginPorMinuto", "10000");
            builder.ConfigureServices(servicios => servicios.AddTransient<IStartupFilter, SimularIpDeConexion>());
        });

        // Fuerza el arranque (migraciones y usuarios iniciales).
        _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient ClienteAnonimo() => _factory.CreateClient();

    /// <summary>
    /// Cliente que se presenta como un equipo concreto: su User-Agent, la IP desde la que "llega" la
    /// conexión (el servidor de pruebas no tiene dirección remota) y, si hay un proxy, lo que éste reenvía
    /// en X-Forwarded-For. Los encabezados quedan en todos sus pedidos, también en el login.
    /// </summary>
    public HttpClient ClienteDesde(string userAgent, string? ipDeConexion = null, string? reenviado = null)
    {
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.UserAgent.Clear();
        cliente.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        if (ipDeConexion != null) cliente.DefaultRequestHeaders.Add(SimularIpDeConexion.Encabezado, ipDeConexion);
        if (reenviado != null) cliente.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", reenviado);
        return cliente;
    }

    public async Task<HttpClient> IniciarSesion(HttpClient cliente, string email, string? clave = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new { email, password = clave ?? Clave });
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonObject>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (string)cuerpo!["token"]!);
        return cliente;
    }

    /// <summary>Cliente HTTP ya autenticado con el usuario indicado.</summary>
    public async Task<HttpClient> ClienteComo(string email, string? clave = null)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new { email, password = clave ?? Clave });
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonObject>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (string)cuerpo!["token"]!);
        return cliente;
    }

    public Task<HttpClient> Jefe() => ClienteComo("jefe@gym.com");
    public Task<HttpClient> Contador() => ClienteComo("contador@gymstudio.com");
    public Task<HttpClient> Recepcion() => ClienteComo("recepcion@gymstudio.com");
}

/// <summary>
/// Solo para las pruebas: el servidor en memoria no tiene dirección remota, así que este filtro la toma del
/// encabezado X-Test-Remote-Ip. Va antes que el resto del pipeline, igual que la conexión real de un proxy.
/// </summary>
public sealed class SimularIpDeConexion : IStartupFilter
{
    public const string Encabezado = "X-Test-Remote-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => app =>
    {
        app.Use(async (contexto, continuar) =>
        {
            if (contexto.Request.Headers.TryGetValue(Encabezado, out var valor) && System.Net.IPAddress.TryParse(valor.ToString(), out var ip))
                contexto.Connection.RemoteIpAddress = ip;
            await continuar();
        });
        siguiente(app);
    };
}

[CollectionDefinition(Nombre)]
public class ColeccionApi : ICollectionFixture<ApiFixture>
{
    public const string Nombre = "API con PostgreSQL real";
}

/// <summary>Lecturas frecuentes de la API para comparar libros y saldos.</summary>
public static class Consultas
{
    public static async Task<JsonNode> GetJson(this HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<JsonNode>())!;
    }

    public static async Task<decimal> SaldoLibro(this HttpClient cliente, string libro)
        => (decimal)(await cliente.GetJson($"/api/{libro}?clienteId=1"))["saldoActual"]!;

    /// <summary>Saldo (Debe - Haber) de una cuenta en el Libro Mayor (vista v_libro_mayor).</summary>
    public static async Task<decimal> SaldoMayor(this HttpClient cliente, string cuenta)
    {
        var mayor = (await cliente.GetJson("/api/asientos/mayor?clienteId=1")).AsArray();
        var fila = mayor.FirstOrDefault(m => (string)m!["nombreCuenta"]! == cuenta);
        return fila == null ? 0 : (decimal)fila["saldo"]!;
    }

    public static async Task<int> StockDe(this HttpClient cliente, long productoId)
    {
        var productos = (await cliente.GetJson("/api/productos")).AsArray();
        return (int)productos.First(p => (long)p!["id"]! == productoId)!["stockActual"]!;
    }

    public static async Task<string> Detalle(this HttpResponseMessage respuesta)
    {
        var texto = await respuesta.Content.ReadAsStringAsync();
        try { return JsonDocument.Parse(texto).RootElement.GetProperty("detail").GetString() ?? texto; }
        catch { return texto; }
    }
}
