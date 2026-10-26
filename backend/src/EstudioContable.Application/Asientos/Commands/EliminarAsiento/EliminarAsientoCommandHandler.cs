using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common;
using EstudioContable.Application.Common.Exceptions;
using EstudioContable.Application.Common.Interfaces;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Asientos.Commands.EliminarAsiento
{
    /// <summary>
    /// "Eliminar" un asiento respeta la trazabilidad contable:
    /// - Borrador: se borra físicamente (todavía no tiene efecto contable).
    /// - Confirmado de una factura: se anula la factura completa (contraasiento,
    ///   tesorería y stock), para que los libros no queden desfasados.
    /// - Confirmado manual: se genera su contraasiento.
    /// Un asiento confirmado nunca se borra.
    /// </summary>
    public class EliminarAsientoCommandHandler : IRequestHandler<EliminarAsientoCommand>
    {
        private readonly IAsientoRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAppDbContext _context;
        private readonly IFacturacionService _facturacionService;

        public EliminarAsientoCommandHandler(
            IAsientoRepository repository,
            IUnitOfWork unitOfWork,
            IAppDbContext context,
            IFacturacionService facturacionService)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _context = context;
            _facturacionService = facturacionService;
        }

        public async Task Handle(EliminarAsientoCommand request, CancellationToken cancellationToken)
        {
            var asiento = await _repository.ObtenerPorIdConLineasAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(AsientoContable), request.Id);

            if (asiento.Estado == EstadoAsiento.Borrador)
            {
                _repository.Eliminar(asiento);
                await _unitOfWork.GuardarCambiosAsync(cancellationToken);
                return;
            }

            if (asiento.Estado != EstadoAsiento.Confirmado)
                throw new InvalidOperationException($"El asiento N° {asiento.NumeroAsiento} ya está anulado.");

            if (asiento.AsientoRevertidoId.HasValue)
                throw new InvalidOperationException($"El asiento N° {asiento.NumeroAsiento} es un contraasiento y no puede revertirse.");

            var yaRevertido = await _context.AsientosContables
                .AnyAsync(a => a.AsientoRevertidoId == asiento.Id, cancellationToken);
            if (yaRevertido)
                throw new InvalidOperationException($"El asiento N° {asiento.NumeroAsiento} ya fue revertido.");

            var factura = await _context.Facturas
                .FirstOrDefaultAsync(f => f.AsientoContableId == asiento.Id && !f.Anulada, cancellationToken);

            if (factura != null)
            {
                await _facturacionService.AnularFacturaAsync(factura.Id, cancellationToken);
                return;
            }

            var numero = await _repository.ObtenerMaximoNumeroAsientoAsync(asiento.ClienteId, cancellationToken) + 1;
            var reverso = asiento.CrearContraasiento(numero, DateOnly.FromDateTime(DateTime.UtcNow), PlanDeCuentas.UsuarioSistemaId);
            reverso.Confirmar(PlanDeCuentas.UsuarioSistemaId);

            _repository.Agregar(reverso);
            await _unitOfWork.GuardarCambiosAsync(cancellationToken);
        }
    }
}
