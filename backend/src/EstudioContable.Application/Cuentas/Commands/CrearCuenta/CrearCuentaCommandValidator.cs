using FluentValidation;

namespace EstudioContable.Application.Cuentas.Commands.CrearCuenta
{
    public class CrearCuentaCommandValidator : AbstractValidator<CrearCuentaCommand>
    {
        public CrearCuentaCommandValidator()
        {
            RuleFor(c => c.ClienteId)
                .GreaterThan(0).WithMessage("El Id del cliente es requerido y debe ser mayor a 0.");

            RuleFor(c => c.Codigo)
                .NotEmpty().WithMessage("El código de la cuenta es requerido.");

            RuleFor(c => c.Nombre)
                .NotEmpty().WithMessage("El nombre de la cuenta es requerido.");
                
            RuleFor(c => c.Naturaleza)
                .IsInEnum().WithMessage("La naturaleza de la cuenta no es válida.");
        }
    }
}
