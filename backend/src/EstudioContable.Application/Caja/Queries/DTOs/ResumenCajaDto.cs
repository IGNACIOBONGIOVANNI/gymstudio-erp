using System.Collections.Generic;

namespace EstudioContable.Application.Caja.Queries.DTOs
{
    public record ResumenCajaDto(
        decimal SaldoAnterior,
        decimal TotalIngresos,
        decimal TotalEgresos,
        decimal SaldoActual,
        List<MovimientoCajaDto> ListaMovimientos
    );
}
