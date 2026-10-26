using System;

namespace EstudioContable.Application.Compras.Queries.DTOs
{
    public record FacturaCompraDto(
        long Id,
        DateTime Fecha,
        string TipoFactura,
        string NumeroFactura,
        string ProveedorNombre,
        string Cuit,
        decimal Neto,
        decimal Iva,
        decimal Total,
        string CondicionPago,
        EstudioContable.Domain.Entities.Proveedor? Proveedor = null
    );
}

