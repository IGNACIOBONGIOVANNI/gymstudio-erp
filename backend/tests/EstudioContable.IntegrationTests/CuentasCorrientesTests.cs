using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Cuentas a cobrar y a pagar: cada cobro o pago debe reducir la deuda de su cliente o proveedor y el total
/// debe coincidir siempre con el saldo de Deudores por Ventas / Proveedores del Libro Mayor.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CuentasCorrientesTests(ApiFixture api)
{
    private const string Hoy = "2026-10-27";
    private const long DeudoresPorVentas = 103, Proveedores = 202;
    private static readonly int[] Multiplicadores = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

    /// <summary>CUIT válido (con su dígito verificador) para no chocar con los de otros tests.</summary>
    private static string CuitNuevo()
    {
        while (true)
        {
            var d = "30" + Random.Shared.Next(10_000_000, 99_999_999);
            var suma = Multiplicadores.Select((m, i) => m * (d[i] - '0')).Sum();
            var v = 11 - suma % 11;
            if (v == 11) v = 0;
            if (v != 10) return d + v;
        }
    }

    private async Task<(long Id, string Nombre)> Tercero(HttpClient c, string ruta, string prefijo)
    {
        var nombre = $"{prefijo} {Guid.NewGuid().ToString("N")[..6]}";
        var r = await c.PostAsJsonAsync($"/api/{ruta}", new { razonSocial = nombre, direccion = "Calle 1", localidad = "Haedo", cuit = CuitNuevo(), condicionIva = "Responsable Inscripto" });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return ((long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!, nombre);
    }

    private static async Task<long> VentaEnCuenta(HttpClient c, long clienteId, string fecha, decimal neto)
    {
        var r = await c.PostAsJsonAsync("/api/facturas", new
        {
            fecha, tipoOperacion = "Venta", condicionPago = "CuentaCorriente", tipoComprobante = "A", clienteId,
            subtotal = neto, iva = neto * 0.21m, total = neto * 1.21m,
            items = new[] { new { productoId = 0, descripcion = "Servicio", cantidad = 1, precioUnitario = neto, subtotal = neto } }
        });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private static long _numero = 80000;
    private static async Task<long> CompraEnCuenta(HttpClient c, long proveedorId, string fecha, decimal neto)
    {
        var r = await c.PostAsJsonAsync("/api/facturas", new
        {
            fecha, tipoOperacion = "Compra", condicionPago = "CuentaCorriente", tipoComprobante = "A", proveedorId,
            puntoVenta = 5, numeroComprobante = Interlocked.Increment(ref _numero),
            subtotal = neto, iva = neto * 0.21m, total = neto * 1.21m,
            items = new[] { new { productoId = 0, descripcion = "Insumos", cantidad = 1, precioUnitario = neto, subtotal = neto } }
        });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private static async Task<HttpResponseMessage> Movimiento(HttpClient c, string libro, bool ingreso, decimal importe, long contrapartida, long? clienteId = null, long? proveedorId = null)
        => await c.PostAsJsonAsync($"/api/{libro}", new
        {
            clienteId = "1", fecha = "2026-10-20T12:00:00Z", detalle = "Movimiento de prueba", tipoMovimiento = ingreso ? 1 : 2, importe,
            cuentaContrapartidaId = contrapartida, clienteComercialId = clienteId, proveedorId
        });

    private static async Task<JsonNode> Listado(HttpClient c, string tipo) => await c.GetJson($"/api/cuentas-corrientes/{tipo}?hoy={Hoy}");

    private static JsonNode Fila(JsonNode listado, long id) => listado["terceros"]!.AsArray().Single(t => (long?)t!["id"] == id)!;

    [Fact]
    public async Task Un_cobro_reduce_el_saldo_del_cliente_y_el_total_coincide_con_el_mayor()
    {
        var jefe = await api.Jefe();
        var (cliente, _) = await Tercero(jefe, "clientes", "Cliente deudor");
        await VentaEnCuenta(jefe, cliente, "2026-09-17T12:00:00Z", 1000);     // total 1.210, hace 40 días

        Assert.Equal(1210m, (decimal)Fila(await Listado(jefe, "clientes"), cliente)["saldo"]!);

        var cobro = await Movimiento(jefe, "caja", ingreso: true, importe: 500, DeudoresPorVentas, clienteId: cliente);
        Assert.True(cobro.IsSuccessStatusCode, await cobro.Detalle());

        var listado = await Listado(jefe, "clientes");
        var fila = Fila(listado, cliente);
        Assert.Equal(1210m, (decimal)fila["facturado"]!);
        Assert.Equal(500m, (decimal)fila["pagado"]!);
        Assert.Equal(710m, (decimal)fila["saldo"]!);
        Assert.Equal(710m, (decimal)fila["antiguedad"]!["de31A60"]!);       // la deuda que queda tiene 40 días
        Assert.Equal(1, (int)fila["facturasPendientes"]!);
        Assert.Equal(0m, (decimal)listado["mayor"]!["diferencia"]!);         // coincide con Deudores por Ventas del Mayor
    }

    [Fact]
    public async Task El_estado_de_cuenta_lista_facturas_y_cobros_con_saldo_corrido()
    {
        var jefe = await api.Jefe();
        var (cliente, nombre) = await Tercero(jefe, "clientes", "Cliente con estado");
        await VentaEnCuenta(jefe, cliente, "2026-08-10T12:00:00Z", 1000);
        Assert.True((await Movimiento(jefe, "banco", ingreso: true, importe: 300, DeudoresPorVentas, clienteId: cliente)).IsSuccessStatusCode);

        var detalle = await jefe.GetJson($"/api/cuentas-corrientes/clientes/{cliente}?hoy={Hoy}");

        Assert.Equal(nombre, (string)detalle["tercero"]!["nombre"]!);
        var estado = detalle["estado"]!.AsArray();
        Assert.Equal(2, estado.Count);
        Assert.Equal(1210m, (decimal)estado[0]!["saldo"]!);
        Assert.Equal(910m, (decimal)estado[1]!["saldo"]!);
        Assert.Contains("Cobro en Banco", (string)estado[1]!["descripcion"]!);
        Assert.Equal(910m, (decimal)detalle["saldo"]!);
    }

    [Fact]
    public async Task Un_pago_a_un_proveedor_reduce_lo_que_se_le_debe_y_coincide_con_el_mayor()
    {
        var jefe = await api.Jefe();
        var (proveedor, _) = await Tercero(jefe, "proveedores", "Proveedor acreedor");
        await CompraEnCuenta(jefe, proveedor, "2026-07-01T12:00:00Z", 2000);   // total 2.420, hace 118 días

        var pago = await Movimiento(jefe, "banco", ingreso: false, importe: 1000, Proveedores, proveedorId: proveedor);
        Assert.True(pago.IsSuccessStatusCode, await pago.Detalle());

        var listado = await Listado(jefe, "proveedores");
        var fila = Fila(listado, proveedor);
        Assert.Equal(1420m, (decimal)fila["saldo"]!);
        Assert.Equal(1420m, (decimal)fila["antiguedad"]!["mas90"]!);
        Assert.Equal(0m, (decimal)listado["mayor"]!["diferencia"]!);
    }

    [Fact]
    public async Task Un_pago_de_mas_queda_como_saldo_a_favor()
    {
        var jefe = await api.Jefe();
        var (cliente, _) = await Tercero(jefe, "clientes", "Cliente anticipo");
        await VentaEnCuenta(jefe, cliente, "2026-10-01T12:00:00Z", 100);       // total 121
        Assert.True((await Movimiento(jefe, "caja", ingreso: true, importe: 200, DeudoresPorVentas, clienteId: cliente)).IsSuccessStatusCode);

        var fila = Fila(await Listado(jefe, "clientes"), cliente);

        Assert.Equal(-79m, (decimal)fila["saldo"]!);
        Assert.Equal(79m, (decimal)fila["aFavor"]!);
        Assert.Equal(0, (int)fila["facturasPendientes"]!);
    }

    [Fact]
    public async Task Una_factura_anulada_deja_de_figurar_en_la_deuda()
    {
        var jefe = await api.Jefe();
        var (cliente, _) = await Tercero(jefe, "clientes", "Cliente anulado");
        var factura = await VentaEnCuenta(jefe, cliente, "2026-10-05T12:00:00Z", 500);
        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PostAsync($"/api/facturas/{factura}/anular", null)).StatusCode);

        var listado = await Listado(jefe, "clientes");

        Assert.DoesNotContain(listado["terceros"]!.AsArray(), t => (long?)t!["id"] == cliente);   // sin movimientos vigentes no aparece
        Assert.Equal(0m, (decimal)listado["mayor"]!["diferencia"]!);
    }

    [Fact]
    public async Task Un_cobro_a_clientes_exige_el_cliente_y_un_pago_a_proveedores_el_proveedor()
    {
        var jefe = await api.Jefe();
        var (cliente, _) = await Tercero(jefe, "clientes", "Cliente exigido");

        var sinCliente = await Movimiento(jefe, "caja", ingreso: true, importe: 10, DeudoresPorVentas);
        Assert.Equal(HttpStatusCode.BadRequest, sinCliente.StatusCode);
        Assert.Contains("cliente", await sinCliente.Detalle());

        var sinProveedor = await Movimiento(jefe, "banco", ingreso: false, importe: 10, Proveedores);
        Assert.Equal(HttpStatusCode.BadRequest, sinProveedor.StatusCode);
        Assert.Contains("proveedor", await sinProveedor.Detalle());

        Assert.Equal(HttpStatusCode.BadRequest, (await Movimiento(jefe, "caja", ingreso: true, importe: 10, DeudoresPorVentas, clienteId: 99999999)).StatusCode);

        // Con otra contrapartida (Otros Ingresos = 106) el cliente se ignora: no genera deuda ni cobro.
        Assert.True((await Movimiento(jefe, "caja", ingreso: true, importe: 10, 106, clienteId: cliente)).IsSuccessStatusCode);
        Assert.DoesNotContain((await Listado(jefe, "clientes"))["terceros"]!.AsArray(), t => (long?)t!["id"] == cliente);
    }

    [Fact]
    public async Task Editar_un_cobro_conserva_su_cliente_y_se_puede_pasar_a_otro_y_eliminarlo_devuelve_la_deuda()
    {
        var jefe = await api.Jefe();
        var (primero, _) = await Tercero(jefe, "clientes", "Cliente primero");
        var (segundo, _) = await Tercero(jefe, "clientes", "Cliente segundo");
        await VentaEnCuenta(jefe, primero, "2026-10-01T12:00:00Z", 1000);
        await VentaEnCuenta(jefe, segundo, "2026-10-01T12:00:00Z", 1000);

        var creado = await Movimiento(jefe, "caja", ingreso: true, importe: 400, DeudoresPorVentas, clienteId: primero);
        var id = (long)(await creado.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;

        // Editar solo el importe: sigue siendo del primer cliente.
        var edicion = await jefe.PutAsJsonAsync($"/api/caja/{id}", new { fecha = "2026-10-20T12:00:00Z", detalle = "corregido", ingreso = 600m, egreso = (decimal?)null });
        Assert.True(edicion.IsSuccessStatusCode, await edicion.Detalle());
        Assert.Equal(610m, (decimal)Fila(await Listado(jefe, "clientes"), primero)["saldo"]!);   // 1.210 - 600

        // Pasarlo al segundo cliente.
        var cambio = await jefe.PutAsJsonAsync($"/api/caja/{id}", new { fecha = "2026-10-20T12:00:00Z", detalle = "es del otro", ingreso = 600m, egreso = (decimal?)null, cuentaContrapartidaId = DeudoresPorVentas, clienteComercialId = segundo });
        Assert.True(cambio.IsSuccessStatusCode, await cambio.Detalle());
        var listado = await Listado(jefe, "clientes");
        Assert.Equal(1210m, (decimal)Fila(listado, primero)["saldo"]!);
        Assert.Equal(610m, (decimal)Fila(listado, segundo)["saldo"]!);

        // Eliminar el cobro devuelve la deuda.
        Assert.True((await jefe.DeleteAsync($"/api/caja/{id}")).IsSuccessStatusCode);
        listado = await Listado(jefe, "clientes");
        Assert.Equal(1210m, (decimal)Fila(listado, segundo)["saldo"]!);
        Assert.Equal(0m, (decimal)listado["mayor"]!["diferencia"]!);
    }

    [Fact]
    public async Task Solo_administrador_y_contador_ven_las_cuentas_corrientes()
    {
        var recepcion = await api.Recepcion();
        var contador = await api.Contador();

        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.GetAsync("/api/cuentas-corrientes/clientes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.GetAsync("/api/cuentas-corrientes/proveedores/1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await contador.GetAsync("/api/cuentas-corrientes/clientes")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await contador.GetAsync("/api/cuentas-corrientes/otra-cosa")).StatusCode);
    }

    [Fact]
    public async Task Un_cliente_con_cobros_no_se_puede_borrar_de_la_base()
    {
        var jefe = await api.Jefe();
        var (cliente, _) = await Tercero(jefe, "clientes", "Cliente con cobro");
        Assert.True((await Movimiento(jefe, "caja", ingreso: true, importe: 50, DeudoresPorVentas, clienteId: cliente)).IsSuccessStatusCode);

        // La baja desde la API es lógica; la base además impide borrar la fila (clave foránea con RESTRICT).
        await using var conexion = new Npgsql.NpgsqlConnection(api.ConnectionString);
        await conexion.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand($"DELETE FROM contabilidad.\"ClientesComerciales\" WHERE \"Id\" = {cliente}", conexion);
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal(Npgsql.PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }
}
