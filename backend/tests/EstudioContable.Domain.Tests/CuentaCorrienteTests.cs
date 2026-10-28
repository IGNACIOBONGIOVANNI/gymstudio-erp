using EstudioContable.Domain;

namespace EstudioContable.Domain.Tests;

public class CuentaCorrienteTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 27);

    private static ComprobanteDeCuenta Factura(long id, int diasAtras, decimal total)
        => new(id, Hoy.AddDays(-diasAtras), $"Factura {id}", total);

    private static PagoDeCuenta Pago(long id, int diasAtras, decimal importe)
        => new(id, Hoy.AddDays(-diasAtras), $"Cobro {id}", importe);

    [Fact]
    public void Sin_pagos_todo_lo_facturado_esta_pendiente()
    {
        var r = CuentaCorrienteCalculo.Calcular(new[] { Factura(1, 10, 1000), Factura(2, 5, 500) }, Array.Empty<PagoDeCuenta>(), Hoy);

        Assert.Equal(1500m, r.Facturado);
        Assert.Equal(0m, r.Pagado);
        Assert.Equal(1500m, r.Saldo);
        Assert.Equal(2, r.Pendientes.Count);
        Assert.Equal(0m, r.AFavor);
    }

    [Fact]
    public void Los_pagos_se_aplican_primero_a_las_facturas_mas_viejas()
    {
        // Facturas de 1000 (hace 80 días), 500 (hace 40) y 300 (hace 5). Se pagaron 1200.
        var r = CuentaCorrienteCalculo.Calcular(
            new[] { Factura(3, 5, 300), Factura(1, 80, 1000), Factura(2, 40, 500) },    // desordenadas a propósito
            new[] { Pago(1, 20, 1200) }, Hoy);

        // La de 1000 se paga entera, la de 500 queda con 300 pendientes y la de 300 intacta.
        Assert.Equal(new long[] { 2, 3 }, r.Pendientes.Select(p => p.Id));
        Assert.Equal(300m, r.Pendientes.Single(p => p.Id == 2).Pendiente);
        Assert.Equal(300m, r.Pendientes.Single(p => p.Id == 3).Pendiente);
        Assert.Equal(600m, r.Saldo);
        Assert.Equal(600m, r.Pendientes.Sum(p => p.Pendiente));      // lo pendiente siempre coincide con el saldo
    }

    [Fact]
    public void La_antiguedad_agrupa_lo_pendiente_por_dias_desde_la_factura()
    {
        var r = CuentaCorrienteCalculo.Calcular(
            new[] { Factura(1, 100, 400), Factura(2, 61, 300), Factura(3, 31, 200), Factura(4, 30, 100), Factura(5, 0, 50) },
            Array.Empty<PagoDeCuenta>(), Hoy);

        Assert.Equal(150m, r.Antiguedad.Corriente);    // 30 y 0 días
        Assert.Equal(200m, r.Antiguedad.De31A60);      // 31 días
        Assert.Equal(300m, r.Antiguedad.De61A90);      // 61 días
        Assert.Equal(400m, r.Antiguedad.Mas90);        // 100 días
        Assert.Equal(r.Saldo, r.Antiguedad.Total);
    }

    [Fact]
    public void Un_pago_de_mas_queda_como_saldo_a_favor()
    {
        var r = CuentaCorrienteCalculo.Calcular(new[] { Factura(1, 10, 1000) }, new[] { Pago(1, 5, 1500) }, Hoy);

        Assert.Empty(r.Pendientes);
        Assert.Equal(-500m, r.Saldo);
        Assert.Equal(500m, r.AFavor);
        Assert.Equal(0m, r.Antiguedad.Total);
    }

    [Fact]
    public void Un_pago_adelantado_se_aplica_a_la_factura_posterior()
    {
        // El cliente pagó antes de que se le facture: igual cancela la factura cuando aparece.
        var r = CuentaCorrienteCalculo.Calcular(new[] { Factura(1, 5, 800) }, new[] { Pago(1, 20, 800) }, Hoy);

        Assert.Empty(r.Pendientes);
        Assert.Equal(0m, r.Saldo);
    }

    [Fact]
    public void El_estado_de_cuenta_va_en_orden_de_fecha_con_el_saldo_corrido()
    {
        var r = CuentaCorrienteCalculo.Calcular(
            new[] { Factura(1, 30, 1000), Factura(2, 10, 500) },
            new[] { Pago(1, 20, 400), Pago(2, 10, 100) }, Hoy);

        Assert.Equal(new[] { 1000m, 600m, 1100m, 1000m }, r.Estado.Select(f => f.Saldo));
        // El mismo día, primero la factura y después el cobro.
        Assert.Equal(new[] { "Factura 1", "Cobro 1", "Factura 2", "Cobro 2" }, r.Estado.Select(f => f.Descripcion));
        Assert.Equal(r.Saldo, r.Estado[^1].Saldo);
    }

    [Fact]
    public void Una_cuenta_vacia_da_todo_en_cero()
    {
        var r = CuentaCorrienteCalculo.Calcular(Array.Empty<ComprobanteDeCuenta>(), Array.Empty<PagoDeCuenta>(), Hoy);

        Assert.Equal(0m, r.Saldo);
        Assert.Empty(r.Pendientes);
        Assert.Empty(r.Estado);
    }
}
