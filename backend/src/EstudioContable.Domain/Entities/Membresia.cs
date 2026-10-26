using System;

namespace EstudioContable.Domain.Entities
{
    public class Membresia
    {
        public long Id { get; private set; }
        public long SocioId { get; private set; }
        public long PlanId { get; private set; }
        public DateTime FechaInicio { get; private set; }
        public DateTime FechaVencimiento { get; private set; }
        public bool EstaPagada { get; private set; }

        public Socio Socio { get; private set; }
        public Plan Plan { get; private set; }

        protected Membresia() { } // EF Core

        public Membresia(long socioId, long planId, DateTime fechaInicio, DateTime fechaVencimiento, bool estaPagada = false)
        {
            if (fechaVencimiento < fechaInicio)
                throw new ArgumentException("El vencimiento de la membresía no puede ser anterior a su inicio.");

            SocioId = socioId;
            PlanId = planId;
            FechaInicio = fechaInicio;
            FechaVencimiento = fechaVencimiento;
            EstaPagada = estaPagada;
        }

        public void MarcarComoPagada()
        {
            EstaPagada = true;
        }
    }
}
