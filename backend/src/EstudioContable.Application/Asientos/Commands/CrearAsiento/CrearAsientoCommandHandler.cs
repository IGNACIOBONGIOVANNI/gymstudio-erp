using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common.Interfaces;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Interfaces;
using MediatR;

namespace EstudioContable.Application.Asientos.Commands.CrearAsiento
{
    public class CrearAsientoCommandHandler : IRequestHandler<CrearAsientoCommand, CrearAsientoResult>
    {
        private readonly IAsientoRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly EstudioContable.Application.Interfaces.IAppDbContext _context;

        public CrearAsientoCommandHandler(IAsientoRepository repository, IUnitOfWork unitOfWork, EstudioContable.Application.Interfaces.IAppDbContext context)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _context = context;
        }

        public async Task<CrearAsientoResult> Handle(CrearAsientoCommand request, CancellationToken cancellationToken)
        {
            if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(_context.Clientes, c => c.Id == request.ClienteId, cancellationToken))
                throw new EstudioContable.Application.Common.Exceptions.NotFoundException("Cliente", request.ClienteId);

            long numeroAsiento = await _repository.ObtenerMaximoNumeroAsientoAsync(request.ClienteId, cancellationToken) + 1;

            var asiento = new AsientoContable(
                request.ClienteId,
                numeroAsiento,
                request.Fecha,
                request.Descripcion,
                request.UsuarioCreadorId > 0 ? request.UsuarioCreadorId : 1 // fallback for anonymous/swagger tests
            );

            if (request.Lineas != null)
            {
                foreach (var linea in request.Lineas)
                {
                    asiento.AgregarLinea(linea.CuentaId, linea.Debe, linea.Haber, linea.Descripcion);
                }
            }

            _repository.Agregar(asiento);
            await _unitOfWork.GuardarCambiosAsync(cancellationToken);

            return new CrearAsientoResult(
                asiento.Id,
                asiento.NumeroAsiento,
                (EstudioContable.Domain.Enums.EstadoAsiento)(int)asiento.Estado,
                asiento.TotalDebe,
                asiento.TotalHaber,
                asiento.FechaCreacion
            );
        }
    }
}
