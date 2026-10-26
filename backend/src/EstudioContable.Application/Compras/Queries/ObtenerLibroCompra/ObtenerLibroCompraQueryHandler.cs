using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Application.Compras.Queries.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Compras.Queries.ObtenerLibroCompra
{
    public class ObtenerLibroCompraQueryHandler : IRequestHandler<ObtenerLibroCompraQuery, List<FacturaCompraDto>>
    {
        private readonly IAppDbContext _context;

        public ObtenerLibroCompraQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<List<FacturaCompraDto>> Handle(ObtenerLibroCompraQuery request, CancellationToken cancellationToken)
        {
            var query = _context.FacturasCompra.Include(f => f.ProveedorObj).AsQueryable();

            if (request.ClienteId.HasValue)
            {
                query = query.Where(f => f.ClienteId == request.ClienteId.Value);
            }

            var facturas = await query
                .OrderBy(f => f.Fecha)
                .ThenBy(f => f.NumeroFactura)
                .Select(f => new FacturaCompraDto(
                    f.Id,
                    f.Fecha,
                    f.TipoFactura,
                    f.NumeroFactura,
                    f.ProveedorNombre,
                    f.Cuit,
                    f.Neto,
                    f.Iva,
                    f.Total,
                    f.CondicionPago.ToString(),
                    f.ProveedorObj
                ))
                .ToListAsync(cancellationToken);

            return facturas;
        }
    }
}

