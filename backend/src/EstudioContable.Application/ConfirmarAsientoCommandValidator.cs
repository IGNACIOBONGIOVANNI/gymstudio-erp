using FluentValidation;

namespace EstudioContable.Application.Asientos.Commands.ConfirmarAsiento
{
    /// <summary>
    /// Validación de forma (no de negocio contable — eso es responsabilidad del dominio).
    /// Se ejecuta automáticamente vía el ValidationBehavior del pipeline de MediatR.
    /// </summary>
    public class ConfirmarAsientoCommandValidator : AbstractValidator<ConfirmarAsientoCommand>
    {
        public ConfirmarAsientoCommandValidator()
        {
            RuleFor(c => c.AsientoId)
                .GreaterThan(0).WithMessage("El Id del asiento es inválido.");

            RuleFor(c => c.UsuarioId)
                .GreaterThan(0).WithMessage("El Id del usuario que confirma es inválido.");
        }
    }
}
