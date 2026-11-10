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

    // --- Ingresos al gimnasio, ficha de un socio, planes, socios nuevos y comparaciones ---------------------

    private static DateOnly Real => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));      // el día de hoy según el servidor
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Rotulo(string apellido) => $"{apellido}, Socio";

    private async Task<(long Id, string Dni, string Apellido)> NuevoSocio(HttpClient c, string prefijo = "Fichero")
    {
        var dni = Random.Shared.Next(20_000_000, 49_999_999).ToString();
        var apellido = prefijo + Nombres.Aleatorio(8);          // una sola palabra y única
        var r = await c.PostAsJsonAsync("/api/socios", new { nombre = "Socio", apellido, dni, telefono = "" });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return ((long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!, dni, apellido);
    }

    private static async Task PagarPlan(HttpClient c, long socioId)
    {
        var plan = await c.PostAsJsonAsync("/api/planes", new { nombre = $"Plan asistente {Guid.NewGuid():N}"[..30], precioMensual = 1000, duracionMeses = 1 });
        plan.EnsureSuccessStatusCode();
        var planId = (long)(await plan.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        var cobro = await c.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", new { socioId, planId, metodoPago = "caja" });
        Assert.True(cobro.IsSuccessStatusCode, await cobro.Detalle());
    }

    private async Task VisitaEn(long socioId, DateOnly fecha)
    {
        await using var conexion = new Npgsql.NpgsqlConnection(api.ConnectionString);
        await conexion.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand("""INSERT INTO contabilidad."AsistenciasSocio" ("SocioId","Fecha","RegistradaEn","RegistradaPor") VALUES (@s,@f,now(),'prueba')""", conexion);
        cmd.Parameters.AddWithValue("s", socioId);
        cmd.Parameters.AddWithValue("f", fecha);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Contesta_quien_vino_hoy_y_ayer_y_cuantas_visitas_hubo_a_cualquier_perfil()
    {
        var jefe = await api.Jefe();
        var recepcion = await api.Recepcion();
        var (id, dni, apellido) = await NuevoSocio(jefe);
        Assert.True((await jefe.PostAsJsonAsync("/api/asistencias/ingreso", new { dni })).IsSuccessStatusCode);
        await VisitaEn(id, Real.AddDays(-1));

        foreach (var pregunta in new[] { "¿Quién vino hoy?", "quien vino", "¿Qué socios ingresaron hoy?", "asistencia de hoy" })
        {
            var r = await Preguntar(recepcion, pregunta, Iso(Real));
            Assert.StartsWith("Hoy hubo ", Texto(r));
            Assert.Matches(@"^\d{2}:\d{2}$", Fila(r, Rotulo(apellido)));                  // la hora a la que entró
        }

        var ayer = await Preguntar(jefe, "¿quién vino ayer?", Iso(Real));
        Assert.StartsWith("Ayer hubo ", Texto(ayer));
        Assert.Contains(ayer["filas"]!.AsArray(), f => (string?)f!["etiqueta"] == Rotulo(apellido));

        var mes = await Preguntar(jefe, "¿cuántas visitas hubo este mes?", Iso(Real));
        Assert.Contains("ingresos de", Texto(mes));
        Assert.Contains("Los que más vinieron", Texto(mes));
    }

    [Fact]
    public async Task Lista_los_socios_al_dia_que_no_vienen_hace_tiempo_y_entiende_cuantos_dias()
    {
        var jefe = await api.Jefe();
        var (id, _, apellido) = await NuevoSocio(jefe, "Aaa");            // "Aaa" lo deja primero entre los empatados
        await PagarPlan(jefe, id);

        var lejos = Iso(Real.AddDays(20));                                // veinte días después: al día, pero sin haber venido nunca
        foreach (var pregunta in new[] { "¿Quiénes no vienen hace tiempo?", "socios que dejaron de venir", "¿quién está en riesgo de abandono?" })
        {
            var r = await Preguntar(jefe, pregunta, lejos);
            Assert.Contains("hace 14 días o más", Texto(r));
            Assert.Contains("todavía no vino", Fila(r, Rotulo(apellido)));
        }

        Assert.Contains("hace 14 días o más", Texto(await Preguntar(await api.Recepcion(), "no vienen hace 2 semanas", lejos)));
        var masAlto = await Preguntar(jefe, "quiénes no vienen hace 30 días", lejos);
        Assert.Contains("30 días", Texto(masAlto));
        Assert.DoesNotContain(masAlto["filas"]!.AsArray(), f => (string?)f!["etiqueta"] == Rotulo(apellido));    // con 20 días todavía no llega a 30
    }

    [Fact]
    public async Task Contesta_con_la_ficha_de_un_socio_por_apellido_nombre_o_dni()
    {
        var jefe = await api.Jefe();
        var (id, dni, apellido) = await NuevoSocio(jefe);
        var (sinPlan, _, apellidoSinPlan) = await NuevoSocio(jefe);
        await PagarPlan(jefe, id);
        await jefe.PostAsJsonAsync("/api/asistencias/ingreso", new { dni });

        foreach (var pregunta in new[] { $"¿Cuándo vence la cuota de {apellido}?", $"ficha del dni {dni}", $"¿cuándo vino Socio {apellido}?", $"como esta {apellido}" })
        {
            var r = await Preguntar(await api.Recepcion(), pregunta, Iso(Real));         // los datos del gimnasio los ve cualquier perfil
            Assert.StartsWith($"{apellido}, Socio tiene la cuota al día, vence el ", Texto(r));
            Assert.Equal(dni, Fila(r, "DNI"));
            Assert.StartsWith("Plan asistente", Fila(r, "Plan"));
            Assert.Equal("sin rutina asignada", Fila(r, "Rutina"));
            Assert.Equal("hoy", Fila(r, "Última visita"));
            Assert.Equal("1", Fila(r, "Visitas este mes"));
        }

        var sin = await Preguntar(jefe, $"cuota de {apellidoSinPlan}", Iso(Real));
        Assert.Contains("todavía no pagó ninguna cuota", Texto(sin));
        Assert.Equal("todavía no vino", Fila(sin, "Última visita"));

        Assert.Contains("No encontré ningún socio con el DNI 10000002", Texto(await Preguntar(jefe, "ficha del dni 10000002", Iso(Real))));
    }

    [Fact]
    public async Task Una_pregunta_de_plata_con_el_nombre_de_un_socio_no_se_confunde_con_su_ficha()
    {
        var jefe = await api.Jefe();
        var (_, _, apellido) = await NuevoSocio(jefe);

        var r = await Preguntar(jefe, $"¿cuánto vendí a {apellido}?", Iso(Real));

        Assert.DoesNotContain("cuota", Texto(r));
        Assert.DoesNotContain(apellido, Texto(r), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dos_socios_con_el_mismo_apellido_se_desambiguan_por_el_nombre_o_piden_el_dni()
    {
        var jefe = await api.Jefe();
        var apellido = "Repetido" + Nombres.Aleatorio(8);
        var dnis = new List<string>();
        foreach (var nombre in new[] { "Alicia", "Bruno" })
        {
            var dni = Random.Shared.Next(20_000_000, 49_999_999).ToString();
            dnis.Add(dni);
            Assert.True((await jefe.PostAsJsonAsync("/api/socios", new { nombre, apellido, dni, telefono = "" })).IsSuccessStatusCode);
        }

        var ambigua = await Preguntar(jefe, $"cuota de {apellido}", Iso(Real));
        Assert.Contains("Encontré 2 socios", Texto(ambigua));
        Assert.Equal($"DNI {dnis[0]}", Fila(ambigua, $"{apellido}, Alicia"));

        var precisa = await Preguntar(jefe, $"cuota de Bruno {apellido}", Iso(Real));
        Assert.StartsWith($"{apellido}, Bruno", Texto(precisa));
    }

    [Fact]
    public async Task Contesta_los_planes_con_sus_precios_a_cualquier_perfil()
    {
        var jefe = await api.Jefe();
        var nombre = $"Plan barato {Guid.NewGuid():N}"[..24];
        Assert.True((await jefe.PostAsJsonAsync("/api/planes", new { nombre, precioMensual = 1, duracionMeses = 3 })).IsSuccessStatusCode);

        foreach (var pregunta in new[] { "¿Qué planes hay?", "¿cuánto cuesta el plan?", "precios de los planes" })
        {
            var r = await Preguntar(await api.Recepcion(), pregunta, Iso(Real));
            Assert.Contains("planes activos", Texto(r));
            Assert.Equal($"{Pesos(1)} por mes, dura 3 meses", Fila(r, nombre));
        }
    }

    [Fact]
    public async Task Cuenta_los_socios_nuevos_del_periodo()
    {
        var jefe = await api.Jefe();
        var (_, _, apellido) = await NuevoSocio(jefe);

        var r = await Preguntar(jefe, "¿cuántos socios nuevos hay este mes?", Iso(Real));

        Assert.Matches("se anotaron|se anotó", Texto(r));
        Assert.Matches(@"^\d{2}/\d{2}/\d{4}$", Fila(r, Rotulo(apellido)));
    }

    [Fact]
    public async Task Compara_ventas_compras_y_resultado_con_el_periodo_anterior()
    {
        var jefe = await api.Jefe();
        await Factura(jefe, "Venta", "2021-11-10T12:00:00Z", 1000);
        await Factura(jefe, "Venta", "2021-12-10T12:00:00Z", 1500);
        await Factura(jefe, "Compra", "2021-11-12T12:00:00Z", 800);
        await Factura(jefe, "Compra", "2021-12-12T12:00:00Z", 200);
        const string hoy = "2021-12-20";

        var ventas = await Preguntar(jefe, "¿Vendí más que el mes pasado?", hoy);
        Assert.Contains($"de este mes (diciembre de 2021): {Pesos(1500)}; de noviembre de 2021: {Pesos(1000)}", Texto(ventas));
        Assert.Contains($"{Pesos(500)} más (+50 %)", Texto(ventas));
        Assert.Equal(Pesos(500), Fila(ventas, "Diferencia"));

        var compras = await Preguntar(jefe, "¿gasté menos que el mes pasado?", hoy);
        Assert.Contains($"{Pesos(600)} menos (-75 %)", Texto(compras));

        var resultado = await Preguntar(jefe, "comparame el resultado de este mes con el anterior", hoy);
        Assert.Contains($"de este mes (diciembre de 2021): {Pesos(1300)}; de noviembre de 2021: {Pesos(200)}", Texto(resultado));

        // Una pregunta normal sobre el mes pasado no se vuelve una comparación.
        Assert.StartsWith("En noviembre de 2021 vendiste", Texto(await Preguntar(jefe, "¿cuánto vendí el mes pasado?", hoy)));
    }

    [Fact]
    public async Task El_asistente_no_confunde_su_propio_nombre_con_una_pregunta_de_asistencia()
    {
        var r = await Preguntar(await api.Jefe(), "¿para qué sirve el asistente?", Iso(Real));
        Assert.DoesNotContain("ingreso", Texto(r));
        Assert.Contains("No entendí", Texto(r));
    }

    [Fact]
    public async Task Las_comparaciones_son_financieras_y_Recepcion_no_las_recibe()
    {
        var r = await Preguntar(await api.Recepcion(), "¿Vendí más que el mes pasado?", Iso(Real));
        Assert.Contains("solo Administración o Contaduría", Texto(r));
        Assert.Empty(r["filas"]!.AsArray());
    }

    [Fact]
    public async Task Contesta_cuanto_debe_un_cliente_y_cuanto_se_le_debe_a_un_proveedor_en_particular()
    {
        var jefe = await api.Jefe();
        var nombreCliente = $"Cliente asistente {Guid.NewGuid():N}"[..28];
        var nombreProveedor = $"Proveedor asistente {Guid.NewGuid():N}"[..30];
        var cliente = await (await jefe.PostAsJsonAsync("/api/clientes", new { razonSocial = nombreCliente, direccion = "Calle 1", localidad = "Haedo", cuit = CuitNuevo(), condicionIva = "Responsable Inscripto" })).Content.ReadFromJsonAsync<JsonNode>();
        var proveedor = await (await jefe.PostAsJsonAsync("/api/proveedores", new { razonSocial = nombreProveedor, direccion = "Calle 2", localidad = "Haedo", cuit = CuitNuevo(), condicionIva = "Responsable Inscripto" })).Content.ReadFromJsonAsync<JsonNode>();
        await Factura(jefe, "Venta", "2021-10-01T12:00:00Z", 7_000_000m, "CuentaCorriente", (long)cliente!["id"]!);
        await Factura(jefe, "Compra", "2021-10-01T12:00:00Z", 3_000_000m, "CuentaCorriente", (long)proveedor!["id"]!);

        var debe = await Preguntar(jefe, $"¿Cuánto me debe {nombreCliente}?", "2021-10-20");
        Assert.StartsWith($"{nombreCliente} te debe {Pesos(8_470_000m)}", Texto(debe));
        Assert.Equal(Pesos(8_470_000m), Fila(debe, "Saldo"));

        var debo = await Preguntar(jefe, $"¿cuánto le debo a {nombreProveedor}?", "2021-10-20");
        Assert.StartsWith($"Le debés {Pesos(3_630_000m)} a {nombreProveedor}", Texto(debo));

        // Una pregunta general sigue dando la lista de todos.
        Assert.Contains("Los clientes te deben", Texto(await Preguntar(jefe, "¿cuánto me deben los clientes?", "2021-10-20")));
    }

    [Fact]
    public async Task Ayer_y_esta_semana_se_entienden_en_las_ventas()
    {
        var jefe = await api.Jefe();
        // El 20/12/2021 fue lunes: ayer (domingo 19) y esta semana (20 al 26) no tienen ventas cargadas; el mes pasado sí.
        Assert.StartsWith("Ayer no hay ventas", Texto(await Preguntar(jefe, "¿cuánto vendí ayer?", "2021-12-20")));
        Assert.StartsWith("Esta semana no hay ventas", Texto(await Preguntar(jefe, "¿cuánto vendí esta semana?", "2021-12-20")));
        Assert.StartsWith("La semana pasada no hay ventas", Texto(await Preguntar(jefe, "¿cuánto vendí la semana pasada?", "2021-12-20")));
    }
}
