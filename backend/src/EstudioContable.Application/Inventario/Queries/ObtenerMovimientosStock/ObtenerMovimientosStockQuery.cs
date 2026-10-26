using System.Collections.Generic;
using EstudioContable.Application.Inventario.Queries.DTOs;
using MediatR;

namespace EstudioContable.Application.Inventario.Queries.ObtenerMovimientosStock
{
    public record ObtenerMovimientosStockQuery(long ProductoId) : IRequest<List<MovimientoStockDto>>;
}
