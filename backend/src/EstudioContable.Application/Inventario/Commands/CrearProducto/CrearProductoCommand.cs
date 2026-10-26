using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain.Entities;
using MediatR;

namespace EstudioContable.Application.Inventario.Commands.CrearProducto
{
    public class CrearProductoCommand : IRequest<long>
    {
        public string Nombre { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public decimal PrecioVenta { get; set; }
        public int StockInicial { get; set; }
        public string? Marca { get; set; }
        public string? Sabor { get; set; }
        public decimal? Calorias { get; set; }
        public decimal? Proteinas { get; set; }
        public decimal? Carbohidratos { get; set; }
        public decimal? Grasas { get; set; }
    }

    public class CrearProductoCommandHandler : IRequestHandler<CrearProductoCommand, long>
    {
        private readonly IAppDbContext _context;

        public CrearProductoCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<long> Handle(CrearProductoCommand request, CancellationToken cancellationToken)
        {
            var producto = new Producto(
                request.Nombre,
                request.PrecioUnitario,
                request.PrecioVenta,
                request.StockInicial,
                request.Marca,
                request.Sabor,
                request.Calorias,
                request.Proteinas,
                request.Carbohidratos,
                request.Grasas
            );

            _context.Productos.Add(producto);

            if (request.StockInicial > 0)
            {
                var movimiento = new MovimientoStock(
                    producto.Id, // EF Core asume esto post-guardado, pero mejor lo hacemos con ID auto
                    System.DateTimeOffset.UtcNow,
                    "Stock Inicial",
                    request.StockInicial,
                    0,
                    request.StockInicial
                );
                // Trick: necesitamos el ID del producto, EF Core lo inyecta si hacemos SaveChanges
                // pero si el generador de IDs es Identity, necesitamos guardarlo primero
                await _context.SaveChangesAsync(cancellationToken);
                
                movimiento = new MovimientoStock(producto.Id, System.DateTimeOffset.UtcNow, "Stock Inicial", request.StockInicial, 0, request.StockInicial);
                _context.MovimientosStock.Add(movimiento);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return producto.Id;
        }
    }
}
