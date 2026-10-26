using System;
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
    public class ObtenerEstadoSociosQueryHandler : IRequestHandler<ObtenerEstadoSociosQuery, List<SocioEstadoDto>>
    {
        private readonly IAppDbContext _context;

        public ObtenerEstadoSociosQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<List<SocioEstadoDto>> Handle(ObtenerEstadoSociosQuery request, CancellationToken cancellationToken)
        {
            var hoy = DateTime.UtcNow;

            return await _context.Socios
                .Include(s => s.Membresias)
                .OrderBy(s => s.Apellido).ThenBy(s => s.Nombre)
                .Select(s => new SocioEstadoDto
                {
                    Id = s.Id,
                    Nombre = s.Nombre,
                    Apellido = s.Apellido,
                    Dni = s.Dni,
                    Telefono = s.Telefono,
                    AlDia = s.Membresias.Any(m => m.EstaPagada && m.FechaVencimiento >= hoy)
                })
                .ToListAsync(cancellationToken);
        }
    }
}
