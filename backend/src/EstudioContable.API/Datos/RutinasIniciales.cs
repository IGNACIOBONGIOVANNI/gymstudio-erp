using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EstudioContable.Domain.Entities;
using EstudioContable.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.API.Datos
{
    /// <summary>
    /// Catálogo de ejercicios y dos rutinas de ejemplo para que el módulo no arranque vacío.
    /// Solo se carga si no hay ningún ejercicio: nunca pisa lo que cargó el gimnasio.
    /// </summary>
    public static class RutinasIniciales
    {
        private static readonly (string Nombre, string Grupo)[] Catalogo =
        {
            ("Press de banca con barra", "Pecho"), ("Press inclinado con mancuernas", "Pecho"), ("Aperturas con mancuernas", "Pecho"),
            ("Flexiones de brazos", "Pecho"), ("Jalón al pecho", "Espalda"), ("Remo con barra", "Espalda"),
            ("Remo en polea baja", "Espalda"), ("Dominadas asistidas", "Espalda"), ("Sentadilla con barra", "Piernas"),
            ("Prensa de piernas", "Piernas"), ("Peso muerto rumano", "Piernas"), ("Estocadas con mancuernas", "Piernas"),
            ("Extensión de cuádriceps", "Piernas"), ("Curl femoral", "Piernas"), ("Elevación de talones", "Piernas"),
            ("Puente de glúteos", "Glúteos"), ("Patada de glúteo en polea", "Glúteos"), ("Press militar con mancuernas", "Hombros"),
            ("Elevaciones laterales", "Hombros"), ("Curl de bíceps con barra", "Brazos"), ("Extensión de tríceps en polea", "Brazos"),
            ("Plancha frontal", "Abdomen"), ("Abdominales en banco", "Abdomen"), ("Elevación de piernas", "Abdomen"),
            ("Cinta: trote suave", "Cardio"), ("Bicicleta fija", "Cardio"), ("Remo en máquina", "Cardio"),
            ("Burpees", "Cuerpo completo"), ("Kettlebell swing", "Cuerpo completo")
        };

        public static async Task CrearSiNoExistenAsync(AppDbContext context)
        {
            if (await context.Ejercicios.AnyAsync())
                return;

            context.Ejercicios.AddRange(Catalogo.Select(e => new Ejercicio(e.Nombre, e.Grupo)));
            await context.SaveChangesAsync();

            var id = await context.Ejercicios.ToDictionaryAsync(e => e.Nombre, e => e.Id);
            RutinaEjercicio R(string nombre, int dia, int series, string reps, int descanso, string? nota = null)
                => new(id[nombre], dia, series, reps, descanso, nota);

            context.Rutinas.Add(new Rutina("Full body principiante", "Adaptación general: aprender la técnica de los ejercicios básicos", "Principiante", new List<RutinaEjercicio>
            {
                R("Cinta: trote suave", 1, 1, "10", 0, "Entrada en calor: 10 minutos"),
                R("Sentadilla con barra", 1, 3, "10-12", 90, "Barra sola o con poco peso, espalda recta"),
                R("Press de banca con barra", 1, 3, "10-12", 90),
                R("Remo en polea baja", 1, 3, "10-12", 90),
                R("Plancha frontal", 1, 3, "30", 60, "Mantener 30 segundos"),
                R("Prensa de piernas", 2, 3, "12", 90),
                R("Press militar con mancuernas", 2, 3, "10-12", 90),
                R("Jalón al pecho", 2, 3, "10-12", 90),
                R("Puente de glúteos", 2, 3, "15", 60),
                R("Abdominales en banco", 2, 3, "15", 45),
                R("Estocadas con mancuernas", 3, 3, "10", 90, "10 por pierna"),
                R("Flexiones de brazos", 3, 3, "8-10", 90, "Con las rodillas apoyadas si cuesta"),
                R("Remo con barra", 3, 3, "10", 90),
                R("Curl de bíceps con barra", 3, 2, "12", 60),
                R("Bicicleta fija", 3, 1, "15", 0, "Vuelta a la calma: 15 minutos")
            }));

            context.Rutinas.Add(new Rutina("Tren superior e inferior", "Ganar fuerza y masa muscular, dividiendo el cuerpo en dos días", "Intermedio", new List<RutinaEjercicio>
            {
                R("Press de banca con barra", 1, 4, "8-10", 120),
                R("Press inclinado con mancuernas", 1, 3, "10", 90),
                R("Remo con barra", 1, 4, "8-10", 120),
                R("Jalón al pecho", 1, 3, "10-12", 90),
                R("Elevaciones laterales", 1, 3, "12-15", 60),
                R("Extensión de tríceps en polea", 1, 3, "12", 60),
                R("Curl de bíceps con barra", 1, 3, "12", 60),
                R("Sentadilla con barra", 2, 4, "8-10", 150),
                R("Peso muerto rumano", 2, 4, "8-10", 120),
                R("Prensa de piernas", 2, 3, "12", 90),
                R("Curl femoral", 2, 3, "12", 75),
                R("Elevación de talones", 2, 4, "15", 45),
                R("Elevación de piernas", 2, 3, "15", 45)
            }));

            await context.SaveChangesAsync();
        }
    }
}
