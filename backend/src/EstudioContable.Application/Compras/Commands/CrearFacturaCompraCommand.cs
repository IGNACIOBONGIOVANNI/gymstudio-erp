using System;
using EstudioContable.Domain.Enums;
using MediatR;

namespace EstudioContable.Application.Compras.Commands
{
    public record CrearFacturaCompraCommand(
        long ClienteId,
        DateTime Fecha,
        string TipoFactura,
        string NumeroFactura,
        string ProveedorNombre,
        string Cuit,
        System.Collections.Generic.List<ItemFacturaDto> Items,
        CondicionPago CondicionPago
    ) : IRequest<long>;

    public record ItemFacturaDto(long ProductoId, int Cantidad);
}
