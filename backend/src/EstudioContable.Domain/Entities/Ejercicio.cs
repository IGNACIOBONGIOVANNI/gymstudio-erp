using System;
using System.Linq;

namespace EstudioContable.Domain.Entities
{
    /// <summary>Un ejercicio del catálogo (sentadilla, press de banca...), reutilizable en muchas rutinas.</summary>
    public class Ejercicio
    {
        public static readonly string[] GruposMusculares =
            { "Pecho", "Espalda", "Piernas", "Glúteos", "Hombros", "Brazos", "Abdomen", "Cardio", "Cuerpo completo" };

        public long Id { get; private set; }
        public string Nombre { get; private set; } = string.Empty;
        public string GrupoMuscular { get; private set; } = string.Empty;
        public bool Activo { get; private set; } = true;

        protected Ejercicio() { } // EF Core

        public Ejercicio(string nombre, string grupoMuscular) => Actualizar(nombre, grupoMuscular);

        public void Actualizar(string nombre, string grupoMuscular)
        {
            var n = (nombre ?? string.Empty).Trim();
            if (n.Length == 0) throw new ArgumentException("El nombre del ejercicio es obligatorio.");
            if (n.Length > 100) throw new ArgumentException("El nombre del ejercicio no puede superar los 100 caracteres.");

            var grupo = GruposMusculares.FirstOrDefault(g => string.Equals(g, grupoMuscular?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Grupo muscular inválido. Opciones: {string.Join(", ", GruposMusculares)}.");

            Nombre = n;
            GrupoMuscular = grupo;
        }

        public void Desactivar() => Activo = false;
        public void Activar() => Activo = true;
    }
}
