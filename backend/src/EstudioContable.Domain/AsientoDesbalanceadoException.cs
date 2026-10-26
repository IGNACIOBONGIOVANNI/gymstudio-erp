using System;

namespace EstudioContable.Domain.Exceptions
{
    /// <summary>
    /// Se lanza cuando se intenta confirmar un asiento cuya sumatoria
    /// de Debe no coincide con la sumatoria de Haber (viola la partida doble).
    /// La capa API debe mapear esta excepción a un HTTP 422 (Unprocessable Entity),
    /// nunca a un 500 genérico, ya que es un error de negocio esperado.
    /// </summary>
    public class AsientoDesbalanceadoException : Exception
    {
        public decimal TotalDebe { get; }
        public decimal TotalHaber { get; }
        public decimal Diferencia => TotalDebe - TotalHaber;

        public AsientoDesbalanceadoException(decimal totalDebe, decimal totalHaber)
            : base($"El asiento no está balanceado. Debe: {totalDebe:N2} | Haber: {totalHaber:N2} | Diferencia: {(totalDebe - totalHaber):N2}")
        {
            TotalDebe = totalDebe;
            TotalHaber = totalHaber;
        }
    }
}
