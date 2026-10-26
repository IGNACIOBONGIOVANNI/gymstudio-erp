using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Gimnasio.Socios.Dtos;
using EstudioContable.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Gimnasio.Socios.Queries
{
    public class ObtenerSociosQueryHandler : IRequestHandler<ObtenerSociosQuery, List<SocioDto>>
    {
        private readonly IAppDbContext _context;

        public ObtenerSociosQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<List<SocioDto>> Handle(ObtenerSociosQuery request, CancellationToken cancellationToken)
        {
            return await _context.Socios
                .Include(s => s.PlanActualObj)
                .OrderBy(s => s.Apellido).ThenBy(s => s.Nombre)
                .Select(s => new SocioDto
                {
                    Id = s.Id,
                    Nombre = s.Nombre,
                    Apellido = s.Apellido,
                    Dni = s.Dni,
                    Telefono = s.Telefono,
                    Activo = s.Activo,
                    PlanActualNombre = s.PlanActualObj != null ? s.PlanActualObj.Nombre : null,
                    FechaVencimiento = s.FechaVencimiento,
                    Estado = s.Estado
                })
                .ToListAsync(cancellationToken);
        }
    }
}
