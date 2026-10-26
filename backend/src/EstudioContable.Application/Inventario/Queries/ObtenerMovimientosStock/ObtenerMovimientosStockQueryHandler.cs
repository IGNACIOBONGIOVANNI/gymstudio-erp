using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Application.Inventario.Queries.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Inventario.Queries.ObtenerMovimientosStock
{
    public class ObtenerMovimientosStockQueryHandler : IRequestHandler<ObtenerMovimientosStockQuery, List<MovimientoStockDto>>
    {
        private readonly IAppDbContext _context;

        public ObtenerMovimientosStockQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<List<MovimientoStockDto>> Handle(ObtenerMovimientosStockQuery request, CancellationToken cancellationToken)
        {
            return await _context.MovimientosStock
                .Where(m => m.ProductoId == request.ProductoId)
                .OrderBy(m => m.Fecha)
                .Select(m => new MovimientoStockDto
                {
                    Id = m.Id,
                    Fecha = m.Fecha,
                    Detalle = m.Detalle,
                    Ingreso = m.Ingreso,
                    Salida = m.Salida,
                    Saldo = m.Saldo
                })
                .ToListAsync(cancellationToken);
        }
    }
}
