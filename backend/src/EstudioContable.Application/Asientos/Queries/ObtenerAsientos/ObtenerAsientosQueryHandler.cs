using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Asientos.Queries.DTOs;
using EstudioContable.Domain.Interfaces;
using MediatR;
using EstadoAsiento = EstudioContable.Domain.Enums.EstadoAsiento;

namespace EstudioContable.Application.Asientos.Queries.ObtenerAsientos
{
    public class ObtenerAsientosQueryHandler : IRequestHandler<ObtenerAsientosQuery, List<AsientoDto>>
    {
        private readonly IAsientoRepository _repository;
        private readonly EstudioContable.Application.Interfaces.IAppDbContext _context;

        public ObtenerAsientosQueryHandler(IAsientoRepository repository, EstudioContable.Application.Interfaces.IAppDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        public async Task<List<AsientoDto>> Handle(ObtenerAsientosQuery request, CancellationToken cancellationToken)
        {
            var asientos = await _repository.ObtenerTodosAsync(request.ClienteId, cancellationToken);

            var cuentaIds = asientos.SelectMany(a => a.Lineas).Select(l => l.CuentaId).Distinct().ToList();
            var cuentas = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToDictionaryAsync(
                System.Linq.Queryable.Where(_context.CuentasContables, c => cuentaIds.Contains(c.Id)),
                c => c.Id,
                c => c.Nombre,
                cancellationToken
            );

            return asientos.Select(asiento => new AsientoDto(
                asiento.Id,
                asiento.ClienteId,
                asiento.NumeroAsiento,
                asiento.Fecha,
                asiento.Descripcion,
                (EstadoAsiento)(int)asiento.Estado,
                asiento.UsuarioCreadorId,
                asiento.FechaCreacion,
                asiento.FechaConfirmacion,
                asiento.UsuarioConfirmacionId,
                asiento.TotalDebe,
                asiento.TotalHaber,
                asiento.EstaBalanceado,
                asiento.Lineas.Select(l => new LineaAsientoDto(
                    l.Id,
                    l.CuentaId,
                    cuentas.TryGetValue(l.CuentaId, out var nombre) ? nombre : "Cuenta " + l.CuentaId,
                    l.Debe,
                    l.Haber,
                    l.Descripcion,
                    l.Orden
                )).ToList()
            )).ToList();
        }
    }
}
