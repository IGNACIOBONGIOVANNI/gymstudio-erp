using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Gimnasio.Planes.Dtos;
using EstudioContable.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Gimnasio.Planes.Queries
{
    public class ObtenerPlanesQueryHandler : IRequestHandler<ObtenerPlanesQuery, List<PlanDto>>
    {
        private readonly IAppDbContext _context;

        public ObtenerPlanesQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PlanDto>> Handle(ObtenerPlanesQuery request, CancellationToken cancellationToken)
        {
            return await _context.Planes
                .OrderBy(p => p.Nombre)
                .Select(p => new PlanDto
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    PrecioMensual = p.PrecioMensual,
                    DuracionMeses = p.DuracionMeses,
                    Activo = p.Activo
                })
                .ToListAsync(cancellationToken);
        }
    }
}
