using System;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// Un ingreso del socio al gimnasio. Hay uno solo por socio y día: si pasa dos veces el mismo día,
    /// vale el primer ingreso.
    /// </summary>
    public class AsistenciaSocio
    {
        public long Id { get; private set; }
        public long SocioId { get; private set; }
        /// <summary>Día del ingreso, en hora de Argentina.</summary>
        public DateOnly Fecha { get; private set; }
        public DateTime RegistradaEn { get; private set; }
        /// <summary>Quién lo registró en Recepción.</summary>
        public string RegistradaPor { get; private set; } = string.Empty;

        protected AsistenciaSocio() { } // EF Core

        public AsistenciaSocio(long socioId, DateOnly fecha, string? registradaPor)
        {
            if (socioId <= 0) throw new ArgumentException("Elegí el socio.");
            if (fecha.Year < 2000 || fecha.Year > 2100) throw new ArgumentException("La fecha del ingreso no es válida.");

            var quien = (registradaPor ?? string.Empty).Trim();
            SocioId = socioId;
            Fecha = fecha;
            RegistradaEn = DateTime.UtcNow;
            RegistradaPor = quien.Length > 100 ? quien[..100] : quien;
        }
    }
}
