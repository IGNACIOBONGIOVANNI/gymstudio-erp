using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using MediatR;

namespace EstudioContable.Application.Gimnasio.Planes.Commands
{
    public class ActualizarPlanCommandHandler : IRequestHandler<ActualizarPlanCommand, bool>
    {
        private readonly IAppDbContext _context;

        public ActualizarPlanCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        // Devuelve false si el plan no existe (el controller lo traduce a 404).
        public async Task<bool> Handle(ActualizarPlanCommand request, CancellationToken cancellationToken)
        {
            var plan = await _context.Planes.FindAsync(new object[] { request.Id }, cancellationToken);
            if (plan == null) return false;

            plan.Actualizar(request.Nombre, request.PrecioMensual, request.DuracionMeses);
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
