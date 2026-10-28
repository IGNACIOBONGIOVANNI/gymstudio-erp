using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Resumen de inicio. Cada test usa un mes propio (2025) que ningún otro test toca, así los totales
/// son exactamente los de las facturas que carga.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class DashboardTests(ApiFixture api)
{
    private static object Venta(string fecha, int cantidad, decimal precio = 150, long productoId = 0) => new
    {
        fecha, tipoOperacion = "Venta", condicionPago = "ContadoCaja", tipoComprobante = "B", cliente = "Consumidor Final",
        subtotal = cantidad * precio, iva = Math.Round(cantidad * precio * 0.21m, 2), total = cantidad * precio + Math.Round(cantidad * precio * 0.21m, 2),
        items = new[] { new { productoId, descripcion = "Venta de prueba", cantidad, precioUnitario = precio, subtotal = cantidad * precio } }
    };

    private static object Compra(string fecha, long numero, decimal neto) => new
    {
        fecha, tipoOperacion = "Compra", condicionPago = "CuentaCorriente", tipoComprobante = "A", cliente = "Proveedor de prueba",
        puntoVenta = 4, numeroComprobante = numero, subtotal = neto, iva = neto * 0.21m, total = neto * 1.21m,
        items = new[] { new { productoId = 0, descripcion = "Insumos", cantidad = 1, precioUnitario = neto, subtotal = neto } }
    };

    private static async Task<long> Crear(HttpClient c, object factura)
    {
        var r = await c.PostAsJsonAsync("/api/facturas", factura);
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private static async Task<JsonNode> Resumen(HttpClient c, string mes, string hoy)
        => await c.GetJson($"/api/dashboard?mes={mes}&hoy={hoy}");

    [Fact]
    public async Task Los_totales_del_mes_salen_de_las_facturas_cargadas()
    {
        var jefe = await api.Jefe();
        await Crear(jefe, Venta("2025-03-10T12:00:00Z", cantidad: 2));            // neto 300, IVA 63
        await Crear(jefe, Compra("2025-03-12T12:00:00Z", numero: 9501, neto: 100)); // neto 100, IVA 21

        var finanzas = (await Resumen(jefe, "2025-03", "2025-03-20"))["finanzas"]!;

        Assert.Equal(300m, (decimal)finanzas["ventas"]!["neto"]!);
        Assert.Equal(63m, (decimal)finanzas["ventas"]!["iva"]!);
        Assert.Equal(1, (int)finanzas["ventas"]!["cantidad"]!);
        Assert.Equal(100m, (decimal)finanzas["compras"]!["neto"]!);
        Assert.Equal(200m, (decimal)finanzas["resultado"]!);
        Assert.Equal(42m, (decimal)finanzas["ivaAPagar"]!);   // 63 de débito fiscal menos 21 de crédito fiscal

        var serie = finanzas["serie"]!.AsArray();
        Assert.Equal(6, serie.Count);
        Assert.Equal("2025-03", (string)serie[5]!["mes"]!);
        Assert.Equal("2024-10", (string)serie[0]!["mes"]!);
        Assert.Equal(300m, (decimal)serie[5]!["ventas"]!);
        Assert.Equal(0m, (decimal)serie[4]!["ventas"]!);       // un mes sin movimiento aparece en cero
    }

    [Fact]
    public async Task Una_factura_anulada_no_suma_y_se_cuenta_aparte()
    {
        var jefe = await api.Jefe();
        var id = await Crear(jefe, Venta("2025-04-10T12:00:00Z", cantidad: 1));
        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PostAsync($"/api/facturas/{id}/anular", null)).StatusCode);

        var finanzas = (await Resumen(jefe, "2025-04", "2025-04-20"))["finanzas"]!;

        Assert.Equal(0m, (decimal)finanzas["ventas"]!["neto"]!);
        Assert.Equal(0, (int)finanzas["ventas"]!["cantidad"]!);
        Assert.Equal(1, (int)finanzas["facturasAnuladas"]!);
    }

    [Fact]
    public async Task Los_productos_mas_vendidos_y_el_origen_de_los_ingresos_se_arman_con_los_items()
    {
        var jefe = await api.Jefe();
        await Crear(jefe, Venta("2025-05-10T12:00:00Z", cantidad: 3, precio: 150, productoId: 1));   // producto A, neto 450

        var finanzas = (await Resumen(jefe, "2025-05", "2025-05-20"))["finanzas"]!;

        var top = finanzas["topProductos"]!.AsArray();
        Assert.Single(top);
        Assert.Equal(3, (int)top[0]!["cantidad"]!);
        Assert.Equal(450m, (decimal)top[0]!["total"]!);
        var origen = finanzas["ingresosPorOrigen"]!.AsArray().ToDictionary(o => (string)o!["origen"]!, o => (decimal)o!["neto"]!);
        Assert.Equal(450m, origen["Productos y otros"]);
        Assert.Equal(0m, origen["Cuotas de socios"]);
    }

    private static readonly int[] Multiplicadores = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

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

    [Fact]
    public async Task Lo_que_hay_que_cobrar_y_pagar_es_el_total_de_las_cuentas_corrientes()
    {
        var jefe = await api.Jefe();
        var cliente = await jefe.PostAsJsonAsync("/api/clientes", new { razonSocial = $"Cliente resumen {Guid.NewGuid():N}"[..28], direccion = "Calle 1", localidad = "Haedo", cuit = CuitNuevo(), condicionIva = "Responsable Inscripto" });
        Assert.True(cliente.IsSuccessStatusCode, await cliente.Detalle());
        var clienteId = (long)(await cliente.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        var venta = await jefe.PostAsJsonAsync("/api/facturas", new
        {
            fecha = "2025-06-10T12:00:00Z", tipoOperacion = "Venta", condicionPago = "CuentaCorriente", tipoComprobante = "A", clienteId,
            subtotal = 1000m, iva = 210m, total = 1210m,
            items = new[] { new { productoId = 0, descripcion = "Servicio", cantidad = 1, precioUnitario = 1000m, subtotal = 1000m } }
        });
        Assert.True(venta.IsSuccessStatusCode, await venta.Detalle());

        var resumen = (await Resumen(jefe, "2025-06", "2025-06-20"))["finanzas"]!;
        var cobrar = await jefe.GetJson("/api/cuentas-corrientes/clientes?hoy=2025-06-20");
        var pagar = await jefe.GetJson("/api/cuentas-corrientes/proveedores?hoy=2025-06-20");

        Assert.Equal((decimal)cobrar["total"]!["saldo"]!, (decimal)resumen["aCobrar"]!);
        Assert.Equal((decimal)pagar["total"]!["saldo"]!, (decimal)resumen["aPagar"]!);
        Assert.True((decimal)resumen["aCobrar"]! >= 1210m);
    }


    private static string[] Novedades(JsonNode resumen) => resumen["novedades"]!.AsArray().Select(n => (string)n!["texto"]!).ToArray();

    [Fact]
    public async Task Las_novedades_comparan_las_ventas_con_el_mes_anterior()
    {
        var jefe = await api.Jefe();
        await Crear(jefe, Venta("2022-03-10T12:00:00Z", cantidad: 1, precio: 1000));    // mes anterior: neto 1.000
        await Crear(jefe, Venta("2022-04-10T12:00:00Z", cantidad: 1, precio: 2500));    // mes elegido: neto 2.500

        var textos = Novedades(await Resumen(jefe, "2022-04", "2022-04-20"));

        Assert.Contains(textos, t => t.Contains("Las ventas de abril de 2022 subieron un 150 % respecto de marzo de 2022"));
    }

    [Fact]
    public async Task Una_deuda_de_cliente_de_mas_de_90_dias_se_avisa_con_el_nombre_del_cliente()
    {
        var jefe = await api.Jefe();
        var nombre = $"Cliente moroso {Guid.NewGuid():N}"[..26];
        var cliente = await jefe.PostAsJsonAsync("/api/clientes", new { razonSocial = nombre, direccion = "Calle 1", localidad = "Haedo", cuit = CuitNuevo(), condicionIva = "Responsable Inscripto" });
        Assert.True(cliente.IsSuccessStatusCode, await cliente.Detalle());
        var clienteId = (long)(await cliente.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        var venta = await jefe.PostAsJsonAsync("/api/facturas", new
        {
            fecha = "2023-04-10T12:00:00Z", tipoOperacion = "Venta", condicionPago = "CuentaCorriente", tipoComprobante = "A", clienteId,
            subtotal = 1000m, iva = 210m, total = 1210m,
            items = new[] { new { productoId = 0, descripcion = "Servicio", cantidad = 1, precioUnitario = 1000m, subtotal = 1000m } }
        });
        Assert.True(venta.IsSuccessStatusCode, await venta.Detalle());

        // 144 días después de la factura, sin cobrar.
        var textos = Novedades(await Resumen(jefe, "2023-09", "2023-09-01"));

        Assert.Contains(textos, t => t.Contains("con más de 90 días de antigüedad") && t.Contains(nombre) && t.Contains("$ 1.210,00"));
    }

    [Fact]
    public async Task Con_la_contabilidad_en_orden_las_novedades_lo_confirman_y_Recepcion_no_ve_frases_financieras()
    {
        var jefe = await api.Jefe();
        Assert.Contains(Novedades(await Resumen(jefe, "2025-03", "2025-03-20")), t => t.StartsWith("Contabilidad verificada"));

        var recepcion = await api.Recepcion();
        var textos = Novedades(await Resumen(recepcion, "2025-03", "2025-03-20"));
        Assert.NotEmpty(textos);
        Assert.DoesNotContain(textos, t => t.Contains("$") || t.Contains("ventas") || t.Contains("Caja") || t.Contains("clientes") || t.Contains("Contabilidad") || t.Contains("proveedores"));
    }

    [Fact]
    public async Task Las_novedades_ponen_primero_lo_que_requiere_atencion()
    {
        var jefe = await api.Jefe();
        var nivel = (await Resumen(jefe, "2025-03", "2025-03-20"))["novedades"]!.AsArray().Select(n => (string)n!["nivel"]!).ToList();

        var orden = nivel.Select(n => n switch { "atencion" => 0, "info" => 1, _ => 2 }).ToList();
        Assert.Equal(orden.OrderBy(x => x), orden);
    }
    [Fact]
    public async Task Recepcion_ve_solo_lo_del_gimnasio_y_nunca_los_importes()
    {
        var recepcion = await api.Recepcion();
        var texto = await (await recepcion.GetAsync("/api/dashboard?mes=2025-03&hoy=2025-03-20")).Content.ReadAsStringAsync();
        var json = JsonNode.Parse(texto)!;

        Assert.Null(json["finanzas"]);
        Assert.NotNull(json["gimnasio"]);
        Assert.DoesNotContain("neto", texto);
        Assert.DoesNotContain("cajaSaldo", texto);
    }

    [Fact]
    public async Task Un_producto_con_poco_stock_aparece_en_las_alertas()
    {
        var jefe = await api.Jefe();
        var nombre = $"Producto escaso {Guid.NewGuid():N}"[..28];
        var creado = await jefe.PostAsJsonAsync("/api/productos", new { nombre, precioUnitario = 10, precioVenta = 20, stockInicial = 2 });
        Assert.True(creado.IsSuccessStatusCode, await creado.Detalle());

        var resumen = await Resumen(jefe, "2025-03", "2025-03-20");

        Assert.Contains(resumen["stockBajo"]!.AsArray(), p => (string?)p!["nombre"] == nombre && (int)p["stock"]! == 2);
    }

    [Fact]
    public async Task Las_rutinas_vencidas_se_cuentan_con_la_fecha_de_hoy_que_manda_el_navegador()
    {
        var jefe = await api.Jefe();
        var antes = (int)(await Resumen(jefe, "2025-03", "2031-01-01"))["gimnasio"]!["rutinasVencidas"]!;

        var ejercicio = (long)(await (await jefe.PostAsJsonAsync("/api/ejercicios", new { nombre = $"Ej {Guid.NewGuid():N}"[..20], grupoMuscular = "Piernas" })).Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        var rutina = (long)(await (await jefe.PostAsJsonAsync("/api/rutinas", new
        {
            nombre = $"Rutina {Guid.NewGuid():N}"[..22], nivel = "Principiante",
            ejercicios = new[] { new { ejercicioId = ejercicio, dia = 1, series = 3, repeticiones = "10", descansoSegundos = 60, indicaciones = "" } }
        })).Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        var socio = (long)(await (await jefe.PostAsJsonAsync("/api/socios", new { nombre = "Socio", apellido = "Vencido", dni = Random.Shared.Next(20_000_000, 49_999_999).ToString(), telefono = "" })).Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        var asignada = await jefe.PostAsJsonAsync($"/api/socios/{socio}/rutinas", new { rutinaId = rutina, fechaInicio = "2030-12-01", fechaFin = "2030-12-31" });
        Assert.True(asignada.IsSuccessStatusCode, await asignada.Detalle());

        Assert.Equal(antes + 1, (int)(await Resumen(jefe, "2025-03", "2031-01-01"))["gimnasio"]!["rutinasVencidas"]!);   // venció el 31/12
        Assert.Equal(antes, (int)(await Resumen(jefe, "2025-03", "2030-12-15"))["gimnasio"]!["rutinasVencidas"]!);       // todavía vigente
    }

    [Fact]
    public async Task Un_socio_que_nunca_pago_cuenta_como_sin_plan_y_no_como_vencido()
    {
        var jefe = await api.Jefe();
        var antes = (await Resumen(jefe, "2025-03", "2025-03-20"))["gimnasio"]!;

        var creado = await jefe.PostAsJsonAsync("/api/socios", new { nombre = "Nuevo", apellido = "SinCuota", dni = Random.Shared.Next(20_000_000, 49_999_999).ToString(), telefono = "" });
        Assert.True(creado.IsSuccessStatusCode, await creado.Detalle());

        var despues = (await Resumen(jefe, "2025-03", "2025-03-20"))["gimnasio"]!;
        Assert.Equal((int)antes["sociosSinPlan"]! + 1, (int)despues["sociosSinPlan"]!);
        Assert.Equal((int)antes["sociosVencidos"]!, (int)despues["sociosVencidos"]!);
    }

    [Theory]
    [InlineData("2025-13")]
    [InlineData("octubre")]
    [InlineData("2025-3-1")]
    public async Task Un_mes_con_formato_invalido_se_rechaza(string mes)
    {
        var jefe = await api.Jefe();
        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.GetAsync($"/api/dashboard?mes={mes}")).StatusCode);
    }
}
