using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Ciclo de vida de una factura contra la API y la base reales: cada operación debe
/// dejar el Libro Caja, el Mayor y el stock en el mismo estado.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class FacturacionTests(ApiFixture api)
{
    private const long ProductoA = 1; // sembrado: precio de venta 150

    private static object Venta(int cantidad, string condicion = "ContadoCaja", decimal precio = 150) => new
    {
        fecha = "2026-10-20T12:00:00Z",
        tipoOperacion = "Venta",
        condicionPago = condicion,
        tipoComprobante = "B",
        cliente = "Consumidor Final",
        subtotal = cantidad * precio,
        iva = Math.Round(cantidad * precio * 0.21m, 2),
        total = cantidad * precio + Math.Round(cantidad * precio * 0.21m, 2),
        items = new[] { new { productoId = ProductoA, descripcion = "Producto A", cantidad, precioUnitario = precio, subtotal = cantidad * precio } }
    };

    [Fact]
    public async Task Venta_al_contado_impacta_caja_stock_y_mayor_y_anularla_lo_revierte_todo()
    {
        var jefe = await api.Jefe();
        var caja0 = await jefe.SaldoLibro("caja");
        var mayorVentas0 = await jefe.SaldoMayor("Ventas");
        var stock0 = await jefe.StockDe(ProductoA);

        var creada = await jefe.PostAsJsonAsync("/api/facturas", Venta(cantidad: 2));
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var id = (long)(await creada.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;

        Assert.Equal(caja0 + 363, await jefe.SaldoLibro("caja"));
        Assert.Equal(mayorVentas0 - 300, await jefe.SaldoMayor("Ventas"));   // saldo acreedor
        Assert.Equal(stock0 - 2, await jefe.StockDe(ProductoA));
        Assert.Equal(await jefe.SaldoLibro("caja"), await jefe.SaldoMayor("Caja"));

        var anulada = await jefe.PostAsync($"/api/facturas/{id}/anular", null);
        Assert.Equal(HttpStatusCode.NoContent, anulada.StatusCode);

        Assert.Equal(caja0, await jefe.SaldoLibro("caja"));
        Assert.Equal(mayorVentas0, await jefe.SaldoMayor("Ventas"));
        Assert.Equal(stock0, await jefe.StockDe(ProductoA));
        Assert.Equal(await jefe.SaldoLibro("caja"), await jefe.SaldoMayor("Caja"));

        var segundaVez = await jefe.PostAsync($"/api/facturas/{id}/anular", null);
        Assert.Equal(HttpStatusCode.BadRequest, segundaVez.StatusCode);
    }

    [Fact]
    public async Task Editar_una_factura_revierte_la_version_anterior_y_aplica_la_nueva()
    {
        var jefe = await api.Jefe();
        var caja0 = await jefe.SaldoLibro("caja");
        var banco0 = await jefe.SaldoLibro("banco");
        var stock0 = await jefe.StockDe(ProductoA);

        var creada = await jefe.PostAsJsonAsync("/api/facturas", Venta(cantidad: 1));
        var id = (long)(await creada.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;

        // Pasa a 3 unidades cobradas por banco.
        var editada = await jefe.PutAsJsonAsync($"/api/facturas/{id}", Venta(cantidad: 3, condicion: "TransferenciaBanco"));
        Assert.Equal(HttpStatusCode.NoContent, editada.StatusCode);

        Assert.Equal(caja0, await jefe.SaldoLibro("caja"));          // el cobro en caja se revirtió
        Assert.Equal(banco0 + 544.5m, await jefe.SaldoLibro("banco"));
        Assert.Equal(stock0 - 3, await jefe.StockDe(ProductoA));
        Assert.Equal(await jefe.SaldoLibro("banco"), await jefe.SaldoMayor("Banco"));
    }

    [Fact]
    public async Task Anular_la_factura_1_no_toca_la_factura_10()
    {
        // Regresión: antes se buscaban los movimientos con Contains("N° 1"),
        // que también coincidía con las facturas 10 a 19.
        var jefe = await api.Jefe();
        var ids = new List<long>();
        for (var i = 0; i < 11; i++)
        {
            var r = await jefe.PostAsJsonAsync("/api/facturas", Venta(cantidad: 1));
            ids.Add((long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!);
        }
        var caja0 = await jefe.SaldoLibro("caja");

        await jefe.PostAsync($"/api/facturas/{ids[0]}/anular", null);

        var facturas = (await jefe.GetJson("/api/facturas")).AsArray();
        var anuladas = ids.Where(id => (bool)facturas.First(f => (long)f!["id"]! == id)!["anulada"]!).ToList();
        Assert.Equal(new[] { ids[0] }, anuladas);
        Assert.Equal(caja0 - 181.5m, await jefe.SaldoLibro("caja"));
    }

    [Theory]
    [InlineData(1, 10, 1)]       // el total no es subtotal + IVA
    [InlineData(10, 0, 10)]      // el subtotal no coincide con los ítems
    public async Task Importes_inconsistentes_son_rechazados(decimal subtotal, decimal iva, decimal total)
    {
        var jefe = await api.Jefe();
        var factura = new
        {
            fecha = "2026-10-20T12:00:00Z", tipoOperacion = "Venta", condicionPago = "ContadoCaja",
            tipoComprobante = "C", cliente = "Consumidor Final", subtotal, iva, total,
            items = new[] { new { productoId = 0, descripcion = "Clase", cantidad = 1, precioUnitario = 1m, subtotal = 1m } }
        };

        var respuesta = await jefe.PostAsJsonAsync("/api/facturas", factura);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    private static object Compra(long numero, int cantidad = 1, decimal precio = 100) => new
    {
        fecha = "2026-10-20T12:00:00Z",
        tipoOperacion = "Compra",
        condicionPago = "CuentaCorriente",
        tipoComprobante = "A",
        cliente = "Proveedor de prueba",
        puntoVenta = 3,
        numeroComprobante = numero,
        subtotal = cantidad * precio,
        iva = cantidad * precio * 0.21m,
        total = cantidad * precio * 1.21m,
        items = new[] { new { productoId = 0, descripcion = "Insumos", cantidad, precioUnitario = precio, subtotal = cantidad * precio } }
    };

    [Fact]
    public async Task Editar_y_anular_no_dejan_haber_en_credito_fiscal_ni_debe_en_debito_fiscal()
    {
        // Anular o editar pasa el asiento a Anulado: no hay contraasientos con el IVA invertido.
        var jefe = await api.Jefe();
        var venta = (long)(await (await jefe.PostAsJsonAsync("/api/facturas", Venta(cantidad: 1))).Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        var compra = (long)(await (await jefe.PostAsJsonAsync("/api/facturas", Compra(numero: 9101))).Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PutAsJsonAsync($"/api/facturas/{venta}", Venta(cantidad: 2))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PutAsJsonAsync($"/api/facturas/{compra}", Compra(numero: 9101, cantidad: 2))).StatusCode);
        await jefe.PostAsync($"/api/facturas/{venta}/anular", null);
        await jefe.PostAsync($"/api/facturas/{compra}/anular", null);

        var lineas = (await jefe.GetJson("/api/asientos?clienteId=1")).AsArray()
            .Where(a => (int)a!["estado"]! == 1)
            .SelectMany(a => a!["lineas"]!.AsArray())
            .ToList();
        Assert.DoesNotContain(lineas, l => (long)l!["cuentaId"]! == 105 && (decimal)l["haber"]! != 0);
        Assert.DoesNotContain(lineas, l => (long)l!["cuentaId"]! == 201 && (decimal)l["debe"]! != 0);
    }

    [Fact]
    public async Task Las_ventas_se_numeran_solas_y_una_compra_no_se_carga_dos_veces()
    {
        var jefe = await api.Jefe();
        var a = await (await jefe.PostAsJsonAsync("/api/facturas", Venta(cantidad: 1))).Content.ReadFromJsonAsync<JsonObject>();
        var b = await (await jefe.PostAsJsonAsync("/api/facturas", Venta(cantidad: 1))).Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal((long)a!["numeroComprobante"]! + 1, (long)b!["numeroComprobante"]!);
        Assert.Matches(@"^0001-\d{8}$", (string)b["numeroFactura"]!);

        Assert.Equal(HttpStatusCode.Created, (await jefe.PostAsJsonAsync("/api/facturas", Compra(numero: 9201))).StatusCode);
        var repetida = await jefe.PostAsJsonAsync("/api/facturas", Compra(numero: 9201));
        Assert.Equal(HttpStatusCode.BadRequest, repetida.StatusCode);
    }

    [Fact]
    public async Task Un_cliente_dado_de_baja_se_reactiva_al_cargarlo_de_nuevo()
    {
        // La baja es lógica (sus facturas se conservan); volver a cargarlo con el mismo
        // CUIT reactiva el mismo registro en lugar de crear un duplicado.
        var jefe = await api.Jefe();
        var datos = new { razonSocial = "Gimnasio Vecino SRL", direccion = "Calle 2", localidad = "Haedo", cuit = "30-71111111-1", condicionIva = "Responsable Inscripto" };
        var id = (long)(await (await jefe.PostAsJsonAsync("/api/clientes", datos)).Content.ReadFromJsonAsync<JsonObject>())!["id"]!;

        Assert.Equal(HttpStatusCode.NoContent, (await jefe.DeleteAsync($"/api/clientes/{id}")).StatusCode);
        var otraVez = await jefe.PostAsJsonAsync("/api/clientes", datos);

        Assert.True(otraVez.IsSuccessStatusCode, await otraVez.Detalle());
        Assert.Equal(id, (long)(await otraVez.Content.ReadFromJsonAsync<JsonObject>())!["id"]!);
    }

    [Fact]
    public async Task Un_cliente_puede_cargarse_con_dni_en_lugar_de_cuit()
    {
        var jefe = await api.Jefe();
        object Datos(string doc) => new { razonSocial = "Lionel Messi", direccion = "Balcarce 1828", localidad = "San Justo", cuit = doc, condicionIva = "Consumidor Final" };

        var ok = await jefe.PostAsJsonAsync("/api/clientes", Datos("45.641.666"));
        Assert.True(ok.IsSuccessStatusCode, await ok.Detalle());
        Assert.Equal("45641666", (string)(await ok.Content.ReadFromJsonAsync<JsonObject>())!["cuit"]!);

        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsJsonAsync("/api/clientes", Datos("123456"))).StatusCode);
    }

    [Fact]
    public async Task Un_dni_solo_vale_para_consumidor_final()
    {
        var jefe = await api.Jefe();
        object Datos(string condicion) => new { razonSocial = "Persona con DNI", direccion = "Calle 3", localidad = "Ramos", cuit = "30111222", condicionIva = condicion };

        var inscripto = await jefe.PostAsJsonAsync("/api/clientes", Datos("Responsable Inscripto"));
        Assert.Equal(HttpStatusCode.BadRequest, inscripto.StatusCode);
        Assert.Contains("CUIT", await inscripto.Detalle());
        Assert.True((await jefe.PostAsJsonAsync("/api/clientes", Datos("Consumidor Final"))).IsSuccessStatusCode);
    }

    [Fact]
    public async Task La_factura_A_solo_se_emite_a_responsables_inscriptos_o_monotributistas()
    {
        var jefe = await api.Jefe();
        async Task<long> Cliente(string razon, string cuit, string condicion)
            => (long)(await (await jefe.PostAsJsonAsync("/api/clientes", new { razonSocial = razon, direccion = "Calle 4", localidad = "Haedo", cuit, condicionIva = condicion }))
                .Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        object FacturaA(long clienteId) => new
        {
            fecha = "2026-10-20T12:00:00Z", tipoOperacion = "Venta", condicionPago = "ContadoCaja", tipoComprobante = "A", clienteId,
            subtotal = 150m, iva = 31.5m, total = 181.5m,
            items = new[] { new { productoId = ProductoA, descripcion = "Producto A", cantidad = 1, precioUnitario = 150m, subtotal = 150m } }
        };

        var consumidor = await Cliente("Maria Consumidora", "27111222", "Consumidor Final");
        var rechazada = await jefe.PostAsJsonAsync("/api/facturas", FacturaA(consumidor));
        Assert.Equal(HttpStatusCode.BadRequest, rechazada.StatusCode);
        Assert.Contains("factura B", await rechazada.Detalle());

        var inscripto = await Cliente("Comercio RI SRL", "30-70707070-2", "Responsable Inscripto");
        var aceptada = await jefe.PostAsJsonAsync("/api/facturas", FacturaA(inscripto));
        Assert.Equal(HttpStatusCode.Created, aceptada.StatusCode);
    }

    [Fact]
    public async Task La_razon_social_se_completa_desde_el_cliente_elegido()
    {
        // Regresión: el formulario mandaba cliente vacío y ninguna factura se guardaba.
        var jefe = await api.Jefe();
        var cliente = await jefe.PostAsJsonAsync("/api/clientes", new
        {
            razonSocial = "Club Atlético Ejemplo", direccion = "Calle 1", localidad = "Ramos Mejía",
            cuit = "30-71234567-1", condicionIva = "Responsable Inscripto"
        });
        Assert.True(cliente.IsSuccessStatusCode, await cliente.Detalle());
        var clienteId = (long)(await cliente.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;

        var factura = await jefe.PostAsJsonAsync("/api/facturas", new
        {
            fecha = "2026-10-20T12:00:00Z", tipoOperacion = "Venta", condicionPago = "CuentaCorriente",
            tipoComprobante = "A", cliente = "", clienteId, subtotal = 100m, iva = 21m, total = 121m,
            items = new[] { new { productoId = 0, descripcion = "Servicio", cantidad = 1, precioUnitario = 100m, subtotal = 100m } }
        });

        Assert.Equal(HttpStatusCode.Created, factura.StatusCode);
        Assert.Equal("Club Atlético Ejemplo", (string)(await factura.Content.ReadFromJsonAsync<JsonObject>())!["cliente"]!);
    }
}
