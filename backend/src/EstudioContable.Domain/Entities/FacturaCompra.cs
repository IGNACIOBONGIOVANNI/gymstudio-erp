using System;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Domain.Entities
{
    public class FacturaCompra
    {
        public long Id { get; private set; }
        public long ClienteId { get; private set; }
        public DateTime Fecha { get; private set; }
        public string TipoFactura { get; private set; }
        public string NumeroFactura { get; private set; }
        public string ProveedorNombre { get; private set; }
        public long? ProveedorId { get; private set; }
        public Proveedor? ProveedorObj { get; private set; }
        public string Cuit { get; private set; }
        public decimal Neto { get; private set; }
        public decimal Iva { get; private set; }
        public decimal Total { get; private set; }
        public CondicionPago CondicionPago { get; private set; }
        public long? AsientoContableId { get; private set; }

        private readonly System.Collections.Generic.List<FacturaDetalle> _detalles = new();
        public System.Collections.Generic.IReadOnlyCollection<FacturaDetalle> Detalles => _detalles.AsReadOnly();

        protected FacturaCompra() { } // EF Core

        // Constructor para facturas con ítems (las nuevas)
        public FacturaCompra(long clienteId, DateTime fecha, string tipoFactura, string numeroFactura, string proveedorNombre, string cuit, CondicionPago condicionPago = CondicionPago.CuentaCorriente)
        {
            if (string.IsNullOrWhiteSpace(tipoFactura)) throw new ArgumentException("El tipo de factura es requerido.");
            if (string.IsNullOrWhiteSpace(numeroFactura)) throw new ArgumentException("El número de factura es requerido.");
            if (string.IsNullOrWhiteSpace(proveedorNombre)) throw new ArgumentException("El nombre del proveedor es requerido.");
            if (string.IsNullOrWhiteSpace(cuit)) throw new ArgumentException("El CUIT es requerido.");

            ClienteId = clienteId;
            Fecha = fecha;
            TipoFactura = tipoFactura;
            NumeroFactura = numeroFactura;
            ProveedorNombre = proveedorNombre;
            Cuit = cuit;
            CondicionPago = condicionPago;
            Neto = 0;
            Iva = 0;
            Total = 0;
        }

        // Constructor para importaciones legacy sin detalle
        public FacturaCompra(long clienteId, DateTime fecha, string tipoFactura, string numeroFactura, string proveedorNombre, string cuit, decimal neto, decimal iva, decimal total, CondicionPago condicionPago = CondicionPago.CuentaCorriente)
        {
            if (string.IsNullOrWhiteSpace(tipoFactura)) throw new ArgumentException("El tipo de factura es requerido.");
            if (string.IsNullOrWhiteSpace(numeroFactura)) throw new ArgumentException("El número de factura es requerido.");
            if (string.IsNullOrWhiteSpace(proveedorNombre)) throw new ArgumentException("El nombre del proveedor es requerido.");
            if (string.IsNullOrWhiteSpace(cuit)) throw new ArgumentException("El CUIT es requerido.");

            ClienteId = clienteId;
            Fecha = fecha;
            TipoFactura = tipoFactura;
            NumeroFactura = numeroFactura;
            ProveedorNombre = proveedorNombre;
            Cuit = cuit;
            Neto = neto;
            Iva = iva;
            Total = total;
            CondicionPago = condicionPago;
        }

        public void AgregarDetalle(Producto producto, int cantidad)
        {
            var detalle = new FacturaDetalle(producto.Id, cantidad, producto.PrecioUnitario); // Compras usan precio de costo
            _detalles.Add(detalle);

            Neto += detalle.Subtotal;
            Iva = Math.Round(Neto * 0.21m, 2);
            Total = Neto + Iva;
        }

        public void AsignarAsiento(long asientoId)
        {
            AsientoContableId = asientoId;
        }
    }
}

