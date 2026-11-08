using System;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// La programación de las copias de seguridad automáticas y cómo le fue a la última. Hay una sola fila en la base (Id = 1).
    /// Cuándo toca una copia lo decide <see cref="ReglasDeCopiasAutomaticas"/>; esta clase guarda los valores y los valida,
    /// todos juntos: una programación inválida no cambia nada.
    /// </summary>
    public class ConfiguracionDeCopias
    {
        public const int IdUnico = 1;
        public const int ConservarMinimo = 1;
        public const int ConservarMaximo = 60;
        public const int DiaDelMesMaximo = 28;
        public const int LargoMaximoDelError = 500;

        public int Id { get; private set; } = IdUnico;
        public bool Activa { get; private set; }
        public FrecuenciaDeCopia Frecuencia { get; private set; } = FrecuenciaDeCopia.Diaria;
        /// <summary>Hora (0 a 23) de Argentina en que se hace la copia; en las frecuencias de 6 y 12 horas, desde dónde se cuenta.</summary>
        public int Hora { get; private set; } = 22;
        public int Minuto { get; private set; }
        /// <summary>1 = lunes ... 7 = domingo. Solo en la frecuencia semanal.</summary>
        public int? DiaDeLaSemana { get; private set; }
        /// <summary>1 a 28 (el 29, 30 y 31 no existen todos los meses). Solo en la frecuencia mensual.</summary>
        public int? DiaDelMes { get; private set; }
        /// <summary>Cuántas copias automáticas se guardan: al hacer una nueva se borran las más viejas.</summary>
        public int Conservar { get; private set; } = 14;
        /// <summary>Cuándo se activó o se cambió la programación: desde ahí se cuenta la próxima copia.</summary>
        public DateTimeOffset ActivaDesde { get; private set; } = DateTimeOffset.UtcNow;
        /// <summary>La última copia guardada con éxito (automática o hecha con «Hacer una copia ahora»).</summary>
        public DateTimeOffset? UltimaCopia { get; private set; }
        /// <summary>El último intento de copia automática, haya salido bien o mal: desde ahí se espera para reintentar tras una falla.</summary>
        public DateTimeOffset? UltimoIntento { get; private set; }
        /// <summary>Por qué falló el último intento; <c>null</c> si salió bien.</summary>
        public string? UltimoError { get; private set; }

        public void Programar(bool activa, FrecuenciaDeCopia frecuencia, int hora, int minuto, int? diaDeLaSemana, int? diaDelMes, int conservar, DateTimeOffset ahora)
        {
            // Se valida todo antes de asignar nada.
            if (!Enum.IsDefined(frecuencia)) throw new ArgumentException("La frecuencia de la copia no es válida.");
            ReglasDeDatos.Entero(hora, "La hora", 0, 23);
            ReglasDeDatos.Entero(minuto, "Los minutos", 0, 59);
            ReglasDeDatos.Entero(conservar, "La cantidad de copias a conservar", ConservarMinimo, ConservarMaximo);
            int? semana = null, mes = null;
            if (frecuencia == FrecuenciaDeCopia.Semanal)
            {
                if (diaDeLaSemana is null) throw new ArgumentException("Elegí el día de la semana de la copia.");
                semana = ReglasDeDatos.Entero(diaDeLaSemana.Value, "El día de la semana", 1, 7);
            }
            else if (frecuencia == FrecuenciaDeCopia.Mensual)
            {
                if (diaDelMes is null) throw new ArgumentException("Elegí el día del mes de la copia.");
                mes = ReglasDeDatos.Entero(diaDelMes.Value, "El día del mes", 1, DiaDelMesMaximo);
            }

            Activa = activa;
            Frecuencia = frecuencia;
            Hora = hora;
            Minuto = minuto;
            DiaDeLaSemana = semana;
            DiaDelMes = mes;
            Conservar = conservar;
            ActivaDesde = ahora;
        }

        /// <summary>Se guardó una copia: queda como la última y se borra el error anterior.</summary>
        public void RegistrarCopia(DateTimeOffset ahora)
        {
            UltimaCopia = ahora;
            UltimoIntento = ahora;
            UltimoError = null;
        }

        /// <summary>No se pudo guardar la copia: se anota el intento y el motivo (corto y sin detalles técnicos).</summary>
        public void RegistrarFalla(DateTimeOffset ahora, string mensaje)
        {
            var texto = (mensaje ?? string.Empty).Trim();
            if (texto.Length == 0) throw new ArgumentException("Falta el motivo de la falla.");
            UltimoIntento = ahora;
            UltimoError = texto.Length > LargoMaximoDelError ? texto[..LargoMaximoDelError] : texto;
        }
    }
}
