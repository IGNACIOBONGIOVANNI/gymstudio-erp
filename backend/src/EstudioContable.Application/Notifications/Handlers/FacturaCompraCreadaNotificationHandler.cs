using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;
using MediatR;

namespace EstudioContable.Application.Notifications.Handlers
{
    public class FacturaCompraCreadaNotificationHandler : INotificationHandler<FacturaCompraCreadaNotification>
    {
        private readonly IAppDbContext _context;

        public FacturaCompraCreadaNotificationHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task Handle(FacturaCompraCreadaNotification notification, CancellationToken cancellationToken)
        {
            var factura = notification.Factura;

            if (factura.CondicionPago == CondicionPago.ContadoCaja)
            {
                var movimientoCaja = new MovimientoCaja(
                    factura.ClienteId.ToString(),
                    factura.Fecha,
                    $"Pago Factura Compra N° {factura.NumeroFactura}",
                    TipoMovimientoCaja.Egreso,
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
                    $"Transferencia Factura Compra N° {factura.NumeroFactura}",
                    TipoMovimientoBanco.Egreso,
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
