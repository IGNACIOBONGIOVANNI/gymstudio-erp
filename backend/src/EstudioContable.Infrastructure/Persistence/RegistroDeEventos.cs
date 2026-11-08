using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EstudioContable.Infrastructure.Persistence
{
    /// <inheritdoc />
    public class RegistroDeEventos : IRegistroDeEventos
    {
        private const int LargoMaximoDelUsuario = 254;
        private const int LargoMaximoDelRegistro = 50;   // auditoria.registro_id es varchar(50)

        private readonly AppDbContext _context;
        private readonly IUsuarioActual _equipo;
        private readonly ILogger<RegistroDeEventos> _log;

        public RegistroDeEventos(AppDbContext context, IUsuarioActual equipo, ILogger<RegistroDeEventos> log)
        {
            _context = context;
            _equipo = equipo;
            _log = log;
        }

        public async Task RegistrarAsync(string tabla, string operacion, string usuario, CancellationToken cancellationToken, string? registroId = null)
        {
            try
            {
                var quien = Sanear(usuario, LargoMaximoDelUsuario);
                var fila = string.IsNullOrWhiteSpace(registroId) ? null : Sanear(registroId, LargoMaximoDelRegistro);
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO contabilidad.auditoria
                        (tabla, operacion, registro_id, campos_modificados, usuario, fecha, ip, sistema_operativo, navegador)
                    VALUES
                        ({tabla}, {operacion}, {fila}::varchar, '{{}}'::text[], {quien}, now(),
                         {_equipo.Ip}::varchar, {_equipo.SistemaOperativo}::varchar, {_equipo.Navegador}::varchar)", cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "No se pudo registrar el evento {Tabla}/{Operacion}.", tabla, operacion);
            }
        }

        /// <summary>Texto que viene del cliente, listo para guardar: sin caracteres de control y con tope de largo.</summary>
        internal static string Sanear(string? texto, int maximo)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "(vacío)";
            var sb = new StringBuilder(Math.Min(texto.Length, maximo));
            foreach (var c in texto)
            {
                if (sb.Length >= maximo) break;
                sb.Append(char.IsControl(c) ? ' ' : c);
            }
            return sb.ToString().Trim();
        }
    }
}
