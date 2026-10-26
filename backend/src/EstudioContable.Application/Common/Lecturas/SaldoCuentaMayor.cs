using EstudioContable.Domain.Entities;

namespace EstudioContable.Application.Common.Lecturas
{
    /// <summary>Fila de la vista contabilidad.v_libro_mayor (sólo lectura).</summary>
    public class SaldoCuentaMayor
    {
        public long ClienteId { get; set; }
        public long CuentaId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public NaturalezaCuenta Naturaleza { get; set; }
        public decimal TotalDebe { get; set; }
        public decimal TotalHaber { get; set; }

        // Debe - Haber
        public decimal Saldo { get; set; }
    }
}
