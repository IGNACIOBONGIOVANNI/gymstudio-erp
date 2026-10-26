using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using MediatR;
using System;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Notifications.Handlers
{
    public class ActualizarStockFacturaVentaHandler : INotificationHandler<FacturaVentaCreadaNotification>
    {
        private readonly IAppDbContext _context;

        public ActualizarStockFacturaVentaHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task Handle(FacturaVentaCreadaNotification notification, CancellationToken cancellationToken)
        {
            var factura = notification.Factura;

            foreach (var detalle in factura.Detalles)
            {
                var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Id == detalle.ProductoId, cancellationToken);
                if (producto != null)
                {
                    producto.ActualizarStock(-detalle.Cantidad);

                    var movimiento = new MovimientoStock(
                        producto.Id,
                        factura.Fecha,
                        $"Venta Factura {factura.TipoFactura} {factura.NumeroFactura}",
                        0,
                        detalle.Cantidad,
                        producto.StockActual
                    );

                    _context.MovimientosStock.Add(movimiento);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
