using System;
using System.Linq;
using EstudioContable.Domain;

namespace EstudioContable.Application.Comercial
{
    /// <summary>
    /// Datos de un cliente o proveedor tal como llegan del formulario. Es lo único que
    /// acepta la API: el Id y el estado Activo nunca vienen del cliente.
    /// </summary>
    public record DatosComerciales(string? RazonSocial, string? Direccion, string? Localidad, string? Cuit, string? CondicionIva)
    {
        public static readonly string[] CondicionesIva = { "Responsable Inscripto", "Monotributo", "Exento", "Consumidor Final" };

        /// <summary>
        /// Valida y normaliza los datos. Lanza ArgumentException con un mensaje para el
        /// usuario. Devuelve el CUIT con formato 20-11111111-2.
        /// </summary>
        public DatosComercialesValidados Validar()
        {
            var razonSocial = (RazonSocial ?? string.Empty).Trim();
            if (razonSocial.Length == 0)
                throw new ArgumentException("La razón social es obligatoria.");
            if (razonSocial.Length > 150)
                throw new ArgumentException("La razón social no puede superar los 150 caracteres.");

            var condicion = CondicionesIva.FirstOrDefault(c => string.Equals(c, CondicionIva?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Condición frente al IVA inválida. Opciones: {string.Join(", ", CondicionesIva)}.");

            if (!Domain.Cuit.EsValido(Cuit))
                throw new ArgumentException("El CUIT no es válido: debe tener 11 dígitos y un dígito verificador correcto (ej. 20-12345678-6).");

            var direccion = (Direccion ?? string.Empty).Trim();
            var localidad = (Localidad ?? string.Empty).Trim();
            if (direccion.Length > 200 || localidad.Length > 100)
                throw new ArgumentException("La dirección (máx. 200) o la localidad (máx. 100) son demasiado largas.");

            return new DatosComercialesValidados(razonSocial, direccion, localidad, Domain.Cuit.Formatear(Domain.Cuit.Normalizar(Cuit)), condicion);
        }
    }

    public record DatosComercialesValidados(string RazonSocial, string Direccion, string Localidad, string Cuit, string CondicionIva);
}
