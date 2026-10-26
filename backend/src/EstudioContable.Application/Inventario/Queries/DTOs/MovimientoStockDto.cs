using System;

namespace EstudioContable.Application.Inventario.Queries.DTOs
{
    public class MovimientoStockDto
    {
        public long Id { get; set; }
        public DateTimeOffset Fecha { get; set; }
        public string Detalle { get; set; } = string.Empty;
        public int Ingreso { get; set; }
        public int Salida { get; set; }
        public int Saldo { get; set; }
    }
}
