using System.Collections.Generic;
using EstudioContable.Application.Ventas.Queries.DTOs;
using MediatR;

namespace EstudioContable.Application.Ventas.Queries.ObtenerLibroVenta
{
    public record ObtenerLibroVentaQuery(long? ClienteId) : IRequest<List<FacturaVentaDto>>;
}
