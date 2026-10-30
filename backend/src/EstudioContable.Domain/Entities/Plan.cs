using System;
using System.Collections.Generic;

namespace EstudioContable.Domain.Entities
{
    public class Plan
    {
        public const int LargoMaximoDelNombre = 100;

        public long Id { get; private set; }
        public string Nombre { get; private set; }
        public decimal PrecioMensual { get; private set; }
        public int DuracionMeses { get; private set; }
        public bool Activo { get; private set; }

        public ICollection<Membresia> Membresias { get; private set; } = new List<Membresia>();

        protected Plan() { } // EF Core

        public Plan(string nombre, decimal precioMensual, int duracionMeses = 1, bool activo = true)
        {
            Nombre = ReglasDeDatos.TextoRequerido(nombre, "El nombre del plan", LargoMaximoDelNombre);
            PrecioMensual = ReglasDeDatos.Importe(precioMensual, "El precio mensual");
            DuracionMeses = ReglasDeDatos.Entero(duracionMeses, "La duración en meses", 1, ReglasDeDatos.MesesMaximosDeUnPlan);
            Activo = activo;
        }

        public void Actualizar(string nombre, decimal precioMensual, int duracionMeses)
        {
            Nombre = ReglasDeDatos.TextoRequerido(nombre, "El nombre del plan", LargoMaximoDelNombre);
            PrecioMensual = ReglasDeDatos.Importe(precioMensual, "El precio mensual");
            DuracionMeses = ReglasDeDatos.Entero(duracionMeses, "La duración en meses", 1, ReglasDeDatos.MesesMaximosDeUnPlan);
        }
    }
}
