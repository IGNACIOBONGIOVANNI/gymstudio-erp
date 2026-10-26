// =====================================================================
// EstudioContable.Domain / Entities
// Entidades "ricas" (no simples DTOs): validan invariantes de negocio
// en el propio constructor/métodos, no sólo en la capa de Application.
// =====================================================================
using System;
using System.Collections.Generic;
using System.Linq;

namespace EstudioContable.Domain.Entities
{
    public enum RolUsuario
    {
        Administrador,
        Contador,
        Cliente,
        // Se persiste como entero: los valores nuevos van siempre al final.
        Recepcion
    }

    public enum TipoContribuyente
    {
        Monotributista,
        ResponsableInscripto,
        ExentoIVA,
        ConsumidorFinal
    }

    public enum EstadoAsiento
    {
        Borrador,
        Confirmado,
        Anulado
    }

    public enum NaturalezaCuenta
    {
        Activo,
        Pasivo,
        PatrimonioNeto,
        Resultado
    }

    public class Cliente
    {
        public long Id { get; private set; }
        public string RazonSocial { get; private set; } = default!;
        public string? NombreFantasia { get; private set; }
        public string Cuit { get; private set; } = default!;
        public TipoContribuyente TipoContribuyente { get; private set; }
        public string? Email { get; private set; }
        public string? Telefono { get; private set; }
        public string? DomicilioFiscal { get; private set; }
        public bool Activo { get; private set; } = true;
        public DateTimeOffset FechaAlta { get; private set; } = DateTimeOffset.UtcNow;

        private readonly List<Usuario> _usuarios = new();
        public IReadOnlyCollection<Usuario> Usuarios => _usuarios.AsReadOnly();

        protected Cliente() { } // EF Core

        public Cliente(string razonSocial, string cuit, TipoContribuyente tipo)
        {
            if (string.IsNullOrWhiteSpace(razonSocial))
                throw new ArgumentException("La razón social es obligatoria.", nameof(razonSocial));

            if (cuit is null || cuit.Length != 11 || !cuit.All(char.IsDigit))
                throw new ArgumentException("El CUIT debe tener 11 dígitos numéricos.", nameof(cuit));

            RazonSocial = razonSocial;
            Cuit = cuit;
            TipoContribuyente = tipo;
        }

        public void Desactivar() => Activo = false;
    }

    public class Usuario
    {
        public long Id { get; private set; }
        public string NombreCompleto { get; private set; } = default!;
        public string Email { get; private set; } = default!;
        public string PasswordHash { get; private set; } = default!;
        public RolUsuario Rol { get; private set; }
        public long? ClienteId { get; private set; }
        public Cliente? Cliente { get; private set; }
        public string? Telefono { get; private set; }
        public bool Activo { get; private set; } = true;

        // El administrador principal no puede eliminarse, desactivarse ni perder
        // el rol de Administrador: garantiza que siempre quede alguien con acceso total.
        public bool EsPrincipal { get; private set; }
        public DateTimeOffset? UltimoLogin { get; private set; }
        public DateTimeOffset FechaCreacion { get; private set; } = DateTimeOffset.UtcNow;

        protected Usuario() { }

        public Usuario(string nombreCompleto, string email, string passwordHash, RolUsuario rol,
            long? clienteId = null, string? telefono = null, bool esPrincipal = false)
        {
            if (rol == RolUsuario.Cliente && clienteId is null)
                throw new InvalidOperationException("Un usuario con rol Cliente debe estar vinculado a un Cliente.");
            if (rol != RolUsuario.Cliente && clienteId is not null)
                throw new InvalidOperationException("Sólo los usuarios con rol Cliente pueden vincularse a un Cliente.");
            if (esPrincipal && rol != RolUsuario.Administrador)
                throw new InvalidOperationException("El usuario principal debe ser Administrador.");

            AsignarDatos(nombreCompleto, email, telefono);
            CambiarPassword(passwordHash);
            Rol = rol;
            ClienteId = clienteId;
            EsPrincipal = esPrincipal;
        }

        public void ActualizarDatos(string nombreCompleto, string email, RolUsuario rol, string? telefono)
        {
            if (EsPrincipal && rol != RolUsuario.Administrador)
                throw new InvalidOperationException("El administrador principal no puede cambiar de rol.");
            if (rol == RolUsuario.Cliente && ClienteId is null)
                throw new InvalidOperationException("Un usuario con rol Cliente debe estar vinculado a un Cliente.");

            AsignarDatos(nombreCompleto, email, telefono);
            Rol = rol;
        }

        public void CambiarPassword(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("El hash de la contraseña es obligatorio.", nameof(passwordHash));
            PasswordHash = passwordHash;
        }

        public void Activar() => Activo = true;

        public void Desactivar()
        {
            if (EsPrincipal)
                throw new InvalidOperationException("El administrador principal no puede desactivarse.");
            Activo = false;
        }

        public void RegistrarLogin() => UltimoLogin = DateTimeOffset.UtcNow;

        /// <summary>Normaliza el email (minúsculas, sin espacios) para compararlo y guardarlo.</summary>
        public static string NormalizarEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        private void AsignarDatos(string nombreCompleto, string email, string? telefono)
        {
            if (string.IsNullOrWhiteSpace(nombreCompleto))
                throw new ArgumentException("El nombre es obligatorio.", nameof(nombreCompleto));

            var emailNormalizado = NormalizarEmail(email);
            if (emailNormalizado.Length < 3 || !emailNormalizado.Contains('@'))
                throw new ArgumentException("El email no es válido.", nameof(email));

            NombreCompleto = nombreCompleto.Trim();
            Email = emailNormalizado;
            Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();
        }
    }

    public class CuentaContable
    {
        public long Id { get; private set; }
        public long ClienteId { get; private set; }
        public string Codigo { get; private set; } = default!;
        public string Nombre { get; private set; } = default!;
        public NaturalezaCuenta Naturaleza { get; private set; }
        public long? CuentaPadreId { get; private set; }
        public bool Activa { get; private set; } = true;

        protected CuentaContable() { }

        public CuentaContable(long clienteId, string codigo, string nombre, NaturalezaCuenta naturaleza)
        {
            ClienteId = clienteId;
            Codigo = codigo;
            Nombre = nombre;
            Naturaleza = naturaleza;
        }
    }
}
