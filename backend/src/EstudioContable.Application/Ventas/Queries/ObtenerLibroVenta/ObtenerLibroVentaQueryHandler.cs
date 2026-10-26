using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Application.Ventas.Queries.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Ventas.Queries.ObtenerLibroVenta
{
    public class ObtenerLibroVentaQueryHandler : IRequestHandler<ObtenerLibroVentaQuery, List<FacturaVentaDto>>
    {
        private readonly IAppDbContext _context;

        public ObtenerLibroVentaQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<List<FacturaVentaDto>> Handle(ObtenerLibroVentaQuery request, CancellationToken cancellationToken)
        {
            var query = _context.FacturasVenta.AsQueryable();

            if (request.ClienteId.HasValue)
            {
                query = query.Where(f => f.ClienteId == request.ClienteId.Value);
            }

            var facturas = await query
                .OrderBy(f => f.Fecha)
                .ThenBy(f => f.NumeroFactura)
                .Select(f => new FacturaVentaDto(
                    f.Id,
                    f.Fecha,
                    f.TipoFactura,
                    f.NumeroFactura,
                    f.ClienteNombre,
                    f.Cuit,
                    f.Neto,
                    f.Iva,
                    f.Total,
                    f.CondicionPago.ToString()
                ))
                .ToListAsync(cancellationToken);

            return facturas;
        }
    }
}
