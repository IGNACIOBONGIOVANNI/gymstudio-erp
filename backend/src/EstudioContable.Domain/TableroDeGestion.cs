using System;
using System.Collections.Generic;
using System.Linq;

namespace EstudioContable.Domain
{
    /// <summary>Si al indicador le conviene subir (ingresos) o bajar (cuotas vencidas).</summary>
    public enum SentidoDelIndicador { MasEsMejor, MenosEsMejor }

    /// <summary>Un indicador del tablero de comando: su clave fija, a qué perspectiva del Balanced Scorecard pertenece y cómo se lee.</summary>
    public record DefinicionDeIndicador(string Clave, string Nombre, string Perspectiva, SentidoDelIndicador Sentido, bool EsPorcentaje, bool AdmiteMeta);

    /// <summary>
    /// Catálogo de indicadores del Tablero de Gestión y las reglas del ciclo de control: comparar el valor real con la meta
    /// (semáforo) y con el período anterior (variación). Las metas solo se pueden cargar para estas claves.
    /// </summary>
    public static class TableroDeGestion
    {
        public const string Verde = "verde";
        public const string Amarillo = "amarillo";
        public const string Rojo = "rojo";
        public const string SinMeta = "sin-meta";
        public const string SinDatos = "sin-datos";

        /// <summary>Hasta dónde se tolera el desvío antes de pasar a rojo: 10 % por debajo (o por encima) de la meta.</summary>
        public const decimal Tolerancia = 0.10m;

        /// <summary>Una meta en porcentaje no puede pasar de este valor.</summary>
        public const decimal MaximoPorcentaje = 1000m;

        private const SentidoDelIndicador Mas = SentidoDelIndicador.MasEsMejor;
        private const SentidoDelIndicador Menos = SentidoDelIndicador.MenosEsMejor;

        public static IReadOnlyList<DefinicionDeIndicador> Indicadores { get; } = new List<DefinicionDeIndicador>
        {
            new("ingresos", "Ingresos (ventas netas)", "Financiera", Mas, false, true),
            new("resultado", "Resultado del mes", "Financiera", Mas, false, true),
            new("margen", "Margen sobre ventas", "Financiera", Mas, true, true),
            new("liquidez", "Caja + Banco (hoy)", "Financiera", Mas, false, false),
            new("socios-activos", "Socios activos", "Socios", Mas, false, true),
            new("altas", "Altas de socios", "Socios", Mas, false, false),
            new("bajas", "Bajas (no renovaron)", "Socios", Menos, false, false),
            new("retencion", "Retención de socios", "Socios", Mas, true, true),
            new("cuotas-vencidas", "Socios con cuota vencida (hoy)", "Socios", Menos, true, true),
            new("asistencias", "Asistencias del mes", "Procesos", Mas, false, false),
            new("asistencia-por-socio", "Asistencias por socio activo", "Procesos", Mas, false, true),
            new("stock-critico", "Productos sin stock o con poco (hoy)", "Procesos", Menos, false, true),
            new("horas-entrenadores", "Horas de entrenadores", "Personas", Mas, false, false),
            new("costo-entrenadores", "Costo de entrenadores", "Personas", Menos, false, false),
            new("costo-sobre-cuotas", "Costo de entrenadores sobre cuotas", "Personas", Menos, true, true),
        };

        public static DefinicionDeIndicador? Buscar(string? clave)
            => Indicadores.FirstOrDefault(i => string.Equals(i.Clave, clave, StringComparison.Ordinal));

        /// <summary>
        /// Verde si cumple la meta; amarillo si se desvía hasta un 10 %; rojo si se desvía más. Sin meta queda gris y sin valor, «sin datos».
        /// </summary>
        public static string Semaforo(decimal? valor, decimal? meta, SentidoDelIndicador sentido)
        {
            if (valor is null) return SinDatos;
            if (meta is null) return SinMeta;
            var v = valor.Value;
            var m = meta.Value;
            return sentido == Mas
                ? v >= m ? Verde : v >= m * (1 - Tolerancia) ? Amarillo : Rojo
                : v <= m ? Verde : v <= m * (1 + Tolerancia) ? Amarillo : Rojo;
        }

        /// <summary>
        /// Cuánto cambió contra el período anterior: en % del anterior, o en puntos si el indicador ya es un porcentaje.
        /// Null si falta alguno de los dos o si el anterior es 0 (no hay base para un porcentaje).
        /// </summary>
        public static decimal? Variacion(decimal? actual, decimal? anterior, bool esPorcentaje)
        {
            if (actual is null || anterior is null) return null;
            if (esPorcentaje) return Math.Round(actual.Value - anterior.Value, 2);
            if (anterior.Value == 0) return null;
            return Math.Round((actual.Value - anterior.Value) / Math.Abs(anterior.Value) * 100m, 2);
        }
    }
}
