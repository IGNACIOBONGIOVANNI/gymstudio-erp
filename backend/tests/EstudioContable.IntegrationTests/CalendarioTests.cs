using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Calendario semanal de entrenadores: lo ven todos los roles, lo guardan el Administrador y el Contador, valida todo en el servidor
/// (celdas y que el entrenador exista y esté activo), guarda solo las diferencias (y las audita) y lo repiten las restricciones de la
/// propia base.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CalendarioTests(ApiFixture api)
{
    private static object Turno(int dia, int hora, long entrenadorId) => new { dia, hora, entrenadorId };

    private static async Task<HttpResponseMessage> Guardar(HttpClient cliente, params object?[] turnos)
        => await cliente.PutAsJsonAsync("/api/calendario", new { turnos });

    private static async Task<JsonArray> Leer(HttpClient cliente) => (await cliente.GetJson("/api/calendario")).AsArray();

    private static string Resumen(JsonArray calendario)
        => string.Join(" | ", calendario.Select(t => $"{(int)t!["dia"]!}-{(int)t["hora"]!}:{(string)t["entrenador"]!}"));

    private Task<long> E(string nombre) => api.Asegurar(nombre);

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
        var ana = await E("Ana");
        var jefe = await Preparar(Turno(1, 6, ana));

        Assert.Equal("1-6:Ana", Resumen(await Leer(jefe)));
        Assert.Equal("1-6:Ana", Resumen(await Leer(await api.Contador())));
        Assert.Equal("1-6:Ana", Resumen(await Leer(await api.Recepcion())));
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ClienteAnonimo().GetAsync("/api/calendario")).StatusCode);
        await Preparar();
    }

    [Fact]
    public async Task Solo_el_administrador_y_el_contador_modifican_el_calendario()
    {
        var (ana, carlos, intruso) = (await E("Ana"), await E("Carlos"), await E("Intruso"));
        var jefe = await Preparar(Turno(1, 6, ana));

        Assert.Equal(HttpStatusCode.Forbidden, (await Guardar(await api.Recepcion(), Turno(2, 7, intruso))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Guardar(api.ClienteAnonimo(), Turno(2, 7, intruso))).StatusCode);
        Assert.Equal("1-6:Ana", Resumen(await Leer(jefe)));

        var contador = await Guardar(await api.Contador(), Turno(1, 6, ana), Turno(2, 7, carlos));
        Assert.Equal(HttpStatusCode.OK, contador.StatusCode);
        Assert.Equal("1-6:Ana | 2-7:Carlos", Resumen(await Leer(jefe)));
        await Preparar();
    }

    // ---------- guardar y leer ----------

    [Fact]
    public async Task Se_guarda_ordenado_y_el_guardado_devuelve_el_calendario_con_el_id_y_el_nombre()
    {
        var (ana, beto) = (await E("Ana"), await E("Beto"));
        var jefe = await api.Jefe();
        var r = await Guardar(jefe, Turno(5, 21, ana), Turno(1, 7, beto), Turno(1, 6, beto));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var devuelto = (await r.Content.ReadFromJsonAsync<JsonArray>())!;
        Assert.Equal("1-6:Beto | 1-7:Beto | 5-21:Ana", Resumen(devuelto));
        Assert.Equal(beto, (long)devuelto[0]!["entrenadorId"]!);
        Assert.Equal(Resumen(devuelto), Resumen(await Leer(jefe)));
        await Preparar();
    }

    [Fact]
    public async Task Un_calendario_vacio_borra_todo()
    {
        var (ana, beto) = (await E("Ana"), await E("Beto"));
        var jefe = await Preparar(Turno(1, 6, ana), Turno(2, 7, beto));
        Assert.Equal(2, (await Leer(jefe)).Count);

        Assert.Equal(HttpStatusCode.OK, (await Guardar(jefe)).StatusCode);
        Assert.Empty(await Leer(jefe));
    }

    [Fact]
    public async Task Las_celdas_de_los_extremos_de_la_semana_se_aceptan_y_completa_son_80()
    {
        var extremo = await E("Extremo");
        var jefe = await api.Jefe();
        var todas = Enumerable.Range(1, 5).SelectMany(d => Enumerable.Range(6, 16).Select(h => Turno(d, h, extremo))).ToArray();
        Assert.Equal(80, todas.Length);

        Assert.Equal(HttpStatusCode.OK, (await Guardar(jefe, todas)).StatusCode);
        Assert.Equal(80, (await Leer(jefe)).Count);
        await Preparar();
    }

    [Fact]
    public async Task El_texto_hostil_de_un_entrenador_se_guarda_y_se_muestra_como_texto()
    {
        var hostiles = new[] { "'; DROP TABLE contabilidad.\"TurnosEncargado\"; --", "<script>alert(1)</script>", "=HYPERLINK(\"http://x\")", "Ñandú \"con comillas\" & más" };
        var ids = new List<long>();
        foreach (var h in hostiles) ids.Add(await E(h));
        var jefe = await api.Jefe();

        var r = await Guardar(jefe, ids.Select((id, i) => Turno(1, 6 + i, id)).ToArray());

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var guardado = (await Leer(jefe)).Select(t => (string)t!["entrenador"]!).ToArray();
        Assert.Equal(hostiles, guardado);
        await Preparar();
    }

    // ---------- validación en el servidor ----------

    private static object[] Caso(string nombre, Func<long, object?[]> turnos) => new object[] { nombre, turnos };

    public static IEnumerable<object[]> Pedidos_invalidos()
    {
        yield return Caso("día 0", id => new object?[] { Turno(0, 8, id) });
        yield return Caso("sábado", id => new object?[] { Turno(6, 8, id) });
        yield return Caso("antes de las 06", id => new object?[] { Turno(1, 5, id) });
        yield return Caso("a las 22 el gimnasio ya cerró", id => new object?[] { Turno(1, 22, id) });
        yield return Caso("hora negativa", id => new object?[] { Turno(1, -3, id) });
        yield return Caso("día enorme", id => new object?[] { Turno(int.MaxValue, 8, id) });
        yield return Caso("entrenador 0", _ => new object?[] { Turno(1, 8, 0) });
        yield return Caso("entrenador negativo", _ => new object?[] { Turno(1, 8, -4) });
        yield return Caso("entrenador que no existe", _ => new object?[] { Turno(1, 8, long.MaxValue) });
        yield return Caso("dos turnos en la misma celda", id => new object?[] { Turno(1, 8, id), Turno(1, 8, id) });
        yield return Caso("un turno nulo en la lista", id => new object?[] { Turno(1, 8, id), null });
        yield return Caso("más de 80 turnos", id => Enumerable.Range(0, 81).Select(i => (object?)Turno(1 + i % 5, 6 + i % 16, id)).ToArray());
    }

    [Theory]
    [MemberData(nameof(Pedidos_invalidos))]
    public async Task Un_pedido_invalido_se_rechaza_con_400_y_no_cambia_nada(string caso, Func<long, object?[]> turnos)
    {
        var (original, otro) = (await E("Original"), await E("Otro"));
        var jefe = await Preparar(Turno(2, 9, original));

        var r = await Guardar(jefe, turnos(otro));

        Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{caso}: se esperaba 400 y fue {(int)r.StatusCode}. {await r.Detalle()}");
        Assert.Equal("2-9:Original", Resumen(await Leer(jefe)));
        await Preparar();
    }

    [Fact]
    public async Task Un_entrenador_desactivado_se_rechaza_con_400_y_no_cambia_nada()
    {
        var (original, desactivada) = (await E("Original"), await E("Desactivada"));
        var jefe = await Preparar(Turno(2, 9, original));
        await api.PonerActivo(desactivada, false);

        var r = await Guardar(jefe, Turno(2, 9, original), Turno(3, 10, desactivada));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("desactivado", await r.Detalle());
        Assert.Equal("2-9:Original", Resumen(await Leer(jefe)));
        await Preparar();
    }

    [Fact]
    public async Task Un_cuerpo_sin_turnos_o_roto_se_rechaza_sin_error_interno()
    {
        var original = await E("Original");
        var jefe = await Preparar(Turno(2, 9, original));

        var sinTurnos = await jefe.PutAsJsonAsync("/api/calendario", new { turnos = (object?)null });
        var vacio = await jefe.PutAsync("/api/calendario", new StringContent("{}", Encoding.UTF8, "application/json"));
        var roto = await jefe.PutAsync("/api/calendario", new StringContent("{ esto no es json", Encoding.UTF8, "application/json"));
        var tipoEquivocado = await jefe.PutAsync("/api/calendario", new StringContent($"{{\"turnos\":[{{\"dia\":\"lunes\",\"hora\":8,\"entrenadorId\":{original}}}]}}", Encoding.UTF8, "application/json"));

        foreach (var r in new[] { sinTurnos, vacio, roto, tipoEquivocado })
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("2-9:Original", Resumen(await Leer(jefe)));
        await Preparar();
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
        var (ana, beto, carla, dani) = (await E("Ana"), await E("Beto"), await E("Carla"), await E("Dani"));
        var jefe = await Preparar(Turno(1, 6, ana), Turno(1, 7, ana), Turno(2, 6, beto));
        var desde = await UltimoIdDeAuditoria(jefe);

        // 1-6 queda igual, 1-7 cambia de persona, 2-6 se borra y 3-8 es nueva.
        var r = await Guardar(jefe, Turno(1, 6, ana), Turno(1, 7, carla), Turno(3, 8, dani));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("1-6:Ana | 1-7:Carla | 3-8:Dani", Resumen(await Leer(jefe)));

        var filas = (await jefe.GetJson("/api/auditoria?cantidad=200&tabla=contabilidad.TurnosEncargado")).AsArray()
            .Where(f => (long)f!["id"]! > desde)
            .Select(f => (string)f!["operacion"]!).OrderBy(o => o).ToArray();
        Assert.Equal(new[] { "DELETE", "INSERT", "UPDATE" }, filas);
        await Preparar();
    }

    [Fact]
    public async Task Guardar_lo_mismo_dos_veces_no_genera_cambios_ni_auditoria()
    {
        var ana = await E("Ana");
        var jefe = await Preparar(Turno(4, 10, ana), Turno(4, 11, ana));
        var desde = await UltimoIdDeAuditoria(jefe);

        Assert.Equal(HttpStatusCode.OK, (await Guardar(jefe, Turno(4, 11, ana), Turno(4, 10, ana))).StatusCode);

        var nuevas = (await jefe.GetJson("/api/auditoria?cantidad=200&tabla=contabilidad.TurnosEncargado")).AsArray()
            .Count(f => (long)f!["id"]! > desde);
        Assert.Equal(0, nuevas);
        await Preparar();
    }

    // ---------- restricciones de la base ----------

    private async Task<PostgresException> InsertarDirecto(int dia, int hora, long entrenadorId)
    {
        await using var c = new NpgsqlConnection(api.ConnectionString);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand("INSERT INTO contabilidad.\"TurnosEncargado\" (\"DiaSemana\", \"Hora\", \"EntrenadorId\") VALUES (@d, @h, @e)", c);
        cmd.Parameters.AddWithValue("d", dia);
        cmd.Parameters.AddWithValue("h", hora);
        cmd.Parameters.AddWithValue("e", entrenadorId);
        return await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
    }

    [Theory]
    [InlineData(0, 8, "CK_TurnosEncargado_Dia")]
    [InlineData(6, 8, "CK_TurnosEncargado_Dia")]
    [InlineData(1, 5, "CK_TurnosEncargado_Hora")]
    [InlineData(1, 22, "CK_TurnosEncargado_Hora")]
    public async Task La_base_rechaza_celdas_invalidas_aunque_se_escriba_directo(int dia, int hora, string restriccion)
    {
        var ana = await E("Ana");
        await Preparar();
        var error = await InsertarDirecto(dia, hora, ana);
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(restriccion, error.ConstraintName);
    }

    [Fact]
    public async Task La_base_no_admite_dos_entrenadores_en_la_misma_celda_aunque_se_escriba_directo()
    {
        var (ana, beto) = (await E("Ana"), await E("Beto"));
        await Preparar(Turno(2, 8, ana));
        var error = await InsertarDirecto(2, 8, beto);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ux_turnos_encargado_dia_hora", error.ConstraintName);
        await Preparar();
    }

    [Fact]
    public async Task La_base_rechaza_un_turno_de_un_entrenador_inexistente()
    {
        await Preparar();
        var error = await InsertarDirecto(1, 8, long.MaxValue);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal("FK_TurnosEncargado_Entrenadores_EntrenadorId", error.ConstraintName);
    }

    [Fact]
    public async Task La_base_no_deja_borrar_un_entrenador_con_turnos()
    {
        var ana = await E("Ana");
        await Preparar(Turno(1, 6, ana));

        await using var c = new NpgsqlConnection(api.ConnectionString);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM contabilidad.\"Entrenadores\" WHERE \"Id\" = @id", c);
        cmd.Parameters.AddWithValue("id", ana);
        var error = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal("FK_TurnosEncargado_Entrenadores_EntrenadorId", error.ConstraintName);
        await Preparar();
    }

    // ---------- copias de seguridad ----------

    [Fact]
    public async Task La_copia_de_seguridad_incluye_el_calendario_y_a_sus_entrenadores()
    {
        var copia = await E("Ana Copia");
        var jefe = await Preparar(Turno(1, 6, copia));

        var zip = await jefe.GetByteArrayAsync("/api/copias/descargar");
        using var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        Assert.NotNull(archivo.GetEntry("datos/contabilidad.TurnosEncargado.csv"));
        var entrenadores = archivo.GetEntry("datos/contabilidad.Entrenadores.csv");
        Assert.NotNull(entrenadores);
        using var lector = new StreamReader(entrenadores!.Open());
        Assert.Contains("Ana Copia", await lector.ReadToEndAsync());

        var formulario = new MultipartFormDataContent { { new ByteArrayContent(zip), "file", "copia.zip" } };
        var comparacion = (await (await jefe.PostAsync("/api/copias/comparar", formulario)).Content.ReadFromJsonAsync<JsonObject>())!;
        var tablas = comparacion["tablas"]!.AsArray();

        var turnos = tablas.First(t => (string?)t!["tabla"] == "contabilidad.TurnosEncargado")!;
        Assert.Equal("Calendario de entrenadores", (string)turnos["etiqueta"]!);
        Assert.Equal("Gimnasio", (string)turnos["grupo"]!);
        Assert.Equal("igual", (string)turnos["estado"]!);

        var fichas = tablas.First(t => (string?)t!["tabla"] == "contabilidad.Entrenadores")!;
        Assert.Equal("Entrenadores", (string)fichas["etiqueta"]!);
        Assert.Equal("igual", (string)fichas["estado"]!);
        await Preparar();
    }
}
