using System.Collections.Generic;

namespace EstudioContable.Application.Banco.Queries.DTOs
{
    public record ResumenBancoDto(
        decimal SaldoAnterior,
        decimal TotalIngresos,
        decimal TotalEgresos,
        decimal SaldoActual,
        List<MovimientoBancoDto> ListaMovimientos
    );
}
