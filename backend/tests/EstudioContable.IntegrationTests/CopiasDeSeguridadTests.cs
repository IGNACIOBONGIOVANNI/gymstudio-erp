using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Copia de seguridad completa desde la pantalla: descargar, comparar con el sistema actual y restaurar. Se prueba contra un
/// PostgreSQL real, incluida la restauración, y con archivos manipulados: nada de eso puede cambiar la base ni darle un error interno.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CopiasDeSeguridadTests(ApiFixture api)
{
    // ---------- ayudas ----------

    private static MultipartFormDataContent Formulario(byte[] zip, string nombre = "copia.zip", string? confirmacion = null)
    {
        var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(zip);
        parte.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        contenido.Add(parte, "file", nombre);
        if (confirmacion != null) contenido.Add(new StringContent(confirmacion), "confirmacion");
        return contenido;
    }

    private static async Task<byte[]> Descargar(HttpClient jefe)
    {
        var r = await jefe.GetAsync("/api/copias/descargar");
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        Assert.Equal("application/zip", r.Content.Headers.ContentType?.MediaType);
        return await r.Content.ReadAsByteArrayAsync();
    }

    private static Dictionary<string, byte[]> Desarmar(byte[] zip)
    {
        using var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return archivo.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var s = e.Open();
            using var m = new MemoryStream();
            s.CopyTo(m);
            return m.ToArray();
        });
    }

    private static byte[] Armar(Dictionary<string, byte[]> archivos)
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (nombre, bytes) in archivos)
            {
                using var s = zip.CreateEntry(nombre).Open();
                s.Write(bytes);
            }
        return memoria.ToArray();
    }

    private static JsonObject Manifiesto(Dictionary<string, byte[]> archivos) => JsonNode.Parse(archivos["manifiesto.json"])!.AsObject();

    private static void GuardarManifiesto(Dictionary<string, byte[]> archivos, JsonObject manifiesto)
        => archivos["manifiesto.json"] = Encoding.UTF8.GetBytes(manifiesto.ToJsonString());

    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private async Task<string> CrearPlan(HttpClient jefe, string? nombre = null)
    {
        nombre ??= $"Plan copia {Guid.NewGuid():N}";
        var r = await jefe.PostAsJsonAsync("/api/planes", new { nombre, precioMensual = 1000, duracionMeses = 1 });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return nombre;
    }

    private static async Task<bool> ExistePlan(HttpClient jefe, string nombre)
        => (await jefe.GetJson("/api/planes")).AsArray().Any(p => (string?)p!["nombre"] == nombre);

    private async Task<long> Contar(string tabla)
    {
        await using var c = new NpgsqlConnection(api.ConnectionString);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand($"SELECT count(*) FROM {tabla}", c);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<JsonObject> Comparar(HttpClient jefe, byte[] zip)
    {
        var r = await jefe.PostAsync("/api/copias/comparar", Formulario(zip));
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (await r.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    private static JsonNode? Tabla(JsonObject comparacion, string nombre)
        => comparacion["tablas"]!.AsArray().FirstOrDefault(t => (string?)t!["tabla"] == nombre);

    // ---------- descargar ----------

    [Fact]
    public async Task La_copia_trae_todas_las_tablas_con_su_manifiesto_y_las_huellas_coinciden()
    {
        var jefe = await api.Jefe();
        var zip = Desarmar(await Descargar(jefe));
        var manifiesto = Manifiesto(zip);

        Assert.Equal("gymstudio-copia-v1", (string)manifiesto["formato"]!);
        Assert.False(string.IsNullOrEmpty((string)manifiesto["migracion"]!));
        var tablas = manifiesto["tablas"]!.AsObject();
        foreach (var requerida in new[] { "contabilidad.Facturas", "contabilidad.asientos_contables", "contabilidad.Socios", "contabilidad.Usuario", "contabilidad.auditoria", "caja.movimientos_caja", "inventario.Productos" })
            Assert.True(tablas.ContainsKey(requerida), $"falta la tabla {requerida}");

        foreach (var (nombre, datos) in tablas)
        {
            var archivo = $"datos/{nombre}.csv";
            Assert.True(zip.ContainsKey(archivo), $"falta {archivo}");
            Assert.Equal((string)datos!["sha256"]!, Sha(zip[archivo]));
        }
        Assert.Equal(tablas.Count + 1, zip.Count);   // los CSV y el manifiesto, nada más
    }

    [Fact]
    public async Task Descargar_una_copia_queda_en_la_auditoria_con_el_equipo()
    {
        var cliente = await api.IniciarSesion(api.ClienteDesde("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36", "10.7.7.7"), "jefe@gym.com");
        await Descargar(cliente);

        var registros = (await cliente.GetJson("/api/auditoria?tabla=copia&cantidad=20")).AsArray();
        var descarga = registros.First(r => (string?)r!["operacion"] == "DESCARGA")!;
        Assert.Equal("jefe@gym.com", (string)descarga["usuario"]!);
        Assert.Equal("10.7.7.7", (string)descarga["ip"]!);
        Assert.Equal("Chrome 141", (string)descarga["navegador"]!);
    }

    [Fact]
    public async Task Solo_el_administrador_puede_descargar_comparar_o_restaurar()
    {
        var jefe = await api.Jefe();
        var zip = await Descargar(jefe);
        foreach (var cliente in new[] { await api.Contador(), await api.Recepcion(), api.ClienteAnonimo() })
        {
            var esperado = cliente.DefaultRequestHeaders.Authorization == null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            Assert.Equal(esperado, (await cliente.GetAsync("/api/copias/descargar")).StatusCode);
            Assert.Equal(esperado, (await cliente.PostAsync("/api/copias/comparar", Formulario(zip))).StatusCode);
            Assert.Equal(esperado, (await cliente.PostAsync("/api/copias/restaurar", Formulario(zip, confirmacion: "RESTAURAR"))).StatusCode);
        }
    }

    // ---------- comparar ----------

    [Fact]
    public async Task Una_copia_recien_hecha_es_identica_al_sistema_y_se_puede_restaurar()
    {
        var jefe = await api.Jefe();
        var comparacion = await Comparar(jefe, await Descargar(jefe));

        Assert.Equal("identica", (string)comparacion["veredicto"]!);
        Assert.True((bool)comparacion["mismoEsquema"]!);
        Assert.True((bool)comparacion["puedeRestaurar"]!);
        Assert.Null((string?)comparacion["motivoSiNoPuede"]);
        Assert.All(comparacion["tablas"]!.AsArray().Where(t => !(bool)t!["esAuditoria"]!), t => Assert.Equal("igual", (string)t!["estado"]!));
        Assert.Equal((string)comparacion["copia"]!["migracion"]!, (string)comparacion["actual"]!["migracion"]!);
    }

    [Fact]
    public async Task Despues_de_cargar_datos_la_comparacion_dice_que_tablas_cambiaron_con_sus_filas()
    {
        var jefe = await api.Jefe();
        var copia = await Descargar(jefe);
        await CrearPlan(jefe);

        var comparacion = await Comparar(jefe, copia);

        Assert.Equal("anterior", (string)comparacion["veredicto"]!);
        var planes = Tabla(comparacion, "contabilidad.Planes")!;
        Assert.Equal("cambia", (string)planes["estado"]!);
        Assert.Equal((long)planes["filasCopia"]! + 1, (long)planes["filasAhora"]!);
        Assert.Equal("Planes", (string)planes["etiqueta"]!);
        Assert.Equal("Gimnasio", (string)planes["grupo"]!);
        Assert.Equal("Facturas", (string)Tabla(comparacion, "contabilidad.Facturas")!["etiqueta"]!);
        Assert.Equal("igual", (string)Tabla(comparacion, "contabilidad.Facturas")!["estado"]!);
    }

    [Fact]
    public async Task Una_copia_de_otra_version_de_la_base_se_compara_pero_no_se_puede_restaurar()
    {
        var jefe = await api.Jefe();
        var archivos = Desarmar(await Descargar(jefe));
        var manifiesto = Manifiesto(archivos);
        manifiesto["migracion"] = "29991231000000_DeUnaVersionFutura";
        GuardarManifiesto(archivos, manifiesto);
        var zip = Armar(archivos);

        var comparacion = await Comparar(jefe, zip);
        Assert.Equal("otra-version", (string)comparacion["veredicto"]!);
        Assert.False((bool)comparacion["puedeRestaurar"]!);
        Assert.Contains("otra versión", (string)comparacion["motivoSiNoPuede"]!);

        var planes = await Contar("contabilidad.\"Planes\"");
        var r = await jefe.PostAsync("/api/copias/restaurar", Formulario(zip, confirmacion: "RESTAURAR"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(planes, await Contar("contabilidad.\"Planes\""));
    }

    // ---------- restaurar ----------

    [Fact]
    public async Task Restaurar_devuelve_el_sistema_a_la_copia_y_deja_la_contabilidad_integra()
    {
        var jefe = await api.Jefe();
        var copia = await Descargar(jefe);
        var plan = await CrearPlan(jefe);
        Assert.True(await ExistePlan(jefe, plan));

        var r = await jefe.PostAsync("/api/copias/restaurar", Formulario(copia, confirmacion: "RESTAURAR"));
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        var resultado = (await r.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.True((int)resultado["tablas"]! >= 20);
        Assert.Equal((int)resultado["integridad"]!["total"]!, (int)resultado["integridad"]!["correctos"]!);

        // El plan cargado después de la copia ya no está, y el resto sigue funcionando.
        Assert.False(await ExistePlan(jefe, plan));
        var otraVez = await Comparar(jefe, copia);
        Assert.Equal("identica", (string)otraVez["veredicto"]!);
        Assert.Equal(HttpStatusCode.OK, (await jefe.GetAsync("/api/dashboard")).StatusCode);

        // Y las secuencias siguen desde el mayor: se puede cargar un plan nuevo sin chocar con un id existente.
        Assert.True(await ExistePlan(jefe, await CrearPlan(jefe)));

        // Quedó constancia de la restauración.
        var registros = (await jefe.GetJson("/api/auditoria?tabla=copia&cantidad=20")).AsArray();
        Assert.Contains(registros, x => (string?)x!["operacion"] == "RESTAURADA" && (string?)x["usuario"] == "jefe@gym.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("restaurar")]
    [InlineData("SI")]
    public async Task Sin_escribir_RESTAURAR_no_se_restaura_nada(string? confirmacion)
    {
        var jefe = await api.Jefe();
        var copia = await Descargar(jefe);
        var plan = await CrearPlan(jefe);

        var r = await jefe.PostAsync("/api/copias/restaurar", Formulario(copia, confirmacion: confirmacion));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("RESTAURAR", await r.Detalle());
        Assert.True(await ExistePlan(jefe, plan));
    }

    // ---------- archivos manipulados ----------

    [Fact]
    public async Task Una_copia_dañada_o_modificada_se_rechaza_sin_tocar_la_base()
    {
        var jefe = await api.Jefe();
        var plan = await CrearPlan(jefe);
        var archivos = Desarmar(await Descargar(jefe));
        var csv = archivos["datos/contabilidad.Planes.csv"];
        archivos["datos/contabilidad.Planes.csv"] = csv.Concat("1,2,3\n"u8.ToArray()).ToArray();   // un byte cambia y la huella ya no coincide
        var zip = Armar(archivos);

        var comparar = await jefe.PostAsync("/api/copias/comparar", Formulario(zip));
        Assert.Equal(HttpStatusCode.BadRequest, comparar.StatusCode);
        Assert.Contains("dañada", await comparar.Detalle());

        var restaurar = await jefe.PostAsync("/api/copias/restaurar", Formulario(zip, confirmacion: "RESTAURAR"));
        Assert.Equal(HttpStatusCode.BadRequest, restaurar.StatusCode);
        Assert.True(await ExistePlan(jefe, plan));
    }

    [Theory]
    [InlineData("contabilidad.Usuario\"; DROP TABLE contabilidad.\"Planes\";--")]
    [InlineData("contabilidad.Planes; DROP TABLE x")]
    [InlineData("../../etc/passwd.x")]
    [InlineData("sin_esquema")]
    public async Task Un_manifiesto_con_nombres_de_tabla_inyectados_se_rechaza_y_la_base_queda_intacta(string tablaHostil)
    {
        var jefe = await api.Jefe();
        var plan = await CrearPlan(jefe);
        var archivos = Desarmar(await Descargar(jefe));
        var manifiesto = Manifiesto(archivos);
        var datos = JsonNode.Parse(manifiesto["tablas"]!["contabilidad.Planes"]!.ToJsonString())!;
        manifiesto["tablas"]![tablaHostil] = datos;
        GuardarManifiesto(archivos, manifiesto);
        var zip = Armar(archivos);

        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsync("/api/copias/comparar", Formulario(zip))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsync("/api/copias/restaurar", Formulario(zip, confirmacion: "RESTAURAR"))).StatusCode);
        Assert.True(await ExistePlan(jefe, plan));
    }

    [Fact]
    public async Task Una_copia_con_una_tabla_que_el_sistema_no_tiene_no_se_puede_restaurar()
    {
        var jefe = await api.Jefe();
        var plan = await CrearPlan(jefe);
        var archivos = Desarmar(await Descargar(jefe));
        var extra = "id\n1\n"u8.ToArray();
        archivos["datos/otro.tabla_inexistente.csv"] = extra;
        var manifiesto = Manifiesto(archivos);
        manifiesto["tablas"]!["otro.tabla_inexistente"] = new JsonObject
        {
            ["filas"] = 1, ["huella"] = "abc", ["archivo"] = "datos/otro.tabla_inexistente.csv", ["sha256"] = Sha(extra),
        };
        GuardarManifiesto(archivos, manifiesto);
        var zip = Armar(archivos);

        var comparacion = await Comparar(jefe, zip);
        Assert.False((bool)comparacion["puedeRestaurar"]!);
        Assert.Equal("solo-copia", (string)Tabla(comparacion, "otro.tabla_inexistente")!["estado"]!);

        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsync("/api/copias/restaurar", Formulario(zip, confirmacion: "RESTAURAR"))).StatusCode);
        Assert.True(await ExistePlan(jefe, plan));
    }

    [Fact]
    public async Task Si_la_carga_falla_a_mitad_de_camino_no_cambia_nada_porque_es_una_sola_transaccion()
    {
        var jefe = await api.Jefe();
        var plan = await CrearPlan(jefe);
        var antesPlanes = await Contar("contabilidad.\"Planes\"");
        var antesSocios = await Contar("contabilidad.\"Socios\"");

        // Un atacante que sabe armar la huella: el archivo pasa la verificación pero la tabla de planes trae una fila rota.
        var archivos = Desarmar(await Descargar(jefe));
        var rota = archivos["datos/contabilidad.Planes.csv"].Concat("esto no es una fila\n"u8.ToArray()).ToArray();
        archivos["datos/contabilidad.Planes.csv"] = rota;
        var manifiesto = Manifiesto(archivos);
        manifiesto["tablas"]!["contabilidad.Planes"]!["sha256"] = Sha(rota);
        GuardarManifiesto(archivos, manifiesto);

        var r = await jefe.PostAsync("/api/copias/restaurar", Formulario(Armar(archivos), confirmacion: "RESTAURAR"));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);           // un error claro, no un 500
        Assert.Equal(antesPlanes, await Contar("contabilidad.\"Planes\""));   // nada se perdió: las demás tablas ya se habían vaciado dentro de la transacción
        Assert.Equal(antesSocios, await Contar("contabilidad.\"Socios\""));
        Assert.True(await ExistePlan(jefe, plan));
        Assert.Equal(HttpStatusCode.OK, (await jefe.GetAsync("/api/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Los_archivos_que_no_son_una_copia_se_rechazan_con_un_mensaje_claro()
    {
        var jefe = await api.Jefe();
        var basura = await jefe.PostAsync("/api/copias/comparar", Formulario("no soy un zip"u8.ToArray()));
        Assert.Equal(HttpStatusCode.BadRequest, basura.StatusCode);

        var sinManifiesto = Armar(new Dictionary<string, byte[]> { ["datos/contabilidad.Planes.csv"] = "x"u8.ToArray() });
        var r = await jefe.PostAsync("/api/copias/comparar", Formulario(sinManifiesto));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);

        var extranio = Armar(new Dictionary<string, byte[]> { ["manifiesto.json"] = "{}"u8.ToArray(), ["../../fuera.txt"] = "x"u8.ToArray() });
        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsync("/api/copias/comparar", Formulario(extranio))).StatusCode);

        var sinExtension = await jefe.PostAsync("/api/copias/comparar", Formulario(await Descargar(jefe), "copia.txt"));
        Assert.Equal(HttpStatusCode.BadRequest, sinExtension.StatusCode);
        Assert.Contains(".zip", await sinExtension.Detalle());
    }

    [Fact]
    public async Task Una_bomba_zip_se_corta_antes_de_agotar_la_memoria()
    {
        // Un zip de unos KB que al descomprimirse supera los 300 MB (y el tamaño declarado miente).
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using var s = zip.CreateEntry("datos/contabilidad.Planes.csv", CompressionLevel.Fastest).Open();
            var ceros = new byte[1024 * 1024];
            for (var i = 0; i < 310; i++) await s.WriteAsync(ceros);
        }
        Assert.True(memoria.Length < 20 * 1024 * 1024, $"el zip de prueba tendría que ser mucho más chico que lo que descomprime (310 MB), y pesa {memoria.Length / 1024} KB");

        var jefe = await api.Jefe();
        var r = await jefe.PostAsync("/api/copias/comparar", Formulario(memoria.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("demasiado grande", await r.Detalle());
        Assert.Equal(HttpStatusCode.OK, (await jefe.GetAsync("/api/planes")).StatusCode);
    }
}
