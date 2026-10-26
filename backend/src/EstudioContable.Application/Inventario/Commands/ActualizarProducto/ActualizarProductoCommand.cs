using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Inventario.Commands.ActualizarProducto
{
    public class ActualizarProductoCommand : IRequest<bool>
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public decimal PrecioVenta { get; set; }
        public string? Marca { get; set; }
        public string? Sabor { get; set; }
        public decimal? Calorias { get; set; }
        public decimal? Proteinas { get; set; }
        public decimal? Carbohidratos { get; set; }
        public decimal? Grasas { get; set; }
    }

    public class ActualizarProductoCommandHandler : IRequestHandler<ActualizarProductoCommand, bool>
    {
        private readonly IAppDbContext _context;

        public ActualizarProductoCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(ActualizarProductoCommand request, CancellationToken cancellationToken)
        {
            var producto = await _context.Productos.FindAsync(new object[] { request.Id }, cancellationToken);
            if (producto == null) return false;

            producto.ActualizarDatos(
                request.Nombre,
                request.PrecioUnitario,
                request.PrecioVenta,
                request.Marca,
                request.Sabor,
                request.Calorias,
                request.Proteinas,
                request.Carbohidratos,
                request.Grasas
            );

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
