namespace EstudioContable.Application.Caja.Queries.DTOs
{
    public record MovimientoCajaDto(
        long Id,
        System.DateTimeOffset Fecha,
        string Detalle,
        decimal? Ingreso,
        decimal? Egreso,
        long? FacturaId,
        bool EsTransferencia
    );
}
