using System;
using System.Collections.Generic;
using System.Linq;

namespace EstudioContable.Domain
{
    /// <summary>Cómo está la cuota de un socio un día dado.</summary>
    public enum EstadoDeCuota
    {
        /// <summary>Todavía no pagó ninguna cuota.</summary>
        SinPlan,
        AlDia,
        /// <summary>Vigente, pero vence en 7 días o menos.</summary>
        PorVencer,
        Vencida
    }

    /// <summary>
    /// Reglas del ingreso de socios al gimnasio, sin base de datos: estado de la cuota, qué día de la rutina toca
    /// y cuándo un socio al día empieza a estar en riesgo de abandonar.
    /// </summary>
    public static class ReglasDeAsistencia
    {
        public const int DiasPorVencer = 7;
        public const int DiasSinVenirPorDefecto = 14;

        /// <summary>
        /// Estado de la cuota según su vencimiento. Se compara por día: una cuota que vence hoy sigue vigente.
        /// Es el mismo criterio que usan el resumen de inicio y la pantalla de Recepción.
        /// </summary>
        public static EstadoDeCuota CalcularEstado(DateTime? vencimiento, DateOnly hoy)
        {
            if (vencimiento is null) return EstadoDeCuota.SinPlan;
            var dias = DateOnly.FromDateTime(vencimiento.Value).DayNumber - hoy.DayNumber;
            if (dias < 0) return EstadoDeCuota.Vencida;
            return dias <= DiasPorVencer ? EstadoDeCuota.PorVencer : EstadoDeCuota.AlDia;
        }

        /// <summary>
        /// Qué día de la rutina le toca en esta visita: se van rotando en orden, uno por cada vez que el socio
        /// vino desde que empezó la rutina. Con los días 1, 2 y 3: la 1.ª visita es el día 1, la 2.ª el 2, la 3.ª el 3
        /// y la 4.ª vuelve al 1. Los días pueden no ser consecutivos (por ejemplo 1, 3 y 5).
        /// </summary>
        /// <param name="diasDeLaRutina">Los números de día que tiene la rutina.</param>
        /// <param name="visitasPrevias">Cuántas veces vino antes de hoy desde que empezó la rutina.</param>
        public static int? DiaDeRutina(IEnumerable<int> diasDeLaRutina, int visitasPrevias)
        {
            var dias = diasDeLaRutina.Distinct().OrderBy(d => d).ToList();
            if (dias.Count == 0) return null;
            return dias[Math.Max(0, visitasPrevias) % dias.Count];
        }

        /// <summary>
        /// Días que pasaron desde la última visita. Si nunca vino cuenta desde <paramref name="desdeSiNuncaVino"/>
        /// (cuando empezó su cuota), para no marcar como ausente a quien recién se anotó.
        /// </summary>
        public static int DiasSinVenir(DateOnly? ultimaVisita, DateOnly desdeSiNuncaVino, DateOnly hoy)
            => Math.Max(0, hoy.DayNumber - (ultimaVisita ?? desdeSiNuncaVino).DayNumber);

        /// <summary>
        /// Un socio está en riesgo cuando tiene la cuota al día (o por vencer) pero hace <paramref name="umbral"/> días
        /// o más que no viene: paga y no usa el gimnasio, que es lo que suele pasar antes de que se dé de baja.
        /// Los de cuota vencida o sin plan ya se ven en las listas de cuotas.
        /// </summary>
        public static bool EstaEnRiesgo(EstadoDeCuota estado, int diasSinVenir, int umbral = DiasSinVenirPorDefecto)
            => estado is EstadoDeCuota.AlDia or EstadoDeCuota.PorVencer && diasSinVenir >= umbral;
    }
}
