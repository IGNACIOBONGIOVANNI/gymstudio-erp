using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// La foto de la ficha del entrenador: la sube y la quita el Administrador o el Contador, la ven todos los roles con sesión, solo se
/// acepta un JPEG de verdad (la estructura del archivo, no lo que diga quien lo manda), la lista de entrenadores no carga los bytes,
/// la subida y la baja quedan en la auditoría y la foto entra en la copia de seguridad y vuelve igual al restaurarla.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class FotosDeEntrenadorTests(ApiFixture api)
{
    /// <summary>Un nombre que no se repite entre pruebas (la base es la misma para todas).</summary>
    private static string Nuevo(string baseDelNombre) => $"{baseDelNombre} {Guid.NewGuid():N}"[..Math.Min(70, baseDelNombre.Length + 9)];

    private static async Task<long> CrearEntrenador(HttpClient cliente, string baseDelNombre = "Con Foto")
    {
        var r = await cliente.PostAsJsonAsync("/api/entrenadores", new { nombre = Nuevo(baseDelNombre) });
        Assert.True(r.StatusCode == HttpStatusCode.Created, $"Alta: se esperaba 201 y fue {(int)r.StatusCode}. {await r.Detalle()}");
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private static ByteArrayContent Cuerpo(byte[] bytes)
    {
        var contenido = new ByteArrayContent(bytes);
        contenido.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");   // lo que diga el cliente no cuenta
        return contenido;
    }

    private static Task<HttpResponseMessage> Subir(HttpClient cliente, long id, byte[] bytes) => cliente.PutAsync($"/api/entrenadores/{id}/foto", Cuerpo(bytes));

    private static Task<HttpResponseMessage> Quitar(HttpClient cliente, long id) => cliente.DeleteAsync($"/api/entrenadores/{id}/foto");

    private static Task<HttpResponseMessage> Ver(HttpClient cliente, long id) => cliente.GetAsync($"/api/entrenadores/{id}/foto");

    private static async Task<byte[]> BytesDeLaFoto(HttpClient cliente, long id)
    {
        var r = await Ver(cliente, id);
        Assert.True(r.StatusCode == HttpStatusCode.OK, $"Foto: se esperaba 200 y fue {(int)r.StatusCode}. {await r.Detalle()}");
        return await r.Content.ReadAsByteArrayAsync();
    }

    private static async Task<JsonNode> Ficha(HttpClient cliente, long id)
        => (await cliente.GetJson("/api/entrenadores")).AsArray().First(e => (long)e!["id"]! == id)!;

    private static MultipartFormDataContent Formulario(byte[] zip, string? confirmacion = null)
    {
        var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(zip);
        parte.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        contenido.Add(parte, "file", "copia.zip");
        if (confirmacion != null) contenido.Add(new StringContent(confirmacion), "confirmacion");
        return contenido;
    }

    // ---------- permisos ----------

    [Fact]
    public async Task Todos_los_roles_ven_la_foto_y_solo_la_gestion_la_sube_y_la_quita()
    {
        var jefe = await api.Jefe();
        var contador = await api.Contador();
        var recepcion = await api.Recepcion();
        var anonimo = api.ClienteAnonimo();
        var id = await CrearEntrenador(jefe);
        var foto = JpegDePrueba.Crear(256, 256);

        // Sin foto todavía: 404 para quien tiene sesión, 401 sin sesión.
        foreach (var cliente in new[] { jefe, contador, recepcion })
            Assert.Equal(HttpStatusCode.NotFound, (await Ver(cliente, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ver(anonimo, id)).StatusCode);

        // Recepción y quien no entró no pueden subir ni quitar.
        Assert.Equal(HttpStatusCode.Forbidden, (await Subir(recepcion, id, foto)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Subir(anonimo, id, foto)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Ver(jefe, id)).StatusCode);

        // El Contador sí puede subir, y los tres roles la ven.
        Assert.Equal(HttpStatusCode.NoContent, (await Subir(contador, id, foto)).StatusCode);
        foreach (var cliente in new[] { jefe, contador, recepcion })
            Assert.Equal(foto, await BytesDeLaFoto(cliente, id));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ver(anonimo, id)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await Quitar(recepcion, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Quitar(anonimo, id)).StatusCode);
        Assert.Equal(foto, await BytesDeLaFoto(jefe, id));

        Assert.Equal(HttpStatusCode.NoContent, (await Quitar(contador, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Ver(jefe, id)).StatusCode);
    }

    [Fact]
    public async Task Un_entrenador_inexistente_da_404_al_subir_quitar_o_ver_la_foto()
    {
        var jefe = await api.Jefe();
        var inexistente = await CrearEntrenador(jefe) + 1_000_000;

        Assert.Equal(HttpStatusCode.NotFound, (await Subir(jefe, inexistente, JpegDePrueba.Crear(256, 256))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Quitar(jefe, inexistente)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Ver(jefe, inexistente)).StatusCode);
    }

    // ---------- subir, reemplazar, ver y quitar ----------

    [Fact]
    public async Task Se_sube_se_reemplaza_se_ve_y_se_quita_la_foto()
    {
        var jefe = await api.Jefe();
        var id = await CrearEntrenador(jefe);
        var primera = JpegDePrueba.Crear(256, 256, 400);
        var segunda = JpegDePrueba.Crear(300, 300, 700);

        Assert.Equal(HttpStatusCode.NoContent, (await Subir(jefe, id, primera)).StatusCode);
        var vista = await Ver(jefe, id);
        Assert.Equal(HttpStatusCode.OK, vista.StatusCode);
        Assert.Equal(primera, await vista.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/jpeg", vista.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", vista.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("no-cache", string.Join(",", vista.Headers.GetValues("Cache-Control")));
        var etiquetaDeLaPrimera = vista.Headers.ETag;
        Assert.NotNull(etiquetaDeLaPrimera);

        // Reemplazar cambia los bytes y la etiqueta.
        Assert.Equal(HttpStatusCode.NoContent, (await Subir(jefe, id, segunda)).StatusCode);
        var reemplazada = await Ver(jefe, id);
        Assert.Equal(segunda, await reemplazada.Content.ReadAsByteArrayAsync());
        var etiquetaDeLaSegunda = reemplazada.Headers.ETag;
        Assert.NotNull(etiquetaDeLaSegunda);
        Assert.NotEqual(etiquetaDeLaPrimera, etiquetaDeLaSegunda);

        // Con la etiqueta vigente el servidor dice «no cambió»; con la vieja, manda la foto nueva.
        var vigente = new HttpRequestMessage(HttpMethod.Get, $"/api/entrenadores/{id}/foto");
        vigente.Headers.IfNoneMatch.Add(etiquetaDeLaSegunda);
        Assert.Equal(HttpStatusCode.NotModified, (await jefe.SendAsync(vigente)).StatusCode);

        var vieja = new HttpRequestMessage(HttpMethod.Get, $"/api/entrenadores/{id}/foto");
        vieja.Headers.IfNoneMatch.Add(etiquetaDeLaPrimera);
        var conLaVieja = await jefe.SendAsync(vieja);
        Assert.Equal(HttpStatusCode.OK, conLaVieja.StatusCode);
        Assert.Equal(segunda, await conLaVieja.Content.ReadAsByteArrayAsync());

        // Quitarla dos veces no da error; después, 404.
        Assert.Equal(HttpStatusCode.NoContent, (await Quitar(jefe, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Quitar(jefe, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Ver(jefe, id)).StatusCode);
    }

    // ---------- archivos que no son una foto ----------

    public static IEnumerable<object[]> Archivos_que_no_son_una_foto_valida()
    {
        yield return new object[] { "PNG", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.Concat(new byte[300]).ToArray() };
        yield return new object[] { "SVG con script", Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>" + new string(' ', 200)) };
        yield return new object[] { "HTML", Encoding.UTF8.GetBytes("<html><body><script>alert(1)</script></body></html>" + new string(' ', 200)) };
        yield return new object[] { "JPEG truncado", JpegDePrueba.Crear(256, 256)[..^2] };
        yield return new object[] { "JPEG de 20 000 por 20 000", JpegDePrueba.Crear(20_000, 20_000) };
        yield return new object[] { "JPEG de 15 por 15", JpegDePrueba.Crear(15, 15) };
        yield return new object[] { "cuerpo vacío", Array.Empty<byte>() };
        yield return new object[] { "150 001 bytes", JpegDePrueba.Crear(256, 256, 150_001) };
    }

    [Theory]
    [MemberData(nameof(Archivos_que_no_son_una_foto_valida))]
    public async Task Un_archivo_que_no_es_un_JPEG_valido_se_rechaza_con_400_y_no_se_guarda(string caso, byte[] archivo)
    {
        var jefe = await api.Jefe();
        var conFoto = await CrearEntrenador(jefe, "Con Foto Previa");
        var sinFoto = await CrearEntrenador(jefe, "Sin Foto Previa");
        var anterior = JpegDePrueba.Crear(256, 256, 500);
        Assert.Equal(HttpStatusCode.NoContent, (await Subir(jefe, conFoto, anterior)).StatusCode);
        var actualizadaAntes = (string)(await Ficha(jefe, conFoto))["fotoActualizada"]!;

        var alReemplazar = await Subir(jefe, conFoto, archivo);
        var alSubir = await Subir(jefe, sinFoto, archivo);

        Assert.True(alReemplazar.StatusCode == HttpStatusCode.BadRequest, $"{caso} (reemplazo): se esperaba 400 y fue {(int)alReemplazar.StatusCode}. {await alReemplazar.Detalle()}");
        Assert.True(alSubir.StatusCode == HttpStatusCode.BadRequest, $"{caso} (alta): se esperaba 400 y fue {(int)alSubir.StatusCode}. {await alSubir.Detalle()}");
        Assert.False(string.IsNullOrWhiteSpace(await alSubir.Detalle()));

        // La foto que había sigue igual y el que no tenía sigue sin tener.
        Assert.Equal(anterior, await BytesDeLaFoto(jefe, conFoto));
        Assert.Equal(actualizadaAntes, (string)(await Ficha(jefe, conFoto))["fotoActualizada"]!);
        Assert.Equal(HttpStatusCode.NotFound, (await Ver(jefe, sinFoto)).StatusCode);
        Assert.False((bool)(await Ficha(jefe, sinFoto))["tieneFoto"]!);
    }

    [Fact]
    public async Task Un_cuerpo_de_200000_bytes_se_rechaza_sin_guardarse()
    {
        var jefe = await api.Jefe();
        var id = await CrearEntrenador(jefe);

        var r = await Subir(jefe, id, JpegDePrueba.Crear(256, 256, 200_000));

        Assert.True(r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge, $"Se esperaba 400 o 413 y fue {(int)r.StatusCode}.");
        Assert.Equal(HttpStatusCode.NotFound, (await Ver(jefe, id)).StatusCode);
    }

    // ---------- la lista ----------

    [Fact]
    public async Task La_lista_no_trae_los_bytes_pero_dice_si_hay_foto()
    {
        var jefe = await api.Jefe();
        var conFoto = await CrearEntrenador(jefe, "Lista Con Foto");
        var sinFoto = await CrearEntrenador(jefe, "Lista Sin Foto");
        Assert.Equal(HttpStatusCode.NoContent, (await Subir(jefe, conFoto, JpegDePrueba.Crear(256, 256, 140_000))).StatusCode);

        var respuesta = await jefe.GetAsync("/api/entrenadores");
        var texto = await respuesta.Content.ReadAsStringAsync();
        var lista = JsonNode.Parse(texto)!.AsArray();

        // La foto pesa 140 000 bytes: si la lista la llevara (en base64 serían unos 187 000 caracteres) no pasaría de este tope.
        Assert.True(texto.Length < 100_000, $"La lista pesa {texto.Length} caracteres: no debería llevar las fotos.");
        Assert.DoesNotContain("\"foto\"", texto);
        var a = lista.First(e => (long)e!["id"]! == conFoto)!;
        Assert.True((bool)a["tieneFoto"]!);
        Assert.NotNull((string?)a["fotoActualizada"]);
        var b = lista.First(e => (long)e!["id"]! == sinFoto)!;
        Assert.False((bool)b["tieneFoto"]!);
        Assert.Null((string?)b["fotoActualizada"]);

        // Quitar la foto la saca de la lista.
        Assert.Equal(HttpStatusCode.NoContent, (await Quitar(jefe, conFoto)).StatusCode);
        var despues = await Ficha(jefe, conFoto);
        Assert.False((bool)despues["tieneFoto"]!);
        Assert.Null((string?)despues["fotoActualizada"]);
    }

    // ---------- auditoría ----------

    [Fact]
    public async Task Subir_y_quitar_la_foto_quedan_en_la_auditoria_con_el_entrenador_al_que_corresponden()
    {
        var contador = await api.Contador();
        var jefe = await api.Jefe();
        var a = await CrearEntrenador(jefe, "Auditoria A");
        var b = await CrearEntrenador(jefe, "Auditoria B");

        Assert.Equal(HttpStatusCode.NoContent, (await Subir(contador, a, JpegDePrueba.Crear(256, 256))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Subir(contador, b, JpegDePrueba.Crear(256, 256))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Quitar(contador, b)).StatusCode);

        // Cada evento dice quién lo hizo y a qué entrenador le tocó: sin eso, dos renglones «FOTO» no se pueden distinguir.
        var registros = (await jefe.GetJson("/api/auditoria?tabla=entrenador&cantidad=100")).AsArray();
        bool Hay(string operacion, long entrenadorId) => registros.Any(x =>
            (string?)x!["operacion"] == operacion && (string?)x["usuario"] == "contador@gymstudio.com" && (string?)x["registroId"] == entrenadorId.ToString());
        Assert.True(Hay("FOTO", a), "falta el evento FOTO del entrenador A");
        Assert.True(Hay("FOTO", b), "falta el evento FOTO del entrenador B");
        Assert.True(Hay("FOTO_BAJA", b), "falta el evento FOTO_BAJA del entrenador B");
        Assert.False(Hay("FOTO_BAJA", a), "A no perdió su foto: no tiene que haber FOTO_BAJA de A");
    }

    // ---------- copia de seguridad ----------

    [Fact]
    public async Task La_foto_entra_en_la_copia_de_seguridad_y_vuelve_igual_al_restaurarla()
    {
        var jefe = await api.Jefe();
        var id = await CrearEntrenador(jefe, "Foto En La Copia");
        var foto = JpegDePrueba.Crear(256, 256, 5_000);
        Assert.Equal(HttpStatusCode.NoContent, (await Subir(jefe, id, foto)).StatusCode);

        var zip = await jefe.GetByteArrayAsync("/api/copias/descargar");
        using (var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
            Assert.NotNull(archivo.GetEntry("datos/contabilidad.FotosDeEntrenador.csv"));

        var comparar = await jefe.PostAsync("/api/copias/comparar", Formulario(zip));
        Assert.True(comparar.IsSuccessStatusCode, await comparar.Detalle());
        var comparacion = (await comparar.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("identica", (string)comparacion["veredicto"]!);
        var tabla = comparacion["tablas"]!.AsArray().Single(t => (string?)t!["tabla"] == "contabilidad.FotosDeEntrenador")!;
        Assert.Equal("Fotos de los entrenadores", (string)tabla["etiqueta"]!);
        Assert.Equal("igual", (string)tabla["estado"]!);

        // Se quita la foto, se restaura la copia y la foto vuelve con los mismos bytes.
        Assert.Equal(HttpStatusCode.NoContent, (await Quitar(jefe, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Ver(jefe, id)).StatusCode);

        var restaurar = await jefe.PostAsync("/api/copias/restaurar", Formulario(zip, confirmacion: "RESTAURAR"));
        Assert.True(restaurar.IsSuccessStatusCode, await restaurar.Detalle());

        Assert.Equal(foto, await BytesDeLaFoto(jefe, id));
        Assert.True((bool)(await Ficha(jefe, id))["tieneFoto"]!);
    }

    // ---------- la base ----------

    [Fact]
    public async Task La_base_rechaza_fotos_fuera_de_tamano_y_emails_mal_formados_aunque_se_escriba_directo()
    {
        var jefe = await api.Jefe();
        var id = await CrearEntrenador(jefe, "Reglas De La Base");

        foreach (var bytes in new[] { 99, 150_001 })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(
                """INSERT INTO contabilidad."FotosDeEntrenador" ("EntrenadorId", "Foto", "Actualizada") VALUES (@id, decode(repeat('ff', @bytes), 'hex'), now())""",
                ("id", id), ("bytes", bytes)));
            Assert.Equal("CK_FotosDeEntrenador_Tamano", error.ConstraintName);
        }

        var email = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(
            """UPDATE contabilidad."Entrenadores" SET "Email" = @email WHERE "Id" = @id""", ("id", id), ("email", "sin-arroba")));
        Assert.Equal("CK_Entrenadores_Email", email.ConstraintName);

        // Lo que la base acepta: 100 y 150 000 bytes, y un email con forma de email.
        foreach (var bytes in new[] { 100, 150_000 })
            await EjecutarAsync("""
                INSERT INTO contabilidad."FotosDeEntrenador" ("EntrenadorId", "Foto", "Actualizada") VALUES (@id, decode(repeat('ff', @bytes), 'hex'), now())
                ON CONFLICT ("EntrenadorId") DO UPDATE SET "Foto" = EXCLUDED."Foto"
                """, ("id", id), ("bytes", bytes));
        await EjecutarAsync("""UPDATE contabilidad."Entrenadores" SET "Email" = @email WHERE "Id" = @id""", ("id", id), ("email", "ana@gym.com"));
    }

    private async Task EjecutarAsync(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = new NpgsqlConnection(api.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros) comando.Parameters.AddWithValue(nombre, valor);
        await comando.ExecuteNonQueryAsync();
    }
}
