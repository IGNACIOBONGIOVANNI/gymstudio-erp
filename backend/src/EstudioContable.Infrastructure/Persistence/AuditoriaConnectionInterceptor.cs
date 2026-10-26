using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EstudioContable.Infrastructure.Persistence
{
    /// <summary>
    /// Cada vez que EF abre una conexión, guarda en la sesión de PostgreSQL el email del
    /// usuario logueado (variable "app.usuario"). El trigger de auditoría la lee con
    /// current_setting('app.usuario') para registrar quién hizo cada cambio.
    /// </summary>
    public class AuditoriaConnectionInterceptor : DbConnectionInterceptor
    {
        private const string Sql = "SELECT set_config('app.usuario', @usuario, false)";
        private readonly IUsuarioActual _usuarioActual;

        public AuditoriaConnectionInterceptor(IUsuarioActual usuarioActual)
        {
            _usuarioActual = usuarioActual;
        }

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            using var command = CrearComando(connection);
            command.ExecuteNonQuery();
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            await using var command = CrearComando(connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private DbCommand CrearComando(DbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = Sql;
            var parametro = command.CreateParameter();
            parametro.ParameterName = "usuario";
            parametro.Value = _usuarioActual.Email ?? "sistema";
            command.Parameters.Add(parametro);
            return command;
        }
    }
}
