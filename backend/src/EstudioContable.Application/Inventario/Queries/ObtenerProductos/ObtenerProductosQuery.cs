using System.Collections.Generic;
using EstudioContable.Application.Inventario.Queries.DTOs;
using MediatR;

namespace EstudioContable.Application.Inventario.Queries.ObtenerProductos
{
    public record ObtenerProductosQuery() : IRequest<List<ProductoDto>>;
}
