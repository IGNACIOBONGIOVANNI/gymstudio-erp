using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Services
{
    public class FacturacionService : IFacturacionService
    {
        private readonly IAppDbContext _context;

        public FacturacionService(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<Factura> CrearFacturaAsync(Factura factura, CancellationToken cancellationToken)
        {
            // 1. Guardar la factura principal
            _context.Facturas.Add(factura);

            // 2. Generar el Asiento Contable (Libro Diario y Mayor)
            var maxNumero = await _context.AsientosContables
                .MaxAsync(a => (long?)a.NumeroAsiento, cancellationToken) ?? 0;
                
            var asiento = new AsientoContable(
                clienteId: 1,
                numeroAsiento: maxNumero + 1,
                fecha: DateOnly.FromDateTime(factura.Fecha),
                descripcion: $"Factura de {factura.TipoOperacion} {factura.TipoComprobante} - {factura.Cliente}",
                usuarioCreadorId: 1 
            );

            var cuentaCaja = await _context.CuentasContables.FirstOrDefaultAsync(c => c.Nombre.Contains("Caja"), cancellationToken);
            var cuentaBanco = await _context.CuentasContables.FirstOrDefaultAsync(c => c.Nombre.Contains("Banco"), cancellationToken);
            var cuentaVentas = await _context.CuentasContables.FirstOrDefaultAsync(c => c.Nombre.Contains("Ingreso") || c.Nombre.Contains("Venta") || c.Nombre.Contains("Cuota"), cancellationToken);

            long cuentaDebeId = factura.CondicionPago == "ContadoCaja" ? (cuentaCaja?.Id ?? 1) : (cuentaBanco?.Id ?? 2);
            long cuentaHaberId = cuentaVentas?.Id ?? 4; 

            asiento.AgregarLinea(cuentaId: cuentaDebeId, debe: factura.Total, haber: 0, descripcion: "Cobro de Factura");
            asiento.AgregarLinea(cuentaId: cuentaHaberId, debe: 0, haber: factura.Total, descripcion: "Ventas / Servicios");

            asiento.Confirmar(1);
            _context.AsientosContables.Add(asiento);

            // Guardar para asignar IDs
            await _context.SaveChangesAsync(cancellationToken);
            factura.AsientoContableId = asiento.Id;

            // 3. Generar el Movimiento de Tesorera (Libro Caja)
            if (factura.CondicionPago == "TransferenciaBanco")
            {
                var tipoMov = factura.TipoOperacion == "Venta" ? TipoMovimientoBanco.Ingreso : TipoMovimientoBanco.Egreso;
                var mov = new MovimientoBanco("1", factura.Fecha, $"Factura de {factura.TipoOperacion} N {factura.Id} - {factura.Cliente}", tipoMov, factura.Total, factura.Id);
                _context.MovimientosBanco.Add(mov);
            }
            else if (factura.CondicionPago == "ContadoCaja")
            {
                var tipoMov = factura.TipoOperacion == "Venta" ? TipoMovimientoCaja.Ingreso : TipoMovimientoCaja.Egreso;
                var mov = new MovimientoCaja("1", factura.Fecha, $"Factura de {factura.TipoOperacion} N {factura.Id} - {factura.Cliente}", tipoMov, factura.Total, factura.Id);
                _context.MovimientosCaja.Add(mov);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return factura;
        }
    }
}
