using System;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Domain.Entities
{
    public class MovimientoCaja : IMovimientoTesoreria
    {
        public long Id { get; private set; }
        public string ClienteId { get; private set; }
        public DateTime Fecha { get; private set; }
        public string Detalle { get; private set; }
        public TipoMovimientoCaja TipoMovimiento { get; private set; }
        public decimal Importe { get; private set; }
        public long? FacturaId { get; private set; }
        public long? AsientoContableId { get; private set; }

        // Cliente al que se le cobró o proveedor al que se le pagó: permite armar su cuenta corriente.
        public long? ClienteComercialId { get; private set; }
        public long? ProveedorId { get; private set; }

        public bool EsIngreso => TipoMovimiento == TipoMovimientoCaja.Ingreso;

        private MovimientoCaja() { } // Para EF Core

        public MovimientoCaja(string clienteId, DateTime fecha, string detalle, TipoMovimientoCaja tipoMovimiento, decimal importe, long? facturaId = null)
        {
            if (string.IsNullOrWhiteSpace(clienteId)) throw new ArgumentException("El ClienteId es requerido", nameof(clienteId));
            if (string.IsNullOrWhiteSpace(detalle)) throw new ArgumentException("El detalle es requerido", nameof(detalle));
            if (importe <= 0) throw new ArgumentException("El importe debe ser mayor a cero", nameof(importe));
            Validar(fecha, detalle, tipoMovimiento, importe);

            ClienteId = clienteId;
            Fecha = fecha;
            Detalle = detalle;
            TipoMovimiento = tipoMovimiento;
            Importe = importe;
            FacturaId = facturaId;
        }

        public void Actualizar(DateTime fecha, string detalle, TipoMovimientoCaja tipoMovimiento, decimal importe)
        {
            if (string.IsNullOrWhiteSpace(detalle)) throw new ArgumentException("El detalle es requerido", nameof(detalle));
            if (importe <= 0) throw new ArgumentException("El importe debe ser mayor a cero", nameof(importe));
            Validar(fecha, detalle, tipoMovimiento, importe);

            Fecha = fecha;
            Detalle = detalle;
            TipoMovimiento = tipoMovimiento;
            Importe = importe;
        }

        public void ActualizarDatos(DateTime fecha, string detalle, bool esIngreso, decimal importe)
            => Actualizar(fecha, detalle, esIngreso ? TipoMovimientoCaja.Ingreso : TipoMovimientoCaja.Egreso, importe);

        public void AsignarAsiento(long asientoId) => AsientoContableId = asientoId;

        public void AsignarTercero(long? clienteComercialId, long? proveedorId)
        {
            if (clienteComercialId.HasValue && proveedorId.HasValue)
                throw new ArgumentException("Un movimiento corresponde a un cliente o a un proveedor, no a los dos.");
            ClienteComercialId = clienteComercialId;
            ProveedorId = proveedorId;
        }

        private static void Validar(DateTime fecha, string detalle, TipoMovimientoCaja tipoMovimiento, decimal importe)
        {
            if (!Enum.IsDefined(tipoMovimiento))
                throw new ArgumentException("El tipo de movimiento debe ser Ingreso o Egreso.");
            if (fecha.Year < 2000 || fecha.Year > 2100)
                throw new ArgumentException("La fecha del movimiento es obligatoria y debe ser válida.");
            if (detalle.Trim().Length > 255)
                throw new ArgumentException("El detalle no puede superar los 255 caracteres.");
            if (importe > 999_999_999_999.99m)
                throw new ArgumentException("El importe es demasiado grande.");
        }
    }
}
