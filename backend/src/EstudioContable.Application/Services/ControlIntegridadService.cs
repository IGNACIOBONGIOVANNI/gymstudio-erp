using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;
using EstadoAsiento = EstudioContable.Domain.Entities.EstadoAsiento;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Services
{
    /// <summary>
    /// Un control: se comparan dos números que, si el sistema está sano, deben ser iguales
    /// (o un conteo de casos malos que debe ser cero).
    /// </summary>
    public record ControlDeIntegridad(
        string Clave,
        string Titulo,
        string Explicacion,
        bool Correcto,
        decimal Obtenido,
        decimal Esperado,
        string EtiquetaObtenido,
        string EtiquetaEsperado,
        bool EsImporte);

    public record ResultadoIntegridad(DateTimeOffset Fecha, IReadOnlyList<ControlDeIntegridad> Controles)
    {
        public int Total => Controles.Count;
        public int Correctos => Controles.Count(c => c.Correcto);
    }

    public interface IControlIntegridadService
    {
        Task<ResultadoIntegridad> EjecutarAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Auditoría contable en vivo, de solo lectura. Cruza las facturas, los libros de Caja y Banco, el stock
    /// y el Libro Mayor: si algo no cierra (por ejemplo, un dato tocado directamente en la base) se ve acá.
    /// </summary>
    public class ControlIntegridadService : IControlIntegridadService
    {
        private const decimal Tolerancia = 0.005m;
        private readonly IAppDbContext _context;

        public ControlIntegridadService(IAppDbContext context) => _context = context;

        public async Task<ResultadoIntegridad> EjecutarAsync(CancellationToken cancellationToken)
        {
            var clienteId = PlanDeCuentas.ClienteIdPorDefecto;
            var clienteTexto = clienteId.ToString();

            // Saldos del Libro Mayor por cuenta (solo asientos confirmados) y por naturaleza.
            var mayor = await _context.LibroMayor.Where(m => m.ClienteId == clienteId).ToListAsync(cancellationToken);
            decimal DebeMenosHaber(long cuenta) => mayor.Where(m => m.CuentaId == cuenta).Sum(m => m.TotalDebe - m.TotalHaber);
            decimal HaberMenosDebe(long cuenta) => -DebeMenosHaber(cuenta);

            var facturasVigentes = _context.Facturas.AsNoTracking().Where(f => !f.Anulada);
            var ventas = facturasVigentes.Where(f => f.TipoOperacion == "Venta");
            var compras = facturasVigentes.Where(f => f.TipoOperacion == "Compra");

            var controles = new List<ControlDeIntegridad>();

            // 1. Partida doble en todo el Diario.
            var lineas = _context.LineasAsiento.AsNoTracking().Where(l => l.Asiento.Estado == EstadoAsiento.Confirmado);
            var totalDebe = await lineas.SumAsync(l => l.Debe, cancellationToken);
            var totalHaber = await lineas.SumAsync(l => l.Haber, cancellationToken);
            controles.Add(Comparar("partida-doble", "Partida doble: Debe = Haber",
                "La suma de todo lo que se debitó en el Libro Diario es igual a la suma de todo lo que se acreditó.",
                totalDebe, "Total Debe", totalHaber, "Total Haber"));

            // 2. Cada asiento confirmado, por separado.
            var desbalanceados = await lineas.GroupBy(l => l.AsientoId)
                .Where(g => g.Sum(l => l.Debe) != g.Sum(l => l.Haber))
                .CountAsync(cancellationToken);
            controles.Add(Contar("asientos-balanceados", "Cada asiento confirmado está balanceado",
                "Ningún asiento del Diario tiene un Debe distinto de su Haber.", desbalanceados, "Asientos desbalanceados"));

            // 3. Ecuación contable.
            decimal Neta(NaturalezaCuenta n, bool deudora) => mayor.Where(m => m.Naturaleza == n)
                .Sum(m => deudora ? m.TotalDebe - m.TotalHaber : m.TotalHaber - m.TotalDebe);
            var activo = Neta(NaturalezaCuenta.Activo, true);
            var pasivoMasPatrimonio = Neta(NaturalezaCuenta.Pasivo, false) + Neta(NaturalezaCuenta.PatrimonioNeto, false) + Neta(NaturalezaCuenta.Resultado, false);
            controles.Add(Comparar("ecuacion-contable", "Ecuación contable: Activo = Pasivo + Patrimonio + Resultado",
                "El balance general cierra: lo que tiene el gimnasio es igual a lo que debe más lo que le pertenece.",
                activo, "Activo", pasivoMasPatrimonio, "Pasivo + Patrimonio + Resultado"));

            // 4 y 5. Caja y Banco: el libro y el Mayor cuentan lo mismo.
            var saldoCaja = await _context.MovimientosCaja.AsNoTracking().Where(m => m.ClienteId == clienteTexto)
                .SumAsync(m => m.TipoMovimiento == TipoMovimientoCaja.Ingreso ? m.Importe : -m.Importe, cancellationToken);
            controles.Add(Comparar("caja", "Libro Caja = cuenta Caja del Mayor",
                "Cada movimiento de caja tiene su asiento: el saldo del libro coincide con el de la contabilidad.",
                saldoCaja, "Libro Caja", DebeMenosHaber(PlanDeCuentas.Caja), "Mayor: Caja"));
            var saldoBanco = await _context.MovimientosBanco.AsNoTracking().Where(m => m.ClienteId == clienteTexto)
                .SumAsync(m => m.TipoMovimiento == TipoMovimientoBanco.Ingreso ? m.Importe : -m.Importe, cancellationToken);
            controles.Add(Comparar("banco", "Libro Banco = cuenta Banco del Mayor",
                "Cada movimiento del banco tiene su asiento: el saldo del libro coincide con el de la contabilidad.",
                saldoBanco, "Libro Banco", DebeMenosHaber(PlanDeCuentas.Banco), "Mayor: Banco"));

            // 6 a 9. Lo que dicen las facturas contra lo que registró el Mayor.
            var netoVentas = await ventas.SumAsync(f => f.Subtotal, cancellationToken);
            var netoCompras = await compras.SumAsync(f => f.Subtotal, cancellationToken);
            var ivaVentas = await ventas.SumAsync(f => f.Iva, cancellationToken);
            var ivaCompras = await compras.SumAsync(f => f.Iva, cancellationToken);
            controles.Add(Comparar("ventas", "Libro de Ventas = cuenta Ventas del Mayor",
                "El neto de las facturas de venta vigentes es el que registró la contabilidad.",
                netoVentas, "Facturas de venta (neto)", HaberMenosDebe(PlanDeCuentas.Ventas), "Mayor: Ventas"));
            controles.Add(Comparar("compras", "Libro de Compras = cuenta Compras del Mayor",
                "El neto de las facturas de compra vigentes es el que registró la contabilidad.",
                netoCompras, "Facturas de compra (neto)", DebeMenosHaber(PlanDeCuentas.Compras), "Mayor: Compras"));
            controles.Add(Comparar("iva-debito", "IVA de ventas = IVA Débito Fiscal del Mayor",
                "El IVA cobrado en las facturas es el que figura en la cuenta de IVA Débito Fiscal.",
                ivaVentas, "IVA de las facturas de venta", HaberMenosDebe(PlanDeCuentas.IvaDebitoFiscal), "Mayor: IVA Débito Fiscal"));
            controles.Add(Comparar("iva-credito", "IVA de compras = IVA Crédito Fiscal del Mayor",
                "El IVA pagado en las facturas es el que figura en la cuenta de IVA Crédito Fiscal.",
                ivaCompras, "IVA de las facturas de compra", DebeMenosHaber(PlanDeCuentas.IvaCreditoFiscal), "Mayor: IVA Crédito Fiscal"));

            // 10 y 11. Cuentas a cobrar y a pagar: lo facturado en cuenta corriente menos lo cobrado o pagado.
            var facturadoClientes = await ventas.Where(f => f.CondicionPago == "CuentaCorriente").SumAsync(f => f.Total, cancellationToken);
            var cobrado = await _context.MovimientosCaja.AsNoTracking().Where(m => m.ClienteComercialId != null && m.TipoMovimiento == TipoMovimientoCaja.Ingreso).SumAsync(m => m.Importe, cancellationToken)
                + await _context.MovimientosBanco.AsNoTracking().Where(m => m.ClienteComercialId != null && m.TipoMovimiento == TipoMovimientoBanco.Ingreso).SumAsync(m => m.Importe, cancellationToken);
            controles.Add(Comparar("cuentas-a-cobrar", "Cuentas a cobrar = Deudores por Ventas del Mayor",
                "Lo que los clientes deben según sus cuentas corrientes es lo que figura en la contabilidad.",
                facturadoClientes - cobrado, "Suma de las cuentas de clientes", DebeMenosHaber(PlanDeCuentas.DeudoresPorVentas), "Mayor: Deudores por Ventas"));
            var facturadoProveedores = await compras.Where(f => f.CondicionPago == "CuentaCorriente").SumAsync(f => f.Total, cancellationToken);
            var pagado = await _context.MovimientosCaja.AsNoTracking().Where(m => m.ProveedorId != null && m.TipoMovimiento == TipoMovimientoCaja.Egreso).SumAsync(m => m.Importe, cancellationToken)
                + await _context.MovimientosBanco.AsNoTracking().Where(m => m.ProveedorId != null && m.TipoMovimiento == TipoMovimientoBanco.Egreso).SumAsync(m => m.Importe, cancellationToken);
            controles.Add(Comparar("cuentas-a-pagar", "Cuentas a pagar = Proveedores del Mayor",
                "Lo que se debe a los proveedores según sus cuentas corrientes es lo que figura en la contabilidad.",
                facturadoProveedores - pagado, "Suma de las cuentas de proveedores", HaberMenosDebe(PlanDeCuentas.Proveedores), "Mayor: Proveedores"));

            // 12 y 13. Cada factura y su asiento.
            var sinAsiento = await facturasVigentes.CountAsync(f => f.AsientoContableId == null
                || !_context.AsientosContables.Any(a => a.Id == f.AsientoContableId && a.Estado == EstadoAsiento.Confirmado), cancellationToken);
            controles.Add(Contar("facturas-con-asiento", "Cada factura vigente tiene su asiento confirmado",
                "No hay facturas sin registrar en la contabilidad.", sinAsiento, "Facturas sin asiento"));
            var anuladasMal = await _context.Facturas.AsNoTracking().CountAsync(f => f.Anulada && f.AsientoContableId != null
                && _context.AsientosContables.Any(a => a.Id == f.AsientoContableId && a.Estado != EstadoAsiento.Anulado), cancellationToken);
            controles.Add(Contar("anuladas-sin-efecto", "Las facturas anuladas ya no suman",
                "El asiento de toda factura anulada quedó anulado: no cuenta en el Diario ni en el Mayor.", anuladasMal, "Anuladas que todavía suman"));

            // 14 y 15. Stock.
            var stockNegativo = await _context.Productos.AsNoTracking().CountAsync(p => p.StockActual < 0, cancellationToken);
            controles.Add(Contar("stock-no-negativo", "Ningún producto tiene stock negativo",
                "No se vendió más de lo que había.", stockNegativo, "Productos con stock negativo"));
            var stockDesfasado = await _context.Productos.AsNoTracking().CountAsync(p =>
                _context.MovimientosStock.Any(m => m.ProductoId == p.Id)
                && _context.MovimientosStock.Where(m => m.ProductoId == p.Id).OrderByDescending(m => m.Id).Select(m => m.Saldo).FirstOrDefault() != p.StockActual, cancellationToken);
            controles.Add(Contar("stock-libro", "El Libro Stock cierra con el stock de cada producto",
                "El último saldo del libro de cada producto es el stock que muestra el inventario.", stockDesfasado, "Productos desfasados"));

            // 16. Numeración de ventas.
            var repetidas = await ventas.Where(f => f.NumeroComprobante != null)
                .GroupBy(f => new { f.TipoComprobante, f.PuntoVenta, f.NumeroComprobante })
                .Where(g => g.Count() > 1).CountAsync(cancellationToken);
            controles.Add(Contar("numeracion", "No hay facturas de venta con número repetido",
                "Cada tipo de comprobante tiene su numeración correlativa sin duplicados.", repetidas, "Números repetidos"));

            return new ResultadoIntegridad(DateTimeOffset.UtcNow, controles);
        }

        private static ControlDeIntegridad Comparar(string clave, string titulo, string explicacion, decimal obtenido, string etiquetaObtenido, decimal esperado, string etiquetaEsperado)
            => new(clave, titulo, explicacion, Math.Abs(obtenido - esperado) < Tolerancia, obtenido, esperado, etiquetaObtenido, etiquetaEsperado, EsImporte: true);

        private static ControlDeIntegridad Contar(string clave, string titulo, string explicacion, int casosMalos, string etiqueta)
            => new(clave, titulo, explicacion, casosMalos == 0, casosMalos, 0, etiqueta, "Esperado", EsImporte: false);
    }
}
