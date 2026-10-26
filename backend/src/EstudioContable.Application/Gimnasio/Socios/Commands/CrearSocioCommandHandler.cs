using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using MediatR;

namespace EstudioContable.Application.Gimnasio.Socios.Commands
{
    public class CrearSocioCommandHandler : IRequestHandler<CrearSocioCommand, long>
    {
        private readonly IAppDbContext _context;

        public CrearSocioCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<long> Handle(CrearSocioCommand request, CancellationToken cancellationToken)
        {
            await DniUnico.ValidarAsync(_context, request.Dni, idExcluido: null, cancellationToken);

            var socio = new Socio(
                request.Nombre,
                request.Apellido,
                request.Dni,
                request.Telefono,
                System.DateTime.UtcNow
            );

            _context.Socios.Add(socio);
            await _context.SaveChangesAsync(cancellationToken);

            return socio.Id;
        }
    }
}
