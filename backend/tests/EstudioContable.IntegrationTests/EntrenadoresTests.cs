using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Entrenadores: fichas sin acceso al sistema que se ponen en el calendario. Las leen todos los roles, las cargan y editan el
/// Administrador y el Contador, el nombre no se repite (sin importar mayúsculas ni espacios) y no se puede desactivar a quien
/// tiene horas en el calendario.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class EntrenadoresTests(ApiFixture api)
{
    /// <summary>Un nombre que no se repite entre pruebas (la base es la misma para todas).</summary>
    private static string Nuevo(string baseDelNombre) => $"{baseDelNombre} {Guid.NewGuid():N}"[..Math.Min(70, baseDelNombre.Length + 9)];

    private static object Datos(string? nombre, string? telefono = null, string? especialidad = null, string? email = null, string? whatsApp = null,
        decimal? valorHora = null)
        => new { nombre, telefono, especialidad, email, whatsApp, valorHora };

    private static async Task<long> Crear(HttpClient cliente, string nombre, string? telefono = null, string? especialidad = null,
        string? email = null, string? whatsApp = null, decimal? valorHora = null)
    {
        var r = await cliente.PostAsJsonAsync("/api/entrenadores", Datos(nombre, telefono, especialidad, email, whatsApp, valorHora));
        Assert.True(r.StatusCode == HttpStatusCode.Created, $"Alta de «{nombre}»: se esperaba 201 y fue {(int)r.StatusCode}. {await r.Detalle()}");
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private static async Task<JsonArray> Lista(HttpClient cliente, bool soloActivos = false)
        => (await cliente.GetJson($"/api/entrenadores?soloActivos={soloActivos.ToString().ToLowerInvariant()}")).AsArray();

    private static async Task<JsonNode> Ficha(HttpClient cliente, long id)
        => (await Lista(cliente)).First(e => (long)e!["id"]! == id)!;

    private static Task<HttpResponseMessage> Estado(HttpClient cliente, long id, bool activo)
        => cliente.PutAsJsonAsync($"/api/entrenadores/{id}/estado", new { activo });

    /// <summary>Deja el calendario con esas celdas (lunes a viernes) para el entrenador, o vacío si no se pasan celdas.</summary>
    private static async Task PonerEnCalendario(HttpClient jefe, long entrenadorId, params (int Dia, int Hora)[] celdas)
    {
        var r = await jefe.PutAsJsonAsync("/api/calendario", new { turnos = celdas.Select(c => new { dia = c.Dia, hora = c.Hora, entrenadorId }).ToArray() });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
    }

    // ---------- permisos ----------

    [Fact]
    public async Task Todos_los_roles_leen_la_lista_y_sin_sesion_no_se_ve()
    {
        var jefe = await api.Jefe();
        var nombre = Nuevo("Lectura");
        var id = await Crear(jefe, nombre, "11 1234-5678", "Yoga");

        foreach (var cliente in new[] { jefe, await api.Contador(), await api.Recepcion() })
        {
            var ficha = await Ficha(cliente, id);
            Assert.Equal(nombre, (string)ficha["nombre"]!);
            Assert.Equal("11 1234-5678", (string)ficha["telefono"]!);
            Assert.Equal("Yoga", (string)ficha["especialidad"]!);
            Assert.True((bool)ficha["activo"]!);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ClienteAnonimo().GetAsync("/api/entrenadores")).StatusCode);
    }

    [Fact]
    public async Task Solo_Administrador_y_Contador_cargan_editan_y_cambian_el_estado()
    {
        var nombre = Nuevo("Permisos");
        var contador = await api.Contador();
        var id = await Crear(contador, nombre);   // el Contador puede crear

        foreach (var (cliente, esperado) in new[] { (await api.Recepcion(), HttpStatusCode.Forbidden), (api.ClienteAnonimo(), HttpStatusCode.Unauthorized) })
        {
            Assert.Equal(esperado, (await cliente.PostAsJsonAsync("/api/entrenadores", Datos(Nuevo("Intruso")))).StatusCode);
            Assert.Equal(esperado, (await cliente.PutAsJsonAsync($"/api/entrenadores/{id}", Datos("Cambiado"))).StatusCode);
            Assert.Equal(esperado, (await Estado(cliente, id, false)).StatusCode);
        }
        var ficha = await Ficha(contador, id);
        Assert.Equal(nombre, (string)ficha["nombre"]!);
        Assert.True((bool)ficha["activo"]!);

        Assert.Equal(HttpStatusCode.NoContent, (await contador.PutAsJsonAsync($"/api/entrenadores/{id}", Datos(nombre, "111", "Pilates"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Estado(await api.Jefe(), id, false)).StatusCode);
        Assert.False((bool)(await Ficha(contador, id))["activo"]!);
    }

    // ---------- nombre repetido ----------

    [Fact]
    public async Task Un_nombre_repetido_en_otra_grafia_se_rechaza()
    {
        var jefe = await api.Jefe();
        var baseDelNombre = Nuevo("Ana López");
        var id = await Crear(jefe, baseDelNombre);
        var otro = await Crear(jefe, Nuevo("Otra Persona"));
        var enMayusculas = "  " + baseDelNombre.ToUpperInvariant().Replace(" ", "   ") + " ";   // «ANA   LÓPEZ …» con espacios de más

        var repetido = await jefe.PostAsJsonAsync("/api/entrenadores", Datos(enMayusculas));
        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);
        Assert.Contains("Ya hay un entrenador", await repetido.Detalle());

        var aOtroNombre = await jefe.PutAsJsonAsync($"/api/entrenadores/{otro}", Datos(enMayusculas));
        Assert.Equal(HttpStatusCode.BadRequest, aOtroNombre.StatusCode);
        Assert.Contains("Ya hay un entrenador", await aOtroNombre.Detalle());

        // Su propio nombre en otra grafía sí se puede: es el mismo entrenador.
        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PutAsJsonAsync($"/api/entrenadores/{id}", Datos(enMayusculas))).StatusCode);
        Assert.Equal(baseDelNombre.ToUpperInvariant(), (string)(await Ficha(jefe, id))["nombre"]!);
    }

    [Fact]
    public async Task Un_nombre_con_espacio_duro_es_el_mismo_entrenador_y_se_rechaza_como_repetido()
    {
        var jefe = await api.Jefe();
        var nombre = Nuevo("Ana López");
        await Crear(jefe, nombre);

        // Pegar un nombre desde WhatsApp o Word trae espacios duros en lugar de espacios comunes.
        var conEspacioDuro = nombre.Replace(" ", "\u00A0");
        var repetido = await jefe.PostAsJsonAsync("/api/entrenadores", Datos(conEspacioDuro));

        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);
        Assert.Contains("Ya hay un entrenador", await repetido.Detalle());
    }

    // ---------- validación en el servidor ----------

    // Marca los casos en que el nombre es válido y lo inválido es otro campo: la prueba le pone un nombre único.
    private const string NombreValido = "<nombre válido>";

    public static IEnumerable<object?[]> Pedidos_invalidos()
    {
        yield return new object?[] { "nombre nulo", null, null, null };
        yield return new object?[] { "nombre vacío", "", null, null };
        yield return new object?[] { "nombre en blanco", "     ", null, null };
        yield return new object?[] { "nombre de 81 letras", new string('x', 81), null, null };
        yield return new object?[] { "nombre con salto de línea", "Ana\nBeto", null, null };
        yield return new object?[] { "nombre con carácter nulo", "Ana\u0000", null, null };
        yield return new object?[] { "teléfono de 31 caracteres", NombreValido, new string('1', 31), null };
        yield return new object?[] { "especialidad de 61 caracteres", NombreValido, null, new string('e', 61) };
    }

    [Theory]
    [MemberData(nameof(Pedidos_invalidos))]
    public async Task Un_pedido_invalido_se_rechaza_con_400_y_no_cambia_nada(string caso, string? nombre, string? telefono, string? especialidad)
    {
        var jefe = await api.Jefe();
        var nombreValido = Nuevo("Valido");
        var id = await Crear(jefe, nombreValido);
        var cantidad = (await Lista(jefe)).Count;
        var nombreDelPedido = nombre == NombreValido ? Nuevo("Valido nuevo") : nombre;

        var alta = await jefe.PostAsJsonAsync("/api/entrenadores", Datos(nombreDelPedido, telefono, especialidad));
        var cambio = await jefe.PutAsJsonAsync($"/api/entrenadores/{id}", Datos(nombreDelPedido, telefono, especialidad));

        Assert.True(alta.StatusCode == HttpStatusCode.BadRequest, $"{caso} (alta): se esperaba 400 y fue {(int)alta.StatusCode}. {await alta.Detalle()}");
        Assert.True(cambio.StatusCode == HttpStatusCode.BadRequest, $"{caso} (cambio): se esperaba 400 y fue {(int)cambio.StatusCode}. {await cambio.Detalle()}");
        Assert.Equal(cantidad, (await Lista(jefe)).Count);
        Assert.Equal(nombreValido, (string)(await Ficha(jefe, id))["nombre"]!);
    }

    // ---------- contacto: email y WhatsApp ----------

    [Fact]
    public async Task El_alta_y_la_edicion_guardan_el_email_y_el_whatsapp_limpios_y_todos_los_roles_los_ven()
    {
        var jefe = await api.Jefe();
        var nombre = Nuevo("Con Contacto");
        var id = await Crear(jefe, nombre, email: "  ana@gym.com ", whatsApp: "+54\u00A011\u00A05555-1234");   // el espacio duro lo trae pegar desde el teléfono

        foreach (var cliente in new[] { jefe, await api.Contador(), await api.Recepcion() })
        {
            var ficha = await Ficha(cliente, id);
            Assert.Equal("ana@gym.com", (string)ficha["email"]!);
            Assert.Equal("+54 11 5555-1234", (string)ficha["whatsApp"]!);
            Assert.False((bool)ficha["tieneFoto"]!);
            Assert.Null((string?)ficha["fotoActualizada"]);
        }

        Assert.Equal(HttpStatusCode.NoContent,
            (await jefe.PutAsJsonAsync($"/api/entrenadores/{id}", Datos(nombre, email: "ana.lopez@gym.com", whatsApp: "(011) 15 5555-1234"))).StatusCode);
        var editada = await Ficha(jefe, id);
        Assert.Equal("ana.lopez@gym.com", (string)editada["email"]!);
        Assert.Equal("(011) 15 5555-1234", (string)editada["whatsApp"]!);

        // Un dato en blanco se guarda como vacío: así se borra un contacto.
        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PutAsJsonAsync($"/api/entrenadores/{id}", Datos(nombre, email: "  ", whatsApp: ""))).StatusCode);
        var vaciada = await Ficha(jefe, id);
        Assert.Null((string?)vaciada["email"]);
        Assert.Null((string?)vaciada["whatsApp"]);
    }

    public static IEnumerable<object?[]> Contactos_invalidos()
    {
        yield return new object?[] { "email sin arroba", "sin-arroba", null };
        yield return new object?[] { "email con espacio", "a b@c.com", null };
        yield return new object?[] { "email sin punto en el dominio", "a@b", null };
        yield return new object?[] { "email de 255 caracteres", new string('a', 247) + "@gym.com", null };
        yield return new object?[] { "whatsApp con letras", null, "abc" };
        yield return new object?[] { "whatsApp con punto", null, "11.5555.1234" };
        yield return new object?[] { "whatsApp de 31 caracteres", null, new string('1', 31) };
    }

    [Theory]
    [MemberData(nameof(Contactos_invalidos))]
    public async Task Email_y_WhatsApp_invalidos_dan_400_y_no_cambian_nada(string caso, string? email, string? whatsApp)
    {
        var jefe = await api.Jefe();
        var nombre = Nuevo("Contacto Valido");
        var id = await Crear(jefe, nombre, email: "valido@gym.com", whatsApp: "11 5555-1234");
        var cantidad = (await Lista(jefe)).Count;

        var alta = await jefe.PostAsJsonAsync("/api/entrenadores", Datos(Nuevo("Contacto Nuevo"), email: email, whatsApp: whatsApp));
        var cambio = await jefe.PutAsJsonAsync($"/api/entrenadores/{id}", Datos(Nuevo("Contacto Cambiado"), email: email, whatsApp: whatsApp));

        Assert.True(alta.StatusCode == HttpStatusCode.BadRequest, $"{caso} (alta): se esperaba 400 y fue {(int)alta.StatusCode}. {await alta.Detalle()}");
        Assert.True(cambio.StatusCode == HttpStatusCode.BadRequest, $"{caso} (cambio): se esperaba 400 y fue {(int)cambio.StatusCode}. {await cambio.Detalle()}");
        Assert.Equal(cantidad, (await Lista(jefe)).Count);
        var ficha = await Ficha(jefe, id);
        Assert.Equal(nombre, (string)ficha["nombre"]!);
        Assert.Equal("valido@gym.com", (string)ficha["email"]!);
        Assert.Equal("11 5555-1234", (string)ficha["whatsApp"]!);
    }

    // ---------- valor de la hora (solo para la gestión) ----------

    [Fact]
    public async Task La_gestion_ve_y_carga_el_valor_de_la_hora_y_Recepcion_siempre_ve_null()
    {
        var jefe = await api.Jefe();
        var id = await Crear(jefe, Nuevo("Con Valor"), valorHora: 1500.50m);

        foreach (var gestion in new[] { jefe, await api.Contador() })
            Assert.Equal(1500.50m, (decimal)(await Ficha(gestion, id))["valorHora"]!);

        var recepcion = await api.Recepcion();
        var vista = await Ficha(recepcion, id);
        Assert.Null((string?)vista["valorHora"]);   // el servidor lo oculta: no llega ni como cero
        Assert.Contains("\"valorHora\":null", (await (await recepcion.GetAsync("/api/entrenadores")).Content.ReadAsStringAsync()).Replace(" ", ""));
    }

    [Fact]
    public async Task Editar_sin_valor_de_hora_lo_borra_y_con_valor_lo_cambia()
    {
        var jefe = await api.Jefe();
        var nombre = Nuevo("Cambia Valor");
        var id = await Crear(jefe, nombre, valorHora: 1000m);

        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PutAsJsonAsync($"/api/entrenadores/{id}", Datos(nombre, valorHora: 1200.25m))).StatusCode);
        Assert.Equal(1200.25m, (decimal)(await Ficha(jefe, id))["valorHora"]!);

        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PutAsJsonAsync($"/api/entrenadores/{id}", Datos(nombre))).StatusCode);   // sin valorHora
        Assert.Null((string?)(await Ficha(jefe, id))["valorHora"]);

        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PutAsJsonAsync($"/api/entrenadores/{id}", Datos(nombre, valorHora: 0m))).StatusCode);
        Assert.Equal(0m, (decimal)(await Ficha(jefe, id))["valorHora"]!);   // cero es un valor, no «sin valor»
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1000000000000")]
    [InlineData("1.234")]
    public async Task Un_valor_de_hora_invalido_da_400_y_no_cambia_nada(string valor)
    {
        var jefe = await api.Jefe();
        var nombre = Nuevo("Valor Valido");
        var id = await Crear(jefe, nombre, valorHora: 1000m);
        var cantidad = (await Lista(jefe)).Count;
        var numero = decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture);

        var alta = await jefe.PostAsJsonAsync("/api/entrenadores", Datos(Nuevo("Valor Nuevo"), valorHora: numero));
        var cambio = await jefe.PutAsJsonAsync($"/api/entrenadores/{id}", Datos(Nuevo("Valor Cambiado"), valorHora: numero));

        Assert.True(alta.StatusCode == HttpStatusCode.BadRequest, $"alta: se esperaba 400 y fue {(int)alta.StatusCode}. {await alta.Detalle()}");
        Assert.True(cambio.StatusCode == HttpStatusCode.BadRequest, $"cambio: se esperaba 400 y fue {(int)cambio.StatusCode}. {await cambio.Detalle()}");
        Assert.Contains("valor de la hora", await alta.Detalle(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(cantidad, (await Lista(jefe)).Count);
        var ficha = await Ficha(jefe, id);
        Assert.Equal(nombre, (string)ficha["nombre"]!);
        Assert.Equal(1000m, (decimal)ficha["valorHora"]!);
    }

    [Fact]
    public async Task Recepcion_no_puede_cargar_ni_cambiar_el_valor_de_la_hora()
    {
        var jefe = await api.Jefe();
        var nombre = Nuevo("Sin Permiso");
        var id = await Crear(jefe, nombre, valorHora: 1000m);
        var recepcion = await api.Recepcion();

        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.PutAsJsonAsync($"/api/entrenadores/{id}", Datos(nombre, valorHora: 5m))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.PostAsJsonAsync("/api/entrenadores", Datos(Nuevo("Intruso"), valorHora: 5m))).StatusCode);
        Assert.Equal(1000m, (decimal)(await Ficha(jefe, id))["valorHora"]!);
    }

    [Fact]
    public async Task El_texto_hostil_se_guarda_como_texto()
    {
        var jefe = await api.Jefe();
        var nombre = "'; DROP TABLE contabilidad.\"Entrenadores\"; -- <script>alert(1)</script> " + Guid.NewGuid().ToString("N")[..6];
        var id = await Crear(jefe, nombre, "=HYPERLINK(\"http://x\")", "Ñandú \"con comillas\" & más");

        var ficha = await Ficha(jefe, id);
        Assert.Equal(nombre, (string)ficha["nombre"]!);
        Assert.Equal("=HYPERLINK(\"http://x\")", (string)ficha["telefono"]!);
        Assert.Equal("Ñandú \"con comillas\" & más", (string)ficha["especialidad"]!);
    }

    [Fact]
    public async Task Un_entrenador_inexistente_da_404()
    {
        var jefe = await api.Jefe();
        var nombre = Nuevo("Existe");
        var existente = await Crear(jefe, nombre);   // la ruta existe y responde para un id real
        var inexistente = existente + 1_000_000;

        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PutAsJsonAsync($"/api/entrenadores/{existente}", Datos(nombre))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await jefe.PutAsJsonAsync($"/api/entrenadores/{inexistente}", Datos("Nadie"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Estado(jefe, inexistente, false)).StatusCode);
    }

    // ---------- activar y desactivar ----------

    [Fact]
    public async Task Desactivar_con_horas_en_el_calendario_se_rechaza_con_la_cantidad()
    {
        var jefe = await api.Jefe();
        var id = await Crear(jefe, Nuevo("Con Horas"));

        await PonerEnCalendario(jefe, id, (1, 6), (1, 7), (2, 6));
        var tres = await Estado(jefe, id, false);
        Assert.Equal(HttpStatusCode.BadRequest, tres.StatusCode);
        Assert.Contains("3 horas en el calendario", await tres.Detalle());
        Assert.True((bool)(await Ficha(jefe, id))["activo"]!);

        await PonerEnCalendario(jefe, id, (1, 6));
        var una = await Estado(jefe, id, false);
        Assert.Equal(HttpStatusCode.BadRequest, una.StatusCode);
        Assert.Contains("1 hora en el calendario", await una.Detalle());

        await PonerEnCalendario(jefe, id);   // calendario vacío
        Assert.Equal(HttpStatusCode.NoContent, (await Estado(jefe, id, false)).StatusCode);
    }

    [Fact]
    public async Task Se_desactiva_sin_horas_y_se_vuelve_a_activar_y_soloActivos_oculta_a_los_desactivados()
    {
        var jefe = await api.Jefe();
        var id = await Crear(jefe, Nuevo("Baja"));

        Assert.Equal(HttpStatusCode.NoContent, (await Estado(jefe, id, false)).StatusCode);
        Assert.False((bool)(await Ficha(jefe, id))["activo"]!);
        Assert.DoesNotContain(await Lista(jefe, soloActivos: true), e => (long)e!["id"]! == id);
        Assert.Contains(await Lista(jefe), e => (long)e!["id"]! == id);

        Assert.Equal(HttpStatusCode.NoContent, (await Estado(jefe, id, true)).StatusCode);
        Assert.Contains(await Lista(jefe, soloActivos: true), e => (long)e!["id"]! == id);
    }

    // ---------- relación con el calendario ----------

    [Fact]
    public async Task La_lista_trae_las_horas_semanales()
    {
        var jefe = await api.Jefe();
        var (conHoras, sinHoras) = (await Crear(jefe, Nuevo("Con Horas Semanales")), await Crear(jefe, Nuevo("Sin Horas")));

        await PonerEnCalendario(jefe, conHoras, (1, 6), (3, 10), (5, 21), (5, 20));

        Assert.Equal(4, (int)(await Ficha(jefe, conHoras))["horasSemanales"]!);
        Assert.Equal(0, (int)(await Ficha(jefe, sinHoras))["horasSemanales"]!);
        await PonerEnCalendario(jefe, conHoras);
    }

    [Fact]
    public async Task Renombrar_a_un_entrenador_se_ve_en_el_calendario()
    {
        var jefe = await api.Jefe();
        var viejo = Nuevo("Nombre Viejo");
        var nuevo = Nuevo("Nombre Nuevo");
        var id = await Crear(jefe, viejo);
        await PonerEnCalendario(jefe, id, (2, 8));
        Assert.Equal(viejo, (string)(await jefe.GetJson("/api/calendario")).AsArray().Single()!["entrenador"]!);

        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PutAsJsonAsync($"/api/entrenadores/{id}", Datos(nuevo))).StatusCode);

        var turno = (await jefe.GetJson("/api/calendario")).AsArray().Single()!;
        Assert.Equal(nuevo, (string)turno["entrenador"]!);
        Assert.Equal(id, (long)turno["entrenadorId"]!);
        await PonerEnCalendario(jefe, id);
    }

    // ---------- copias de seguridad ----------

    [Fact]
    public async Task Un_entrenador_cargado_desde_la_API_entra_en_la_copia_de_seguridad()
    {
        var jefe = await api.Jefe();
        var nombre = Nuevo("Para La Copia");
        await Crear(jefe, nombre);

        var zip = await jefe.GetByteArrayAsync("/api/copias/descargar");
        using var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        using var lector = new StreamReader(archivo.GetEntry("datos/contabilidad.Entrenadores.csv")!.Open());
        Assert.Contains(nombre, await lector.ReadToEndAsync());
    }
}
