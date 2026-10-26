using System;
using System.Collections.Generic;

namespace EstudioContable.Domain.Entities
{
    public class Socio
    {
        public long Id { get; private set; }
        public string Nombre { get; private set; }
        public string Apellido { get; private set; }
        public string Dni { get; private set; }
        public string Telefono { get; private set; }
        public DateTime FechaAlta { get; private set; }
        public bool Activo { get; private set; }

        public DateTime? FechaVencimiento { get; private set; }
        public long? PlanActualId { get; private set; }
        public Plan? PlanActualObj { get; private set; }
        public string Estado { get; private set; } = "Vencido";

        public ICollection<Membresia> Membresias { get; private set; } = new List<Membresia>();

        protected Socio() { } // EF Core

        public Socio(string nombre, string apellido, string dni, string telefono, DateTime fechaAlta, bool activo = true)
        {
            if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre es requerido.");
            if (string.IsNullOrWhiteSpace(apellido)) throw new ArgumentException("El apellido es requerido.");

            Nombre = nombre;
            Apellido = apellido;
            Dni = dni;
            Telefono = telefono;
            FechaAlta = fechaAlta;
            Activo = activo;
            Estado = "Vencido";
        }

        public void RenovarPlan(long planId, int duracionMeses)
        {
            PlanActualId = planId;
            
            DateTime fechaBase = (FechaVencimiento.HasValue && FechaVencimiento.Value > DateTime.UtcNow) 
                                 ? FechaVencimiento.Value 
                                 : DateTime.UtcNow;

            int mesesAsumar = duracionMeses > 0 ? duracionMeses : 1;
            FechaVencimiento = fechaBase.AddMonths(mesesAsumar);
            Estado = "Activo";
        }
    }
}
