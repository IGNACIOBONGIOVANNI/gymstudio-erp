using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EstudioContable.API.Auth;
using Microsoft.Extensions.Configuration;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Lo que encontró la prueba de intrusión del 9/11/2026: pedidos simultáneos que duplicaban cobros y socios, y la clave de las
/// sesiones publicada en el repositorio. Contra la API y la base reales.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CarrerasYClaveDeSesionTests(ApiFixture api)
{
    private static string Dni() => Random.Shared.Next(20_000_000, 49_999_999).ToString();

    private static async Task<long> Id(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private static async Task<(long Socio, long Plan, string Apellido)> SocioYPlan(HttpClient jefe)
    {
        var apellido = "Cobro " + Nombres.Aleatorio(10);
        var socio = await Id(await jefe.PostAsJsonAsync("/api/socios", new { nombre = "Ana", apellido, dni = Dni(), telefono = "" }));
        var plan = await Id(await jefe.PostAsJsonAsync("/api/planes", new { nombre = "Plan " + Nombres.Aleatorio(10), precioMensual = 1000, duracionMeses = 1 }));
        return (socio, plan, apellido);
    }

    private static async Task<int> FacturasDe(HttpClient jefe, string apellido)
        => (await jefe.GetFromJsonAsync<JsonArray>("/api/facturas"))!.Count(f => ((string?)f!["cliente"] ?? "").StartsWith(apellido));

    // ---------- cobro de cuotas ----------

    [Fact]
    public async Task Ocho_cobros_simultaneos_de_la_misma_cuota_cobran_una_sola_vez()
    {
        var jefe = await api.Jefe();
        var (socio, plan, apellido) = await SocioYPlan(jefe);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            jefe.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", new { socioId = socio, planId = plan, metodoPago = "caja" })));

        Assert.Equal(1, respuestas.Count(r => r.IsSuccessStatusCode));
        foreach (var rechazada in respuestas.Where(r => !r.IsSuccessStatusCode))
        {
            Assert.Equal(HttpStatusCode.BadRequest, rechazada.StatusCode);
            Assert.Contains("hace instantes", await rechazada.Content.ReadAsStringAsync());
        }
        Assert.Equal(1, await FacturasDe(jefe, apellido));

        var ficha = (await jefe.GetFromJsonAsync<JsonArray>("/api/socios"))!.Select(s => s!.AsObject()).Single(s => (long)s["id"]! == socio);
        var vencimiento = DateTime.Parse((string)ficha["fechaVencimiento"]!, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.InRange((vencimiento - DateTime.UtcNow).TotalDays, 27, 32);   // un mes, no ocho
    }

    [Fact]
    public async Task Un_segundo_cobro_enseguida_del_primero_se_rechaza_con_el_motivo()
    {
        var jefe = await api.Jefe();
        var (socio, plan, apellido) = await SocioYPlan(jefe);
        var cuerpo = new { socioId = socio, planId = plan, metodoPago = "caja" };

        Assert.True((await jefe.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", cuerpo)).IsSuccessStatusCode);
        var repetido = await jefe.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);
        Assert.Contains("Ya se registró un cobro de cuota para este socio hace instantes", await repetido.Content.ReadAsStringAsync());
        Assert.Equal(1, await FacturasDe(jefe, apellido));
    }

    [Fact]
    public async Task El_cobro_de_un_socio_no_frena_el_de_otro()
    {
        var jefe = await api.Jefe();
        var (uno, plan, _) = await SocioYPlan(jefe);
        var (otro, _, _) = await SocioYPlan(jefe);

        var respuestas = await Task.WhenAll(new[] { uno, otro }.Select(socio =>
            jefe.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", new { socioId = socio, planId = plan, metodoPago = "caja" })));

        Assert.All(respuestas, r => Assert.True(r.IsSuccessStatusCode));
    }

    // ---------- DNI de los socios ----------

    [Fact]
    public async Task Ocho_altas_simultaneas_con_el_mismo_dni_crean_un_solo_socio()
    {
        var jefe = await api.Jefe();
        var dni = Dni();
        var apellido = "Mismo " + Nombres.Aleatorio(10);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            jefe.PostAsJsonAsync("/api/socios", new { nombre = "Ana", apellido, dni, telefono = "" })));

        Assert.Equal(1, respuestas.Count(r => r.IsSuccessStatusCode));
        foreach (var rechazada in respuestas.Where(r => !r.IsSuccessStatusCode))
        {
            Assert.Equal(HttpStatusCode.BadRequest, rechazada.StatusCode);
            Assert.Contains("Ya existe un socio con el DNI", await rechazada.Detalle());
        }
        Assert.Equal(1, (await jefe.GetFromJsonAsync<JsonArray>("/api/socios"))!.Count(s => (string)s!["dni"]! == dni));
    }

    [Fact]
    public async Task Cambiarle_el_dni_a_un_socio_por_el_de_otro_se_rechaza()
    {
        var jefe = await api.Jefe();
        var (dniUno, dniOtro) = (Dni(), Dni());
        await Id(await jefe.PostAsJsonAsync("/api/socios", new { nombre = "Ana", apellido = "Uno " + Nombres.Aleatorio(8), dni = dniUno, telefono = "" }));
        var otro = await Id(await jefe.PostAsJsonAsync("/api/socios", new { nombre = "Beto", apellido = "Otro " + Nombres.Aleatorio(8), dni = dniOtro, telefono = "" }));

        var cambio = await jefe.PutAsJsonAsync($"/api/socios/{otro}", new { nombre = "Beto", apellido = "Otro", dni = dniUno, telefono = "" });

        Assert.Equal(HttpStatusCode.BadRequest, cambio.StatusCode);
        Assert.Contains("Ya existe un socio con el DNI", await cambio.Detalle());
    }

    // ---------- clave de las sesiones ----------

    // La clave que estuvo publicada en appsettings.Development.json hasta el 9/11/2026: ya no tiene que abrir nada.
    private const string ClavePublicadaEnElRepositorio = "dev-vZZT_adb0PMWvur9DxZ7L5hPKfHyEiei7E4awSg9JdJbbrI7sY6ES4AeVbAcRpQP";

    private static string B64(byte[] datos) => Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string TokenFirmadoCon(string clave)
    {
        var ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var cabecera = B64(Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));
        var cuerpo = B64(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["sub"] = "1", ["role"] = "Administrador", ["iss"] = "EstudioContable.API", ["aud"] = "EstudioContable.Frontend",
            ["exp"] = ahora + 600, ["nbf"] = ahora - 10, ["iat"] = ahora - 10,
        }));
        var firma = B64(HMACSHA256.HashData(Encoding.UTF8.GetBytes(clave), Encoding.UTF8.GetBytes($"{cabecera}.{cuerpo}")));
        return $"{cabecera}.{cuerpo}.{firma}";
    }

    [Fact]
    public async Task Un_token_fabricado_con_la_clave_que_estuvo_en_el_repositorio_no_entra()
    {
        var intruso = api.ClienteAnonimo();
        intruso.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFirmadoCon(ClavePublicadaEnElRepositorio));

        Assert.Equal(HttpStatusCode.Unauthorized, (await intruso.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await intruso.GetAsync("/api/usuarios")).StatusCode);
    }

    [Fact]
    public void Sin_clave_configurada_se_genera_una_al_azar_se_guarda_y_se_vuelve_a_usar()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "gymstudio-clave-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sinClave = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var primera = ClaveDeSesion.Resolver(sinClave, carpeta);
            var segunda = ClaveDeSesion.Resolver(new ConfigurationBuilder().AddInMemoryCollection().Build(), carpeta);

            Assert.True(primera.Length >= 64, "la clave generada tiene que ser larga");
            Assert.Equal(primera, segunda);                                   // la segunda vez lee la guardada: las sesiones sobreviven a un reinicio
            Assert.Equal(primera, sinClave["Jwt:Key"]);                       // queda en la configuración para quien la lee de ahí
            Assert.Equal(primera, File.ReadAllText(Path.Combine(carpeta, ClaveDeSesion.NombreDelArchivo)).Trim());
            Assert.NotEqual(ClavePublicadaEnElRepositorio, primera);

            var otraCarpeta = carpeta + "-b";
            try { Assert.NotEqual(primera, ClaveDeSesion.Resolver(new ConfigurationBuilder().AddInMemoryCollection().Build(), otraCarpeta)); }   // cada instalación, la suya
            finally { if (Directory.Exists(otraCarpeta)) Directory.Delete(otraCarpeta, true); }
        }
        finally { if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true); }
    }

    [Fact]
    public void Una_clave_configurada_manda_y_no_se_escribe_ningun_archivo()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "gymstudio-clave-" + Guid.NewGuid().ToString("N"));
        var configurada = new string('k', 40);
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = configurada }).Build();

        Assert.Equal(configurada, ClaveDeSesion.Resolver(configuracion, carpeta));
        Assert.False(Directory.Exists(carpeta));
    }

    [Fact]
    public void Una_clave_configurada_demasiado_corta_se_rechaza_en_vez_de_reemplazarla_en_silencio()
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = "corta" }).Build();
        Assert.Throws<InvalidOperationException>(() => ClaveDeSesion.Resolver(configuracion, Path.GetTempPath()));
    }

    [Fact]
    public void Un_archivo_de_clave_dañado_o_corto_se_rechaza()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "gymstudio-clave-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        try
        {
            File.WriteAllText(Path.Combine(carpeta, ClaveDeSesion.NombreDelArchivo), "abc");
            Assert.Throws<InvalidOperationException>(() => ClaveDeSesion.Resolver(new ConfigurationBuilder().AddInMemoryCollection().Build(), carpeta));
        }
        finally { Directory.Delete(carpeta, true); }
    }
}
