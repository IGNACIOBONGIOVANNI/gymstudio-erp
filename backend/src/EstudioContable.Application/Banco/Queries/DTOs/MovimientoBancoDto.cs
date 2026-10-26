namespace EstudioContable.Application.Banco.Queries.DTOs
{
    public record MovimientoBancoDto(
        long Id,
        System.DateTimeOffset Fecha,
        string Detalle,
        decimal? Ingreso,
        decimal? Egreso,
        long? FacturaId,
        bool EsTransferencia
    );
}
