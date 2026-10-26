namespace EstudioContable.Application.Gimnasio.Socios.Dtos
{
    public class SocioDto
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public string? PlanActualNombre { get; set; }
        public System.DateTime? FechaVencimiento { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}
