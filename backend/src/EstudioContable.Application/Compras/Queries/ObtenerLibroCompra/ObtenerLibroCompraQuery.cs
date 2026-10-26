using System.Collections.Generic;
using EstudioContable.Application.Compras.Queries.DTOs;
using MediatR;

namespace EstudioContable.Application.Compras.Queries.ObtenerLibroCompra
{
    public record ObtenerLibroCompraQuery(long? ClienteId) : IRequest<List<FacturaCompraDto>>;
}
