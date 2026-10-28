using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>La vista previa del asiento debe ser, línea por línea, el asiento que después se registra.</summary>
[Collection(ColeccionApi.Nombre)]
public class VistaPreviaAsientoTests(ApiFixture api)
{
    private static object Previa(string operacion, string condicion, decimal subtotal, decimal iva)
        => new { tipoOperacion = operacion, condicionPago = condicion, subtotal, iva, total = subtotal + iva };

    private static object Factura(string operacion, string condicion, string fecha, decimal subtotal, decimal iva) => new
    {
        fecha,
        tipoOperacion = operacion,
        condicionPago = condicion,
        tipoComprobante = operacion == "Venta" ? "B" : "A",
        cliente = operacion == "Venta" ? "Consumidor Final" : "Proveedor vista previa",
        subtotal,
        iva,
        total = subtotal + iva,
        items = new[] { new { productoId = 0, descripcion = "Servicio", cantidad = 1, precioUnitario = subtotal, subtotal } }
    };

    private static List<(string Cuenta, decimal Debe, decimal Haber)> Normalizar(IEnumerable<JsonNode?> lineas, string campoCuenta)
        => lineas.Select(l => ((string)l![campoCuenta]!, (decimal)l["debe"]!, (decimal)l["haber"]!)).OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();

    [Theory]
    [InlineData("Venta", "ContadoCaja", "2024-02-01")]
    [InlineData("Venta", "TransferenciaBanco", "2024-02-02")]
    [InlineData("Venta", "CuentaCorriente", "2024-02-03")]
    [InlineData("Compra", "ContadoCaja", "2024-02-04")]
    [InlineData("Compra", "TransferenciaBanco", "2024-02-05")]
    [InlineData("Compra", "CuentaCorriente", "2024-02-06")]
    public async Task La_vista_previa_es_igual_al_asiento_que_se_registra(string operacion, string condicion, string fecha)
    {
        var jefe = await api.Jefe();
        var previa = await jefe.PostAsJsonAsync("/api/facturas/vista-previa-asiento", Previa(operacion, condicion, 1000m, 210m));
        Assert.Equal(HttpStatusCode.OK, previa.StatusCode);
        var cuerpoPrevia = (await previa.Content.ReadFromJsonAsync<JsonNode>())!;

        var emitida = await jefe.PostAsJsonAsync("/api/facturas", Factura(operacion, condicion, $"{fecha}T12:00:00Z", 1000m, 210m));
        Assert.Equal(HttpStatusCode.Created, emitida.StatusCode);
        var diario = (await jefe.GetJson($"/api/reportes/generar?tipo=diario&desde={fecha}&hasta={fecha}")).AsArray();
        var asiento = Assert.Single(diario)!;

        Assert.Equal(
            Normalizar(asiento["lineas"]!.AsArray(), "nombreCuenta"),
            Normalizar(cuerpoPrevia["lineas"]!.AsArray(), "cuenta"));
        Assert.Equal((decimal)asiento["totalDebe"]!, (decimal)cuerpoPrevia["totalDebe"]!);
        Assert.Equal((decimal)cuerpoPrevia["totalDebe"]!, (decimal)cuerpoPrevia["totalHaber"]!);   // la partida doble cierra también en la vista previa
    }

    [Fact]
    public async Task Sin_iva_la_vista_previa_no_incluye_la_linea_de_iva_y_los_efectos_dependen_de_la_condicion()
    {
        var jefe = await api.Jefe();

        var contado = (await (await jefe.PostAsJsonAsync("/api/facturas/vista-previa-asiento", Previa("Venta", "ContadoCaja", 500m, 0m))).Content.ReadFromJsonAsync<JsonNode>())!;
        Assert.Equal(2, contado["lineas"]!.AsArray().Count);
        Assert.DoesNotContain(contado["lineas"]!.AsArray(), l => ((string)l!["cuenta"]!).Contains("IVA"));
        Assert.Contains("ingreso en la Caja", string.Join(" ", contado["efectos"]!.AsArray().Select(e => (string)e!)));

        var credito = (await (await jefe.PostAsJsonAsync("/api/facturas/vista-previa-asiento", Previa("Compra", "CuentaCorriente", 500m, 105m))).Content.ReadFromJsonAsync<JsonNode>())!;
        Assert.Contains("deuda con el proveedor", string.Join(" ", credito["efectos"]!.AsArray().Select(e => (string)e!)));
    }

    [Theory]
    [InlineData("Alquiler", "ContadoCaja", 100, 21, 121)]       // operación inexistente
    [InlineData("Venta", "Trueque", 100, 21, 121)]              // condición inexistente
    [InlineData("Venta", "ContadoCaja", 100, 21, 500)]          // el total no es subtotal + IVA
    [InlineData("Venta", "ContadoCaja", -100, 0, -100)]         // importe negativo
    [InlineData("Venta", "ContadoCaja", 1e15, 0, 1e15)]         // importe absurdo
    public async Task Los_datos_invalidos_se_rechazan_con_400(string operacion, string condicion, double subtotal, double iva, double total)
    {
        var jefe = await api.Jefe();
        var r = await jefe.PostAsJsonAsync("/api/facturas/vista-previa-asiento", new { tipoOperacion = operacion, condicionPago = condicion, subtotal, iva, total });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Recepcion_puede_ver_la_vista_previa_y_sin_sesion_no()
    {
        var recepcion = await api.Recepcion();
        Assert.Equal(HttpStatusCode.OK, (await recepcion.PostAsJsonAsync("/api/facturas/vista-previa-asiento", Previa("Venta", "ContadoCaja", 100m, 21m))).StatusCode);

        var anonimo = api.ClienteAnonimo();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/facturas/vista-previa-asiento", Previa("Venta", "ContadoCaja", 100m, 21m))).StatusCode);
    }
}
