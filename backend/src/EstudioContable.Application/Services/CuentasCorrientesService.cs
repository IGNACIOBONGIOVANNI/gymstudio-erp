using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain;
using EstudioContable.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Services
{
    public record TerceroConSaldo(long? Id, string Nombre, string Cuit, decimal Facturado, decimal Pagado, decimal Saldo, decimal AFavor,
        Antiguedad Antiguedad, int FacturasPendientes, int DiasMasAntigua);

    public record ListadoCuentasCorrientes(DateOnly Hoy, decimal Facturado, decimal Pagado, decimal Saldo, decimal AFavor, Antiguedad Antiguedad,
        IReadOnlyList<TerceroConSaldo> Terceros, string CuentaMayor, decimal SaldoMayor);

    public record DetalleDeCuenta(long Id, string Nombre, string Cuit, string CondicionIva, ResultadoCuentaCorriente Resultado);

    public interface ICuentasCorrientesService
    {
        Task<ListadoCuentasCorrientes> ListadoAsync(bool clientes, DateOnly hoy, CancellationToken cancellationToken);
        Task<DetalleDeCuenta?> DetalleAsync(bool clientes, long id, DateOnly hoy, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Cuentas a cobrar (clientes) y a pagar (proveedores). La deuda de cada uno sale de sus facturas en cuenta
    /// corriente menos los cobros o pagos cargados en Caja o Banco a su nombre; las facturas anuladas no cuentan.
    /// Lo usan la pantalla de cuentas corrientes y las alertas del resumen de inicio.
    /// </summary>
    public class CuentasCorrientesService : ICuentasCorrientesService
    {
        private const string CuentaCorriente = "CuentaCorriente";
        private readonly IAppDbContext _context;

        public CuentasCorrientesService(IAppDbContext context) => _context = context;

        private record FacturaDeCuenta(long Id, long? TerceroId, DateOnly Fecha, string Descripcion, decimal Total);
        private record PagoConTercero(long TerceroId, PagoDeCuenta Pago);
        private record Tercero(string Nombre, string Cuit, string CondicionIva);

        public async Task<ListadoCuentasCorrientes> ListadoAsync(bool clientes, DateOnly hoy, CancellationToken cancellationToken)
        {
            var facturas = await FacturasAsync(clientes, null, cancellationToken);
            var pagos = await PagosAsync(clientes, null, cancellationToken);
            var nombres = await NombresAsync(clientes, cancellationToken);

            var ids = facturas.Select(f => f.TerceroId).Concat(pagos.Select(p => (long?)p.TerceroId)).Distinct().ToList();
            var filas = ids.Select(id =>
            {
                var r = CuentaCorrienteCalculo.Calcular(
                    facturas.Where(f => f.TerceroId == id).Select(f => new ComprobanteDeCuenta(f.Id, f.Fecha, f.Descripcion, f.Total)),
                    pagos.Where(p => (long?)p.TerceroId == id).Select(p => p.Pago), hoy);
                nombres.TryGetValue(id ?? 0, out var tercero);
                return new TerceroConSaldo(
                    id,
                    id is null ? (clientes ? "(Facturas sin cliente cargado)" : "(Facturas sin proveedor cargado)") : tercero?.Nombre ?? "(eliminado)",
                    tercero?.Cuit ?? "",
                    r.Facturado, r.Pagado, r.Saldo, r.AFavor, r.Antiguedad,
                    r.Pendientes.Count,
                    r.Pendientes.Count == 0 ? 0 : r.Pendientes.Max(p => p.Dias));
            })
            .Where(f => f.Facturado != 0 || f.Pagado != 0)
            .OrderByDescending(f => f.Saldo).ThenBy(f => f.Nombre)
            .ToList();

            // Saldo de la cuenta en el Libro Mayor, para compararlo con el total.
            var cuenta = clientes ? PlanDeCuentas.DeudoresPorVentas : PlanDeCuentas.Proveedores;
            var mayor = await _context.LibroMayor
                .Where(m => m.CuentaId == cuenta && m.ClienteId == PlanDeCuentas.ClienteIdPorDefecto)
                .FirstOrDefaultAsync(cancellationToken);
            var saldoMayor = mayor is null ? 0m : clientes ? mayor.TotalDebe - mayor.TotalHaber : mayor.TotalHaber - mayor.TotalDebe;

            return new ListadoCuentasCorrientes(
                hoy,
                filas.Sum(f => f.Facturado), filas.Sum(f => f.Pagado), filas.Sum(f => f.Saldo), filas.Sum(f => f.AFavor),
                new Antiguedad(filas.Sum(f => f.Antiguedad.Corriente), filas.Sum(f => f.Antiguedad.De31A60), filas.Sum(f => f.Antiguedad.De61A90), filas.Sum(f => f.Antiguedad.Mas90)),
                filas,
                clientes ? "Deudores por Ventas" : "Proveedores",
                saldoMayor);
        }

        public async Task<DetalleDeCuenta?> DetalleAsync(bool clientes, long id, DateOnly hoy, CancellationToken cancellationToken)
        {
            var nombres = await NombresAsync(clientes, cancellationToken);
            if (!nombres.TryGetValue(id, out var tercero)) return null;

            var facturas = await FacturasAsync(clientes, id, cancellationToken);
            var pagos = await PagosAsync(clientes, id, cancellationToken);
            var r = CuentaCorrienteCalculo.Calcular(
                facturas.Select(f => new ComprobanteDeCuenta(f.Id, f.Fecha, f.Descripcion, f.Total)),
                pagos.Select(p => p.Pago), hoy);
            return new DetalleDeCuenta(id, tercero.Nombre, tercero.Cuit, tercero.CondicionIva, r);
        }

        private async Task<List<FacturaDeCuenta>> FacturasAsync(bool clientes, long? terceroId, CancellationToken cancellationToken)
        {
            var operacion = clientes ? "Venta" : "Compra";
            var consulta = _context.Facturas.AsNoTracking()
                .Where(f => !f.Anulada && f.CondicionPago == CuentaCorriente && f.TipoOperacion == operacion);
            if (terceroId is long id)
                consulta = clientes ? consulta.Where(f => f.ClienteComercialId == id) : consulta.Where(f => f.ProveedorId == id);

            var filas = await consulta
                .Select(f => new { f.Id, f.ClienteComercialId, f.ProveedorId, f.Fecha, f.TipoComprobante, f.PuntoVenta, f.NumeroComprobante, f.Total })
                .ToListAsync(cancellationToken);

            return filas.Select(f => new FacturaDeCuenta(
                f.Id,
                clientes ? f.ClienteComercialId : f.ProveedorId,
                DateOnly.FromDateTime(f.Fecha),
                $"Factura {f.TipoComprobante} {(f.PuntoVenta.HasValue && f.NumeroComprobante.HasValue ? $"{f.PuntoVenta.Value:D4}-{f.NumeroComprobante.Value:D8}" : $"N° {f.Id}")}",
                f.Total)).ToList();
        }

        /// <summary>Cobros a clientes (ingresos) o pagos a proveedores (egresos) de Caja y Banco, ya vinculados a un tercero.</summary>
        private async Task<List<PagoConTercero>> PagosAsync(bool clientes, long? terceroId, CancellationToken cancellationToken)
        {
            var resultado = new List<PagoConTercero>();
            var verbo = clientes ? "Cobro" : "Pago";

            var caja = _context.MovimientosCaja.AsNoTracking().AsQueryable();
            var banco = _context.MovimientosBanco.AsNoTracking().AsQueryable();
            if (clientes)
            {
                caja = caja.Where(m => m.ClienteComercialId != null && m.TipoMovimiento == TipoMovimientoCaja.Ingreso && (terceroId == null || m.ClienteComercialId == terceroId));
                banco = banco.Where(m => m.ClienteComercialId != null && m.TipoMovimiento == TipoMovimientoBanco.Ingreso && (terceroId == null || m.ClienteComercialId == terceroId));
            }
            else
            {
                caja = caja.Where(m => m.ProveedorId != null && m.TipoMovimiento == TipoMovimientoCaja.Egreso && (terceroId == null || m.ProveedorId == terceroId));
                banco = banco.Where(m => m.ProveedorId != null && m.TipoMovimiento == TipoMovimientoBanco.Egreso && (terceroId == null || m.ProveedorId == terceroId));
            }

            foreach (var m in await caja.Select(m => new { m.Id, m.ClienteComercialId, m.ProveedorId, m.Fecha, m.Detalle, m.Importe }).ToListAsync(cancellationToken))
                resultado.Add(new PagoConTercero((clientes ? m.ClienteComercialId : m.ProveedorId)!.Value,
                    new PagoDeCuenta(m.Id * 2, DateOnly.FromDateTime(m.Fecha), $"{verbo} en Caja - {m.Detalle}", m.Importe)));
            foreach (var m in await banco.Select(m => new { m.Id, m.ClienteComercialId, m.ProveedorId, m.Fecha, m.Detalle, m.Importe }).ToListAsync(cancellationToken))
                resultado.Add(new PagoConTercero((clientes ? m.ClienteComercialId : m.ProveedorId)!.Value,
                    new PagoDeCuenta(m.Id * 2 + 1, DateOnly.FromDateTime(m.Fecha), $"{verbo} en Banco - {m.Detalle}", m.Importe)));
            return resultado;
        }

        private async Task<Dictionary<long, Tercero>> NombresAsync(bool clientes, CancellationToken cancellationToken)
            => clientes
                ? await _context.ClientesComerciales.AsNoTracking().ToDictionaryAsync(c => c.Id, c => new Tercero(c.RazonSocial, c.Cuit, c.CondicionIva), cancellationToken)
                : await _context.Proveedores.AsNoTracking().ToDictionaryAsync(p => p.Id, p => new Tercero(p.RazonSocial, p.Cuit, p.CondicionIva), cancellationToken);
    }
}
