using System;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Copias;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EstudioContable.API.Copias
{
    /// <summary>
    /// La tarea en segundo plano de las copias automáticas: espera un rato a que el sistema termine de arrancar (y de migrar la base)
    /// y después, cada minuto, le pregunta al servicio si toca hacer una copia. Se puede apagar con <c>Copias:TareaAutomatica = false</c>.
    /// </summary>
    public sealed class CopiasAutomaticasHostedService : BackgroundService
    {
        public static readonly TimeSpan EsperaInicial = TimeSpan.FromSeconds(20);
        public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<CopiasAutomaticasHostedService> _log;

        public CopiasAutomaticasHostedService(IServiceScopeFactory scopes, ILogger<CopiasAutomaticasHostedService> log)
        {
            _scopes = scopes;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(EsperaInicial, stoppingToken);
                using var temporizador = new PeriodicTimer(Intervalo);
                do
                {
                    await UnaVueltaAsync(_scopes, _log, stoppingToken);
                }
                while (await temporizador.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // El servidor se está apagando: no es un error.
            }
        }

        /// <summary>
        /// Una vuelta: abre un alcance y le pregunta al servicio si toca. Cualquier falla se registra y se traga, porque una excepción sin
        /// atrapar en una tarea en segundo plano apaga todo el servidor; la única que se deja pasar es la cancelación.
        /// </summary>
        public static async Task UnaVueltaAsync(IServiceScopeFactory scopes, ILogger log, CancellationToken cancellationToken)
        {
            try
            {
                using var alcance = scopes.CreateScope();
                var servicio = alcance.ServiceProvider.GetRequiredService<ICopiasAutomaticasService>();
                await servicio.EjecutarSiTocaAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Falló una vuelta de la tarea de copias automáticas; se vuelve a intentar en la vuelta siguiente.");
            }
        }
    }
}
