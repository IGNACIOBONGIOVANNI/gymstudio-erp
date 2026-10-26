using System;
namespace EstudioContable.Domain.Entities
{
    public class ClienteComercial
    {
        public long Id { get; set; }
        public string RazonSocial { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Localidad { get; set; } = string.Empty;
        public string Cuit { get; set; } = string.Empty;
        public string CondicionIva { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
    }
}