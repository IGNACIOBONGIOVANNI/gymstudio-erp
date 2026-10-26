using System;
using System.Collections.Generic;

namespace EstudioContable.Domain.Entities
{
    public class FacturaItem
    {
        public long Id { get; set; }
        public long FacturaId { get; set; }
        public Factura Factura { get; set; } = null!;
        // Producto de inventario asociado (null para servicios o ítems libres).
        // Permite revertir el stock al anular o modificar la factura.
        public long? ProductoId { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}
