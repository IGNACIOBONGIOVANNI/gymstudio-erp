using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ClosedXML.Excel;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Datos hostiles y archivos que sube una persona. Nada de esto puede dar un error interno ni gastar los
/// recursos del servidor, y lo que no tiene forma de dato válido no entra a los libros.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class ValidacionesYArchivosTests(ApiFixture api)
{
    private static MultipartFormDataContent Archivo(byte[] bytes, string nombre = "libro.xlsx")
    {
        var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(bytes);
        parte.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        contenido.Add(parte, "file", nombre);
        return contenido;
    }

    /// <summary>Un .xlsx real con la cabecera y las filas indicadas (fecha, tipo, número, nombre, CUIT, neto, IVA, total, pago).</summary>
    private static byte[] Libro(params object[][] filas)
    {
        using var wb = new XLWorkbook();
        var hoja = wb.AddWorksheet("Ventas");
        var cabecera = new[] { "Fecha", "Tipo", "Número", "Nombre", "CUIT", "Neto", "IVA", "Total", "Pago" };
        for (var c = 0; c < cabecera.Length; c++) hoja.Cell(1, c + 1).Value = cabecera[c];
        for (var f = 0; f < filas.Length; f++)
            for (var c = 0; c < filas[f].Length; c++)
                hoja.Cell(f + 2, c + 1).Value = XLCellValue.FromObject(filas[f][c]);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string NumeroAlAzar() => $"0001-{Random.Shared.Next(10_000_000, 99_999_999)}";

    /// <summary>CUIT con dígito verificador correcto y distinto en cada llamada (el cliente no puede repetirse).</summary>
    private static string CuitValidoAlAzar()
    {
        int[] multiplicadores = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };
        while (true)
        {
            var baseDigitos = "20" + Random.Shared.Next(10_000_000, 99_999_999);
            var suma = 0;
            for (var i = 0; i < 10; i++) suma += multiplicadores[i] * (baseDigitos[i] - '0');
            var verificador = 11 - suma % 11;
            if (verificador == 11) verificador = 0;
            if (verificador == 10) continue;
            return baseDigitos + verificador;
        }
    }

    private static async Task<long> CrearCliente(HttpClient jefe)
    {
        var alta = await jefe.PostAsJsonAsync("/api/clientes", new { razonSocial = "Cliente de importación", direccion = "Calle 1", localidad = "Haedo", cuit = CuitValidoAlAzar(), condicionIva = "Responsable Inscripto" });
        Assert.True(alta.IsSuccessStatusCode, await alta.Detalle());
        return (long)(await alta.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    // ---------- subida de archivos ----------

    [Fact]
    public async Task Un_archivo_que_no_es_un_excel_se_rechaza_con_un_mensaje_claro()
    {
        var jefe = await api.Jefe();
        foreach (var bytes in new[] { "esto es texto, no un excel"u8.ToArray(), new byte[] { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3 }, new byte[100] })
        {
            var r = await jefe.PostAsync("/api/ventas/import?clienteId=1", Archivo(bytes));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Contains("Excel", await r.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Solo_se_aceptan_archivos_xlsx()
    {
        var jefe = await api.Jefe();
        var r = await jefe.PostAsync("/api/ventas/import?clienteId=1", Archivo(Libro(), "libro.txt"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains(".xlsx", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_archivo_demasiado_grande_se_rechaza_sin_error_interno()
    {
        var jefe = await api.Jefe();
        var r = await jefe.PostAsync("/api/ventas/import?clienteId=1", Archivo(new byte[7 * 1024 * 1024]));
        Assert.True(r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge, $"Respondió {r.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, (await jefe.GetAsync("/api/planes")).StatusCode);   // el servidor sigue de pie
    }

    [Fact]
    public async Task Una_bomba_zip_se_corta_antes_de_agotar_la_memoria()
    {
        // Un zip de unos pocos KB que al descomprimirse ocupa 80 MB (y el tamaño declarado miente).
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entrada = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.SmallestSize);
            await using var s = entrada.Open();
            var ceros = new byte[1024 * 1024];
            for (var i = 0; i < 80; i++) await s.WriteAsync(ceros);
        }
        Assert.True(ms.Length < 1024 * 1024, "el zip de prueba tendría que ser chico");

        var jefe = await api.Jefe();
        var r = await jefe.PostAsync("/api/ventas/import?clienteId=1", Archivo(ms.ToArray()));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("demasiado grande", await r.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await jefe.GetAsync("/api/planes")).StatusCode);
    }

    [Fact]
    public async Task Recepcion_no_puede_subir_archivos()
    {
        var recepcion = await api.Recepcion();
        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.PostAsync("/api/ventas/import?clienteId=1", Archivo(Libro()))).StatusCode);
    }

    [Fact]
    public async Task Al_importar_las_filas_con_datos_invalidos_se_omiten_y_se_cuentan()
    {
        var jefe = await api.Jefe();
        var clienteId = await CrearCliente(jefe);
        var buena1 = new object[] { "2026-10-20", "A", NumeroAlAzar(), "Cliente Uno SA", "20111111112", 1000, 210, 1210, "CuentaCorriente" };   // el circuito viejo no resuelve el pago al contado
        var buena2 = new object[] { "2026-10-21", "b", NumeroAlAzar(), "Cliente Dos SRL", "20-11111111-2", 500, 0, 500, "CuentaCorriente" };
        var tipoMalo = new object[] { "2026-10-22", "NaN", NumeroAlAzar(), "Cliente", "20111111112", 100, 21, 121, "ContadoCaja" };
        var cuitMalo = new object[] { "2026-10-22", "A", NumeroAlAzar(), "Cliente", "' OR '1'='1", 100, 21, 121, "ContadoCaja" };
        var formula = new object[] { "2026-10-22", "A", "=1+1", "Cliente", "20111111112", 100, 21, 121, "ContadoCaja" };
        var importeNegativo = new object[] { "2026-10-22", "A", NumeroAlAzar(), "Cliente", "20111111112", -100, 0, -100, "ContadoCaja" };
        var totalIncoherente = new object[] { "2026-10-22", "A", NumeroAlAzar(), "Cliente", "20111111112", 100, 21, 5000, "ContadoCaja" };
        var fechaMala = new object[] { "no es una fecha", "A", NumeroAlAzar(), "Cliente", "20111111112", 100, 21, 121, "ContadoCaja" };

        var r = await jefe.PostAsync($"/api/ventas/import?clienteId={clienteId}",
            Archivo(Libro(buena1, tipoMalo, buena2, cuitMalo, formula, importeNegativo, totalIncoherente, fechaMala)));

        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        var cuerpo = (await r.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(2, (int)cuerpo["facturasImportadas"]!);
        Assert.Equal(6, (int)cuerpo["filasOmitidas"]!);
    }

    // ---------- endpoints con datos hostiles ----------

    private static object FacturaManual(string tipo = "A", string numero = "0001-00000001", string nombre = "Cliente", string cuit = "20111111112",
        object? condicionPago = null, long clienteId = 1, object[]? items = null) => new
        {
            clienteId, fecha = "2026-10-20", tipoFactura = tipo, numeroFactura = numero, clienteNombre = nombre, cuit,
            condicionPago = condicionPago ?? 2, items = items ?? new object[] { new { productoId = 1, cantidad = 1 } }
        };

    [Theory]
    [InlineData("NaN", "0001-00000001", "Cliente", "20111111112", 2, 1)]
    [InlineData("A", "' OR '1'='1", "Cliente", "20111111112", 2, 1)]
    [InlineData("A", "0001-00000001", "Cliente", "' OR '1'='1", 2, 1)]
    [InlineData("A", "0001-00000001", "Cliente", "20111111112", -1, 1)]
    [InlineData("A", "0001-00000001", "Cliente", "20111111112", 2147483647, 1)]
    [InlineData("A", "0001-00000001", "Cliente", "20111111112", 2, -1)]
    [InlineData("A", "0001-00000001", "Cliente", "20111111112", 2, 0)]
    public async Task Una_venta_manual_con_datos_sin_forma_de_factura_responde_400_y_nunca_500(string tipo, string numero, string nombre, string cuit, int condicion, long clienteId)
    {
        var jefe = await api.Jefe();
        var r = await jefe.PostAsJsonAsync("/api/ventas", FacturaManual(tipo, numero, nombre, cuit, condicion, clienteId));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Una_venta_manual_sin_items_o_con_cantidades_absurdas_responde_400()
    {
        var jefe = await api.Jefe();
        foreach (var items in new object[][]
                 {
                     Array.Empty<object>(),
                     new object[] { new { productoId = 1, cantidad = 0 } },
                     new object[] { new { productoId = 1, cantidad = 2_000_000_000 } },
                     new object[] { new { productoId = 0, cantidad = 1 } },
                     Enumerable.Range(0, 201).Select(_ => (object)new { productoId = 1, cantidad = 1 }).ToArray(),
                 })
        {
            var r = await jefe.PostAsJsonAsync("/api/ventas", FacturaManual(items: items));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
    }

    [Fact]
    public async Task Una_compra_manual_con_datos_sin_forma_de_factura_responde_400()
    {
        var jefe = await api.Jefe();
        var r = await jefe.PostAsJsonAsync("/api/compras", new
        {
            clienteId = 1, fecha = "2026-10-20", tipoFactura = "NaN", numeroFactura = "0001-00000001", proveedorNombre = "Proveedor", cuit = "20111111112",
            condicionPago = 2, items = new object[] { new { productoId = 1, cantidad = 1 } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Planes_productos_y_cuentas_rechazan_los_valores_sin_sentido_que_antes_aceptaban()
    {
        var jefe = await api.Jefe();
        var nombre = $"Valido {Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsJsonAsync("/api/planes", new { nombre, precioMensual = 1000, duracionMeses = int.MaxValue })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsJsonAsync("/api/planes", new { nombre, precioMensual = 99999999999999999999m, duracionMeses = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsJsonAsync("/api/productos", new { nombre, precioUnitario = 10, precioVenta = 15, stockInicial = 1, calorias = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsJsonAsync("/api/productos", new { nombre, precioUnitario = 10, precioVenta = 15, stockInicial = int.MaxValue })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.PostAsJsonAsync("/api/cuentas", new { clienteId = 1, codigo = "' OR '1'='1", nombre = "Hostil", naturaleza = 0, cuentaPadreId = (long?)null })).StatusCode);

        // Y lo válido sigue entrando.
        Assert.True((await jefe.PostAsJsonAsync("/api/planes", new { nombre, precioMensual = 1000, duracionMeses = 12 })).IsSuccessStatusCode);
        Assert.True((await jefe.PostAsJsonAsync("/api/productos", new { nombre, precioUnitario = 10, precioVenta = 15, stockInicial = 1, calorias = 0 })).IsSuccessStatusCode);
    }
}
