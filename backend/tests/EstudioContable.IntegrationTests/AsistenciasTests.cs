using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>Ingreso de socios al gimnasio, contra la API y la base reales.</summary>
[Collection(ColeccionApi.Nombre)]
public class AsistenciasTests(ApiFixture api)
{
    private static int _contador;
    private static string Unico(string prefijo) => $"{prefijo} {Nombres.Aleatorio(10)}";
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));

    private async Task<(long Id, string Dni)> Socio(HttpClient cliente)
    {
        var dni = Random.Shared.Next(20_000_000, 49_999_999).ToString();
        var r = await cliente.PostAsJsonAsync("/api/socios", new { nombre = "Socio", apellido = Unico("Ingreso"), dni, telefono = "" });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return ((long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!, dni);
    }

    private async Task PagarPlan(HttpClient cliente, long socioId)
    {
        var plan = await cliente.PostAsJsonAsync("/api/planes", new { nombre = Unico("Plan ingreso"), precioMensual = 1000, duracionMeses = 1 });
        plan.EnsureSuccessStatusCode();
        var planId = (long)(await plan.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        var cobro = await cliente.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", new { socioId, planId, metodoPago = "caja" });
        Assert.True(cobro.IsSuccessStatusCode, await cobro.Detalle());
    }

    private async Task<long> Rutina(HttpClient cliente, string ejercicioDelDia1, string ejercicioDelDia2)
    {
        var ids = new List<long>();
        foreach (var nombre in new[] { ejercicioDelDia1, ejercicioDelDia2 })
        {
            var e = await cliente.PostAsJsonAsync("/api/ejercicios", new { nombre, grupoMuscular = "Piernas" });
            Assert.True(e.IsSuccessStatusCode, await e.Detalle());
            ids.Add((long)(await e.Content.ReadFromJsonAsync<JsonObject>())!["id"]!);
        }
        var r = await cliente.PostAsJsonAsync("/api/rutinas", new
        {
            nombre = Unico("Rutina ingreso"),
            objetivo = "Prueba",
            nivel = "Principiante",
            ejercicios = ids.Select((id, i) => new { ejercicioId = id, dia = 1 + i, series = 3, repeticiones = "10", descansoSegundos = 60, indicaciones = "" }).ToArray()
        });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private async Task Asignar(HttpClient cliente, long socioId, long rutinaId, DateOnly inicio)
    {
        var r = await cliente.PostAsJsonAsync($"/api/socios/{socioId}/rutinas", new { rutinaId, fechaInicio = inicio.ToString("yyyy-MM-dd"), fechaFin = (string?)null, observaciones = "" });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
    }

    private async Task Sql(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var c = new NpgsqlConnection(api.ConnectionString);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        foreach (var (nombre, valor) in parametros) cmd.Parameters.AddWithValue(nombre, valor);
        await cmd.ExecuteNonQueryAsync();
    }

    private Task VisitaPasada(long socioId, DateOnly fecha)
        => Sql("""INSERT INTO contabilidad."AsistenciasSocio" ("SocioId","Fecha","RegistradaEn","RegistradaPor") VALUES (@s,@f,now(),'prueba')""",
            ("s", socioId), ("f", fecha));

    private static Task<HttpResponseMessage> Ingresar(HttpClient cliente, string dni) => cliente.PostAsJsonAsync("/api/asistencias/ingreso", new { dni });

    private static async Task<JsonObject> Ok(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (await r.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    [Fact]
    public async Task El_ingreso_de_un_socio_sin_plan_se_registra_y_avisa_que_no_tiene_cuota()
    {
        var recepcion = await api.Recepcion();
        var (id, dni) = await Socio(await api.Jefe());

        var r = await Ok(await Ingresar(recepcion, dni));

        Assert.False((bool)r["yaRegistrado"]!);
        Assert.Equal(id, (long)r["socio"]!["id"]!);
        Assert.Equal("SinPlan", (string)r["cuota"]!["estado"]!);
        Assert.Null(r["cuota"]!["vence"]);
        Assert.Equal(1, (int)r["visitasDelMes"]!);
        Assert.Null(r["ultimaVisitaAnterior"]);
        Assert.Null(r["rutina"]);

        var hoy = (await recepcion.GetJson("/api/asistencias/hoy")).AsArray();
        var mio = Assert.Single(hoy, i => (long)i!["socio"]!["id"]! == id);
        Assert.Equal("SinPlan", (string)mio!["estado"]!);
        Assert.Equal("recepcion@gymstudio.com", (string)mio["registradaPor"]!);
    }

    [Fact]
    public async Task El_segundo_ingreso_del_dia_muestra_lo_mismo_y_no_duplica_la_visita()
    {
        var jefe = await api.Jefe();
        var (id, dni) = await Socio(jefe);

        var primero = await Ok(await Ingresar(jefe, dni));
        var segundo = await Ok(await Ingresar(jefe, dni));

        Assert.False((bool)primero["yaRegistrado"]!);
        Assert.True((bool)segundo["yaRegistrado"]!);
        Assert.Equal((long)primero["id"]!, (long)segundo["id"]!);
        Assert.Equal(1, (int)segundo["visitasDelMes"]!);
        Assert.Single((await jefe.GetJson("/api/asistencias/hoy")).AsArray(), i => (long)i!["socio"]!["id"]! == id);
    }

    [Fact]
    public async Task Varios_ingresos_a_la_vez_del_mismo_socio_dejan_una_sola_visita()
    {
        var jefe = await api.Jefe();
        var (id, dni) = await Socio(jefe);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Ingresar(jefe, dni)));

        foreach (var r in respuestas) Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        Assert.Single((await jefe.GetJson("/api/asistencias/hoy")).AsArray(), i => (long)i!["socio"]!["id"]! == id);
    }

    [Fact]
    public async Task Con_plan_y_rutina_muestra_la_cuota_al_dia_y_el_dia_que_toca_rotando_segun_las_visitas()
    {
        var jefe = await api.Jefe();
        var rutina = await Rutina(jefe, Unico("Sentadilla"), Unico("Remo"));

        // Sin visitas anteriores: el día 1.
        var (primero, dniPrimero) = await Socio(jefe);
        await PagarPlan(jefe, primero);
        await Asignar(jefe, primero, rutina, Hoy.AddDays(-5));
        var a = await Ok(await Ingresar(jefe, dniPrimero));
        Assert.Equal("AlDia", (string)a["cuota"]!["estado"]!);
        Assert.NotNull(a["cuota"]!["vence"]);
        Assert.Equal(1, (int)a["rutina"]!["dia"]!);
        Assert.Equal(2, (int)a["rutina"]!["diasTotales"]!);
        Assert.StartsWith("Sentadilla", (string)Assert.Single(a["rutina"]!["ejercicios"]!.AsArray())!["ejercicio"]!);

        // Una visita anterior desde que empezó la rutina: el día 2.
        var (segundo, dniSegundo) = await Socio(jefe);
        await PagarPlan(jefe, segundo);
        await Asignar(jefe, segundo, rutina, Hoy.AddDays(-5));
        await VisitaPasada(segundo, Hoy.AddDays(-2));
        var b = await Ok(await Ingresar(jefe, dniSegundo));
        Assert.Equal(2, (int)b["rutina"]!["dia"]!);
        Assert.StartsWith("Remo", (string)Assert.Single(b["rutina"]!["ejercicios"]!.AsArray())!["ejercicio"]!);
        Assert.Equal(Hoy.AddDays(-2).ToString("yyyy-MM-dd"), (string)b["ultimaVisitaAnterior"]!);
        Assert.Equal(Hoy.AddDays(-2).Month == Hoy.Month ? 2 : 1, (int)b["visitasDelMes"]!);   // hoy y, si es del mismo mes, la de hace 2 días

        // Dos visitas anteriores: vuelve al día 1. Las de antes de empezar la rutina no cuentan.
        var (tercero, dniTercero) = await Socio(jefe);
        await PagarPlan(jefe, tercero);
        await Asignar(jefe, tercero, rutina, Hoy.AddDays(-5));
        await VisitaPasada(tercero, Hoy.AddDays(-20));   // anterior a la rutina: no cuenta
        await VisitaPasada(tercero, Hoy.AddDays(-3));
        await VisitaPasada(tercero, Hoy.AddDays(-1));
        var c = await Ok(await Ingresar(jefe, dniTercero));
        Assert.Equal(1, (int)c["rutina"]!["dia"]!);

        // Sin rutina asignada no hay rutina del día.
        var (cuarto, dniCuarto) = await Socio(jefe);
        await PagarPlan(jefe, cuarto);
        Assert.Null((await Ok(await Ingresar(jefe, dniCuarto)))["rutina"]);
    }

    [Fact]
    public async Task El_dni_se_acepta_con_puntos_y_los_invalidos_o_inexistentes_se_rechazan_con_un_mensaje()
    {
        var jefe = await api.Jefe();
        var (id, dni) = await Socio(jefe);
        var conPuntos = $"{dni[..^6]}.{dni[^6..^3]}.{dni[^3..]}";
        Assert.Equal(id, (long)(await Ok(await Ingresar(jefe, conPuntos)))["socio"]!["id"]!);

        foreach (var invalido in new[] { "", "   ", "abc", "123", "123456789012", "12.34.56.78.90" })
        {
            var r = await Ingresar(jefe, invalido);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Contains("DNI", await r.Detalle());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsJsonAsync("/api/asistencias/ingreso", new { })).StatusCode);

        var inexistente = await Ingresar(jefe, "10000001");
        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
        Assert.Contains("No hay ningún socio", await inexistente.Detalle());

        var (baja, dniBaja) = await Socio(jefe);
        await Sql("""UPDATE contabilidad."Socios" SET "Activo" = false WHERE "Id" = @id""", ("id", baja));
        var rechazado = await Ingresar(jefe, dniBaja);
        Assert.Equal(HttpStatusCode.BadRequest, rechazado.StatusCode);
        Assert.Contains("dado de baja", await rechazado.Detalle());
    }

    [Fact]
    public async Task Los_socios_al_dia_que_no_vienen_aparecen_en_riesgo_y_en_las_novedades_hasta_que_vuelven()
    {
        var jefe = await api.Jefe();
        var (ausente, dniAusente) = await Socio(jefe);
        await PagarPlan(jefe, ausente);
        await Sql("""UPDATE contabilidad."Socios" SET "FechaAlta" = now() - interval '30 days' WHERE "Id" = @id""", ("id", ausente));

        var (reciente, _) = await Socio(jefe);                  // al día pero recién anotado: no es riesgo
        await PagarPlan(jefe, reciente);
        var (vencida, _) = await Socio(jefe);                   // cuota vencida: se ve en las cuotas, no acá
        await PagarPlan(jefe, vencida);
        await Sql("""UPDATE contabilidad."Socios" SET "FechaAlta" = now() - interval '30 days', "FechaVencimiento" = now() - interval '3 days' WHERE "Id" = @id""", ("id", vencida));
        var (sinPlan, _) = await Socio(jefe);
        await Sql("""UPDATE contabilidad."Socios" SET "FechaAlta" = now() - interval '30 days' WHERE "Id" = @id""", ("id", sinPlan));

        var recepcion = await api.Recepcion();
        var lista = (await recepcion.GetJson("/api/asistencias/en-riesgo")).AsArray();
        var fila = Assert.Single(lista, s => (long)s!["id"]! == ausente);
        Assert.True((int)fila!["diasSinVenir"]! >= 29);
        Assert.Null(fila["ultimaVisita"]);
        foreach (var otro in new[] { reciente, vencida, sinPlan })
            Assert.DoesNotContain(lista, s => (long)s!["id"]! == otro);

        var novedades = (await recepcion.GetJson($"/api/dashboard?hoy={Hoy:yyyy-MM-dd}"))["novedades"]!.AsArray();
        Assert.Contains(novedades, n => (string)n!["nivel"]! == "atencion" && ((string)n["texto"]!).Contains("hace 14 días o más"));

        // Con un umbral más alto ya no lo es, y al volver deja de estar en riesgo.
        Assert.DoesNotContain((await recepcion.GetJson("/api/asistencias/en-riesgo?dias=60")).AsArray(), s => (long)s!["id"]! == ausente);
        await Ok(await Ingresar(recepcion, dniAusente));
        Assert.DoesNotContain((await recepcion.GetJson("/api/asistencias/en-riesgo")).AsArray(), s => (long)s!["id"]! == ausente);
    }

    [Fact]
    public async Task Recepcion_registra_ingresos_pero_solo_la_gestion_puede_borrarlos()
    {
        var jefe = await api.Jefe();
        var recepcion = await api.Recepcion();
        var (id, dni) = await Socio(jefe);
        var ingreso = await Ok(await Ingresar(recepcion, dni));
        var ingresoId = (long)ingreso["id"]!;

        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.DeleteAsync($"/api/asistencias/{ingresoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ClienteAnonimo().PostAsJsonAsync("/api/asistencias/ingreso", new { dni })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await jefe.DeleteAsync($"/api/asistencias/{ingresoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await jefe.DeleteAsync($"/api/asistencias/{ingresoId}")).StatusCode);

        // Cargado por error y borrado: el socio puede volver a ingresar y cuenta como una visita.
        var otra = await Ok(await Ingresar(jefe, dni));
        Assert.False((bool)otra["yaRegistrado"]!);
        Assert.Single((await jefe.GetJson("/api/asistencias/hoy")).AsArray(), i => (long)i!["socio"]!["id"]! == id);
    }

    [Fact]
    public async Task Un_socio_con_ingresos_no_se_puede_eliminar()
    {
        var jefe = await api.Jefe();
        var (id, dni) = await Socio(jefe);
        await Ok(await Ingresar(jefe, dni));

        var r = await jefe.DeleteAsync($"/api/socios/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("ingresos", await r.Detalle());
    }

    [Fact]
    public async Task La_base_no_admite_dos_ingresos_del_mismo_socio_en_el_mismo_dia()
    {
        var jefe = await api.Jefe();
        var (id, _) = await Socio(jefe);
        await VisitaPasada(id, Hoy.AddDays(-4));

        var ex = await Assert.ThrowsAsync<PostgresException>(() => VisitaPasada(id, Hoy.AddDays(-4)));
        Assert.Equal("23505", ex.SqlState);
        Assert.Contains("ux_asistencias_socio_fecha", ex.ConstraintName);
    }
}
