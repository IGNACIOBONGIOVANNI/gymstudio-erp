using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Las horas que cumplieron los entrenadores y su presupuesto de pago: solo para el Administrador y el Contador, con las horas
/// precargadas desde el calendario, el valor de la hora congelado por día, el «hoy» de Argentina y todo-o-nada al guardar. Todas las
/// pruebas dejan el calendario y la tabla de horas como los encontraron y usan entrenadores de nombre único.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class HorasDeEntrenadoresTests(ApiFixture api)
{
    /// <summary>Jueves 12/11/2026, 12:00 en Argentina.</summary>
    private static readonly DateTimeOffset JuevesAlMediodia = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);

    private static string Nuevo(string baseDelNombre) => $"{baseDelNombre} {Nombres.Aleatorio(8)}";

    private async Task<(long Id, string Nombre)> CrearEntrenador(string baseDelNombre, decimal? valorHora)
    {
        var jefe = await api.Jefe();
        var nombre = Nuevo(baseDelNombre);
        var r = await jefe.PostAsJsonAsync("/api/entrenadores", new { nombre, valorHora });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return ((long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!, nombre);
    }

    private async Task CambiarValorDeLaHora(long id, string nombre, decimal? valorHora)
    {
        var r = await (await api.Jefe()).PutAsJsonAsync($"/api/entrenadores/{id}", new { nombre, valorHora });
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
    }

    private async Task Desactivar(long id)
    {
        var r = await (await api.Jefe()).PutAsJsonAsync($"/api/entrenadores/{id}/estado", new { activo = false });
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
    }

    private async Task PonerCalendario(params (long Entrenador, int Dia, int Hora)[] celdas)
    {
        var r = await (await api.Jefe()).PutAsJsonAsync("/api/calendario",
            new { turnos = celdas.Select(c => new { dia = c.Dia, hora = c.Hora, entrenadorId = c.Entrenador }).ToArray() });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
    }

    private async Task<long> ContarDias(params long[] entrenadores)
    {
        await using var conexion = new NpgsqlConnection(api.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand("""SELECT count(*) FROM contabilidad."HorasDeEntrenador" WHERE "EntrenadorId" = ANY(@ids)""", conexion);
        comando.Parameters.AddWithValue("ids", entrenadores);
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    /// <summary>Deja el calendario vacío, borra las horas de esos entrenadores y devuelve el reloj al real.</summary>
    private async Task Limpiar(params long[] entrenadores)
    {
        api.Reloj.Liberar();
        await PonerCalendario();
        await using var conexion = new NpgsqlConnection(api.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand("""DELETE FROM contabilidad."HorasDeEntrenador" WHERE "EntrenadorId" = ANY(@ids)""", conexion);
        comando.Parameters.AddWithValue("ids", entrenadores);
        await comando.ExecuteNonQueryAsync();
    }

    private static Task<HttpResponseMessage> Obtener(HttpClient cliente, string desde, string hasta) => cliente.GetAsync($"/api/entrenadores/horas?desde={desde}&hasta={hasta}");

    private static object Renglon(long entrenadorId, string fecha, decimal? horas, string? nota = null) => new { entrenadorId, fecha, horas, nota };

    private static Task<HttpResponseMessage> Guardar(HttpClient cliente, params object[] renglones) => cliente.PutAsJsonAsync("/api/entrenadores/horas", new { items = renglones });

    private static Task<HttpResponseMessage> Precargar(HttpClient cliente, string desde, string hasta) => cliente.PostAsJsonAsync("/api/entrenadores/horas/precargar", new { desde, hasta });

    private static Task<HttpResponseMessage> PedirPresupuesto(HttpClient cliente, string desde, string hasta) => cliente.GetAsync($"/api/entrenadores/presupuesto?desde={desde}&hasta={hasta}");

    private static async Task<JsonObject> Json(HttpResponseMessage respuesta)
    {
        Assert.True(respuesta.IsSuccessStatusCode, $"Se esperaba éxito y fue {(int)respuesta.StatusCode}. {await respuesta.Detalle()}");
        return (await respuesta.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    private static async Task<List<JsonNode>> DiasDe(HttpClient cliente, long entrenadorId, string desde, string hasta)
        => (await Json(await Obtener(cliente, desde, hasta)))["dias"]!.AsArray().Where(d => (long)d!["entrenadorId"]! == entrenadorId).Select(d => d!).ToList();

    // ---------- permisos ----------

    [Fact]
    public async Task Solo_la_gestion_usa_las_rutas_de_horas_y_presupuesto()
    {
        var (ana, _) = await CrearEntrenador("Permisos Horas", 1000m);
        var pedidos = new (string Nombre, Func<HttpClient, Task<HttpResponseMessage>> Pedir, HttpStatusCode Esperado)[]
        {
            ("GET horas", c => Obtener(c, "2026-08-03", "2026-08-09"), HttpStatusCode.OK),
            ("PUT horas", c => Guardar(c), HttpStatusCode.NoContent),
            ("POST precargar", c => Precargar(c, "2026-08-03", "2026-08-09"), HttpStatusCode.OK),
            ("GET presupuesto", c => PedirPresupuesto(c, "2026-08-03", "2026-08-09"), HttpStatusCode.OK),
        };

        try
        {
            foreach (var (nombre, pedir, esperado) in pedidos)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await pedir(api.ClienteAnonimo())).StatusCode);
                Assert.True((await pedir(await api.Recepcion())).StatusCode == HttpStatusCode.Forbidden, $"{nombre}: Recepción tiene que recibir 403");
                Assert.True((await pedir(await api.Jefe())).StatusCode == esperado, $"{nombre}: el Administrador");
                Assert.True((await pedir(await api.Contador())).StatusCode == esperado, $"{nombre}: el Contador");
            }
        }
        finally { await Limpiar(ana); }
    }

    // ---------- ver el período ----------

    [Fact]
    public async Task El_periodo_trae_las_programadas_del_calendario_y_lo_cargado()
    {
        var jefe = await api.Jefe();
        var (ana, nombre) = await CrearEntrenador("Ana Periodo", 1000m);
        api.Reloj.Fijar(JuevesAlMediodia);
        try
        {
            await PonerCalendario((ana, 1, 6), (ana, 1, 7), (ana, 1, 8), (ana, 3, 6));   // lunes 06 a 09 (3 horas) y miércoles 06 a 07 (1 hora)
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-11-07", 4m))).StatusCode);   // un sábado, cargado a mano

            var respuesta = await Json(await Obtener(jefe, "2026-11-02", "2026-11-15"));
            var dias = await DiasDe(jefe, ana, "2026-11-02", "2026-11-15");

            Assert.Equal(new[] { "2026-11-02", "2026-11-04", "2026-11-07", "2026-11-09", "2026-11-11" }, dias.Select(d => (string)d["fecha"]!).ToArray());
            Assert.Equal(new[] { 3, 1, 0, 3, 1 }, dias.Select(d => (int)d["programadas"]!).ToArray());
            Assert.Equal(new decimal?[] { null, null, 4m, null, null }, dias.Select(d => (decimal?)d["cumplidas"]).ToArray());
            Assert.Equal(new decimal?[] { null, null, 1000m, null, null }, dias.Select(d => (decimal?)d["valorHora"]).ToArray());
            var ficha = respuesta["entrenadores"]!.AsArray().Single(e => (long)e!["id"]! == ana)!;
            Assert.Equal((nombre, 1000m, true), ((string)ficha["nombre"]!, (decimal)ficha["valorHora"]!, (bool)ficha["activo"]!));
        }
        finally { await Limpiar(ana); }
    }

    [Fact]
    public async Task Un_entrenador_desactivado_conserva_sus_horas_y_se_sigue_viendo_donde_las_tiene()
    {
        var jefe = await api.Jefe();
        var (ana, _) = await CrearEntrenador("Ana Desactivada", 1000m);
        api.Reloj.Fijar(JuevesAlMediodia);
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-09-10", 8m))).StatusCode);
            await Desactivar(ana);

            // Donde tiene horas se ve (con sus días); donde no tiene nada, no aparece.
            var septiembre = await Json(await Obtener(jefe, "2026-09-01", "2026-09-30"));
            Assert.Contains(septiembre["entrenadores"]!.AsArray(), e => (long)e!["id"]! == ana);
            Assert.Equal(8m, (decimal)(await DiasDe(jefe, ana, "2026-09-01", "2026-09-30")).Single()["cumplidas"]!);
            Assert.False((bool)septiembre["entrenadores"]!.AsArray().Single(e => (long)e!["id"]! == ana)!["activo"]!);   // el selector de «agregar un día» lo necesita
            var agosto = await Json(await Obtener(jefe, "2026-08-01", "2026-08-31"));
            Assert.DoesNotContain(agosto["entrenadores"]!.AsArray(), e => (long)e!["id"]! == ana);

            // Sus horas entran en el presupuesto y se pueden corregir, pero no se le crean días nuevos.
            var presupuesto = await Json(await PedirPresupuesto(jefe, "2026-09-01", "2026-09-30"));
            Assert.Contains(presupuesto["filas"]!.AsArray(), f => (long)f!["entrenadorId"]! == ana);
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-09-10", 7m))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Guardar(jefe, Renglon(ana, "2026-09-11", 7m))).StatusCode);
            Assert.Equal(1, await ContarDias(ana));
        }
        finally { await Limpiar(ana); }
    }

    // ---------- precargar ----------

    [Fact]
    public async Task Precargar_crea_los_dias_hasta_hoy_sin_pisar_y_es_idempotente()
    {
        var jefe = await api.Jefe();
        var (ana, _) = await CrearEntrenador("Ana Precarga", 1000m);
        api.Reloj.Fijar(JuevesAlMediodia);
        try
        {
            await PonerCalendario((ana, 1, 6), (ana, 1, 7), (ana, 1, 8), (ana, 3, 6));
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-11-02", 2m, "Salió antes"))).StatusCode);   // ya cargado: no se pisa

            var primera = await Json(await Precargar(jefe, "2026-11-02", "2026-11-30"));

            // Hasta el jueves 12: lunes 2 (ya cargado), miércoles 4, lunes 9 y miércoles 11. Nada de lo que viene después.
            Assert.Equal(3, (int)primera["creados"]!);
            Assert.Empty(primera["sinValor"]!.AsArray());
            var dias = await DiasDe(jefe, ana, "2026-11-02", "2026-11-30");
            var cumplidas = dias.Where(d => d["cumplidas"] != null).ToDictionary(d => (string)d["fecha"]!, d => (decimal)d["cumplidas"]!);
            Assert.Equal(new Dictionary<string, decimal> { ["2026-11-02"] = 2m, ["2026-11-04"] = 1m, ["2026-11-09"] = 3m, ["2026-11-11"] = 1m }, cumplidas);
            Assert.Equal("Salió antes", (string)dias.First(d => (string)d["fecha"]! == "2026-11-02")["nota"]!);
            Assert.Contains(dias, d => (string)d["fecha"]! == "2026-11-16" && d["cumplidas"] == null && (int)d["programadas"]! == 3);   // lo futuro se ve, sin cargar

            // Repetir no hace nada.
            var segunda = await Json(await Precargar(jefe, "2026-11-02", "2026-11-30"));
            Assert.Equal(0, (int)segunda["creados"]!);
            Assert.Equal(4, await ContarDias(ana));
        }
        finally { await Limpiar(ana); }
    }

    [Fact]
    public async Task Precargar_informa_a_los_entrenadores_sin_valor_y_no_les_carga_nada()
    {
        var jefe = await api.Jefe();
        var (ana, _) = await CrearEntrenador("Ana Con Valor", 1000m);
        var (beto, nombreDeBeto) = await CrearEntrenador("Beto Sin Valor", null);
        api.Reloj.Fijar(JuevesAlMediodia);
        try
        {
            await PonerCalendario((ana, 1, 6), (beto, 2, 6), (beto, 2, 7));   // Ana el lunes, Beto el martes

            var resultado = await Json(await Precargar(jefe, "2026-11-02", "2026-11-12"));

            Assert.Equal(2, (int)resultado["creados"]!);   // los lunes 2 y 9 de Ana; los martes de Beto no se tocan
            Assert.Contains(nombreDeBeto, resultado["sinValor"]!.AsArray().Select(n => (string)n!));
            Assert.Equal(0, await ContarDias(beto));
            Assert.Equal(2, await ContarDias(ana));

            // Un período en el que Beto no tiene nada que cargar (su martes no está) no lo menciona.
            var sinNada = await Json(await Precargar(jefe, "2026-11-13", "2026-11-15"));
            Assert.Equal(0, (int)sinNada["creados"]!);
            Assert.DoesNotContain(nombreDeBeto, sinNada["sinValor"]!.AsArray().Select(n => (string)n!));
        }
        finally { await Limpiar(ana, beto); }
    }

    // ---------- el valor de la hora de cada día ----------

    [Fact]
    public async Task El_valor_guardado_de_un_dia_no_cambia_si_sube_el_valor_del_entrenador()
    {
        var jefe = await api.Jefe();
        var (ana, nombre) = await CrearEntrenador("Ana Sube El Valor", 1000m);
        api.Reloj.Fijar(JuevesAlMediodia);
        try
        {
            await PonerCalendario((ana, 1, 6), (ana, 3, 6));
            Assert.Equal(4, (int)(await Json(await Precargar(jefe, "2026-11-02", "2026-11-12")))["creados"]!);   // lunes 2 y 9, miércoles 4 y 11

            await CambiarValorDeLaHora(ana, nombre, 1500m);

            // Los días ya cargados siguen con 1000.
            var antes = await DiasDe(jefe, ana, "2026-11-02", "2026-11-12");
            Assert.All(antes.Where(d => d["cumplidas"] != null), d => Assert.Equal(1000m, (decimal)d["valorHora"]!));

            // Un día nuevo, creado después, sale con 1500. Y corregir las horas de uno viejo no le cambia el valor.
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-11-07", 5m), Renglon(ana, "2026-11-02", 6m))).StatusCode);
            var despues = await DiasDe(jefe, ana, "2026-11-02", "2026-11-12");
            Assert.Equal(1500m, (decimal)despues.Single(d => (string)d["fecha"]! == "2026-11-07")["valorHora"]!);
            var corregido = despues.Single(d => (string)d["fecha"]! == "2026-11-02");
            Assert.Equal((6m, 1000m), ((decimal)corregido["cumplidas"]!, (decimal)corregido["valorHora"]!));
        }
        finally { await Limpiar(ana); }
    }

    // ---------- guardar ----------

    [Fact]
    public async Task Guardar_crea_cambia_y_borra_dias()
    {
        var jefe = await api.Jefe();
        var (ana, _) = await CrearEntrenador("Ana Guarda", 1000m);
        api.Reloj.Fijar(JuevesAlMediodia);
        try
        {
            async Task<JsonNode?> Dia() => (await DiasDe(jefe, ana, "2026-10-01", "2026-10-31")).SingleOrDefault();

            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-10-05", 8m, "Cubrió a Beto"))).StatusCode);
            var creado = (await Dia())!;
            Assert.Equal((8m, "Cubrió a Beto", 1000m, 0), ((decimal)creado["cumplidas"]!, (string)creado["nota"]!, (decimal)creado["valorHora"]!, (int)creado["programadas"]!));

            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-10-05", 9.5m, "  Se quedó más  "))).StatusCode);
            var cambiado = (await Dia())!;
            Assert.Equal((9.5m, "Se quedó más", 1000m), ((decimal)cambiado["cumplidas"]!, (string)cambiado["nota"]!, (decimal)cambiado["valorHora"]!));

            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-10-05", 0m))).StatusCode);   // faltó
            var falto = (await Dia())!;
            Assert.Equal(0m, (decimal)falto["cumplidas"]!);
            Assert.Null((string?)falto["nota"]);

            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-10-05", null))).StatusCode);   // sin cargar: se borra
            Assert.Null(await Dia());
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-10-06", null))).StatusCode);   // borrar lo que no existe no es un error
            Assert.Equal(0, await ContarDias(ana));
        }
        finally { await Limpiar(ana); }
    }

    [Theory]
    [InlineData("fecha futura")]
    [InlineData("más de 24 horas")]
    [InlineData("no es cuarto de hora")]
    [InlineData("horas negativas")]
    [InlineData("nota con salto de línea")]
    [InlineData("nota de 201 caracteres")]
    [InlineData("renglón repetido")]
    [InlineData("entrenador inexistente")]
    [InlineData("entrenador sin valor de hora")]
    [InlineData("entrenador desactivado con día nuevo")]
    [InlineData("501 renglones")]
    [InlineData("cuerpo roto")]
    [InlineData("lista nula")]
    [InlineData("renglón nulo")]
    public async Task Un_pedido_invalido_se_rechaza_con_400_y_no_cambia_nada(string caso)
    {
        var jefe = await api.Jefe();
        var (ana, _) = await CrearEntrenador("Ana Valida", 1000m);
        var (sinValor, nombreSinValor) = await CrearEntrenador("Sin Valor", null);
        var (baja, _) = await CrearEntrenador("Dada De Baja", 1000m);
        await Desactivar(baja);
        api.Reloj.Fijar(JuevesAlMediodia);
        try
        {
            var bueno = Renglon(ana, "2026-10-05", 8m);   // un renglón válido que viaja junto con el malo: todo o nada
            StringContent Crudo(string json) => new(json, Encoding.UTF8, "application/json");
            var r = caso switch
            {
                "fecha futura" => await Guardar(jefe, bueno, Renglon(ana, "2026-11-13", 8m)),
                "más de 24 horas" => await Guardar(jefe, bueno, Renglon(ana, "2026-10-06", 24.25m)),
                "no es cuarto de hora" => await Guardar(jefe, bueno, Renglon(ana, "2026-10-06", 0.1m)),
                "horas negativas" => await Guardar(jefe, bueno, Renglon(ana, "2026-10-06", -1m)),
                "nota con salto de línea" => await Guardar(jefe, bueno, Renglon(ana, "2026-10-06", 8m, "a\nb")),
                "nota de 201 caracteres" => await Guardar(jefe, bueno, Renglon(ana, "2026-10-06", 8m, new string('n', 201))),
                "renglón repetido" => await Guardar(jefe, bueno, bueno),
                "entrenador inexistente" => await Guardar(jefe, bueno, Renglon(ana + 1_000_000, "2026-10-06", 8m)),
                "entrenador sin valor de hora" => await Guardar(jefe, bueno, Renglon(sinValor, "2026-10-06", 8m)),
                "entrenador desactivado con día nuevo" => await Guardar(jefe, bueno, Renglon(baja, "2026-10-06", 8m)),
                "501 renglones" => await Guardar(jefe, Enumerable.Range(0, 501)
                    .Select(i => Renglon(ana, new DateOnly(2025, 1, 1).AddDays(i).ToString("yyyy-MM-dd"), 1m)).ToArray()),
                "cuerpo roto" => await jefe.PutAsync("/api/entrenadores/horas", Crudo("{ \"items\": [ { \"entrenadorId\": ")),
                "lista nula" => await jefe.PutAsync("/api/entrenadores/horas", Crudo("{ \"items\": null }")),
                "renglón nulo" => await jefe.PutAsync("/api/entrenadores/horas", Crudo("{ \"items\": [ null ] }")),
                _ => throw new InvalidOperationException(caso),
            };

            Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{caso}: se esperaba 400 y fue {(int)r.StatusCode}. {await r.Detalle()}");
            Assert.Equal(0, await ContarDias(ana, sinValor, baja));   // ni siquiera el renglón bueno se guardó
            if (caso == "entrenador sin valor de hora")
            {
                var detalle = await r.Detalle();
                Assert.Contains(nombreSinValor, detalle);
                Assert.Contains("valor de la hora", detalle);
            }
        }
        finally { await Limpiar(ana, sinValor, baja); }
    }

    [Fact]
    public async Task Cerca_de_la_medianoche_hoy_es_el_dia_argentino()
    {
        var jefe = await api.Jefe();
        var (ana, _) = await CrearEntrenador("Ana Medianoche", 1000m);
        api.Reloj.Fijar(new DateTimeOffset(2026, 11, 13, 1, 30, 0, TimeSpan.Zero));   // 22:30 del jueves 12 en Argentina
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-11-12", 8m))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Guardar(jefe, Renglon(ana, "2026-11-13", 8m))).StatusCode);   // el viernes todavía no llegó

            api.Reloj.Fijar(new DateTimeOffset(2026, 11, 13, 3, 0, 0, TimeSpan.Zero));   // 00:00 del viernes 13 en Argentina
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-11-13", 8m))).StatusCode);
            Assert.Equal(2, await ContarDias(ana));
        }
        finally { await Limpiar(ana); }
    }

    // ---------- presupuesto ----------

    [Fact]
    public async Task El_presupuesto_suma_los_dias_con_dos_valores_distintos_y_no_cuenta_los_que_faltaron()
    {
        var jefe = await api.Jefe();
        var (ana, nombreDeAna) = await CrearEntrenador("Ana Presupuesto", 1000m);
        var (bruno, nombreDeBruno) = await CrearEntrenador("Bruno Presupuesto", 1000m);
        api.Reloj.Fijar(JuevesAlMediodia);
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-09-01", 2m), Renglon(ana, "2026-09-02", 2m), Renglon(bruno, "2026-09-01", 3m))).StatusCode);
            await CambiarValorDeLaHora(ana, nombreDeAna, 1200m);
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe,
                Renglon(ana, "2026-09-03", 2m), Renglon(ana, "2026-09-04", 2m), Renglon(ana, "2026-09-05", 0m))).StatusCode);   // el último: faltó

            var presupuesto = await Json(await PedirPresupuesto(jefe, "2026-09-01", "2026-09-30"));

            Assert.Equal(("2026-09-01", "2026-09-30"), ((string)presupuesto["desde"]!, (string)presupuesto["hasta"]!));
            var filas = presupuesto["filas"]!.AsArray();
            Assert.Equal(new[] { nombreDeAna, nombreDeBruno }, filas.Select(f => (string)f!["nombre"]!).ToArray());   // orden alfabético
            var deAna = filas[0]!;
            Assert.Equal((4, 8m, 8800m), ((int)deAna["dias"]!, (decimal)deAna["horas"]!, (decimal)deAna["monto"]!));   // 4 × 2 h; 2 a 1000 y 2 a 1200
            Assert.Null((string?)deAna["valorHora"]);   // dos valores en el período: «varios»
            var deBruno = filas[1]!;
            Assert.Equal((1, 3m, 1000m, 3000m), ((int)deBruno["dias"]!, (decimal)deBruno["horas"]!, (decimal)deBruno["valorHora"]!, (decimal)deBruno["monto"]!));
            Assert.Equal((11m, 11800m), ((decimal)presupuesto["totalHoras"]!, (decimal)presupuesto["totalMonto"]!));
        }
        finally { await Limpiar(ana, bruno); }
    }

    [Fact]
    public async Task El_medio_centavo_se_redondea_por_dia_y_no_sobre_la_suma()
    {
        var jefe = await api.Jefe();
        var (ana, _) = await CrearEntrenador("Ana Centavos", 100.10m);
        api.Reloj.Fijar(JuevesAlMediodia);
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(jefe, Renglon(ana, "2026-08-03", 0.25m), Renglon(ana, "2026-08-04", 0.25m))).StatusCode);

            var presupuesto = await Json(await PedirPresupuesto(jefe, "2026-08-01", "2026-08-31"));

            var fila = presupuesto["filas"]!.AsArray().Single()!;
            Assert.Equal((2, 0.5m, 100.10m), ((int)fila["dias"]!, (decimal)fila["horas"]!, (decimal)fila["valorHora"]!));
            Assert.Equal(50.06m, (decimal)fila["monto"]!);   // 25,03 por día; sumar 0,5 h × 100,10 y redondear daría 50,05
            Assert.Equal(50.06m, (decimal)presupuesto["totalMonto"]!);
        }
        finally { await Limpiar(ana); }
    }

    [Fact]
    public async Task Un_presupuesto_sin_horas_en_el_periodo_viene_vacio()
    {
        var jefe = await api.Jefe();

        var presupuesto = await Json(await PedirPresupuesto(jefe, "2025-01-01", "2025-01-31"));

        Assert.Empty(presupuesto["filas"]!.AsArray());
        Assert.Equal((0m, 0m), ((decimal)presupuesto["totalHoras"]!, (decimal)presupuesto["totalMonto"]!));
    }

    // ---------- períodos ----------

    [Fact]
    public async Task Un_periodo_invertido_o_de_mas_de_62_dias_da_400_y_uno_de_62_vale()
    {
        var jefe = await api.Jefe();
        var rutas = new (string Nombre, Func<string, string, Task<HttpResponseMessage>> Pedir)[]
        {
            ("horas", (d, h) => Obtener(jefe, d, h)),
            ("precargar", (d, h) => Precargar(jefe, d, h)),
            ("presupuesto", (d, h) => PedirPresupuesto(jefe, d, h)),
        };

        foreach (var (nombre, pedir) in rutas)
        {
            Assert.True((await pedir("2026-11-10", "2026-11-09")).StatusCode == HttpStatusCode.BadRequest, $"{nombre}: período invertido");
            Assert.True((await pedir("2026-10-01", "2026-12-02")).StatusCode == HttpStatusCode.BadRequest, $"{nombre}: 63 días");
            Assert.True((await pedir("2026-10-01", "2026-12-01")).IsSuccessStatusCode, $"{nombre}: 62 días justos");
            Assert.True((await pedir("2026-11-09", "2026-11-09")).IsSuccessStatusCode, $"{nombre}: un solo día");
        }
    }

    [Fact]
    public async Task Las_fechas_omitidas_se_rechazan_con_400_y_no_crean_nada()
    {
        var jefe = await api.Jefe();
        var (ana, _) = await CrearEntrenador("Ana Sin Fecha", 1000m);
        api.Reloj.Fijar(JuevesAlMediodia);
        try
        {
            await PonerCalendario((ana, 1, 6));   // un lunes con horas: el 0001-01-01 también fue lunes

            var obtener = await jefe.GetAsync("/api/entrenadores/horas");   // sin desde ni hasta
            var presupuesto = await jefe.GetAsync("/api/entrenadores/presupuesto");
            var precargar = await jefe.PostAsync("/api/entrenadores/horas/precargar", new StringContent("{}", Encoding.UTF8, "application/json"));
            var guardar = await jefe.PutAsync("/api/entrenadores/horas",
                new StringContent($"{{ \"items\": [ {{ \"entrenadorId\": {ana}, \"horas\": 8 }} ] }}", Encoding.UTF8, "application/json"));   // sin fecha

            foreach (var (nombre, r) in new[] { ("GET horas", obtener), ("GET presupuesto", presupuesto), ("POST precargar", precargar), ("PUT horas", guardar) })
                Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{nombre}: se esperaba 400 y fue {(int)r.StatusCode}. {await r.Detalle()}");
            Assert.Equal(0, await ContarDias(ana));
        }
        finally { await Limpiar(ana); }
    }

    [Fact]
    public async Task Un_periodo_que_llega_al_final_del_calendario_da_400_y_no_un_error_interno()
    {
        var jefe = await api.Jefe();

        var r = await Obtener(jefe, "9999-12-01", "9999-12-31");

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("período", await r.Detalle());   // un mensaje del sistema, no el texto técnico de .NET
    }

    // ---------- auditoría ----------

    [Fact]
    public async Task Cargar_horas_queda_en_la_auditoria_con_quien_las_cargo()
    {
        var contador = await api.Contador();
        var jefe = await api.Jefe();
        var (ana, _) = await CrearEntrenador("Ana Auditada", 1000m);
        api.Reloj.Fijar(JuevesAlMediodia);
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(contador, Renglon(ana, "2026-10-05", 8m))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await Guardar(contador, Renglon(ana, "2026-10-05", 6m))).StatusCode);

            var registros = (await jefe.GetJson("/api/auditoria?tabla=contabilidad.HorasDeEntrenador&cantidad=50")).AsArray();

            Assert.Contains(registros, x => (string?)x!["operacion"] == "INSERT" && (string?)x["usuario"] == "contador@gymstudio.com");
            Assert.Contains(registros, x => (string?)x!["operacion"] == "UPDATE" && (string?)x["usuario"] == "contador@gymstudio.com");
        }
        finally { await Limpiar(ana); }
    }
}
