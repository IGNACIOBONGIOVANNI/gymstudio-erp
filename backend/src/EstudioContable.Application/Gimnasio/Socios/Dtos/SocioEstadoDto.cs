namespace EstudioContable.Application.Gimnasio.Socios.Dtos
{
    public class SocioEstadoDto
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public bool AlDia { get; set; } // true = Al Día, false = Vencido
    }
}
