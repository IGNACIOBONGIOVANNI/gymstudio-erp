using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// El asistente por reglas debe contestar con los datos reales del sistema, respetar los permisos de cada rol
/// y no romperse con preguntas raras. Los datos de cada test están en meses de 2021 que ningún otro test usa.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class AsistenteTests(ApiFixture api)
{
    private const string Hoy = "2021-04-15";
    private static readonly int[] Multiplicadores = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

    private static string Pesos(decimal v)
        => (v < 0 ? "-" : "") + "$ " + Math.Abs(v).ToString("N2", CultureInfo.InvariantCulture).Replace(",", "#").Replace(".", ",").Replace("#", ".");

    private static string CuitNuevo()
    {
        while (true)
        {
            var d = "30" + Random.Shared.Next(10_000_000, 99_999_999);
            var v = 11 - Multiplicadores.Select((m, i) => m * (d[i] - '0')).Sum() % 11;
            if (v == 11) v = 0;
            if (v != 10) return d + v;
        }
    }

    private static async Task<JsonNode> Preguntar(HttpClient c, string texto, string hoy = Hoy)
    {
        var r = await c.PostAsJsonAsync("/api/asistente/preguntar", new { texto, hoy });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<JsonNode>())!;
    }

    private static string Texto(JsonNode r) => (string)r["texto"]!;
    private static string Fila(JsonNode r, string etiqueta) => (string)r["filas"]!.AsArray().Single(f => (string?)f!["etiqueta"] == etiqueta)!["valor"]!;

    private static async Task Factura(HttpClient c, string operacion, string fecha, decimal neto, string condicion = "ContadoCaja", object? tercero = null)
    {
        var cuerpo = new JsonObject
        {
            ["fecha"] = fecha, ["tipoOperacion"] = operacion, ["condicionPago"] = condicion,
            ["tipoComprobante"] = operacion == "Venta" ? "B" : "A", ["cliente"] = operacion == "Venta" ? "Consumidor Final" : "Proveedor asistente",
            ["subtotal"] = neto, ["iva"] = neto * 0.21m, ["total"] = neto * 1.21m,
            ["items"] = new JsonArray(new JsonObject { ["productoId"] = 0, ["descripcion"] = "Servicio", ["cantidad"] = 1, ["precioUnitario"] = neto, ["subtotal"] = neto }),
        };
        if (operacion == "Venta" && tercero is long clienteId) { cuerpo["tipoComprobante"] = "A"; cuerpo["clienteId"] = clienteId; }
        if (operacion == "Compra" && tercero is long proveedorId) cuerpo["proveedorId"] = proveedorId;
        var r = await c.PostAsJsonAsync("/api/facturas", cuerpo);
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
    }

    [Fact]
    public async Task Contesta_ventas_compras_resultado_e_iva_de_un_mes_con_los_importes_reales()
    {
        var jefe = await api.Jefe();
        await Factura(jefe, "Venta", "2021-03-10T12:00:00Z", 1000);      // IVA 210, total 1.210
        await Factura(jefe, "Compra", "2021-03-12T12:00:00Z", 400);      // IVA 84, total 484

        var ventas = await Preguntar(jefe, "¿Cuánto vendí en marzo de 2021?");
        Assert.Contains("marzo de 2021", Texto(ventas));
        Assert.Contains($"{Pesos(1000)} netos ({Pesos(1210)} con IVA) en 1 comprobante", Texto(ventas));
        Assert.Equal(Pesos(210), Fila(ventas, "IVA"));

        Assert.Contains($"compraste {Pesos(400)} netos", Texto(await Preguntar(jefe, "cuanto compre en marzo 2021")));
        Assert.Contains($"una ganancia de {Pesos(600)}", Texto(await Preguntar(jefe, "¿cuál fue el resultado de marzo de 2021?")));
        Assert.Contains($"pagar {Pesos(126)} de IVA", Texto(await Preguntar(jefe, "IVA a pagar de marzo de 2021")));
    }

    [Fact]
    public async Task Entiende_el_mes_pasado_el_anio_las_tildes_y_las_mayusculas()
    {
        var jefe = await api.Jefe();
        await Factura(jefe, "Venta", "2021-08-20T12:00:00Z", 2000);

        // Hoy es 15/09/2021: "el mes pasado" es agosto de 2021.
        var pasado = await Preguntar(jefe, "ventas del mes pasado", "2021-09-15");
        Assert.Contains("agosto de 2021", Texto(pasado));
        Assert.Contains(Pesos(2000), Texto(pasado));
        Assert.Equal(Texto(await Preguntar(jefe, "¿Cuánto vendí en marzo de 2021?")), Texto(await Preguntar(jefe, "CUÁNTO VENDÍ EN MARZO DE 2021")));
        Assert.Contains("este año (2021)", Texto(await Preguntar(jefe, "¿cuánto vendí este año?")));
        Assert.Contains("no hay ventas cargadas", Texto(await Preguntar(jefe, "ventas de enero de 2021")));
    }

    [Fact]
    public async Task Cuenta_las_facturas_anuladas_del_periodo()
    {
        var jefe = await api.Jefe();
        var r = await jefe.PostAsJsonAsync("/api/facturas", new
        {
            fecha = "2021-06-10T12:00:00Z", tipoOperacion = "Venta", condicionPago = "ContadoCaja", tipoComprobante = "B", cliente = "Consumidor Final",
            subtotal = 100m, iva = 21m, total = 121m,
            items = new[] { new { productoId = 0, descripcion = "Servicio", cantidad = 1, precioUnitario = 100m, subtotal = 100m } }
        });
        var id = (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        Assert.Equal(HttpStatusCode.NoContent, (await jefe.PostAsync($"/api/facturas/{id}/anular", null)).StatusCode);

        Assert.Contains("1 factura anulada", Texto(await Preguntar(jefe, "¿Cuántas facturas anuladas hay en junio de 2021?")));
        Assert.Contains("no hay ventas cargadas", Texto(await Preguntar(jefe, "ventas de junio de 2021")));     // la anulada no suma
    }

    [Fact]
    public async Task Lista_quien_me_debe_y_a_quien_le_debo_con_los_nombres_y_saldos()
    {
        var jefe = await api.Jefe();
        var nombreCliente = $"Cliente asistente {Guid.NewGuid():N}"[..28];
        var nombreProveedor = $"Proveedor asistente {Guid.NewGuid():N}"[..30];
        var cliente = await (await jefe.PostAsJsonAsync("/api/clientes", new { razonSocial = nombreCliente, direccion = "Calle 1", localidad = "Haedo", cuit = CuitNuevo(), condicionIva = "Responsable Inscripto" })).Content.ReadFromJsonAsync<JsonObject>();
        var proveedor = await (await jefe.PostAsJsonAsync("/api/proveedores", new { razonSocial = nombreProveedor, direccion = "Calle 2", localidad = "Haedo", cuit = CuitNuevo(), condicionIva = "Responsable Inscripto" })).Content.ReadFromJsonAsync<JsonObject>();
        // Montos enormes para que sean los primeros de la lista.
        await Factura(jefe, "Venta", "2021-02-01T12:00:00Z", 90_000_000m, "CuentaCorriente", (long)cliente!["id"]!);
        await Factura(jefe, "Compra", "2021-02-01T12:00:00Z", 80_000_000m, "CuentaCorriente", (long)proveedor!["id"]!);

        var cobrar = await Preguntar(jefe, "¿Quién me debe?");
        Assert.Contains("Los clientes te deben", Texto(cobrar));
        Assert.Equal(Pesos(108_900_000m), Fila(cobrar, nombreCliente));

        var pagar = await Preguntar(jefe, "¿A quién le debo plata?");
        Assert.Contains("Les debés a los proveedores", Texto(pagar));
        Assert.Equal(Pesos(96_800_000m), Fila(pagar, nombreProveedor));
    }

    [Fact]
    public async Task Contesta_el_saldo_de_caja_y_banco_igual_al_de_sus_libros_y_el_estado_de_la_contabilidad()
    {
        var jefe = await api.Jefe();
        var caja = await jefe.SaldoLibro("caja");
        var banco = await jefe.SaldoLibro("banco");

        var r = await Preguntar(jefe, "¿cuánto tengo en caja y banco?");
        Assert.Equal(Pesos(caja), Fila(r, "Saldo de Caja"));
        Assert.Equal(Pesos(banco), Fila(r, "Saldo de Banco"));

        Assert.Contains("todo en orden", Texto(await Preguntar(jefe, "¿Está todo bien con la contabilidad?")));
    }

    [Fact]
    public async Task Contesta_el_stock_de_un_producto_en_particular_y_los_productos_sin_stock()
    {
        var jefe = await api.Jefe();
        var nombre = $"Mancuerna{Guid.NewGuid():N}"[..16].Replace('-', 'x');
        var vacio = $"Cinturon{Guid.NewGuid():N}"[..16].Replace('-', 'x');
        Assert.True((await jefe.PostAsJsonAsync("/api/productos", new { nombre, precioUnitario = 10, precioVenta = 20, stockInicial = 7 })).IsSuccessStatusCode);
        Assert.True((await jefe.PostAsJsonAsync("/api/productos", new { nombre = vacio, precioUnitario = 10, precioVenta = 20, stockInicial = 0 })).IsSuccessStatusCode);

        Assert.Contains($"De {nombre} hay 7 unidades", Texto(await Preguntar(jefe, $"¿Cuánto stock hay de {nombre}?")));
        Assert.Contains("0 unidades", Texto(await Preguntar(jefe, $"stock del {vacio}")));
        Assert.Contains("No encontré productos", Texto(await Preguntar(jefe, "stock de zapatillas voladoras")));
        Assert.Contains("sin stock", Texto(await Preguntar(jefe, "¿Qué productos están sin stock?")));
    }

    [Fact]
    public async Task Las_cantidades_de_socios_coinciden_con_las_del_resumen_de_inicio()
    {
        var jefe = await api.Jefe();
        var hoy = "2026-10-28";
        var resumen = (await jefe.GetJson($"/api/dashboard?mes=2026-10&hoy={hoy}"))["gimnasio"]!;

        var cantidad = await Preguntar(jefe, "¿Cuántos socios hay?", hoy);
        Assert.Equal((int)resumen["sociosConCuotaAlDia"]!, int.Parse(Fila(cantidad, "Con cuota al día")));
        Assert.Equal((int)resumen["sociosVencidos"]!, int.Parse(Fila(cantidad, "Cuota vencida")));
        Assert.Equal((int)resumen["sociosSinPlan"]!, int.Parse(Fila(cantidad, "Sin plan")));

        var porVencer = (int)resumen["cuotasPorVencer"]!;
        var textoPorVencer = Texto(await Preguntar(jefe, "¿Cuántas cuotas vencen esta semana?", hoy));
        Assert.True(porVencer == 0 ? textoPorVencer.StartsWith("No hay cuotas") : textoPorVencer.StartsWith($"{porVencer} cuota"), textoPorVencer);
    }

    [Fact]
    public async Task Recepcion_recibe_ayuda_con_socios_y_stock_pero_nunca_importes()
    {
        var recepcion = await api.Recepcion();

        foreach (var pregunta in new[] { "¿Cuánto vendí este mes?", "¿Quién me debe?", "¿cuánto hay en caja?", "resultado del mes", "IVA", "¿Está todo bien con la contabilidad?", "producto más vendido" })
        {
            var r = await Preguntar(recepcion, pregunta);
            Assert.Contains("solo Administración o Contaduría", Texto(r));
            Assert.Empty(r["filas"]!.AsArray());
            Assert.DoesNotContain("$", Texto(r));
        }

        Assert.Contains("socio", Texto(await Preguntar(recepcion, "¿Cuántos socios hay?")));
        Assert.Contains("stock", Texto(await Preguntar(recepcion, "productos sin stock")));

        // El resumen para Recepción no trae importes.
        var resumen = await Preguntar(recepcion, "dame un resumen");
        Assert.DoesNotContain(resumen["filas"]!.AsArray(), f => ((string)f!["valor"]!).Contains("$"));
        Assert.DoesNotContain(resumen["filas"]!.AsArray(), f => ((string)f!["etiqueta"]!).Contains("Caja"));
    }

    [Fact]
    public async Task Saluda_da_ayuda_y_avisa_cuando_no_entiende_con_sugerencias()
    {
        var jefe = await api.Jefe();
        Assert.StartsWith("¡Hola!", Texto(await Preguntar(jefe, "Hola")));
        Assert.Contains("Puedo contestar", Texto(await Preguntar(jefe, "ayuda")));

        var confuso = await Preguntar(jefe, "asdf qwer zxcv");
        Assert.Contains("No entendí", Texto(confuso));
        Assert.NotEmpty(confuso["sugerencias"]!.AsArray());
        Assert.DoesNotContain(confuso["sugerencias"]!.AsArray(), s => (string)s! == "asdf qwer zxcv");
    }

    [Fact]
    public async Task Un_resumen_para_gestion_incluye_lo_financiero_y_el_estado_de_la_contabilidad()
    {
        var jefe = await api.Jefe();
        var r = await Preguntar(jefe, "¿Cómo vamos?");
        Assert.Contains(r["filas"]!.AsArray(), f => (string?)f!["etiqueta"] == "Saldo de Caja");
        Assert.Contains(r["filas"]!.AsArray(), f => (string?)f!["etiqueta"] == "A cobrar a clientes");
        Assert.StartsWith("en orden", Fila(r, "Contabilidad"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Una_pregunta_vacia_se_rechaza(string texto)
    {
        var jefe = await api.Jefe();
        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsJsonAsync("/api/asistente/preguntar", new { texto })).StatusCode);
    }

    [Fact]
    public async Task Una_pregunta_demasiado_larga_o_una_fecha_absurda_se_rechazan()
    {
        var jefe = await api.Jefe();
        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsJsonAsync("/api/asistente/preguntar", new { texto = new string('a', 301) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsJsonAsync("/api/asistente/preguntar", new { texto = "ventas", hoy = "9999-12-31" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await jefe.PostAsJsonAsync("/api/asistente/preguntar", new { texto = new string('a', 300) })).StatusCode);
    }

    [Theory]
    [InlineData("'; DROP TABLE \"Socios\"; --")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("%00 \u0000 ../../etc/passwd")]
    [InlineData("stock de '); DELETE FROM contabilidad.\"Facturas\"; --")]
    [InlineData("😀😀😀 ¿¿¿???")]
    public async Task Las_preguntas_hostiles_no_rompen_nada_y_no_se_repiten_como_html(string texto)
    {
        var jefe = await api.Jefe();
        var r = await jefe.PostAsJsonAsync("/api/asistente/preguntar", new { texto, hoy = Hoy });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var cuerpo = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<script", cuerpo, StringComparison.OrdinalIgnoreCase);

        // Y la base sigue intacta: las tablas existen y todos los controles de integridad dan correcto.
        Assert.True((bool)(await jefe.GetJson("/api/integridad"))["todoCorrecto"]!);
        Assert.Equal(HttpStatusCode.OK, (await jefe.GetAsync("/api/socios")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await jefe.GetAsync("/api/facturas")).StatusCode);
    }

    [Fact]
    public async Task Sin_sesion_no_se_puede_preguntar()
    {
        var anonimo = api.ClienteAnonimo();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/asistente/preguntar", new { texto = "hola" })).StatusCode);
    }
}
