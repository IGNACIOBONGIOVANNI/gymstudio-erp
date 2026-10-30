using EstudioContable.Domain;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Domain.Tests;

/// <summary>
/// Lo que el atacante automático encontró aceptado sin formato ni tope: ahora el dominio lo rechaza,
/// así que vale por cualquier camino (la API o una importación).
/// </summary>
public class ValidacionesTests
{
    private static readonly DateTime Fecha = new(2026, 10, 29);

    // ---------- reglas generales ----------

    [Fact]
    public void El_texto_requerido_se_recorta_y_rechaza_lo_vacio_lo_largo_y_los_caracteres_de_control()
    {
        Assert.Equal("Ana", ReglasDeDatos.TextoRequerido("  Ana  ", "El nombre", 10));
        Assert.Throws<ArgumentException>(() => ReglasDeDatos.TextoRequerido("   ", "El nombre", 10));
        Assert.Throws<ArgumentException>(() => ReglasDeDatos.TextoRequerido(null, "El nombre", 10));
        Assert.Throws<ArgumentException>(() => ReglasDeDatos.TextoRequerido(new string('a', 11), "El nombre", 10));
        Assert.Throws<ArgumentException>(() => ReglasDeDatos.TextoRequerido("a\0b", "El nombre", 10));
        Assert.Throws<ArgumentException>(() => ReglasDeDatos.TextoRequerido("a\r\nb", "El nombre", 10));
    }

