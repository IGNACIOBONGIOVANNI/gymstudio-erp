using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;
using MediatR;

namespace EstudioContable.Application.Notifications.Handlers
{
    public class FacturaVentaCreadaNotificationHandler : INotificationHandler<FacturaVentaCreadaNotification>
    {
        private readonly IAppDbContext _context;

        public FacturaVentaCreadaNotificationHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task Handle(FacturaVentaCreadaNotification notification, CancellationToken cancellationToken)
        {
            var factura = notification.Factura;

            if (factura.CondicionPago == CondicionPago.ContadoCaja)
            {
                var movimientoCaja = new MovimientoCaja(
                    factura.ClienteId.ToString(),
                    factura.Fecha,
                    $"Cobro Factura Venta N° {factura.NumeroFactura}",
                    TipoMovimientoCaja.Ingreso,
                    factura.Total,
                    factura.Id
                );
                _context.MovimientosCaja.Add(movimientoCaja);
            }
            else if (factura.CondicionPago == CondicionPago.TransferenciaBanco)
            {
                var movimientoBanco = new MovimientoBanco(
                    factura.ClienteId.ToString(),
                    factura.Fecha,
                    $"Transferencia Factura Venta N° {factura.NumeroFactura}",
                    TipoMovimientoBanco.Ingreso,
                    factura.Total,
                    factura.Id
                );
                _context.MovimientosBanco.Add(movimientoBanco);
            }

            if (factura.CondicionPago != CondicionPago.CuentaCorriente)
            {
                // Solo si generamos algún movimiento
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
