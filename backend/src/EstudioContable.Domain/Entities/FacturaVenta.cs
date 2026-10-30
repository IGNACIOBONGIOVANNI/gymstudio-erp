using System;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Domain.Entities
{
    public class FacturaVenta
    {
        public long Id { get; private set; }
        public long ClienteId { get; private set; }
        public DateTime Fecha { get; private set; }
        public string TipoFactura { get; private set; }
        public string NumeroFactura { get; private set; }
        public string ClienteNombre { get; private set; }
        public string Cuit { get; private set; }
        public decimal Neto { get; private set; }
        public decimal Iva { get; private set; }
        public decimal Total { get; private set; }
        public CondicionPago CondicionPago { get; private set; }
        public long? AsientoContableId { get; private set; }

        private readonly System.Collections.Generic.List<FacturaDetalle> _detalles = new();
        public System.Collections.Generic.IReadOnlyCollection<FacturaDetalle> Detalles => _detalles.AsReadOnly();

        protected FacturaVenta() { } // EF Core

        // Constructor para facturas con ítems (las nuevas)
        public FacturaVenta(long clienteId, DateTime fecha, string tipoFactura, string numeroFactura, string clienteNombre, string cuit, CondicionPago condicionPago = CondicionPago.CuentaCorriente)
        {
            Validar(fecha, tipoFactura, numeroFactura, clienteNombre, cuit, condicionPago);

            ClienteId = clienteId;
            Fecha = fecha;
            TipoFactura = ReglasDeComprobante.NormalizarTipo(tipoFactura);
            NumeroFactura = numeroFactura.Trim();
            ClienteNombre = clienteNombre.Trim();
            Cuit = EstudioContable.Domain.Cuit.FormatearDocumento(cuit);   // sólo dígitos (y guiones del CUIT)
            CondicionPago = condicionPago;
            Neto = 0;
            Iva = 0;
            Total = 0;
        }

        // Constructor para importaciones legacy sin detalle
        public FacturaVenta(long clienteId, DateTime fecha, string tipoFactura, string numeroFactura, string clienteNombre, string cuit, decimal neto, decimal iva, decimal total, CondicionPago condicionPago = CondicionPago.CuentaCorriente)
        {
            Validar(fecha, tipoFactura, numeroFactura, clienteNombre, cuit, condicionPago);

            ClienteId = clienteId;
            Fecha = fecha;
            TipoFactura = ReglasDeComprobante.NormalizarTipo(tipoFactura);
            NumeroFactura = numeroFactura.Trim();
            ClienteNombre = clienteNombre.Trim();
            Cuit = EstudioContable.Domain.Cuit.FormatearDocumento(cuit);   // sólo dígitos (y guiones del CUIT)
            var errorImportes = ReglasDeComprobante.ValidarImportes(neto, iva, total);
            if (errorImportes != null) throw new ArgumentException(errorImportes);
            Neto = neto;
            Iva = iva;
            Total = total;
            CondicionPago = condicionPago;
        }

        private static void Validar(DateTime fecha, string? tipoFactura, string? numeroFactura, string? nombre, string? documento, CondicionPago condicionPago)
        {
            var error = ReglasDeComprobante.ValidarCabecera(fecha, tipoFactura, numeroFactura, nombre, documento);
            if (error != null) throw new ArgumentException(error);
            if (!Enum.IsDefined(condicionPago)) throw new ArgumentException("La condición de pago no es válida.");
        }

        public void AgregarDetalle(Producto producto, int cantidad)
        {
            var detalle = new FacturaDetalle(producto.Id, cantidad, producto.PrecioVenta);
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
