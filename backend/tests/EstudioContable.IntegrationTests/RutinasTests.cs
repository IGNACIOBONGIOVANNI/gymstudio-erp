using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>Rutinas de gimnasio: catálogo, asignación a socios y permisos, contra la API y la base reales.</summary>
[Collection(ColeccionApi.Nombre)]
public class RutinasTests(ApiFixture api)
{
    private static int _contador;
    private static string Unico(string prefijo) => $"{prefijo} {Interlocked.Increment(ref _contador)}-{Guid.NewGuid().ToString("N")[..6]}";

    private async Task<long> Ejercicio(HttpClient cliente, string? nombre = null)
    {
        var r = await cliente.PostAsJsonAsync("/api/ejercicios", new { nombre = nombre ?? Unico("Ejercicio"), grupoMuscular = "Piernas" });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private async Task<long> Rutina(HttpClient cliente, params long[] ejercicios)
    {
        var r = await cliente.PostAsJsonAsync("/api/rutinas", CuerpoRutina(Unico("Rutina"), ejercicios));
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private static object CuerpoRutina(string nombre, long[] ejercicios) => new
    {
        nombre,
        objetivo = "Prueba",
        nivel = "Principiante",
        ejercicios = ejercicios.Select((id, i) => new { ejercicioId = id, dia = 1 + i % 2, series = 3, repeticiones = "10", descansoSegundos = 60, indicaciones = "" }).ToArray()
    };

    private async Task<long> Socio(HttpClient cliente)
    {
        var dni = Random.Shared.Next(20_000_000, 49_999_999).ToString();
        var r = await cliente.PostAsJsonAsync("/api/socios", new { nombre = "Socio", apellido = Unico("Prueba"), dni, telefono = "" });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private static object Asignacion(long rutinaId, string inicio = "2026-10-01", string? fin = null)
        => new { rutinaId, fechaInicio = inicio, fechaFin = fin, observaciones = "Cuidar la rodilla" };

    [Fact]
    public async Task Los_datos_iniciales_traen_ejercicios_y_rutinas_de_ejemplo()
    {
        var jefe = await api.Jefe();
        Assert.True((await jefe.GetJson("/api/ejercicios")).AsArray().Count >= 20);
        Assert.Contains((await jefe.GetJson("/api/rutinas")).AsArray(), r => (string?)r!["nombre"] == "Full body principiante");
    }

    [Fact]
    public async Task Asignar_una_rutina_nueva_cierra_la_anterior_y_deja_una_sola_activa()
    {
        var jefe = await api.Jefe();
        var ej = await Ejercicio(jefe);
        var primera = await Rutina(jefe, ej);
        var segunda = await Rutina(jefe, ej);
        var socio = await Socio(jefe);

        var a1 = await jefe.PostAsJsonAsync($"/api/socios/{socio}/rutinas", Asignacion(primera, "2026-10-01"));
        Assert.True(a1.IsSuccessStatusCode, await a1.Detalle());
        var a2 = await jefe.PostAsJsonAsync($"/api/socios/{socio}/rutinas", Asignacion(segunda, "2026-10-20"));
        Assert.True(a2.IsSuccessStatusCode, await a2.Detalle());

        var historial = (await jefe.GetJson($"/api/socios/{socio}/rutinas")).AsArray();
        Assert.Equal(2, historial.Count);
        Assert.Single(historial, h => (bool)h!["activa"]!);

        var actual = historial.Single(h => (bool)h!["activa"]!)!;
        Assert.Equal(segunda, (long)actual["rutinaId"]!);
        var cerrada = historial.Single(h => !(bool)h!["activa"]!)!;
        Assert.Equal(primera, (long)cerrada["rutinaId"]!);
        Assert.Equal("2026-10-20", (string)cerrada["fechaFin"]!);   // se cerró el día que empezó la nueva
    }

    [Fact]
    public async Task Recepcion_asigna_y_finaliza_rutinas_pero_no_arma_el_catalogo()
    {
        var jefe = await api.Jefe();
        var recepcion = await api.Recepcion();
        var rutina = await Rutina(jefe, await Ejercicio(jefe));
        var socio = await Socio(recepcion);

        var asignada = await recepcion.PostAsJsonAsync($"/api/socios/{socio}/rutinas", Asignacion(rutina));
        Assert.True(asignada.IsSuccessStatusCode, await asignada.Detalle());
        var id = (long)(await asignada.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        Assert.Equal(HttpStatusCode.NoContent, (await recepcion.PostAsJsonAsync($"/api/asignaciones-rutina/{id}/finalizar", new { fecha = "2026-10-25" })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.PostAsJsonAsync("/api/ejercicios", new { nombre = Unico("X"), grupoMuscular = "Pecho" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.PostAsJsonAsync("/api/rutinas", CuerpoRutina(Unico("X"), [1]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.DeleteAsync($"/api/rutinas/{rutina}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.DeleteAsync("/api/ejercicios/1")).StatusCode);
    }

    [Fact]
    public async Task La_base_impide_dos_rutinas_activas_para_el_mismo_socio()
    {
        var jefe = await api.Jefe();
        var rutina = await Rutina(jefe, await Ejercicio(jefe));
        var socio = await Socio(jefe);
        Assert.True((await jefe.PostAsJsonAsync($"/api/socios/{socio}/rutinas", Asignacion(rutina))).IsSuccessStatusCode);

        // Se salta la regla de la API y se inserta directo: el índice parcial lo rechaza igual.
        await using var conexion = new Npgsql.NpgsqlConnection(api.ConnectionString);
        await conexion.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand(
            $"INSERT INTO contabilidad.\"AsignacionesRutina\" (\"SocioId\", \"RutinaId\", \"FechaInicio\", \"Observaciones\", \"Activa\") VALUES ({socio}, {rutina}, '2026-10-02', '', true)", conexion);
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal(Npgsql.PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ux_asignaciones_rutina_activa_por_socio", error.ConstraintName);
    }

    [Fact]
    public async Task Una_rutina_con_socios_se_desactiva_en_lugar_de_borrarse_y_ya_no_se_puede_asignar()
    {
        var jefe = await api.Jefe();
        var ej = await Ejercicio(jefe);
        var usada = await Rutina(jefe, ej);
        var libre = await Rutina(jefe, ej);
        var socio = await Socio(jefe);
        Assert.True((await jefe.PostAsJsonAsync($"/api/socios/{socio}/rutinas", Asignacion(usada))).IsSuccessStatusCode);

        var baja = await (await jefe.DeleteAsync($"/api/rutinas/{usada}")).Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("desactivada", (string)baja!["accion"]!);
        var borrada = await (await jefe.DeleteAsync($"/api/rutinas/{libre}")).Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("eliminada", (string)borrada!["accion"]!);

        var otroSocio = await Socio(jefe);
        var rechazo = await jefe.PostAsJsonAsync($"/api/socios/{otroSocio}/rutinas", Asignacion(usada));
        Assert.Equal(HttpStatusCode.BadRequest, rechazo.StatusCode);
        Assert.Contains("desactivada", await rechazo.Detalle());
    }

    [Fact]
    public async Task Un_ejercicio_en_uso_se_desactiva_y_los_nombres_no_se_repiten_sin_importar_mayusculas()
    {
        var jefe = await api.Jefe();
        var nombre = Unico("Remo especial");
        var ej = await Ejercicio(jefe, nombre);
        await Rutina(jefe, ej);

        var repetido = await jefe.PostAsJsonAsync("/api/ejercicios", new { nombre = nombre.ToUpperInvariant(), grupoMuscular = "Espalda" });
        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);

        var baja = await (await jefe.DeleteAsync($"/api/ejercicios/{ej}")).Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("desactivado", (string)baja!["accion"]!);
        Assert.DoesNotContain((await jefe.GetJson("/api/ejercicios")).AsArray(), e => (long)e!["id"]! == ej);

        var nueva = await jefe.PostAsJsonAsync("/api/rutinas", CuerpoRutina(Unico("Otra"), [ej]));
        Assert.Equal(HttpStatusCode.BadRequest, nueva.StatusCode);   // no se puede usar un ejercicio desactivado
    }

    [Fact]
    public async Task Editar_una_rutina_reemplaza_sus_ejercicios_y_el_detalle_sale_ordenado_por_dia()
    {
        var jefe = await api.Jefe();
        var e1 = await Ejercicio(jefe); var e2 = await Ejercicio(jefe); var e3 = await Ejercicio(jefe);
        var rutina = await Rutina(jefe, e1, e2);

        var edicion = await jefe.PutAsJsonAsync($"/api/rutinas/{rutina}", CuerpoRutina(Unico("Editada"), [e3, e1, e2]));
        Assert.Equal(HttpStatusCode.NoContent, edicion.StatusCode);

        var detalle = await jefe.GetJson($"/api/rutinas/{rutina}");
        var ids = detalle["ejercicios"]!.AsArray().Select(e => (long)e!["ejercicioId"]!).ToArray();
        // dia 1: e3 y e2 (posiciones 0 y 2), dia 2: e1
        Assert.Equal(new[] { e3, e2, e1 }, ids);
    }

    [Fact]
    public async Task Un_renglon_invalido_no_guarda_nada()
    {
        var jefe = await api.Jefe();
        var r = await jefe.PostAsJsonAsync("/api/rutinas", new
        {
            nombre = Unico("Mala"), nivel = "Principiante",
            ejercicios = new[] { new { ejercicioId = 1L, dia = 9, series = 3, repeticiones = "10", descansoSegundos = 60, indicaciones = "" } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Un_socio_con_rutinas_no_se_puede_eliminar()
    {
        var jefe = await api.Jefe();
        var socio = await Socio(jefe);
        var rutina = await Rutina(jefe, await Ejercicio(jefe));
        Assert.True((await jefe.PostAsJsonAsync($"/api/socios/{socio}/rutinas", Asignacion(rutina))).IsSuccessStatusCode);

        var r = await jefe.DeleteAsync($"/api/socios/{socio}");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("rutinas", await r.Detalle());
    }
}
