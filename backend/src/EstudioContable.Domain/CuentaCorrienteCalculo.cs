using System;
using System.Collections.Generic;
using System.Linq;

namespace EstudioContable.Domain
{
    /// <summary>Factura de un cliente o proveedor emitida en cuenta corriente.</summary>
    public record ComprobanteDeCuenta(long Id, DateOnly Fecha, string Descripcion, decimal Total);

    /// <summary>Cobro (de un cliente) o pago (a un proveedor) registrado en Caja o Banco.</summary>
    public record PagoDeCuenta(long Id, DateOnly Fecha, string Descripcion, decimal Importe);

    /// <summary>Lo que falta cobrar o pagar de una factura, con su antigüedad.</summary>
    public record ComprobantePendiente(long Id, DateOnly Fecha, string Descripcion, decimal Total, decimal Pendiente, int Dias);

    public record FilaDeEstado(DateOnly Fecha, string Descripcion, decimal Debe, decimal Haber, decimal Saldo);

    /// <summary>Antigüedad de la deuda: lo que se debe agrupado por días desde la fecha de la factura.</summary>
    public record Antiguedad(decimal Corriente, decimal De31A60, decimal De61A90, decimal Mas90)
    {
        public decimal Total => Corriente + De31A60 + De61A90 + Mas90;
    }

    public record ResultadoCuentaCorriente(
        decimal Facturado,
        decimal Pagado,
        decimal Saldo,
        decimal AFavor,
        Antiguedad Antiguedad,
        IReadOnlyList<ComprobantePendiente> Pendientes,
        IReadOnlyList<FilaDeEstado> Estado);

    /// <summary>
    /// Cuenta corriente de un cliente o proveedor: saldo, facturas pendientes y antigüedad.
    /// Los pagos no se imputan a una factura en particular: se aplican a las más viejas primero
    /// (como se hace en la práctica), y lo que sobra queda como saldo a favor.
    /// </summary>
    public static class CuentaCorrienteCalculo
    {
        public static ResultadoCuentaCorriente Calcular(IEnumerable<ComprobanteDeCuenta> comprobantes, IEnumerable<PagoDeCuenta> pagos, DateOnly hoy)
        {
            var facturas = comprobantes.OrderBy(c => c.Fecha).ThenBy(c => c.Id).ToList();
            var cobros = pagos.OrderBy(p => p.Fecha).ThenBy(p => p.Id).ToList();

            var facturado = facturas.Sum(f => f.Total);
            var pagado = cobros.Sum(p => p.Importe);

            // Los pagos se aplican a las facturas más viejas primero.
            var restante = pagado;
            var pendientes = new List<ComprobantePendiente>();
            foreach (var f in facturas)
            {
                var aplicado = Math.Min(f.Total, Math.Max(restante, 0m));
                restante -= aplicado;
                var pendiente = f.Total - aplicado;
                if (pendiente > 0)
                    pendientes.Add(new ComprobantePendiente(f.Id, f.Fecha, f.Descripcion, f.Total, pendiente, hoy.DayNumber - f.Fecha.DayNumber));
            }

            var antiguedad = new Antiguedad(
                pendientes.Where(p => p.Dias <= 30).Sum(p => p.Pendiente),
                pendientes.Where(p => p.Dias is > 30 and <= 60).Sum(p => p.Pendiente),
                pendientes.Where(p => p.Dias is > 60 and <= 90).Sum(p => p.Pendiente),
                pendientes.Where(p => p.Dias > 90).Sum(p => p.Pendiente));

            // Estado de cuenta: facturas (Debe) y pagos (Haber) en orden de fecha, con el saldo corrido.
            // El mismo día va primero la factura y después el pago.
            var movimientos = facturas.Select(f => (f.Fecha, Orden: 0, f.Id, f.Descripcion, Debe: f.Total, Haber: 0m))
                .Concat(cobros.Select(p => (p.Fecha, Orden: 1, p.Id, p.Descripcion, Debe: 0m, Haber: p.Importe)))
                .OrderBy(m => m.Fecha).ThenBy(m => m.Orden).ThenBy(m => m.Id);
            var saldoCorrido = 0m;
            var estado = movimientos.Select(m =>
            {
                saldoCorrido += m.Debe - m.Haber;
                return new FilaDeEstado(m.Fecha, m.Descripcion, m.Debe, m.Haber, saldoCorrido);
            }).ToList();

            return new ResultadoCuentaCorriente(facturado, pagado, facturado - pagado, Math.Max(restante, 0m), antiguedad, pendientes, estado);
        }
    }
}
