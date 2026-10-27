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

        // Número del comprobante: punto de venta + número (ej. 0003-00001234).
        // En las ventas lo asigna el sistema (correlativo por tipo y punto de venta);
        // en las compras es el número de la factura del proveedor.
        public int? PuntoVenta { get; set; }
        public long? NumeroComprobante { get; set; }

        public string? NumeroFormateado => PuntoVenta.HasValue && NumeroComprobante.HasValue
            ? $"{PuntoVenta.Value:D4}-{NumeroComprobante.Value:D8}"
            : null;
        public string Cliente { get; set; } = string.Empty;
        public long? ClienteComercialId { get; set; }
        public ClienteComercial? ClienteObj { get; set; }
        public long? ProveedorId { get; set; }
        public Proveedor? ProveedorObj { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public long? AsientoContableId { get; set; }

        // Las facturas no se borran: se anulan, y sus efectos contables,
        // de tesorería y de stock se revierten con movimientos inversos.
        public bool Anulada { get; set; }
        public DateTime? FechaAnulacion { get; set; }

        public string? Cae { get; set; }
        public DateTime? FechaVencimientoCae { get; set; }

        public long? SocioId { get; set; }
        public Socio? SocioObj { get; set; }

        public List<FacturaItem> Items { get; set; } = new();
    }
}
