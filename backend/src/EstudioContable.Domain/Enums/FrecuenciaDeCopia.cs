namespace EstudioContable.Domain.Enums
{
    /// <summary>Cada cuánto se hace una copia de seguridad automática.</summary>
    public enum FrecuenciaDeCopia
    {
        /// <summary>A las 02:00, 08:00, 14:00 y 20:00 si se eligió la hora 02:00: cada 6 horas contadas desde la hora elegida.</summary>
        CadaSeisHoras = 1,
        /// <summary>Cada 12 horas contadas desde la hora elegida.</summary>
        CadaDoceHoras = 2,
        /// <summary>Todos los días a la hora elegida.</summary>
        Diaria = 3,
        /// <summary>Una vez por semana, el día elegido, a la hora elegida.</summary>
        Semanal = 4,
        /// <summary>Una vez por mes, el día elegido (del 1 al 28), a la hora elegida.</summary>
        Mensual = 5,
    }
}
