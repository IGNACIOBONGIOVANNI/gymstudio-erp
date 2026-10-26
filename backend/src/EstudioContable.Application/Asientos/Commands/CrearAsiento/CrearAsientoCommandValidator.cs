using FluentValidation;

namespace EstudioContable.Application.Asientos.Commands.CrearAsiento
{
    public class CrearAsientoCommandValidator : AbstractValidator<CrearAsientoCommand>
    {
        public CrearAsientoCommandValidator()
        {
            RuleFor(c => c.ClienteId)
                .GreaterThan(0).WithMessage("El Id del cliente es requerido y debe ser mayor a 0.");

            // El número de asiento lo asigna el sistema: no se valida lo que mande el cliente.

            RuleFor(c => c.Descripcion)
                .NotEmpty().WithMessage("La descripción es requerida.");

            RuleFor(c => c.Lineas)
                .NotNull().WithMessage("Se requiere la lista de líneas.")
                .Must(l => l != null && l.Count >= 2).WithMessage("Se requieren al menos dos líneas para un asiento.");
        }
    }
}
