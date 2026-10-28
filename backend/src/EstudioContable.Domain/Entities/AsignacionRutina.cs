using System;

namespace EstudioContable.Domain.Entities
{
    /// <summary>Una rutina asignada a un socio durante un período. Un socio tiene una sola asignación activa.</summary>
    public class AsignacionRutina
    {
        public long Id { get; private set; }
        public long SocioId { get; private set; }
        public Socio? Socio { get; private set; }
        public long RutinaId { get; private set; }
        public Rutina? Rutina { get; private set; }
        public DateOnly FechaInicio { get; private set; }
        public DateOnly? FechaFin { get; private set; }
        public string Observaciones { get; private set; } = string.Empty;
        public bool Activa { get; private set; } = true;

        protected AsignacionRutina() { } // EF Core

        public AsignacionRutina(long socioId, long rutinaId, DateOnly fechaInicio, DateOnly? fechaFin, string? observaciones)
        {
            if (socioId <= 0) throw new ArgumentException("Elegí el socio.");
            if (rutinaId <= 0) throw new ArgumentException("Elegí la rutina.");
            if (fechaInicio.Year < 2000 || fechaInicio.Year > 2100) throw new ArgumentException("La fecha de inicio no es válida.");
            if (fechaFin is DateOnly fin && fin < fechaInicio)
                throw new ArgumentException("La fecha de vencimiento no puede ser anterior a la de inicio.");

            var obs = (observaciones ?? string.Empty).Trim();
            if (obs.Length > 500) throw new ArgumentException("Las observaciones no pueden superar los 500 caracteres.");

            SocioId = socioId;
            RutinaId = rutinaId;
            FechaInicio = fechaInicio;
            FechaFin = fechaFin;
            Observaciones = obs;
        }

        /// <summary>Cierra la asignación en la fecha dada (nunca antes de su inicio).</summary>
        public void Finalizar(DateOnly fecha)
        {
            if (!Activa) throw new InvalidOperationException("La rutina ya estaba finalizada.");
            Activa = false;
            FechaFin = fecha < FechaInicio ? FechaInicio : fecha;
        }

        public bool EstaVigenteEl(DateOnly dia) => Activa && (FechaFin is null || FechaFin >= dia);
    }
}
