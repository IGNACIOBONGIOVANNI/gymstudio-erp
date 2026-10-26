using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using MediatR;

namespace EstudioContable.Application.Gimnasio.Planes.Commands
{
    public class CrearPlanCommandHandler : IRequestHandler<CrearPlanCommand, long>
    {
        private readonly IAppDbContext _context;

        public CrearPlanCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<long> Handle(CrearPlanCommand request, CancellationToken cancellationToken)
        {
            var plan = new Plan(request.Nombre, request.PrecioMensual, request.DuracionMeses);
            
            _context.Planes.Add(plan);
            await _context.SaveChangesAsync(cancellationToken);

            return plan.Id;
        }
    }
}
