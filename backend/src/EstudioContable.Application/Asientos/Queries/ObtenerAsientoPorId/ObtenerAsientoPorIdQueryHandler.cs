using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Asientos.Queries.DTOs;
using EstudioContable.Domain.Interfaces;
using MediatR;
using EstadoAsiento = EstudioContable.Domain.Enums.EstadoAsiento;

namespace EstudioContable.Application.Asientos.Queries.ObtenerAsientoPorId
{
    public class ObtenerAsientoPorIdQueryHandler : IRequestHandler<ObtenerAsientoPorIdQuery, AsientoDto?>
    {
        private readonly IAsientoRepository _repository;
        private readonly EstudioContable.Application.Interfaces.IAppDbContext _context;

        public ObtenerAsientoPorIdQueryHandler(IAsientoRepository repository, EstudioContable.Application.Interfaces.IAppDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        public async Task<AsientoDto?> Handle(ObtenerAsientoPorIdQuery request, CancellationToken cancellationToken)
        {
            var asiento = await _repository.ObtenerPorIdConLineasAsync(request.AsientoId, cancellationToken);
            if (asiento == null)
                return null;

            var cuentaIds = asiento.Lineas.Select(l => l.CuentaId).Distinct().ToList();
            var cuentas = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToDictionaryAsync(
                System.Linq.Queryable.Where(_context.CuentasContables, c => cuentaIds.Contains(c.Id)),
                c => c.Id,
                c => c.Nombre,
                cancellationToken
            );

            return new AsientoDto(
                asiento.Id,
                asiento.ClienteId,
                asiento.NumeroAsiento,
                asiento.Fecha,
                asiento.Descripcion,
                (EstadoAsiento)(int)asiento.Estado, // Cast para evitar conflictos si hay un enum local
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
            );
        }
    }
}
