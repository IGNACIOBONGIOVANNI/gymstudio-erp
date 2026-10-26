using EstudioContable.Domain.Entities;

namespace EstudioContable.Application.Common.Lecturas
{
    /// <summary>Fila devuelta por la función contabilidad.fn_sumas_y_saldos (sólo lectura).</summary>
    public class FilaSumasYSaldos
    {
        public long CuentaId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public NaturalezaCuenta Naturaleza { get; set; }
        public decimal TotalDebe { get; set; }
        public decimal TotalHaber { get; set; }

        // Saldo con el signo propio de la cuenta: Debe - Haber para el Activo,
        // Haber - Debe para Pasivo, Patrimonio Neto y Resultado.
        public decimal SaldoNatural { get; set; }
    }
}
