using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Asientos.Queries.DTOs;
using EstudioContable.Application.Interfaces;
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
            // La agregación (SUM por cuenta de los asientos confirmados) la resuelve
            // la vista contabilidad.v_libro_mayor en la base de datos.
            return await _context.LibroMayor
                .Where(s => s.ClienteId == request.ClienteId)
                .OrderBy(s => s.Codigo)
                .Select(s => new SaldoMayorDto(s.CuentaId, s.Nombre, s.TotalDebe, s.TotalHaber, s.Saldo))
                .ToListAsync(cancellationToken);
        }
    }
}
