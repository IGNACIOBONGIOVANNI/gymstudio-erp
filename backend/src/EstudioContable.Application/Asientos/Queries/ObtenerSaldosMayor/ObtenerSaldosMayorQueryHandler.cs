using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Asientos.Queries.DTOs;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Asientos.Queries.ObtenerSaldosMayor
{
    public class ObtenerSaldosMayorQueryHandler : IRequestHandler<ObtenerSaldosMayorQuery, List<SaldoMayorDto>>
    {
        private readonly IAppDbContext _context;

        public ObtenerSaldosMayorQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<List<SaldoMayorDto>> Handle(ObtenerSaldosMayorQuery request, CancellationToken cancellationToken)
        {
            var lineas = await _context.LineasAsiento
                .Include(l => l.Cuenta)
                .Where(l => l.Asiento.ClienteId == request.ClienteId && l.Asiento.Estado == EstadoAsiento.Confirmado)
                .ToListAsync(cancellationToken);

            var saldos = lineas
                .GroupBy(l => l.Cuenta.Nombre) // Agrupamos SOLO por nombre para forzar la unificación
                .Select(g => new SaldoMayorDto(
                    g.First().CuentaId,
                    g.Key,
                    g.Sum(x => x.Debe),
                    g.Sum(x => x.Haber),
                    g.Sum(x => x.Debe) - g.Sum(x => x.Haber)
                ))
                .OrderBy(s => s.NombreCuenta)
                .ToList();

            return saldos;
        }
    }
}
