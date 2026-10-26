namespace EstudioContable.Application.Gimnasio.Planes.Dtos
{
    public class PlanDto
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal PrecioMensual { get; set; }
        public int DuracionMeses { get; set; }
        public bool Activo { get; set; }
    }
}
