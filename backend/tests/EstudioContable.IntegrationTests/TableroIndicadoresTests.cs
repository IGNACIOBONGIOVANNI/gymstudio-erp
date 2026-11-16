using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static EstudioContable.IntegrationTests.BasesDePrueba;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Indicadores del tablero de comando. Cada prueba usa meses de 2023 que nadie más toca (las membresías y asistencias se
/// fechan por SQL), así los valores son exactamente los de los datos que carga:
/// 01 ventas · 02-03 socios · 04-05 meta y variación · 06-07 mes en curso · 08-09 asistencias · 10 costo de entrenadores · 11 sin ventas.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class TableroIndicadoresTests(ApiFixture api)
{
    private static object Factura(string tipo, string fecha, decimal neto, long numero = 0) => tipo == "Venta"
        ? new
        {
            fecha, tipoOperacion = "Venta", condicionPago = "ContadoCaja", tipoComprobante = "B", cliente = "Consumidor Final",
            subtotal = neto, iva = Math.Round(neto * 0.21m, 2), total = neto + Math.Round(neto * 0.21m, 2),
            items = new[] { new { productoId = 0, descripcion = "Venta de prueba", cantidad = 1, precioUnitario = neto, subtotal = neto } }
        }
        : new
        {
            fecha, tipoOperacion = "Compra", condicionPago = "CuentaCorriente", tipoComprobante = "A", cliente = "Proveedor de prueba",
            subtotal = neto, iva = Math.Round(neto * 0.21m, 2), total = neto + Math.Round(neto * 0.21m, 2),
            items = new[] { new { productoId = 0, descripcion = "Insumos", cantidad = 1, precioUnitario = neto, subtotal = neto } }
        } as object;

    private static async Task<long> Id(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private static async Task Crear(HttpClient c, object factura) => await Id(await c.PostAsJsonAsync("/api/facturas", factura));

    private static async Task<long> CompraConNumero(HttpClient c, string fecha, decimal neto)
    {
        var f = (JsonObject)JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(Factura("Compra", fecha, neto)))!;
        f["puntoVenta"] = 7;
        f["numeroComprobante"] = Random.Shared.Next(100_000, 999_999);
        return await Id(await c.PostAsJsonAsync("/api/facturas", f));
    }

    private async Task<long> SocioDadoDeAlta(HttpClient jefe, string fechaAlta)
    {
        var id = await Id(await jefe.PostAsJsonAsync("/api/socios", new
        {
            nombre = "Tina", apellido = "Tablero " + Nombres.Aleatorio(10),
            dni = Random.Shared.Next(20_000_000, 49_999_999).ToString(), telefono = ""
        }));
        await Ejecutar(api.ConnectionString, $"""UPDATE contabilidad."Socios" SET "FechaAlta" = '{fechaAlta}T15:00:00Z' WHERE "Id" = {id};""");
        return id;
    }

    private async Task<long> PlanNuevo(HttpClient jefe, decimal precio = 1000, int meses = 1)
        => await Id(await jefe.PostAsJsonAsync("/api/planes", new { nombre = "Plan " + Nombres.Aleatorio(10), precioMensual = precio, duracionMeses = meses }));

    /// <summary>Una membresía pagada entre dos días (hora de Argentina: se guardan a las 03:00 UTC).</summary>
    private Task Membresia(long socio, long plan, string desde, string hasta) => Ejecutar(api.ConnectionString, $"""
        INSERT INTO contabilidad."Membresias" ("SocioId", "PlanId", "FechaInicio", "FechaVencimiento", "EstaPagada")
        VALUES ({socio}, {plan}, '{desde}T03:00:00Z', '{hasta}T03:00:00Z', true);
        """);

    private Task Asistencia(long socio, string fecha, string registradaEnUtc) => Ejecutar(api.ConnectionString, $"""
        INSERT INTO contabilidad."AsistenciasSocio" ("SocioId", "Fecha", "RegistradaEn", "RegistradaPor")
        VALUES ({socio}, '{fecha}', '{registradaEnUtc}', 'prueba');
        """);

    private static async Task<Dictionary<string, JsonObject>> Indicadores(HttpClient c, string mes, string? hoy = null)
    {
        var r = await c.GetJson($"/api/tablero/indicadores?mes={mes}&hoy={hoy ?? mes + "-28"}");
        return r["indicadores"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => (string)x["clave"]!);
    }

    private static decimal? Valor(Dictionary<string, JsonObject> i, string clave) => (decimal?)i[clave]["valor"];

    [Fact]
    public async Task Ventas_resultado_y_caja_coinciden_con_el_Resumen()
    {
        var jefe = await api.Jefe();
        await Crear(jefe, Factura("Venta", "2023-01-10T12:00:00Z", 300));
        await CompraConNumero(jefe, "2023-01-12T12:00:00Z", 100);

        var i = await Indicadores(jefe, "2023-01");
        var resumen = (await jefe.GetJson("/api/dashboard?mes=2023-01&hoy=2023-01-28"))["finanzas"]!;

        Assert.Equal(300m, Valor(i, "ingresos"));
        Assert.Equal(200m, Valor(i, "resultado"));
        Assert.Equal(66.67m, Valor(i, "margen"));
        Assert.Equal((decimal)resumen["ventas"]!["neto"]!, Valor(i, "ingresos"));
        Assert.Equal((decimal)resumen["resultado"]!, Valor(i, "resultado"));
        Assert.Equal((decimal)resumen["cajaSaldo"]! + (decimal)resumen["bancoSaldo"]!, Valor(i, "liquidez"));
        Assert.Null(i["liquidez"]["variacion"]);
    }

    [Fact]
    public async Task Un_mes_sin_ventas_da_sin_datos_en_el_margen()
    {
        var i = await Indicadores(await api.Jefe(), "2023-11");
        Assert.Equal(0m, Valor(i, "ingresos"));
        Assert.Null(Valor(i, "margen"));
        Assert.Equal("sin-datos", (string?)i["margen"]["semaforo"]);
    }

    [Fact]
    public async Task Socios_altas_bajas_y_retencion_salen_de_las_membresias()
    {
        var jefe = await api.Jefe();
        var mensual = await PlanNuevo(jefe);
        var trimestral = await PlanNuevo(jefe, meses: 3);
        var socios = new List<long>();
        for (var n = 0; n < 4; n++) socios.Add(await SocioDadoDeAlta(jefe, "2023-03-05"));
        await Membresia(socios[0], mensual, "2023-02-15", "2023-03-15");      // no renueva: baja de marzo
        foreach (var s in socios.Skip(1)) await Membresia(s, trimestral, "2023-02-20", "2023-05-20");

        var i = await Indicadores(jefe, "2023-03", "2023-04-15");

        Assert.Equal(3m, Valor(i, "socios-activos"));
        Assert.Equal(4m, Valor(i, "altas"));
        Assert.Equal(1m, Valor(i, "bajas"));
        Assert.Equal(75m, Valor(i, "retencion"));
    }

    [Fact]
    public async Task En_el_mes_en_curso_el_cierre_es_hoy()
    {
        var jefe = await api.Jefe();
        var socio = await SocioDadoDeAlta(jefe, "2023-06-20");
        await Membresia(socio, await PlanNuevo(jefe), "2023-06-20", "2023-07-20");

        var r = await jefe.GetJson("/api/tablero/indicadores?mes=2023-07&hoy=2023-07-10");
        var i = r["indicadores"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => (string)x["clave"]!);

        Assert.Equal("2023-07-10", (string?)r["cierre"]);
        Assert.Equal(1m, Valor(i, "socios-activos"));
        Assert.Equal(0m, Valor(i, "bajas"));
        Assert.Equal(100m, Valor(i, "retencion"));
    }

    [Fact]
    public async Task Asistencias_por_socio_y_horario_pico()
    {
        var jefe = await api.Jefe();
        var plan = await PlanNuevo(jefe, meses: 3);
        var ana = await SocioDadoDeAlta(jefe, "2023-08-25");
        var beto = await SocioDadoDeAlta(jefe, "2023-08-25");
        await Membresia(ana, plan, "2023-08-25", "2023-11-25");
        await Membresia(beto, plan, "2023-08-25", "2023-11-25");
        await Asistencia(ana, "2023-09-04", "2023-09-04T21:10:00Z");   // 18:10 en Argentina
        await Asistencia(beto, "2023-09-04", "2023-09-04T21:40:00Z");  // 18:40
        await Asistencia(ana, "2023-09-06", "2023-09-06T12:00:00Z");   // 09:00

        var i = await Indicadores(jefe, "2023-09", "2023-10-15");

        Assert.Equal(3m, Valor(i, "asistencias"));
        Assert.Equal(1.5m, Valor(i, "asistencia-por-socio"));
        Assert.Contains("18", (string?)i["asistencias"]["detalle"]);
    }

    [Fact]
    public async Task Costo_de_entrenadores_sobre_lo_cobrado_en_cuotas()
    {
        var jefe = await api.Jefe();
        var socio = await SocioDadoDeAlta(jefe, "2023-10-05");
        var plan = await PlanNuevo(jefe, precio: 20000);
        api.Reloj.Fijar(new DateTimeOffset(2023, 10, 5, 12, 0, 0, TimeSpan.FromHours(-3)));
        try { await Id(await jefe.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", new { socioId = socio, planId = plan, metodoPago = "caja" })); }
        finally { api.Reloj.Liberar(); }
        var entrenador = await api.Asegurar("Eva Tablero " + Nombres.Aleatorio(6));
        await Ejecutar(api.ConnectionString, $"""
            INSERT INTO contabilidad."HorasDeEntrenador" ("EntrenadorId", "Fecha", "Horas", "ValorHora")
            VALUES ({entrenador}, '2023-10-09', 6, 1000), ({entrenador}, '2023-10-10', 4, 1000);
            """);

        var i = await Indicadores(jefe, "2023-10", "2023-11-15");

        Assert.Equal(10m, Valor(i, "horas-entrenadores"));
        Assert.Equal(10000m, Valor(i, "costo-entrenadores"));
        Assert.Equal(50m, Valor(i, "costo-sobre-cuotas"));
    }

    [Fact]
    public async Task La_meta_pone_el_semaforo_y_se_compara_con_el_mes_anterior()
    {
        var jefe = await api.Jefe();
        await Crear(jefe, Factura("Venta", "2023-04-10T12:00:00Z", 100));
        await Crear(jefe, Factura("Venta", "2023-05-10T12:00:00Z", 120));
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await jefe.PutAsJsonAsync("/api/tablero/metas/ingresos", new { valor = 150 })).StatusCode);
            var ingresos = (await Indicadores(jefe, "2023-05"))["ingresos"];

            Assert.Equal(120m, (decimal?)ingresos["valor"]);
            Assert.Equal(100m, (decimal?)ingresos["anterior"]);
            Assert.Equal(20m, (decimal?)ingresos["variacion"]);
            Assert.Equal(150m, (decimal?)ingresos["meta"]);
            Assert.Equal("rojo", (string?)ingresos["semaforo"]);
            Assert.Equal("Financiera", (string?)ingresos["perspectiva"]);
        }
        finally { await jefe.PutAsJsonAsync("/api/tablero/metas/ingresos", new { valor = (decimal?)null }); }
    }

    [Fact]
    public async Task Un_mes_mal_escrito_da_400()
    {
        var r = await (await api.Jefe()).GetAsync("/api/tablero/indicadores?mes=2023-13");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}
