using System;
using EstudioContable.Domain.Enums;
using MediatR;

namespace EstudioContable.Application.Ventas.Commands
{
    public record CrearFacturaVentaCommand(
        long ClienteId,
        DateTime Fecha,
        string TipoFactura,
        string NumeroFactura,
        string ClienteNombre,
        string Cuit,
        System.Collections.Generic.List<ItemFacturaDto> Items,
        CondicionPago CondicionPago
    ) : IRequest<long>;

    public record ItemFacturaDto(long ProductoId, int Cantidad);
}
