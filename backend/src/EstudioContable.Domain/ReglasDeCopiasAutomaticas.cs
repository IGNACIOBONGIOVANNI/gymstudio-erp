using System;
using System.Globalization;
using System.Text.RegularExpressions;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Domain
{
    /// <summary>
    /// Cuándo toca una copia automática y cómo se llaman los archivos. Son funciones puras: el reloj entra como parámetro, así se
    /// prueban sin esperar. Todas las horas son de Argentina (UTC-3, sin horario de verano), igual que el resto del sistema.
    /// </summary>
    public static class ReglasDeCopiasAutomaticas
    {
        public static readonly TimeSpan DesfaseArgentina = TimeSpan.FromHours(-3);

        /// <summary>Después de una falla, cuánto se espera para volver a intentar (no se reintenta cada minuto).</summary>
        public static readonly TimeSpan EsperaTrasUnaFalla = TimeSpan.FromMinutes(30);

        // Solo dígitos ASCII (en .NET «\d» acepta también los de otros alfabetos) y «\z» en vez de «$», que acepta un salto de línea final.
        private static readonly Regex NombreDeCopia = new(@"^gymstudio_auto_[0-9]{8}_[0-9]{6}\.zip\z", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        /// <summary>El primer momento programado estrictamente posterior a la referencia, en hora de Argentina.</summary>
        public static DateTimeOffset ProximaEjecucion(ConfiguracionDeCopias config, DateTimeOffset referencia)
        {
            var local = referencia.ToOffset(DesfaseArgentina);
            var ancla = new TimeSpan(config.Hora, config.Minuto, 0);
            return config.Frecuencia switch
            {
                FrecuenciaDeCopia.CadaSeisHoras => ProximoCadaTanto(local, ancla, TimeSpan.FromHours(6)),
                FrecuenciaDeCopia.CadaDoceHoras => ProximoCadaTanto(local, ancla, TimeSpan.FromHours(12)),
                FrecuenciaDeCopia.Diaria => ProximoDiaQueCumple(local, ancla, _ => true),
                FrecuenciaDeCopia.Semanal => ProximoDiaQueCumple(local, ancla, dia => DiaIso(dia) == config.DiaDeLaSemana),
                FrecuenciaDeCopia.Mensual => ProximoDiaQueCumple(local, ancla, dia => dia.Day == config.DiaDelMes),
                _ => throw new ArgumentException("La frecuencia de la copia no es válida."),
            };
        }

        /// <summary>
        /// La próxima copia programada, o <c>null</c> si no está activa. Se cuenta desde la última copia o, si es posterior, desde que se
        /// activó o se cambió la programación: reprogramar no dispara de inmediato una copia atrasada.
        /// </summary>
        public static DateTimeOffset? ProximaCopia(ConfiguracionDeCopias config)
        {
            if (!config.Activa) return null;
            var referencia = config.UltimaCopia is { } ultima && ultima > config.ActivaDesde ? ultima : config.ActivaDesde;
            return ProximaEjecucion(config, referencia);
        }

        /// <summary>
        /// ¿Hay que hacer una copia ahora? Si el sistema estuvo apagado durante varios momentos programados toca una sola vez, y tras
        /// una falla no se reintenta hasta pasados <see cref="EsperaTrasUnaFalla"/>.
        /// </summary>
        public static bool Toca(ConfiguracionDeCopias config, DateTimeOffset ahora)
        {
            if (!config.Activa) return false;
            if (config.UltimoError != null && config.UltimoIntento is { } intento && ahora < intento + EsperaTrasUnaFalla) return false;
            return ProximaCopia(config) <= ahora;
        }

        /// <summary>Por ejemplo <c>gymstudio_auto_20261107_220509.zip</c>, con la fecha y hora de Argentina.</summary>
        public static string NombreDeArchivo(DateTimeOffset ahora)
            => $"gymstudio_auto_{ahora.ToOffset(DesfaseArgentina).ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.zip";

        /// <summary>
        /// Solo estos nombres exactos son copias automáticas: la retención y la descarga no tocan ningún otro archivo, y un nombre con
        /// barras, puntos dobles o un salto de línea final no pasa.
        /// </summary>
        public static bool EsNombreDeCopiaAutomatica(string? nombre) => nombre != null && NombreDeCopia.IsMatch(nombre);

        private static DateTimeOffset EnArgentina(DateTime dia, TimeSpan hora) => new(dia.Date + hora, DesfaseArgentina);

        // Lunes = 1 ... domingo = 7.
        private static int DiaIso(DateTime dia) => dia.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)dia.DayOfWeek;

        private static DateTimeOffset ProximoDiaQueCumple(DateTimeOffset local, TimeSpan ancla, Func<DateTime, bool> cumple)
        {
            // Como mucho pasa un mes (un día del 1 al 28 siempre existe); el tope de 400 días solo evita un ciclo infinito.
            for (var dia = local.Date; dia < local.Date.AddDays(400); dia = dia.AddDays(1))
            {
                if (!cumple(dia)) continue;
                var momento = EnArgentina(dia, ancla);
                if (momento > local) return momento;
            }
            throw new InvalidOperationException("No se encontró la próxima copia programada.");
        }

        // Los momentos son «ancla + k·intervalo»: como el intervalo divide un día, la grilla se repite igual todos los días.
        private static DateTimeOffset ProximoCadaTanto(DateTimeOffset local, TimeSpan ancla, TimeSpan intervalo)
        {
            var porDia = (int)(TimeSpan.FromDays(1) / intervalo);
            DateTimeOffset? mejor = null;
            for (var dia = local.Date.AddDays(-1); dia <= local.Date.AddDays(1); dia = dia.AddDays(1))
            {
                for (var k = 0; k < porDia; k++)
                {
                    var momento = EnArgentina(dia, ancla + k * intervalo);
                    if (momento > local && (mejor is null || momento < mejor)) mejor = momento;
                }
            }
            return mejor ?? throw new InvalidOperationException("No se encontró la próxima copia programada.");
        }
    }
}
