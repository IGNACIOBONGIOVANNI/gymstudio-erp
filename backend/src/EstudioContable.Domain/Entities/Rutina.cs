using System;
using System.Collections.Generic;
using System.Linq;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// Rutina modelo ("Full body principiante"): días de entrenamiento con sus ejercicios.
    /// Se asigna a los socios mediante <see cref="AsignacionRutina"/>.
    /// </summary>
    public class Rutina
    {
        public static readonly string[] Niveles = { "Principiante", "Intermedio", "Avanzado" };

        /// <summary>Tope de renglones por rutina (7 días con unos 20 ejercicios cada uno ya es mucho).</summary>
        public const int MaximoDeRenglones = 150;

        public long Id { get; private set; }
        public string Nombre { get; private set; } = string.Empty;
        public string Objetivo { get; private set; } = string.Empty;
        public string Nivel { get; private set; } = string.Empty;
        public bool Activa { get; private set; } = true;
        public DateTime ModificadaEn { get; private set; } = DateTime.UtcNow;

        public ICollection<RutinaEjercicio> Ejercicios { get; private set; } = new List<RutinaEjercicio>();

        protected Rutina() { } // EF Core

        public Rutina(string nombre, string? objetivo, string nivel, IEnumerable<RutinaEjercicio> ejercicios)
        {
            Actualizar(nombre, objetivo, nivel, ejercicios);
        }

        /// <summary>Cambia los datos y reemplaza la lista completa de ejercicios.</summary>
        public void Actualizar(string nombre, string? objetivo, string nivel, IEnumerable<RutinaEjercicio> ejercicios)
        {
            var n = (nombre ?? string.Empty).Trim();
            if (n.Length == 0) throw new ArgumentException("El nombre de la rutina es obligatorio.");
            if (n.Length > 100) throw new ArgumentException("El nombre de la rutina no puede superar los 100 caracteres.");

            var obj = (objetivo ?? string.Empty).Trim();
            if (obj.Length > 200) throw new ArgumentException("El objetivo no puede superar los 200 caracteres.");

            var niv = Niveles.FirstOrDefault(x => string.Equals(x, nivel?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Nivel inválido. Opciones: {string.Join(", ", Niveles)}.");

            var lista = ejercicios.ToList();
            if (lista.Count == 0) throw new ArgumentException("La rutina debe tener al menos un ejercicio.");
            if (lista.Count > MaximoDeRenglones)
                throw new ArgumentException($"Una rutina no puede tener más de {MaximoDeRenglones} ejercicios.");

            // Orden dentro de cada día: el de la lista.
            foreach (var dia in lista.GroupBy(e => e.Dia))
            {
                var orden = 1;
                foreach (var e in dia) e.AsignarOrden(orden++);
            }

            Nombre = n;
            Objetivo = obj;
            Nivel = niv;
            Ejercicios.Clear();
            foreach (var e in lista) Ejercicios.Add(e);
            ModificadaEn = DateTime.UtcNow;
        }

        public void Desactivar() { Activa = false; ModificadaEn = DateTime.UtcNow; }
        public void Activar() { Activa = true; ModificadaEn = DateTime.UtcNow; }
    }

    /// <summary>Un renglón de la rutina: qué ejercicio, en qué día y con cuántas series y repeticiones.</summary>
    public class RutinaEjercicio
    {
        public long Id { get; private set; }
        public long RutinaId { get; private set; }
        public long EjercicioId { get; private set; }
        public Ejercicio? Ejercicio { get; private set; }
        public int Dia { get; private set; }
        public int Orden { get; private set; }
        public int Series { get; private set; }
        public string Repeticiones { get; private set; } = string.Empty;
        public int DescansoSegundos { get; private set; }
        public string Indicaciones { get; private set; } = string.Empty;

        protected RutinaEjercicio() { } // EF Core

        public RutinaEjercicio(long ejercicioId, int dia, int series, string repeticiones, int descansoSegundos, string? indicaciones)
        {
            if (ejercicioId <= 0) throw new ArgumentException("Elegí el ejercicio de cada renglón.");
            if (dia is < 1 or > 7) throw new ArgumentException("El día de la rutina debe estar entre 1 y 7.");
            if (series is < 1 or > 20) throw new ArgumentException("Las series deben estar entre 1 y 20.");

            var rep = (repeticiones ?? string.Empty).Trim();
            if (rep.Length == 0) throw new ArgumentException("Indicá las repeticiones (por ejemplo 10-12 o 30 seg).");
            if (rep.Length > 20) throw new ArgumentException("Las repeticiones no pueden superar los 20 caracteres.");

            if (descansoSegundos is < 0 or > 600) throw new ArgumentException("El descanso debe estar entre 0 y 600 segundos.");

            var ind = (indicaciones ?? string.Empty).Trim();
            if (ind.Length > 200) throw new ArgumentException("Las indicaciones no pueden superar los 200 caracteres.");

            EjercicioId = ejercicioId;
            Dia = dia;
            Series = series;
            Repeticiones = rep;
            DescansoSegundos = descansoSegundos;
            Indicaciones = ind;
        }

        internal void AsignarOrden(int orden) => Orden = orden;
    }
}
