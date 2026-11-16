using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static EstudioContable.IntegrationTests.BasesDePrueba;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Análisis del Tablero de Gestión: evolución por mes o por día, ingresos por plan, mapa de asistencia y filtros.
/// Usa agosto a noviembre de 2022, meses que no toca ninguna otra prueba.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class TableroAnalisisTests(ApiFixture api)
{
    private static object Venta(string fecha, decimal neto) => new
    {
        fecha, tipoOperacion = "Venta", condicionPago = "ContadoCaja", tipoComprobante = "B", cliente = "Consumidor Final",
        subtotal = neto, iva = Math.Round(neto * 0.21m, 2), total = neto + Math.Round(neto * 0.21m, 2),
        items = new[] { new { productoId = 0, descripcion = "Venta de análisis", cantidad = 1, precioUnitario = neto, subtotal = neto } }
    };

    private static object Compra(string fecha, decimal neto, string proveedor) => new
    {
        fecha, tipoOperacion = "Compra", condicionPago = "CuentaCorriente", tipoComprobante = "A", cliente = proveedor,
        puntoVenta = 8, numeroComprobante = Random.Shared.Next(100_000, 999_999),
        subtotal = neto, iva = Math.Round(neto * 0.21m, 2), total = neto + Math.Round(neto * 0.21m, 2),
        items = new[] { new { productoId = 0, descripcion = "Insumos", cantidad = 1, precioUnitario = neto, subtotal = neto } }
    };

    private static async Task<long> Id(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private static Task<JsonNode> Analisis(HttpClient c, string desde, string hasta, long? plan = null)
        => c.GetJson($"/api/tablero/analisis?desde={desde}&hasta={hasta}{(plan is null ? "" : $"&planId={plan}")}");

    /// <summary>Un socio con su cuota cobrada el día indicado (el reloj del servidor se fija a ese día).</summary>
    private async Task<long> SocioConCuota(HttpClient jefe, long plan, DateTimeOffset cuando)
    {
        var socio = await Id(await jefe.PostAsJsonAsync("/api/socios", new
        {
            nombre = "Ana", apellido = "Analisis " + Nombres.Aleatorio(10),
            dni = Random.Shared.Next(20_000_000, 49_999_999).ToString(), telefono = ""
        }));
        api.Reloj.Fijar(cuando);
        try { await Id(await jefe.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", new { socioId = socio, planId = plan, metodoPago = "caja" })); }
        finally { api.Reloj.Liberar(); }
        return socio;
    }

    [Fact]
    public async Task La_evolucion_va_por_mes_y_sin_las_anuladas()
    {
        var jefe = await api.Jefe();
        await Id(await jefe.PostAsJsonAsync("/api/facturas", Venta("2022-09-12T12:00:00Z", 500)));
        await Id(await jefe.PostAsJsonAsync("/api/facturas", Venta("2022-10-03T12:00:00Z", 700)));
        await Id(await jefe.PostAsJsonAsync("/api/facturas", Compra("2022-10-04T12:00:00Z", 200, "Distribuidora Análisis")));
        var anulada = await Id(await jefe.PostAsJsonAsync("/api/facturas", Venta("2022-10-05T12:00:00Z", 9999)));
        var r = await jefe.PostAsJsonAsync($"/api/facturas/{anulada}/anular", new { motivo = "Prueba del tablero" });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());

        var a = await Analisis(jefe, "2022-09", "2022-10");

        Assert.Equal("mes", (string?)a["granularidad"]);
        var evolucion = a["evolucion"]!.AsArray();
        Assert.Equal(2, evolucion.Count);
        Assert.Equal("2022-09", (string?)evolucion[0]!["periodo"]);
        Assert.Equal(500m, (decimal)evolucion[0]!["ventas"]!);
        Assert.Equal(700m, (decimal)evolucion[1]!["ventas"]!);
        Assert.Equal(200m, (decimal)evolucion[1]!["compras"]!);
        Assert.Equal(500m, (decimal)evolucion[1]!["resultado"]!);
        Assert.Contains(a["comprasPorProveedor"]!.AsArray(), x => (string?)x!["proveedor"] == "Distribuidora Análisis" && (decimal)x["neto"]! == 200m);
    }

    [Fact]
    public async Task Un_solo_mes_baja_al_detalle_por_dia()
    {
        var jefe = await api.Jefe();
        await Id(await jefe.PostAsJsonAsync("/api/facturas", Venta("2022-08-20T12:00:00Z", 250)));

        var a = await Analisis(jefe, "2022-08", "2022-08");

        Assert.Equal("dia", (string?)a["granularidad"]);
        var evolucion = a["evolucion"]!.AsArray();
        Assert.Equal(31, evolucion.Count);
        Assert.Equal("2022-08-20", (string?)evolucion[19]!["periodo"]);
        Assert.True((decimal)evolucion[19]!["ventas"]! >= 250m);
    }

    [Fact]
    public async Task Ingresos_por_plan_y_mapa_de_asistencia_se_filtran_por_plan()
    {
        var jefe = await api.Jefe();
        var nombreA = "Plan A " + Nombres.Aleatorio(8);
        var nombreB = "Plan B " + Nombres.Aleatorio(8);
        var planA = await Id(await jefe.PostAsJsonAsync("/api/planes", new { nombre = nombreA, precioMensual = 3000, duracionMeses = 1 }));
        var planB = await Id(await jefe.PostAsJsonAsync("/api/planes", new { nombre = nombreB, precioMensual = 5000, duracionMeses = 1 }));
        var noviembre = new DateTimeOffset(2022, 11, 2, 10, 0, 0, TimeSpan.FromHours(-3));
        var socioA = await SocioConCuota(jefe, planA, noviembre);
        var socioB = await SocioConCuota(jefe, planB, noviembre);
        // Miércoles 16/11/2022: 18:30 de Argentina (socio A) entra al mapa; 23:30 (02:30 UTC del jueves, socio B) queda fuera de 06 a 22.
        await Ejecutar(api.ConnectionString, $"""
            INSERT INTO contabilidad."AsistenciasSocio" ("SocioId", "Fecha", "RegistradaEn", "RegistradaPor") VALUES
            ({socioA}, '2022-11-16', '2022-11-16T21:30:00Z', 'prueba'),
            ({socioB}, '2022-11-16', '2022-11-17T02:30:00Z', 'prueba');
            """);

        var todos = await Analisis(jefe, "2022-11", "2022-11");
        var porPlan = todos["ingresosPorPlan"]!.AsArray();
        Assert.Contains(porPlan, x => (string?)x!["plan"] == nombreA && (decimal)x["neto"]! == 3000m && (int)x["cobros"]! == 1);
        Assert.Contains(porPlan, x => (string?)x!["plan"] == nombreB && (decimal)x["neto"]! == 5000m);
        var mapa = todos["asistencia"]!.AsArray();
        Assert.Equal(7, mapa.Count);
        Assert.Equal(17, mapa[0]!.AsArray().Count);
        Assert.Equal(1, (int)mapa[2]![12]!);                                   // miércoles, de 18 a 19
        Assert.Equal(1, mapa[2]!.AsArray().Sum(x => (int)x!));

        var soloA = await Analisis(jefe, "2022-11", "2022-11", planA);
        Assert.All(soloA["ingresosPorPlan"]!.AsArray(), x => Assert.Equal(nombreA, (string?)x!["plan"]));
        Assert.Equal(1, (int)soloA["asistencia"]![2]![12]!);

        var soloB = await Analisis(jefe, "2022-11", "2022-11", planB);
        Assert.Equal(0, soloB["asistencia"]!.AsArray().Sum(d => d!.AsArray().Sum(x => (int)x!)));
        Assert.Equal(5000m, soloB["evolucion"]!.AsArray().Sum(p => (decimal)p!["cuotas"]!));
    }

    [Theory]
    [InlineData("2020-01", "2022-01")]   // 25 meses
    [InlineData("2022-10", "2022-09")]   // desde después de hasta
    [InlineData("2022-13", "2022-12")]
    public async Task Un_periodo_invalido_da_400(string desde, string hasta)
    {
        var r = await (await api.Jefe()).GetAsync($"/api/tablero/analisis?desde={desde}&hasta={hasta}");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Un_plan_que_no_existe_da_400()
    {
        var r = await (await api.Jefe()).GetAsync("/api/tablero/analisis?desde=2022-09&hasta=2022-09&planId=999999999");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}
