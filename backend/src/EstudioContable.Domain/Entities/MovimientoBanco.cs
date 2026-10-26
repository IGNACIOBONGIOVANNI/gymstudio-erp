using System;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Domain.Entities
{
    public class MovimientoBanco
    {
        public long Id { get; private set; }
        public string ClienteId { get; private set; }
        public DateTime Fecha { get; private set; }
        public string Detalle { get; private set; }
        public TipoMovimientoBanco TipoMovimiento { get; private set; }
        public decimal Importe { get; private set; }
        public long? FacturaId { get; private set; }

        private MovimientoBanco() { } // Para EF Core

        public MovimientoBanco(string clienteId, DateTime fecha, string detalle, TipoMovimientoBanco tipoMovimiento, decimal importe, long? facturaId = null)
        {
            if (string.IsNullOrWhiteSpace(clienteId)) throw new ArgumentException("El ClienteId es requerido", nameof(clienteId));
            if (string.IsNullOrWhiteSpace(detalle)) throw new ArgumentException("El detalle es requerido", nameof(detalle));
            if (importe <= 0) throw new ArgumentException("El importe debe ser mayor a cero", nameof(importe));

            ClienteId = clienteId;
            Fecha = fecha;
            Detalle = detalle;
            TipoMovimiento = tipoMovimiento;
            Importe = importe;
            FacturaId = facturaId;
        }

        public void Actualizar(DateTime fecha, string detalle, TipoMovimientoBanco tipoMovimiento, decimal importe)
        {
            if (string.IsNullOrWhiteSpace(detalle)) throw new ArgumentException("El detalle es requerido", nameof(detalle));
            if (importe <= 0) throw new ArgumentException("El importe debe ser mayor a cero", nameof(importe));

            Fecha = fecha;
            Detalle = detalle;
            TipoMovimiento = tipoMovimiento;
            Importe = importe;
        }
    }
}
