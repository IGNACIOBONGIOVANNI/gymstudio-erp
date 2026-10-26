using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using MediatR;

namespace EstudioContable.Application.Gimnasio.Socios.Commands
{
    public class ActualizarSocioCommandHandler : IRequestHandler<ActualizarSocioCommand, bool>
    {
        private readonly IAppDbContext _context;

        public ActualizarSocioCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        // Devuelve false si el socio no existe (el controller lo traduce a 404).
        public async Task<bool> Handle(ActualizarSocioCommand request, CancellationToken cancellationToken)
        {
            var socio = await _context.Socios.FindAsync(new object[] { request.Id }, cancellationToken);
            if (socio == null) return false;

            socio.ActualizarDatos(request.Nombre, request.Apellido, request.Dni, request.Telefono);
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
