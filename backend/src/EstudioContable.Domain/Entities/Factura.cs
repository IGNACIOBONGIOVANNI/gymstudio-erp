using System;
using System.Collections.Generic;

namespace EstudioContable.Domain.Entities
{
    public class Factura
    {
        public long Id { get; set; }
        public DateTime Fecha { get; set; }
        public string TipoOperacion { get; set; } = string.Empty;
        public string CondicionPago { get; set; } = string.Empty;
        public string TipoComprobante { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public long? ClienteComercialId { get; set; }
        public ClienteComercial? ClienteObj { get; set; }
        public long? ProveedorId { get; set; }
        public Proveedor? ProveedorObj { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public long? AsientoContableId { get; set; }

        public string? Cae { get; set; }
        public DateTime? FechaVencimientoCae { get; set; }

        public long? SocioId { get; set; }
        public Socio? SocioObj { get; set; }

        public List<FacturaItem> Items { get; set; } = new();
    }
}
