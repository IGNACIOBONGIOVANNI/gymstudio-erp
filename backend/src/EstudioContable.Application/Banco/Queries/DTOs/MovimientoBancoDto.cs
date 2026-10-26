namespace EstudioContable.Application.Banco.Queries.DTOs
{
    public record MovimientoBancoDto(
        long Id,
        string Detalle,
        decimal? Ingreso,
        decimal? Egreso
    );
}