    [Fact]
    public void El_texto_libre_con_comillas_y_etiquetas_se_acepta_tal_cual_porque_se_guarda_parametrizado_y_se_muestra_escapado()
        => Assert.Equal("' OR '1'='1 <b>", ReglasDeDatos.TextoRequerido("' OR '1'='1 <b>", "El nombre", 50));

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000_000_000)]
    public void Un_importe_negativo_o_gigante_se_rechaza(decimal valor)
        => Assert.Throws<ArgumentException>(() => ReglasDeDatos.Importe(valor, "El precio"));

    [Theory]
    [InlineData("1.1.1.01", true)]
    [InlineData("101", true)]
    [InlineData("1", true)]
    [InlineData("", false)]
    [InlineData("' OR '1'='1", false)]
    [InlineData("1.1.1.", false)]
    [InlineData("1..1", false)]
    [InlineData("1.1.1.01.1.1.1.1", false)]
    [InlineData("abc", false)]
    [InlineData("<script>", false)]
    [InlineData("12345.1", false)]
    public void Un_codigo_de_cuenta_son_numeros_separados_por_puntos(string codigo, bool esperado)
        => Assert.Equal(esperado, ReglasDeDatos.EsCodigoDeCuenta(codigo));

    // ---------- planes ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(121)]
    [InlineData(int.MaxValue)]
    public void La_duracion_de_un_plan_va_de_1_a_120_meses(int meses)
        => Assert.Throws<ArgumentException>(() => new Plan("Mensual", 1000, meses));

    [Fact]
    public void Un_plan_de_120_meses_o_con_nombre_de_100_caracteres_es_valido_y_uno_mas_largo_no()
    {
        Assert.Equal(120, new Plan(new string('a', 100), 1000, 120).DuracionMeses);
        Assert.Throws<ArgumentException>(() => new Plan(new string('a', 101), 1000, 1));
        Assert.Throws<ArgumentException>(() => new Plan("Mensual", ReglasDeDatos.ImporteMaximo + 1, 1));
        Assert.Throws<ArgumentException>(() => new Plan("Mensual", 1000, 1).Actualizar("Mensual", 1000, int.MaxValue));
    }

    // ---------- productos ----------

    [Fact]
    public void Un_producto_rechaza_valores_nutricionales_negativos_precios_gigantes_y_stock_absurdo()
    {
        Assert.Throws<ArgumentException>(() => new Producto("Proteína", 10, 15, calorias: -1));
        Assert.Throws<ArgumentException>(() => new Producto("Proteína", 10, 15, proteinas: 100_001));
        Assert.Throws<ArgumentException>(() => new Producto("Proteína", ReglasDeDatos.ImporteMaximo + 1, 15));
        Assert.Throws<ArgumentException>(() => new Producto("Proteína", 10, 15, stockInicial: ReglasDeDatos.StockMaximo + 1));
        Assert.Throws<ArgumentException>(() => new Producto("Proteína", 10, 15, marca: "a\0b"));
        Assert.Throws<ArgumentException>(() => new Producto(new string('x', 201), 10, 15));

        var bueno = new Producto("  Proteína  ", 10, 15, 5, marca: "  ", calorias: 0, grasas: 12.5m);
        Assert.Equal("Proteína", bueno.Nombre);
        Assert.Null(bueno.Marca);                 // vacío se guarda como null
        Assert.Equal(12.5m, bueno.Grasas);
    }

    [Fact]
    public void Editar_un_producto_aplica_las_mismas_reglas()
    {
        var p = new Producto("Proteína", 10, 15);
        Assert.Throws<ArgumentException>(() => p.ActualizarDatos("Proteína", 10, 15, null, null, -5, null, null, null));
        Assert.Throws<ArgumentException>(() => p.ActualizarDatos("Proteína", 1e15m, 15, null, null, null, null, null, null));
    }

    // ---------- cuentas contables ----------

    [Fact]
    public void Una_cuenta_contable_necesita_un_codigo_con_forma_y_una_naturaleza_definida()
    {
        Assert.Equal("1.1.1.01", new CuentaContable(1, " 1.1.1.01 ", "Caja", NaturalezaCuenta.Activo).Codigo);
        Assert.Throws<ArgumentException>(() => new CuentaContable(1, "' OR '1'='1", "Caja", NaturalezaCuenta.Activo));
        Assert.Throws<ArgumentException>(() => new CuentaContable(1, "1.1", "   ", NaturalezaCuenta.Activo));
        Assert.Throws<ArgumentException>(() => new CuentaContable(1, "1.1", "Caja", (NaturalezaCuenta)99));
    }

    // ---------- facturas cargadas a mano o importadas ----------

    [Theory]
    [InlineData("X")]
    [InlineData("NaN")]
    [InlineData("")]
    [InlineData("AA")]
    [InlineData("‮A")]
    public void El_tipo_de_factura_tiene_que_ser_A_B_o_C(string tipo)
    {
        Assert.Throws<ArgumentException>(() => new FacturaVenta(1, Fecha, tipo, "0001-00000001", "Cliente", "20111111112"));
        Assert.Throws<ArgumentException>(() => new FacturaCompra(1, Fecha, tipo, "0001-00000001", "Proveedor", "20111111112"));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0001-1")]
    [InlineData("' OR '1'='1")]
    [InlineData("=1+1")]
    [InlineData("0001-0000000A")]
    [InlineData("0001_00000001")]
    [InlineData("00001-0000001")]
    public void El_numero_de_factura_tiene_el_formato_0001_00000001(string numero)
        => Assert.Throws<ArgumentException>(() => new FacturaVenta(1, Fecha, "A", numero, "Cliente", "20111111112"));

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("javascript:alert(1)")]
    [InlineData("+cmd|' /C calc'!A0")]
    [InlineData("20111111113")]      // dígito verificador mal
    [InlineData("123")]
    public void El_documento_de_la_factura_tiene_que_ser_un_cuit_o_dni_valido(string documento)
        => Assert.Throws<ArgumentException>(() => new FacturaVenta(1, Fecha, "A", "0001-00000001", "Cliente", documento));

    [Fact]
    public void Una_factura_valida_se_guarda_normalizada()
    {
        var f = new FacturaVenta(1, Fecha, " b ", " 0001-00000001 ", "  Cliente SA ", "20-11111111-2", CondicionPago.ContadoCaja);
        Assert.Equal("B", f.TipoFactura);
        Assert.Equal("0001-00000001", f.NumeroFactura);
        Assert.Equal("Cliente SA", f.ClienteNombre);
        Assert.Equal("20-11111111-2", f.Cuit);

        var dni = new FacturaCompra(1, Fecha, "C", "0003-00001234", "Proveedor", "30.111.222");
        Assert.Equal("30111222", dni.Cuit);
    }

    [Fact]
    public void La_fecha_el_nombre_y_la_condicion_de_pago_de_una_factura_tienen_que_ser_validos()
    {
        Assert.Throws<ArgumentException>(() => new FacturaVenta(1, new DateTime(1999, 1, 1), "A", "0001-00000001", "Cliente", "20111111112"));
        Assert.Throws<ArgumentException>(() => new FacturaVenta(1, new DateTime(2101, 1, 1), "A", "0001-00000001", "Cliente", "20111111112"));
        Assert.Throws<ArgumentException>(() => new FacturaVenta(1, Fecha, "A", "0001-00000001", new string('n', 201), "20111111112"));
        Assert.Throws<ArgumentException>(() => new FacturaVenta(1, Fecha, "A", "0001-00000001", "Cli\0ente", "20111111112"));
        Assert.Throws<ArgumentException>(() => new FacturaVenta(1, Fecha, "A", "0001-00000001", "Cliente", "20111111112", (CondicionPago)(-1)));
        Assert.Throws<ArgumentException>(() => new FacturaCompra(1, Fecha, "A", "0001-00000001", "Proveedor", "20111111112", (CondicionPago)int.MaxValue));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(100, 21, 200)]                  // el total no es neto más IVA
    [InlineData(1e15, 0, 1e15)]                  // gigante
    [InlineData(100, -21, 79)]
    public void Los_importes_importados_tienen_que_ser_coherentes(double neto, double iva, double total)
    {
        Assert.Throws<ArgumentException>(() => new FacturaVenta(1, Fecha, "A", "0001-00000001", "Cliente", "20111111112", (decimal)neto, (decimal)iva, (decimal)total));
        Assert.Throws<ArgumentException>(() => new FacturaCompra(1, Fecha, "A", "0001-00000001", "Proveedor", "20111111112", (decimal)neto, (decimal)iva, (decimal)total));
    }

    [Fact]
    public void Un_total_con_un_peso_de_redondeo_se_acepta()
        => Assert.Equal(1210.50m, new FacturaVenta(1, Fecha, "A", "0001-00000001", "Cliente", "20111111112", 1000m, 210m, 1210.50m).Total);

    [Fact]
    public void Una_factura_manual_necesita_cliente_condicion_de_pago_e_items_validos()
    {
        var uno = new List<(long, int)> { (1, 2) };
        Assert.Null(ReglasDeComprobante.ValidarFacturaManual(1, CondicionPago.ContadoCaja, uno));
        Assert.NotNull(ReglasDeComprobante.ValidarFacturaManual(0, CondicionPago.ContadoCaja, uno));
        Assert.NotNull(ReglasDeComprobante.ValidarFacturaManual(-1, CondicionPago.ContadoCaja, uno));
        Assert.NotNull(ReglasDeComprobante.ValidarFacturaManual(1, (CondicionPago)(-1), uno));
        Assert.NotNull(ReglasDeComprobante.ValidarFacturaManual(1, CondicionPago.ContadoCaja, null));
        Assert.NotNull(ReglasDeComprobante.ValidarFacturaManual(1, CondicionPago.ContadoCaja, new List<(long, int)>()));
        Assert.NotNull(ReglasDeComprobante.ValidarFacturaManual(1, CondicionPago.ContadoCaja, new List<(long, int)> { (0, 1) }));
        Assert.NotNull(ReglasDeComprobante.ValidarFacturaManual(1, CondicionPago.ContadoCaja, new List<(long, int)> { (1, 0) }));
        Assert.NotNull(ReglasDeComprobante.ValidarFacturaManual(1, CondicionPago.ContadoCaja, new List<(long, int)> { (1, 100_001) }));
        Assert.NotNull(ReglasDeComprobante.ValidarFacturaManual(1, CondicionPago.ContadoCaja, Enumerable.Repeat((1L, 1), 201).ToList()));
    }
}
