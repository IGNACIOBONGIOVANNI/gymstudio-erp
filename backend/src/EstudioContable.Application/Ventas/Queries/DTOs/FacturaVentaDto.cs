using System;

namespace EstudioContable.Application.Ventas.Queries.DTOs
{
    public record FacturaVentaDto(
        long Id,
        DateTime Fecha,
        string TipoFactura,
        string NumeroFactura,
        string ClienteNombre,
        string Cuit,
        decimal Neto,
        decimal Iva,
        decimal Total,
        string CondicionPago
    );
}
