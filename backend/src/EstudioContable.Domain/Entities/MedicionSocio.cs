using System;

namespace EstudioContable.Domain.Entities
{
    /// <summary>
    /// Una medición del socio en una fecha: peso (obligatorio) y, si se tomaron, porcentaje de grasa
    /// y perímetros. Sirve para ver cómo evoluciona. Hay una sola por socio y día.
    /// </summary>
    public class MedicionSocio
    {
        public long Id { get; private set; }
        public long SocioId { get; private set; }
        public DateOnly Fecha { get; private set; }
        public decimal PesoKg { get; private set; }
        public decimal? GrasaCorporalPct { get; private set; }
        public decimal? CinturaCm { get; private set; }
        public decimal? CaderaCm { get; private set; }
        public decimal? PechoCm { get; private set; }
        public decimal? BrazoCm { get; private set; }
        public decimal? MusloCm { get; private set; }
        public string Notas { get; private set; } = string.Empty;

        protected MedicionSocio() { } // EF Core

        public MedicionSocio(long socioId, DateOnly fecha, decimal pesoKg, decimal? grasaCorporalPct, decimal? cinturaCm,
            decimal? caderaCm, decimal? pechoCm, decimal? brazoCm, decimal? musloCm, string? notas, DateOnly hoy)
        {
            if (socioId <= 0) throw new ArgumentException("Elegí el socio.");
            SocioId = socioId;
            Actualizar(fecha, pesoKg, grasaCorporalPct, cinturaCm, caderaCm, pechoCm, brazoCm, musloCm, notas, hoy);
        }

        public void Actualizar(DateOnly fecha, decimal pesoKg, decimal? grasaCorporalPct, decimal? cinturaCm,
            decimal? caderaCm, decimal? pechoCm, decimal? brazoCm, decimal? musloCm, string? notas, DateOnly hoy)
        {
            if (fecha.Year < 2000) throw new ArgumentException("La fecha de la medición no es válida.");
            // Un día de margen por la diferencia horaria entre el navegador y el servidor.
            if (fecha > hoy.AddDays(1)) throw new ArgumentException("La fecha de la medición no puede ser futura.");

            if (pesoKg < 20 || pesoKg > 400) throw new ArgumentException("El peso debe estar entre 20 y 400 kg.");
            Rango(grasaCorporalPct, 2, 70, "La grasa corporal", "%");
            Rango(cinturaCm, 30, 250, "La cintura", " cm");
            Rango(caderaCm, 30, 250, "La cadera", " cm");
            Rango(pechoCm, 30, 250, "El pecho", " cm");
            Rango(brazoCm, 10, 100, "El brazo", " cm");
            Rango(musloCm, 20, 150, "El muslo", " cm");

            var n = (notas ?? string.Empty).Trim();
            if (n.Length > 300) throw new ArgumentException("Las notas no pueden superar los 300 caracteres.");

            Fecha = fecha;
            PesoKg = Math.Round(pesoKg, 1);
            GrasaCorporalPct = Redondear(grasaCorporalPct);
            CinturaCm = Redondear(cinturaCm);
            CaderaCm = Redondear(caderaCm);
            PechoCm = Redondear(pechoCm);
            BrazoCm = Redondear(brazoCm);
            MusloCm = Redondear(musloCm);
            Notas = n;
        }

        private static decimal? Redondear(decimal? v) => v.HasValue ? Math.Round(v.Value, 1) : null;

        private static void Rango(decimal? valor, decimal minimo, decimal maximo, string nombre, string unidad)
        {
            if (valor.HasValue && (valor < minimo || valor > maximo))
                throw new ArgumentException($"{nombre} debe estar entre {minimo}{unidad} y {maximo}{unidad} (o dejarse vacío).");
        }
    }
}
