using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Application.Inventario.Queries.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Inventario.Queries.ObtenerProductos
{
    public class ObtenerProductosQueryHandler : IRequestHandler<ObtenerProductosQuery, List<ProductoDto>>
    {
        private readonly IAppDbContext _context;

        public ObtenerProductosQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProductoDto>> Handle(ObtenerProductosQuery request, CancellationToken cancellationToken)
        {
            return await _context.Productos
                .OrderBy(p => p.Nombre)
                .Select(p => new ProductoDto
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    PrecioUnitario = p.PrecioUnitario,
                    PrecioVenta = p.PrecioVenta,
                    StockActual = p.StockActual,
                    Marca = p.Marca,
                    Sabor = p.Sabor,
                    Calorias = p.Calorias,
                    Proteinas = p.Proteinas,
                    Carbohidratos = p.Carbohidratos,
                    Grasas = p.Grasas
                })
                .ToListAsync(cancellationToken);
        }
    }
}
