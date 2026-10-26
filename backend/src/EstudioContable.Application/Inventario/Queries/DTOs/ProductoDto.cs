using System;

namespace EstudioContable.Application.Inventario.Queries.DTOs
{
    public class ProductoDto
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; } // Precio de Costo (para compras)
        public decimal PrecioVenta { get; set; }
        public int StockActual { get; set; }
        public string? Marca { get; set; }
        public string? Sabor { get; set; }
        public decimal? Calorias { get; set; }
        public decimal? Proteinas { get; set; }
        public decimal? Carbohidratos { get; set; }
        public decimal? Grasas { get; set; }
    }
}
