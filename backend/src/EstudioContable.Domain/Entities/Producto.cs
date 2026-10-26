using System;

namespace EstudioContable.Domain.Entities
{
    public class Producto
    {
        public long Id { get; private set; }
        public string Nombre { get; private set; }
        public decimal PrecioUnitario { get; private set; }
        public decimal PrecioVenta { get; private set; }
        public int StockActual { get; private set; }

        public string? Marca { get; private set; }
        public string? Sabor { get; private set; }
        public decimal? Calorias { get; private set; }
        public decimal? Proteinas { get; private set; }
        public decimal? Carbohidratos { get; private set; }
        public decimal? Grasas { get; private set; }

        // EF Core Constructor
        protected Producto() { }

        public Producto(string nombre, decimal precioUnitario, decimal precioVenta, int stockInicial = 0, string? marca = null, string? sabor = null, decimal? calorias = null, decimal? proteinas = null, decimal? carbohidratos = null, decimal? grasas = null)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                throw new ArgumentException("El nombre no puede estar vacío.", nameof(nombre));

            if (precioUnitario < 0)
                throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(precioUnitario));

            if (precioVenta < 0)
                throw new ArgumentException("El precio de venta no puede ser negativo.", nameof(precioVenta));

            if (stockInicial < 0)
                throw new ArgumentException("El stock inicial no puede ser negativo.", nameof(stockInicial));

            Nombre = nombre;
            PrecioUnitario = precioUnitario;
            PrecioVenta = precioVenta;
            StockActual = stockInicial;
            Marca = marca;
            Sabor = sabor;
            Calorias = calorias;
            Proteinas = proteinas;
            Carbohidratos = carbohidratos;
            Grasas = grasas;
        }

        public void ActualizarDatos(string nombre, decimal precioUnitario, decimal precioVenta, string? marca, string? sabor, decimal? calorias, decimal? proteinas, decimal? carbohidratos, decimal? grasas)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                throw new ArgumentException("El nombre no puede estar vacío.", nameof(nombre));
            if (precioUnitario < 0)
                throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(precioUnitario));
            if (precioVenta < 0)
                throw new ArgumentException("El precio de venta no puede ser negativo.", nameof(precioVenta));
            Nombre = nombre.Trim();
            PrecioUnitario = precioUnitario;
            PrecioVenta = precioVenta;
            Marca = marca;
            Sabor = sabor;
            Calorias = calorias;
            Proteinas = proteinas;
            Carbohidratos = carbohidratos;
            Grasas = grasas;
        }

        public void ActualizarStock(int cantidad)
        {
            var nuevoStock = StockActual + cantidad;
            if (nuevoStock < 0)
                throw new InvalidOperationException($"Stock insuficiente. El stock actual es {StockActual} y se intentó descontar {-cantidad}.");

            StockActual = nuevoStock;
        }
    }
}
