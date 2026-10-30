using System;

namespace EstudioContable.Domain.Entities
{
    public class Producto
    {
        // 200 es lo que admite el campo de descripción de Facturar, por donde también nacen productos.
        public const int LargoMaximoDelNombre = 200;
        public const int LargoMaximoDeMarcaYSabor = 100;

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

            Nombre = ReglasDeDatos.TextoRequerido(nombre, "El nombre", LargoMaximoDelNombre);
            PrecioUnitario = ReglasDeDatos.Importe(precioUnitario, "El precio unitario");
            PrecioVenta = ReglasDeDatos.Importe(precioVenta, "El precio de venta");
            StockActual = ReglasDeDatos.Entero(stockInicial, "El stock inicial", 0, ReglasDeDatos.StockMaximo);
            Marca = ReglasDeDatos.TextoOpcional(marca, "La marca", LargoMaximoDeMarcaYSabor);
            Sabor = ReglasDeDatos.TextoOpcional(sabor, "El sabor", LargoMaximoDeMarcaYSabor);
            Calorias = ReglasDeDatos.Nutricion(calorias, "Las calorías");
            Proteinas = ReglasDeDatos.Nutricion(proteinas, "Las proteínas");
            Carbohidratos = ReglasDeDatos.Nutricion(carbohidratos, "Los carbohidratos");
            Grasas = ReglasDeDatos.Nutricion(grasas, "Las grasas");
        }

        public void ActualizarDatos(string nombre, decimal precioUnitario, decimal precioVenta, string? marca, string? sabor, decimal? calorias, decimal? proteinas, decimal? carbohidratos, decimal? grasas)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                throw new ArgumentException("El nombre no puede estar vacío.", nameof(nombre));
            if (precioUnitario < 0)
                throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(precioUnitario));
            if (precioVenta < 0)
                throw new ArgumentException("El precio de venta no puede ser negativo.", nameof(precioVenta));
            Nombre = ReglasDeDatos.TextoRequerido(nombre, "El nombre", LargoMaximoDelNombre);
            PrecioUnitario = ReglasDeDatos.Importe(precioUnitario, "El precio unitario");
            PrecioVenta = ReglasDeDatos.Importe(precioVenta, "El precio de venta");
            Marca = ReglasDeDatos.TextoOpcional(marca, "La marca", LargoMaximoDeMarcaYSabor);
            Sabor = ReglasDeDatos.TextoOpcional(sabor, "El sabor", LargoMaximoDeMarcaYSabor);
            Calorias = ReglasDeDatos.Nutricion(calorias, "Las calorías");
            Proteinas = ReglasDeDatos.Nutricion(proteinas, "Las proteínas");
            Carbohidratos = ReglasDeDatos.Nutricion(carbohidratos, "Los carbohidratos");
            Grasas = ReglasDeDatos.Nutricion(grasas, "Las grasas");
        }

        public void ActualizarStock(int cantidad)
        {
            var nuevoStock = StockActual + cantidad;
            if (nuevoStock < 0)
                throw new InvalidOperationException($"Stock insuficiente. El stock actual es {StockActual} y se intentó descontar {-cantidad}.");

            StockActual = nuevoStock;
        }

        /// <summary>
        /// Un producto dado de alta desde una compra no tiene precio de venta: lo toma
        /// de su primera venta. Si ya tenía uno, no se modifica.
        /// </summary>
        public void CompletarPrecioVenta(decimal precio)
        {
            if (PrecioVenta == 0 && precio > 0)
                PrecioVenta = precio;
        }
    }
}
