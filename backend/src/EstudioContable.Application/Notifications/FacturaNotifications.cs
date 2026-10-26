using EstudioContable.Domain.Entities;
using MediatR;

namespace EstudioContable.Application.Notifications
{
    public record FacturaVentaCreadaNotification(FacturaVenta Factura) : INotification;
    public record FacturaCompraCreadaNotification(FacturaCompra Factura) : INotification;
}
