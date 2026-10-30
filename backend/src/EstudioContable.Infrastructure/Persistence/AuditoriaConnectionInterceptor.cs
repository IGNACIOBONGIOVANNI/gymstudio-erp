using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EstudioContable.Infrastructure.Persistence
{
    /// <summary>
    /// Cada vez que EF abre una conexión, guarda en la sesión de PostgreSQL quién trabaja y desde qué
    /// equipo: el email del usuario logueado ("app.usuario") y la IP, el sistema operativo y el navegador
    /// ("app.ip", "app.sistema_operativo", "app.navegador"). El trigger de auditoría las lee con
    /// current_setting(...) para registrar quién hizo cada cambio y desde dónde. Se vuelven a escribir en
    /// cada apertura: una conexión del pool nunca arrastra los datos de otra request.
    /// </summary>
    public class AuditoriaConnectionInterceptor : DbConnectionInterceptor
    {
        private const string Sql =
            "SELECT set_config('app.usuario', @usuario, false), set_config('app.ip', @ip, false), " +
            "set_config('app.sistema_operativo', @so, false), set_config('app.navegador', @navegador, false)";
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
            Agregar(command, "usuario", _usuarioActual.Email ?? "sistema");
            // Vacío (no null) cuando no hay request: el trigger lo guarda como NULL.
            Agregar(command, "ip", _usuarioActual.Ip ?? string.Empty);
            Agregar(command, "so", _usuarioActual.SistemaOperativo ?? string.Empty);
            Agregar(command, "navegador", _usuarioActual.Navegador ?? string.Empty);
            return command;
        }

        private static void Agregar(DbCommand command, string nombre, string valor)
        {
            var parametro = command.CreateParameter();
            parametro.ParameterName = nombre;
            parametro.Value = valor;
            command.Parameters.Add(parametro);
        }
    }
}
