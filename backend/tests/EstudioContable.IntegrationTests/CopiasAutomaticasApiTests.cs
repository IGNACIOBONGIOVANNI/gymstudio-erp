using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using EstudioContable.API.Copias;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// La API de las copias automáticas (solo el Administrador), la tarea que las dispara cada minuto y la carpeta compartida con la PC
/// que declara el docker-compose.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CopiasAutomaticasApiTests(ApiFixture api)
{
    private const string Programacion = "/api/copias/programacion";
    private const string NombreInexistente = "gymstudio_auto_20250101_000000.zip";

    private static DateTimeOffset Fecha(int anio, int mes, int dia, int hora = 0, int minuto = 0, int segundo = 0)
        => new(anio, mes, dia, hora, minuto, segundo, TimeSpan.FromHours(-3));

    private static StringContent Json(string texto) => new(texto, Encoding.UTF8, "application/json");

    private static readonly string CuerpoValido = """{"activa":true,"frecuencia":"Diaria","hora":22,"minuto":0,"diaDeLaSemana":null,"diaDelMes":null,"conservar":14}""";

    // ---------- permisos ----------

    [Fact]
    public async Task Solo_el_administrador_usa_estas_rutas()
    {
        await api.ReiniciarCopias();
        var jefe = await api.Jefe();
        var antes = (await jefe.GetJson(Programacion)).ToJsonString();

        var rutas = new (HttpMethod Metodo, string Url, string? Cuerpo)[]
        {
            (HttpMethod.Get, Programacion, null),
            (HttpMethod.Put, Programacion, CuerpoValido),
            (HttpMethod.Post, Programacion + "/ahora", null),
            (HttpMethod.Get, "/api/copias/automaticas", null),
            (HttpMethod.Get, "/api/copias/automaticas/" + NombreInexistente, null),
        };
        foreach (var (cliente, esperado) in new[] { (await api.Contador(), HttpStatusCode.Forbidden), (await api.Recepcion(), HttpStatusCode.Forbidden), (api.ClienteAnonimo(), HttpStatusCode.Unauthorized) })
        {
            foreach (var (metodo, url, cuerpo) in rutas)
            {
                var pedido = new HttpRequestMessage(metodo, url) { Content = cuerpo == null ? null : Json(cuerpo) };
                var respuesta = await cliente.SendAsync(pedido);
                Assert.True(respuesta.StatusCode == esperado, $"{metodo} {url}: se esperaba {(int)esperado} y fue {(int)respuesta.StatusCode}");
            }
        }

        Assert.Equal(antes, (await jefe.GetJson(Programacion)).ToJsonString());   // sin efectos
        Assert.False(Directory.Exists(api.CarpetaDeCopias));                       // y no se hizo ninguna copia
    }

    // ---------- la programación ----------

    [Fact]
    public async Task La_programacion_inicial_esta_inactiva_y_sin_proxima_copia()
    {
        await api.ReiniciarCopias();
        var estado = await (await api.Jefe()).GetJson(Programacion);

        Assert.False((bool)estado["activa"]!);
        Assert.Equal("Diaria", (string)estado["frecuencia"]!);
        Assert.Equal((22, 0, 14), ((int)estado["hora"]!, (int)estado["minuto"]!, (int)estado["conservar"]!));
        Assert.Null(estado["diaDeLaSemana"]);
        Assert.Null(estado["diaDelMes"]);
        Assert.Null(estado["proximaCopia"]);
        Assert.Null(estado["ultimaCopia"]);
        Assert.Null(estado["ultimoError"]);
    }

    [Fact]
    public async Task Programar_devuelve_la_proxima_copia_y_deja_quien_lo_hizo_en_la_auditoria()
    {
        await api.ReiniciarCopias();
        var jefe = await api.Jefe();
        var ultimas = (await jefe.GetJson("/api/auditoria?cantidad=1")).AsArray();
        var desde = ultimas.Count == 0 ? 0 : (long)ultimas[0]!["id"]!;
        api.Reloj.Fijar(Fecha(2026, 11, 7, 15));

        var r = await jefe.PutAsync(Programacion, Json(CuerpoValido));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var estado = (await r.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.True((bool)estado["activa"]!);
        Assert.Equal(Fecha(2026, 11, 7, 22), DateTimeOffset.Parse((string)estado["proximaCopia"]!));
        Assert.Equal((string)estado["proximaCopia"]!, (string)(await jefe.GetJson(Programacion))["proximaCopia"]!);

        var programadas = (await jefe.GetJson("/api/auditoria?cantidad=50&tabla=copia")).AsArray()
            .Where(f => (long)f!["id"]! > desde && (string)f["operacion"]! == "PROGRAMADA").ToList();
        Assert.Single(programadas);
        Assert.Equal("jefe@gym.com", (string)programadas[0]!["usuario"]!);   // el email de quien lo cambió, no «sistema»
        await api.ReiniciarCopias();
    }

    public static IEnumerable<object[]> Cuerpos_invalidos()
    {
        yield return new object[] { "semanal sin día", """{"activa":true,"frecuencia":"Semanal","hora":22,"minuto":0,"conservar":14}""" };
        yield return new object[] { "mensual el día 29", """{"activa":true,"frecuencia":"Mensual","hora":22,"minuto":0,"diaDelMes":29,"conservar":14}""" };
        yield return new object[] { "hora 24", """{"activa":true,"frecuencia":"Diaria","hora":24,"minuto":0,"conservar":14}""" };
        yield return new object[] { "conservar 0", """{"activa":true,"frecuencia":"Diaria","hora":22,"minuto":0,"conservar":0}""" };
        yield return new object[] { "conservar 1000", """{"activa":true,"frecuencia":"Diaria","hora":22,"minuto":0,"conservar":1000}""" };
        yield return new object[] { "frecuencia 99 como texto", """{"activa":true,"frecuencia":"99","hora":22,"minuto":0,"conservar":14}""" };
        yield return new object[] { "frecuencia numérica", """{"activa":true,"frecuencia":3,"hora":22,"minuto":0,"conservar":14}""" };
        yield return new object[] { "frecuencia que no existe", """{"activa":true,"frecuencia":"Cada5Minutos","hora":22,"minuto":0,"conservar":14}""" };
        yield return new object[] { "hora como texto", """{"activa":true,"frecuencia":"Diaria","hora":"mediodía","minuto":0,"conservar":14}""" };
        yield return new object[] { "cuerpo vacío", "{}" };
        yield return new object[] { "cuerpo roto", "{ esto no es json" };
        yield return new object[] { "sin cuerpo", "" };
    }

    [Theory]
    [MemberData(nameof(Cuerpos_invalidos))]
    public async Task Una_programacion_invalida_da_400_y_no_cambia_nada(string caso, string cuerpo)
    {
        await api.ReiniciarCopias();
        var jefe = await api.Jefe();
        var antes = (await jefe.GetJson(Programacion)).ToJsonString();

        var r = await jefe.PutAsync(Programacion, Json(cuerpo));

        Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{caso}: se esperaba 400 y fue {(int)r.StatusCode}. {await r.Detalle()}");
        Assert.Equal(antes, (await jefe.GetJson(Programacion)).ToJsonString());
    }

    // ---------- hacer copias, listar y descargar ----------

    [Fact]
    public async Task Hacer_una_copia_ahora_aparece_en_la_lista_y_se_descarga_firmada()
    {
        await api.ReiniciarCopias();
        var jefe = await api.Jefe();
        api.Reloj.Fijar(Fecha(2026, 11, 7, 15));

        var r = await jefe.PostAsync(Programacion + "/ahora", null);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var copia = (await r.Content.ReadFromJsonAsync<JsonObject>())!;
        var nombre = (string)copia["nombre"]!;
        Assert.Equal("gymstudio_auto_20261107_150000.zip", nombre);
        Assert.True((long)copia["bytes"]! > 0);

        var lista = (await jefe.GetJson("/api/copias/automaticas")).AsArray();
        Assert.Equal(nombre, (string)lista[0]!["nombre"]!);

        var descarga = await jefe.GetAsync("/api/copias/automaticas/" + nombre);
        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
        Assert.Equal("application/zip", descarga.Content.Headers.ContentType!.MediaType);
        var zip = await descarga.Content.ReadAsByteArrayAsync();
        Assert.Equal((long)copia["bytes"]!, zip.Length);

        // La copia automática se verifica con la misma firma que las que se descargan desde la pantalla.
        var formulario = new MultipartFormDataContent { { new ByteArrayContent(zip), "file", nombre } };
        var comparacion = (await (await jefe.PostAsync("/api/copias/comparar", formulario)).Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("valida", (string)comparacion["firma"]!);

        Assert.Equal(nombre, (string)(await jefe.GetJson("/api/copias/automaticas"))[0]!["nombre"]!);
        await api.ReiniciarCopias();
    }

    [Fact]
    public async Task Una_copia_hecha_ahora_se_compara_como_identica_porque_la_programacion_se_actualiza_sola()
    {
        // Lo primero que se hace en una demostración: «Hacer una copia ahora» y compararla con el sistema.
        await api.ReiniciarCopias();
        var jefe = await api.Jefe();
        var copia = (await (await jefe.PostAsync(Programacion + "/ahora", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        var zip = await jefe.GetByteArrayAsync("/api/copias/automaticas/" + (string)copia["nombre"]!);

        var formulario = new MultipartFormDataContent { { new ByteArrayContent(zip), "file", (string)copia["nombre"]! } };
        var comparacion = (await (await jefe.PostAsync("/api/copias/comparar", formulario)).Content.ReadFromJsonAsync<JsonObject>())!;

        Assert.Equal("identica", (string)comparacion["veredicto"]!);
        var tabla = comparacion["tablas"]!.AsArray().First(t => (string?)t!["tabla"] == "contabilidad.ConfiguracionDeCopias")!;
        Assert.True((bool)tabla["esAuditoria"]!);   // se muestra como «Se actualiza sola», igual que la auditoría
        await api.ReiniciarCopias();
    }

    [Fact]
    public async Task Hacer_una_copia_ahora_con_la_carpeta_rota_da_400_con_un_mensaje_claro()
    {
        await api.ReiniciarCopias();
        File.WriteAllText(api.CarpetaDeCopias, "no soy una carpeta");

        var r = await (await api.Jefe()).PostAsync(Programacion + "/ahora", null);

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("No se pudo guardar la copia", await r.Detalle());
        await api.ReiniciarCopias();
    }

    [Theory]
    [InlineData("..%2f..%2fappsettings.json")]
    [InlineData("%2e%2e%2fappsettings.json")]
    [InlineData("..%5cappsettings.json")]
    [InlineData("%2e%2e")]
    [InlineData("C%3A%5CWindows%5Cwin.ini")]
    [InlineData("gymstudio_auto_20261107_150000.zip%0A")]
    [InlineData("gymstudio_auto_x.zip")]
    [InlineData(NombreInexistente)]
    public async Task Descargar_con_nombres_hostiles_o_inexistentes_da_404_y_no_entrega_nada(string nombre)
    {
        await api.ReiniciarCopias();
        var jefe = await api.Jefe();

        var r = await jefe.GetAsync("/api/copias/automaticas/" + nombre);

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.NotEqual("application/zip", r.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("Jwt", await r.Content.ReadAsStringAsync());
    }

    // ---------- la tarea en segundo plano ----------

    private sealed class RegistroDeErrores : ILogger
    {
        public List<string> Errores { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error) Errores.Add(formatter(state, exception));
        }
    }

    private sealed class FabricaQueFalla(Exception error) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw error;
    }

    [Fact]
    public async Task Una_vuelta_que_falla_se_registra_y_no_deja_caer_el_servicio()
    {
        var registro = new RegistroDeErrores();

        await CopiasAutomaticasHostedService.UnaVueltaAsync(new FabricaQueFalla(new InvalidOperationException("la base no responde")), registro, CancellationToken.None);

        Assert.Single(registro.Errores);
    }

    [Fact]
    public async Task Una_cancelacion_no_se_traga_porque_es_el_servidor_que_se_apaga()
    {
        var registro = new RegistroDeErrores();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CopiasAutomaticasHostedService.UnaVueltaAsync(new FabricaQueFalla(new OperationCanceledException()), registro, CancellationToken.None));

        Assert.Empty(registro.Errores);
    }

    [Fact]
    public void En_las_pruebas_la_tarea_en_segundo_plano_esta_apagada_por_configuracion()
        => Assert.DoesNotContain(api.Servicios.GetServices<IHostedService>(), s => s is CopiasAutomaticasHostedService);

    // ---------- Docker ----------

    [Fact]
    public void El_docker_compose_guarda_las_copias_automaticas_en_la_carpeta_de_la_PC()
    {
        // Sin esta carpeta compartida, las copias vivirían dentro del contenedor y se perderían al recrearlo.
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta != null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.yml"))) carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);

        var compose = File.ReadAllText(Path.Combine(carpeta!.FullName, "docker-compose.yml"));

        Assert.Contains("./backups:/backups", compose);
        Assert.Contains("Copias__Carpeta=/backups/automaticas", compose);
    }
}
