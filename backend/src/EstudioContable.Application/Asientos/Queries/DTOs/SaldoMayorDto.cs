namespace EstudioContable.Application.Asientos.Queries.DTOs
{
    public record SaldoMayorDto(
        long CuentaId,
        string NombreCuenta,
        decimal TotalDebe,
        decimal TotalHaber,
        decimal Saldo
    );
}
