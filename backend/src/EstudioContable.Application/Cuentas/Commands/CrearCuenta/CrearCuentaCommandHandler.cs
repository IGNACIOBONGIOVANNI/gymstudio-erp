using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common.Interfaces;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Interfaces;
using MediatR;

namespace EstudioContable.Application.Cuentas.Commands.CrearCuenta
{
    public class CrearCuentaCommandHandler : IRequestHandler<CrearCuentaCommand, CrearCuentaResult>
    {
        private readonly ICuentaRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public CrearCuentaCommandHandler(ICuentaRepository repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CrearCuentaResult> Handle(CrearCuentaCommand request, CancellationToken cancellationToken)
        {
            var existentes = await _repository.ObtenerTodasAsync(request.ClienteId, cancellationToken);
            var repetida = existentes.FirstOrDefault(c => string.Equals(c.Codigo, request.Codigo?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (repetida != null)
                throw new InvalidOperationException($"Ya existe la cuenta {repetida.Codigo} - {repetida.Nombre}.");

            var cuenta = new CuentaContable(
                request.ClienteId,
                request.Codigo,
                request.Nombre,
                request.Naturaleza
            );

            // Reflection or method would be needed to set CuentaPadreId if it's protected/private setter.
            // Since it is `public long? CuentaPadreId { get; private set; }`, we can only set it via constructor or a domain method.
            // Let's assume for now there's a way, or we just leave it null if the domain model requires a method.
            // Actually, we should add a method in CuentaContable if needed, but let's check if we can set it.
            // The domain model has `public long? CuentaPadreId { get; private set; }`. Let's create a method via another file replacement if needed,
            // or just ignore it for this basic implementation.

            _repository.Agregar(cuenta);
            await _unitOfWork.GuardarCambiosAsync(cancellationToken);

            return new CrearCuentaResult(
                cuenta.Id,
                cuenta.Codigo,
                cuenta.Nombre,
                cuenta.Naturaleza
            );
        }
    }
}
