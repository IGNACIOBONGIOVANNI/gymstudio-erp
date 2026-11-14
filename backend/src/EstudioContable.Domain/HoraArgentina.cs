using System;

namespace EstudioContable.Domain
{
    /// <summary>
    /// La fecha de un comprobante es un día de Argentina. Las facturas la guardan como la hora de Argentina marcada como UTC: la
    /// pantalla manda el día elegido a las 00:00 UTC, y el asiento, la Caja, el Banco y los libros toman el día de esa parte UTC.
    /// Lo que se registra en el momento (el cobro de una cuota) tiene que seguir la misma convención: si guardara la hora real en
    /// UTC, de 21 a 24 quedaría con fecha del día siguiente.
    /// </summary>
    public static class HoraArgentina
    {
        public static readonly TimeSpan Desfase = TimeSpan.FromHours(-3);

        /// <summary>El momento indicado, con la hora de Argentina, listo para la fecha de un comprobante.</summary>
        public static DateTime ParaComprobante(DateTimeOffset ahora)
            => DateTime.SpecifyKind(ahora.ToOffset(Desfase).DateTime, DateTimeKind.Utc);
    }
}
