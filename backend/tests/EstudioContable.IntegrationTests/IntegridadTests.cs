using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// El control de integridad: con un sistema sano todo da correcto, y si alguien toca la base por fuera de las reglas,
/// el control correspondiente lo detecta.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class IntegridadTests(ApiFixture api)
{
    private static JsonNode Control(JsonNode resultado, string clave) => resultado["controles"]!.AsArray().Single(c => (string?)c!["clave"] == clave)!;

    private async Task Sql(string comando)
    {
        await using var conexion = new NpgsqlConnection(api.ConnectionString);
        await conexion.OpenAsync();
        await using var cmd = new NpgsqlCommand(comando, conexion);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Con_el_sistema_usado_normalmente_todos_los_controles_dan_correcto()
    {
        var jefe = await api.Jefe();

        // Actividad propia, para no depender del orden en que corren los demás tests: una venta que descuenta
        // stock y una compra en cuenta corriente con número interno.
        var venta = await jefe.PostAsJsonAsync("/api/facturas", new
        {
            fecha = "2026-10-21T12:00:00Z", tipoOperacion = "Venta", condicionPago = "ContadoCaja", tipoComprobante = "B", cliente = "Consumidor Final",
            subtotal = 150m, iva = 31.5m, total = 181.5m,
            items = new[] { new { productoId = 1L, descripcion = "Producto A", cantidad = 1, precioUnitario = 150m, subtotal = 150m } }
        });
        Assert.Equal(HttpStatusCode.Created, venta.StatusCode);
        var compra = await jefe.PostAsJsonAsync("/api/facturas", new
        {
            fecha = "2026-10-21T12:00:00Z", tipoOperacion = "Compra", condicionPago = "CuentaCorriente", tipoComprobante = "A", cliente = "Proveedor integridad",
            subtotal = 200m, iva = 42m, total = 242m,
            items = new[] { new { productoId = 0, descripcion = "Insumos", cantidad = 1, precioUnitario = 200m, subtotal = 200m } }
        });
        Assert.Equal(HttpStatusCode.Created, compra.StatusCode);

        var r = await jefe.GetJson("/api/integridad");

        var fallan = r["controles"]!.AsArray().Where(c => !(bool)c!["correcto"]!)
            .Select(c => $"{(string)c!["clave"]!}: {(string)c["etiquetaObtenido"]!}={(decimal)c["obtenido"]!} vs {(string)c["etiquetaEsperado"]!}={(decimal)c["esperado"]!}").ToList();
        Assert.True(fallan.Count == 0, "Controles con diferencias: " + string.Join(" | ", fallan));
        Assert.True((bool)r["todoCorrecto"]!);
        Assert.True((int)r["total"]! >= 16);
        Assert.Equal((int)r["total"]!, (int)r["correctos"]!);
    }

    [Fact]
    public async Task Un_movimiento_de_caja_sin_asiento_cargado_a_escondidas_rompe_el_control_de_caja_y_al_borrarlo_se_arregla()
    {
        var jefe = await api.Jefe();
        const decimal importe = 12345.67m;

        await Sql($"INSERT INTO caja.movimientos_caja (\"ClienteId\", \"Fecha\", \"Detalle\", \"TipoMovimiento\", \"Importe\") VALUES ('1', '2026-10-01', 'TRAMPA integridad', 1, {importe.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        try
        {
            var r = await jefe.GetJson("/api/integridad");
            var caja = Control(r, "caja");
            Assert.False((bool)caja["correcto"]!);
            Assert.Equal(importe, (decimal)caja["obtenido"]! - (decimal)caja["esperado"]!);   // la diferencia es justo la del movimiento
            Assert.False((bool)r["todoCorrecto"]!);
            // Lo demás no se contagia.
            Assert.True((bool)Control(r, "partida-doble")["correcto"]!);
            Assert.True((bool)Control(r, "banco")["correcto"]!);
        }
        finally
        {
            await Sql("DELETE FROM caja.movimientos_caja WHERE \"Detalle\" = 'TRAMPA integridad'");
        }

        Assert.True((bool)(await jefe.GetJson("/api/integridad"))["todoCorrecto"]!);
    }

    [Fact]
    public async Task Una_factura_cuyo_importe_se_cambia_a_mano_en_la_base_rompe_los_controles_de_ventas_e_iva()
    {
        var jefe = await api.Jefe();
        var venta = await jefe.PostAsJsonAsync("/api/facturas", new
        {
            fecha = "2026-10-20T12:00:00Z", tipoOperacion = "Venta", condicionPago = "ContadoCaja", tipoComprobante = "B", cliente = "Consumidor Final",
            subtotal = 1000m, iva = 210m, total = 1210m,
            items = new[] { new { productoId = 0, descripcion = "Servicio", cantidad = 1, precioUnitario = 1000m, subtotal = 1000m } }
        });
        var id = (long)(await venta.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        Assert.True((bool)(await jefe.GetJson("/api/integridad"))["todoCorrecto"]!);

        // Se sube el neto de la factura sin pasar por el sistema (el total se ajusta para no chocar con el CHECK de la base).
        await Sql($"UPDATE contabilidad.\"Facturas\" SET \"Subtotal\" = 1500, \"Total\" = 1710 WHERE \"Id\" = {id}");
        try
        {
            var r = await jefe.GetJson("/api/integridad");
            var ventas = Control(r, "ventas");
            Assert.False((bool)ventas["correcto"]!);
            Assert.Equal(500m, (decimal)ventas["obtenido"]! - (decimal)ventas["esperado"]!);
        }
        finally
        {
            await Sql($"UPDATE contabilidad.\"Facturas\" SET \"Subtotal\" = 1000, \"Total\" = 1210 WHERE \"Id\" = {id}");
        }

        Assert.True((bool)(await jefe.GetJson("/api/integridad"))["todoCorrecto"]!);
    }

    [Fact]
    public async Task Solo_administrador_y_contador_ven_el_control_de_integridad()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.Recepcion()).GetAsync("/api/integridad")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await api.Contador()).GetAsync("/api/integridad")).StatusCode);
    }
}
