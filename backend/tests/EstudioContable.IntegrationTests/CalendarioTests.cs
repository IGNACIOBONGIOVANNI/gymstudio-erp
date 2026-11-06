using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Calendario semanal de encargados: lo ven todos los roles, lo guardan el Administrador y el Contador, valida todo en el servidor,
/// guarda solo las diferencias (y las audita) y lo repiten las restricciones de la propia base.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CalendarioTests(ApiFixture api)
{
    private static object Turno(int dia, int hora, string? encargado) => new { dia, hora, encargado };

    private static async Task<HttpResponseMessage> Guardar(HttpClient cliente, params object?[] turnos)
        => await cliente.PutAsJsonAsync("/api/calendario", new { turnos });

    private static async Task<JsonArray> Leer(HttpClient cliente) => (await cliente.GetJson("/api/calendario")).AsArray();

    private static string Resumen(JsonArray calendario)
        => string.Join(" | ", calendario.Select(t => $"{(int)t!["dia"]!}-{(int)t["hora"]!}:{(string)t["encargado"]!}"));

    /// <summary>Deja el calendario como se pide (vacío si no se pasa nada) y devuelve el cliente administrador.</summary>
    private async Task<HttpClient> Preparar(params object[] turnos)
    {
        var jefe = await api.Jefe();
        var r = await Guardar(jefe, turnos);
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return jefe;
    }

    // ---------- permisos ----------

    [Fact]
    public async Task Todos_los_roles_ven_el_calendario_y_sin_sesion_no_se_ve()
    {
        var jefe = await Preparar(Turno(1, 6, "Ana"));

        Assert.Equal("1-6:Ana", Resumen(await Leer(jefe)));
        Assert.Equal("1-6:Ana", Resumen(await Leer(await api.Contador())));
        Assert.Equal("1-6:Ana", Resumen(await Leer(await api.Recepcion())));
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ClienteAnonimo().GetAsync("/api/calendario")).StatusCode);
    }

    [Fact]
    public async Task Solo_el_administrador_y_el_contador_modifican_el_calendario()
    {
        var jefe = await Preparar(Turno(1, 6, "Ana"));

        Assert.Equal(HttpStatusCode.Forbidden, (await Guardar(await api.Recepcion(), Turno(2, 7, "Intruso"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Guardar(api.ClienteAnonimo(), Turno(2, 7, "Intruso"))).StatusCode);
        Assert.Equal("1-6:Ana", Resumen(await Leer(jefe)));

        var contador = await Guardar(await api.Contador(), Turno(1, 6, "Ana"), Turno(2, 7, "Carlos"));
        Assert.Equal(HttpStatusCode.OK, contador.StatusCode);
        Assert.Equal("1-6:Ana | 2-7:Carlos", Resumen(await Leer(jefe)));
    }

    // ---------- guardar y leer ----------

    [Fact]
    public async Task Se_guarda_ordenado_y_sin_espacios_sobrantes_y_el_guardado_devuelve_el_calendario()
    {
        var jefe = await api.Jefe();
        var r = await Guardar(jefe, Turno(5, 21, "Ana"), Turno(1, 7, "  Beto  "), Turno(1, 6, "Beto"));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var devuelto = (await r.Content.ReadFromJsonAsync<JsonArray>())!;
        Assert.Equal("1-6:Beto | 1-7:Beto | 5-21:Ana", Resumen(devuelto));
        Assert.Equal(Resumen(devuelto), Resumen(await Leer(jefe)));
    }

    [Fact]
    public async Task Un_calendario_vacio_borra_todo()
    {
        var jefe = await Preparar(Turno(1, 6, "Ana"), Turno(2, 7, "Beto"));
        Assert.Equal(2, (await Leer(jefe)).Count);

        Assert.Equal(HttpStatusCode.OK, (await Guardar(jefe)).StatusCode);
        Assert.Empty(await Leer(jefe));
    }

    [Fact]
    public async Task Las_celdas_de_los_extremos_de_la_semana_se_aceptan_y_completa_son_80()
    {
        var jefe = await api.Jefe();
        var todas = Enumerable.Range(1, 5).SelectMany(d => Enumerable.Range(6, 16).Select(h => Turno(d, h, $"E{d}-{h}"))).ToArray();
        Assert.Equal(80, todas.Length);

        Assert.Equal(HttpStatusCode.OK, (await Guardar(jefe, todas)).StatusCode);
        Assert.Equal(80, (await Leer(jefe)).Count);
        await Preparar();
    }

    [Fact]
    public async Task El_texto_hostil_se_guarda_como_texto_y_no_se_ejecuta()
    {
        var jefe = await api.Jefe();
        var hostiles = new[] { "'; DROP TABLE contabilidad.\"TurnosEncargado\"; --", "<script>alert(1)</script>", "=HYPERLINK(\"http://x\")", "Ñandú \"con comillas\" & más" };
        var r = await Guardar(jefe, hostiles.Select((h, i) => Turno(1, 6 + i, h)).ToArray());

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var guardado = (await Leer(jefe)).Select(t => (string)t!["encargado"]!).ToArray();
        Assert.Equal(hostiles, guardado);
        await Preparar();
    }

    // ---------- validación en el servidor ----------

    public static IEnumerable<object[]> Pedidos_invalidos()
    {
        yield return new object[] { "día 0", new object?[] { Turno(0, 8, "Ana") } };
        yield return new object[] { "sábado", new object?[] { Turno(6, 8, "Ana") } };
        yield return new object[] { "antes de las 06", new object?[] { Turno(1, 5, "Ana") } };
        yield return new object[] { "a las 22 el gimnasio ya cerró", new object?[] { Turno(1, 22, "Ana") } };
        yield return new object[] { "hora negativa", new object?[] { Turno(1, -3, "Ana") } };
        yield return new object[] { "día enorme", new object?[] { Turno(int.MaxValue, 8, "Ana") } };
        yield return new object[] { "nombre vacío", new object?[] { Turno(1, 8, "") } };
        yield return new object[] { "nombre en blanco", new object?[] { Turno(1, 8, "     ") } };
        yield return new object[] { "nombre nulo", new object?[] { Turno(1, 8, null) } };
        yield return new object[] { "nombre de 81 letras", new object?[] { Turno(1, 8, new string('x', 81)) } };
        yield return new object[] { "nombre con carácter nulo", new object?[] { Turno(1, 8, "Ana\u0000") } };
        yield return new object[] { "nombre con salto de línea", new object?[] { Turno(1, 8, "Ana\nBeto") } };
        yield return new object[] { "dos encargados en la misma celda", new object?[] { Turno(1, 8, "Ana"), Turno(1, 8, "Beto") } };
        yield return new object[] { "un turno nulo en la lista", new object?[] { Turno(1, 8, "Ana"), null } };
        yield return new object[] { "más de 80 turnos", Enumerable.Range(0, 81).Select(i => (object?)Turno(1 + i % 5, 6 + i % 16, "x")).ToArray() };
    }

    [Theory]
    [MemberData(nameof(Pedidos_invalidos))]
    public async Task Un_pedido_invalido_se_rechaza_con_400_y_no_cambia_nada(string caso, object?[] turnos)
    {
        var jefe = await Preparar(Turno(2, 9, "Original"));

        var r = await Guardar(jefe, turnos);

        Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{caso}: se esperaba 400 y fue {(int)r.StatusCode}. {await r.Detalle()}");
        Assert.Equal("2-9:Original", Resumen(await Leer(jefe)));
    }

    [Fact]
    public async Task Un_cuerpo_sin_turnos_o_roto_se_rechaza_sin_error_interno()
    {
        var jefe = await Preparar(Turno(2, 9, "Original"));

        var sinTurnos = await jefe.PutAsJsonAsync("/api/calendario", new { turnos = (object?)null });
        var vacio = await jefe.PutAsync("/api/calendario", new StringContent("{}", Encoding.UTF8, "application/json"));
        var roto = await jefe.PutAsync("/api/calendario", new StringContent("{ esto no es json", Encoding.UTF8, "application/json"));
        var tipoEquivocado = await jefe.PutAsync("/api/calendario", new StringContent("{\"turnos\":[{\"dia\":\"lunes\",\"hora\":8,\"encargado\":\"Ana\"}]}", Encoding.UTF8, "application/json"));

        foreach (var r in new[] { sinTurnos, vacio, roto, tipoEquivocado })
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("2-9:Original", Resumen(await Leer(jefe)));
    }

    // ---------- guardar solo las diferencias, con su auditoría ----------

    private async Task<long> UltimoIdDeAuditoria(HttpClient jefe)
    {
        var filas = (await jefe.GetJson("/api/auditoria?cantidad=1")).AsArray();
        return filas.Count == 0 ? 0 : (long)filas[0]!["id"]!;
    }

    [Fact]
    public async Task Guardar_registra_en_la_auditoria_solo_lo_que_cambio()
    {
        var jefe = await Preparar(Turno(1, 6, "Ana"), Turno(1, 7, "Ana"), Turno(2, 6, "Beto"));
        var desde = await UltimoIdDeAuditoria(jefe);

        // 1-6 queda igual, 1-7 cambia de persona, 2-6 se borra y 3-8 es nueva.
        var r = await Guardar(jefe, Turno(1, 6, "Ana"), Turno(1, 7, "Carla"), Turno(3, 8, "Dani"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("1-6:Ana | 1-7:Carla | 3-8:Dani", Resumen(await Leer(jefe)));

        var filas = (await jefe.GetJson("/api/auditoria?cantidad=200&tabla=contabilidad.TurnosEncargado")).AsArray()
            .Where(f => (long)f!["id"]! > desde)
            .Select(f => (string)f!["operacion"]!).OrderBy(o => o).ToArray();
        Assert.Equal(new[] { "DELETE", "INSERT", "UPDATE" }, filas);
    }

    [Fact]
    public async Task Guardar_lo_mismo_dos_veces_no_genera_cambios_ni_auditoria()
    {
        var jefe = await Preparar(Turno(4, 10, "Ana"), Turno(4, 11, "Ana"));
        var desde = await UltimoIdDeAuditoria(jefe);

        Assert.Equal(HttpStatusCode.OK, (await Guardar(jefe, Turno(4, 11, "Ana"), Turno(4, 10, "Ana"))).StatusCode);

        var nuevas = (await jefe.GetJson("/api/auditoria?cantidad=200&tabla=contabilidad.TurnosEncargado")).AsArray()
            .Count(f => (long)f!["id"]! > desde);
        Assert.Equal(0, nuevas);
        await Preparar();
    }

    // ---------- restricciones de la base ----------

    private async Task<PostgresException> InsertarDirecto(int dia, int hora, string encargado)
    {
        await using var c = new NpgsqlConnection(api.ConnectionString);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand("INSERT INTO contabilidad.\"TurnosEncargado\" (\"DiaSemana\", \"Hora\", \"Encargado\") VALUES (@d, @h, @e)", c);
        cmd.Parameters.AddWithValue("d", dia);
        cmd.Parameters.AddWithValue("h", hora);
        cmd.Parameters.AddWithValue("e", encargado);
        return await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
    }

    [Theory]
    [InlineData(0, 8, "Ana", "CK_TurnosEncargado_Dia")]
    [InlineData(6, 8, "Ana", "CK_TurnosEncargado_Dia")]
    [InlineData(1, 5, "Ana", "CK_TurnosEncargado_Hora")]
    [InlineData(1, 22, "Ana", "CK_TurnosEncargado_Hora")]
    [InlineData(1, 8, "   ", "CK_TurnosEncargado_Nombre")]
    [InlineData(1, 8, "", "CK_TurnosEncargado_Nombre")]
    public async Task La_base_rechaza_celdas_invalidas_aunque_se_escriba_directo(int dia, int hora, string encargado, string restriccion)
    {
        await Preparar();
        var error = await InsertarDirecto(dia, hora, encargado);
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(restriccion, error.ConstraintName);
    }

    [Fact]
    public async Task La_base_no_admite_dos_encargados_en_la_misma_celda_aunque_se_escriba_directo()
    {
        await Preparar(Turno(2, 8, "Ana"));
        var error = await InsertarDirecto(2, 8, "Beto");
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ux_turnos_encargado_dia_hora", error.ConstraintName);
        await Preparar();
    }

    // ---------- copias de seguridad ----------

    [Fact]
    public async Task La_copia_de_seguridad_incluye_el_calendario()
    {
        var jefe = await Preparar(Turno(1, 6, "Ana Copia"));

        var zip = await jefe.GetByteArrayAsync("/api/copias/descargar");
        using var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var entrada = archivo.GetEntry("datos/contabilidad.TurnosEncargado.csv");
        Assert.NotNull(entrada);
        using var lector = new StreamReader(entrada!.Open());
        Assert.Contains("Ana Copia", await lector.ReadToEndAsync());

        var formulario = new MultipartFormDataContent { { new ByteArrayContent(zip), "file", "copia.zip" } };
        var comparacion = (await (await jefe.PostAsync("/api/copias/comparar", formulario)).Content.ReadFromJsonAsync<JsonObject>())!;
        var tabla = comparacion["tablas"]!.AsArray().First(t => (string?)t!["tabla"] == "contabilidad.TurnosEncargado")!;
        Assert.Equal("Calendario de encargados", (string)tabla["etiqueta"]!);
        Assert.Equal("Gimnasio", (string)tabla["grupo"]!);
        Assert.Equal("igual", (string)tabla["estado"]!);
        await Preparar();
    }
}
