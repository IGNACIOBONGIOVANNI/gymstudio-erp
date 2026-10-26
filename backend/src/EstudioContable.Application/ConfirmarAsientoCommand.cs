using MediatR;

namespace EstudioContable.Application.Asientos.Commands.ConfirmarAsiento
{
    /// <summary>
    /// Comando CQRS: solicita confirmar un asiento existente.
    /// UsuarioId identifica a quién ejecuta la acción (para auditoría),
    /// no necesariamente a quien creó el asiento originalmente.
    /// </summary>
    /// <param name="AsientoId">Id del asiento a confirmar.</param>
    /// <param name="UsuarioId">Id del usuario autenticado que confirma (viene del claim del JWT, nunca del body).</param>
    public record ConfirmarAsientoCommand(long AsientoId, long UsuarioId) : IRequest<ConfirmarAsientoResult>;
}
