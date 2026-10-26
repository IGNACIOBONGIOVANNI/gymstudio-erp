using EstudioContable.Domain.Entities;

namespace EstudioContable.API.Auth
{
    /// <summary>
    /// Nombres de rol tal como viajan en el claim "role" del JWT, para usar en
    /// [Authorize(Roles = ...)].
    /// </summary>
    public static class Roles
    {
        public const string Administrador = nameof(RolUsuario.Administrador);
        public const string Contador = nameof(RolUsuario.Contador);
        public const string Recepcion = nameof(RolUsuario.Recepcion);

        // Operaciones contables sensibles (anular, confirmar, asientos manuales).
        public const string Gestion = Administrador + "," + Contador;
    }
}
