using System;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// Detalle (Debe/Haber) de un AsientoContable. Es una entidad hija del
    /// agregado AsientoContable: no se crea ni persiste de forma independiente,
    /// sólo a través de AsientoContable.AgregarLinea().
    /// </summary>
    public class LineaAsiento
    {
        public long Id { get; private set; }
        public long AsientoId { get; private set; }
        public long CuentaId { get; private set; }
        public CuentaContable Cuenta { get; private set; } = null!;
        public AsientoContable Asiento { get; private set; } = null!;
        public string? Descripcion { get; private set; }

        // decimal (no float/double) es obligatorio para montos: evita errores
        // de redondeo por representación binaria inexacta de fracciones decimales.
        public decimal Debe { get; private set; }
        public decimal Haber { get; private set; }

        // Orden de visualización dentro del asiento (no tiene efecto contable).
        public short Orden { get; private set; }

        protected LineaAsiento() { } // requerido por EF Core

        /// <summary>
        /// Constructor interno: sólo AsientoContable (mismo assembly) puede crear líneas,
        /// garantizando que siempre pasen por las validaciones del agregado raíz.
        /// </summary>
        internal LineaAsiento(long cuentaId, decimal debe, decimal haber, string? descripcion, short orden)
        {
            // Regla contable: una línea se carga exclusivamente al Debe o al Haber,
            // nunca a ambos ni a ninguno de los dos.
            if (debe < 0 || haber < 0)
                throw new ArgumentException("Los montos no pueden ser negativos.");

            if ((debe > 0 && haber > 0) || (debe == 0 && haber == 0))
                throw new ArgumentException("Cada línea debe tener importe exclusivamente en Debe o en Haber.");

            CuentaId = cuentaId;
            Debe = debe;
            Haber = haber;
            Descripcion = descripcion;
            Orden = orden;
        }
    }
}
