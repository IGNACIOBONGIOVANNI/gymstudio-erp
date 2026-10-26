using System;
using System.Collections.Generic;

namespace EstudioContable.Domain.Entities
{
    public class Plan
    {
        public long Id { get; private set; }
        public string Nombre { get; private set; }
        public decimal PrecioMensual { get; private set; }
        public int DuracionMeses { get; private set; }
        public bool Activo { get; private set; }

        public ICollection<Membresia> Membresias { get; private set; } = new List<Membresia>();

        protected Plan() { } // EF Core

        public Plan(string nombre, decimal precioMensual, int duracionMeses = 1, bool activo = true)
        {
            if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre del plan es requerido.");
            if (precioMensual < 0) throw new ArgumentException("El precio mensual no puede ser negativo.");
            if (duracionMeses <= 0) throw new ArgumentException("La duración en meses debe ser mayor a cero.");

            Nombre = nombre;
            PrecioMensual = precioMensual;
            DuracionMeses = duracionMeses;
            Activo = activo;
        }
    }
}
