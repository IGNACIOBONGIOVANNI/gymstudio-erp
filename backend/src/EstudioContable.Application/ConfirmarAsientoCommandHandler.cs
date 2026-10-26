using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common.Exceptions;
using EstudioContable.Application.Common.Interfaces;
using EstudioContable.Domain.Entities;
using EstadoAsiento = EstudioContable.Domain.Enums.EstadoAsiento;
using EstudioContable.Domain.Exceptions;
using EstudioContable.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EstudioContable.Application.Asientos.Commands.ConfirmarAsiento
{
    /// <summary>
    /// Orquesta el caso de uso "Confirmar Asiento":
    ///   1) Carga el agregado con sus líneas.
    ///   2) Delega la regla de partida doble al dominio (asiento.Confirmar()).
    ///   3) Persiste de forma transaccional vía Unit of Work.
    ///   4) Registra auditoría (logging + los campos de auditoría ya
    ///      seteados por el propio dominio: FechaConfirmacion / UsuarioConfirmacionId).
    ///
    /// El Handler NO contiene lógica contable: sólo coordina. Toda la regla
    /// de negocio (balance Debe=Haber) vive en AsientoContable.Confirmar().
    /// </summary>
    public class ConfirmarAsientoCommandHandler
        : IRequestHandler<ConfirmarAsientoCommand, ConfirmarAsientoResult>
    {
        private readonly IAsientoRepository _asientoRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ConfirmarAsientoCommandHandler> _logger;

        public ConfirmarAsientoCommandHandler(
            IAsientoRepository asientoRepository,
            IUnitOfWork unitOfWork,
            ILogger<ConfirmarAsientoCommandHandler> logger)
        {
            _asientoRepository = asientoRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<ConfirmarAsientoResult> Handle(
            ConfirmarAsientoCommand request,
            CancellationToken cancellationToken)
        {
            // 1) Cargar el agregado completo (con líneas) — sin esto, EstaBalanceado
            //    se calcularía sobre una colección vacía y rompería la regla.
            var asiento = await _asientoRepository.ObtenerPorIdConLineasAsync(request.AsientoId, cancellationToken)
                ?? throw new NotFoundException(nameof(AsientoContable), request.AsientoId);

            // 2) Delegar la validación contable al dominio. Si no balancea,
            //    AsientoDesbalanceadoException se propaga tal cual: el middleware
            //    de la API la traduce a 422 Unprocessable Entity con el detalle
            //    de la diferencia (ver TotalDebe/TotalHaber/Diferencia de la excepción).
            try
            {
                asiento.Confirmar(request.UsuarioId);
            }
            catch (AsientoDesbalanceadoException ex)
            {
                _logger.LogWarning(
                    "Intento fallido de confirmar Asiento {AsientoId}: Debe={TotalDebe} Haber={TotalHaber}",
                    request.AsientoId, ex.TotalDebe, ex.TotalHaber);
                throw;
            }

            // 3) Persistir de forma transaccional. AppDbContext.SaveChangesAsync
            //    ya incluye una revalidación de balance como última barrera antes
            //    de tocar la base de datos (defensa en profundidad).
            await _unitOfWork.GuardarCambiosAsync(cancellationToken);

            // 4) Auditoría: el "qué pasó y quién lo hizo" ya quedó grabado en las
            //    columnas del propio agregado (fecha_confirmacion, usuario_confirmacion_id);
            //    aquí sólo dejamos el registro de log de aplicación.
            _logger.LogInformation(
                "Asiento {AsientoId} (Nro. {NumeroAsiento}) confirmado por Usuario {UsuarioId} el {Fecha}",
                asiento.Id, asiento.NumeroAsiento, request.UsuarioId, asiento.FechaConfirmacion);

            return new ConfirmarAsientoResult(
                AsientoId: asiento.Id,
                NumeroAsiento: asiento.NumeroAsiento,
                Estado: (EstudioContable.Domain.Enums.EstadoAsiento)(int)asiento.Estado,
                FechaConfirmacion: asiento.FechaConfirmacion!.Value,
                UsuarioConfirmacionId: asiento.UsuarioConfirmacionId!.Value,
                TotalDebe: asiento.TotalDebe,
                TotalHaber: asiento.TotalHaber);
        }
    }
}
