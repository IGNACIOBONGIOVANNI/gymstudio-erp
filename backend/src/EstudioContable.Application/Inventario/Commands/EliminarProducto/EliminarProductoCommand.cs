using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace EstudioContable.Application.Inventario.Commands.EliminarProducto
{
    public class EliminarProductoCommand : IRequest<bool>
    {
        public long Id { get; set; }
        public EliminarProductoCommand(long id) { Id = id; }
    }

    public class EliminarProductoCommandHandler : IRequestHandler<EliminarProductoCommand, bool>
    {
        private readonly IAppDbContext _context;

        public EliminarProductoCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(EliminarProductoCommand request, CancellationToken cancellationToken)
        {
            var producto = await _context.Productos.FindAsync(new object[] { request.Id }, cancellationToken);
            if (producto == null) return false;

            // Las líneas de facturas viejas (FacturaDetalles) se borrarían en cascada:
            // un producto con historial de ventas o compras no se elimina.
            var tieneHistorial = await _context.FacturaItems.AnyAsync(i => i.ProductoId == producto.Id, cancellationToken)
                || await _context.FacturaDetalles.AnyAsync(d => d.ProductoId == producto.Id, cancellationToken);
            if (tieneHistorial)
                throw new System.InvalidOperationException($"El producto \"{producto.Nombre}\" tiene ventas o compras registradas y no se puede eliminar.");

            var movimientos = await _context.MovimientosStock.Where(m => m.ProductoId == producto.Id).ToListAsync(cancellationToken);
            _context.MovimientosStock.RemoveRange(movimientos);

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
