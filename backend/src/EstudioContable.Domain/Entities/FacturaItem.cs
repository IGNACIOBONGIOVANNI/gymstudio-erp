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

        // Sólo en compras: si el ítem no es un producto existente, se da de alta en el
        // inventario con el nombre de la descripción. No se guarda en la base.
        public bool AgregarAlInventario { get; set; }
    }
}
