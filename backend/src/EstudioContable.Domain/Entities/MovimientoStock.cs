using System;

namespace EstudioContable.Domain.Entities
{
    public class MovimientoStock
    {
        public long Id { get; private set; }
        public long ProductoId { get; private set; }
        public DateTimeOffset Fecha { get; private set; }
        public string Detalle { get; private set; }
        public int Ingreso { get; private set; }
        public int Salida { get; private set; }
        public int Saldo { get; private set; }

        public Producto Producto { get; private set; } = null!;

        // EF Core Constructor
        protected MovimientoStock() { }

        public MovimientoStock(long productoId, DateTimeOffset fecha, string detalle, int ingreso, int salida, int saldo)
        {
            if (string.IsNullOrWhiteSpace(detalle))
                throw new ArgumentException("El detalle no puede estar vacío.", nameof(detalle));

            if (ingreso < 0)
                throw new ArgumentException("El ingreso no puede ser negativo.", nameof(ingreso));

            if (salida < 0)
                throw new ArgumentException("La salida no puede ser negativa.", nameof(salida));

            if (ingreso == 0 && salida == 0)
                throw new ArgumentException("El movimiento debe tener un ingreso o una salida mayor a 0.");

            ProductoId = productoId;
            Fecha = fecha;
            Detalle = detalle;
            Ingreso = ingreso;
            Salida = salida;
            Saldo = saldo;
        }
    }
}
