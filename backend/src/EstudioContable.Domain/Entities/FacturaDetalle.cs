using System;

namespace EstudioContable.Domain.Entities
{
    public class FacturaDetalle
    {
        public long Id { get; private set; }
        public long? FacturaVentaId { get; private set; }
        public long? FacturaCompraId { get; private set; }
        public long ProductoId { get; private set; }
        public int Cantidad { get; private set; }
        public decimal PrecioUnitario { get; private set; }
        public decimal Subtotal { get; private set; }

        public Producto Producto { get; private set; } = null!;

        protected FacturaDetalle() { }

        public FacturaDetalle(long productoId, int cantidad, decimal precioUnitario)
        {
            if (cantidad <= 0) throw new ArgumentException("La cantidad debe ser mayor a 0.");
            if (precioUnitario < 0) throw new ArgumentException("El precio unitario no puede ser negativo.");

            ProductoId = productoId;
            Cantidad = cantidad;
            PrecioUnitario = precioUnitario;
            Subtotal = cantidad * precioUnitario;
        }
    }
}
