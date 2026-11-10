using System;
using System.Linq;
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

        public Socio(string nombre, string apellido, string dni, string? telefono, DateTime fechaAlta, bool activo = true)
        {
            AsignarDatos(nombre, apellido, dni, telefono);
            FechaAlta = fechaAlta;
            Activo = activo;
            Estado = "Vencido";
        }

        public void ActualizarDatos(string nombre, string apellido, string dni, string? telefono)
            => AsignarDatos(nombre, apellido, dni, telefono);

        /// <summary>"30.111.222" -> "30111222"</summary>
        public static string NormalizarDni(string? dni)
            => new string((dni ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '.').ToArray());

        private void AsignarDatos(string nombre, string apellido, string dni, string? telefono)
        {
            if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre es requerido.");
            if (string.IsNullOrWhiteSpace(apellido)) throw new ArgumentException("El apellido es requerido.");
            if (nombre.Trim().Length > 100 || apellido.Trim().Length > 100)
                throw new ArgumentException("El nombre y el apellido no pueden superar los 100 caracteres.");

            var nombreLimpio = ReglasDePersonas.NombreDePersona(nombre, "El nombre", 100);
            var apellidoLimpio = ReglasDePersonas.NombreDePersona(apellido, "El apellido", 100);

            var dniNormalizado = NormalizarDni(dni);
            if (dniNormalizado.Length is < 7 or > 8 || !dniNormalizado.All(char.IsDigit))
                throw new ArgumentException("El DNI debe tener 7 u 8 números (los puntos son opcionales).");

            var tel = ReglasDePersonas.Telefono(telefono, "El teléfono") ?? string.Empty;

            Nombre = nombreLimpio;
            Apellido = apellidoLimpio;
            Dni = dniNormalizado;
            Telefono = tel;
        }

        /// <summary>
        /// Extiende la vigencia del socio y devuelve el período cubierto. Si el socio
        /// todavía está al día, el nuevo período empieza cuando termina el actual.
        /// </summary>
        public (DateTime Inicio, DateTime Vencimiento) RenovarPlan(long planId, int duracionMeses)
        {
            PlanActualId = planId;

            var ahora = DateTime.UtcNow;
            var inicio = (FechaVencimiento.HasValue && FechaVencimiento.Value > ahora)
                         ? FechaVencimiento.Value
                         : ahora;

            int mesesAsumar = duracionMeses > 0 ? duracionMeses : 1;
            FechaVencimiento = inicio.AddMonths(mesesAsumar);
            Estado = "Activo";

            return (inicio, FechaVencimiento.Value);
        }
    }
}
