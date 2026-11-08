using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Copias;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain;
using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;
using EstudioContable.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EstudioContable.Infrastructure.Copias
{
    /// <inheritdoc />
    /// <remarks>
    /// Una sola copia a la vez en todo el proceso (un candado): la tarea en segundo plano y «Hacer una copia ahora» no se pisan. Cada
    /// copia se escribe primero en un archivo temporal y después se renombra, así nunca queda un archivo a medias con nombre de copia.
    /// </remarks>
    public sealed class CopiasAutomaticasService : ICopiasAutomaticasService
    {
        /// <summary>Lo único que ve el usuario cuando falla el guardado: el detalle técnico va al registro del servidor.</summary>
        public const string MensajeDeFalla = "No se pudo guardar la copia en la carpeta de copias automáticas. Revisá los permisos y el espacio en disco.";

        private const string TablaDeAuditoria = "copia";
        private const string UsuarioDelSistema = "sistema";

        private static readonly SemaphoreSlim Candado = new(1, 1);

        private readonly AppDbContext _context;
        private readonly ICopiasDeSeguridadService _copias;
        private readonly IRegistroDeEventos _eventos;
        private readonly IUsuarioActual _usuario;
        private readonly TimeProvider _reloj;
        private readonly ILogger<CopiasAutomaticasService> _log;
        private readonly string _carpeta;

        public CopiasAutomaticasService(AppDbContext context, ICopiasDeSeguridadService copias, IRegistroDeEventos eventos, IUsuarioActual usuario,
            TimeProvider reloj, IConfiguration configuracion, ILogger<CopiasAutomaticasService> log)
        {
            _context = context;
            _copias = copias;
            _eventos = eventos;
            _usuario = usuario;
            _reloj = reloj;
            _log = log;
            var carpeta = configuracion["Copias:Carpeta"];
            _carpeta = Path.TrimEndingDirectorySeparator(Path.GetFullPath(string.IsNullOrWhiteSpace(carpeta) ? Path.Combine("backups", "automaticas") : carpeta));
        }

        // ------------------------------------------------------------------ la programación

        public async Task<EstadoDeCopiasAutomaticas> ObtenerAsync(CancellationToken cancellationToken)
            => Estado(await ObtenerOCrearAsync(cancellationToken));

        public async Task<EstadoDeCopiasAutomaticas> ProgramarAsync(ProgramacionDeCopias pedido, CancellationToken cancellationToken)
        {
            var frecuencia = ParsearFrecuencia(pedido.Frecuencia);
            var config = await ObtenerOCrearAsync(cancellationToken);
            config.Programar(pedido.Activa, frecuencia, pedido.Hora, pedido.Minuto, pedido.DiaDeLaSemana, pedido.DiaDelMes, pedido.Conservar, _reloj.GetUtcNow());
            await _context.SaveChangesAsync(cancellationToken);
            await _eventos.RegistrarAsync(TablaDeAuditoria, "PROGRAMADA", _usuario.Email ?? UsuarioDelSistema, cancellationToken);
            return Estado(config);
        }

        // El nombre exacto del valor: Enum.TryParse también acepta números ("3") y listas ("CadaSeisHoras,CadaDoceHoras").
        private static FrecuenciaDeCopia ParsearFrecuencia(string? texto)
        {
            if (texto != null && Enum.TryParse<FrecuenciaDeCopia>(texto, ignoreCase: false, out var frecuencia)
                && Enum.IsDefined(frecuencia) && texto == frecuencia.ToString())
                return frecuencia;
            throw new ArgumentException("La frecuencia de la copia no es válida.");
        }

        private async Task<ConfiguracionDeCopias> ObtenerOCrearAsync(CancellationToken cancellationToken)
        {
            var config = await _context.ConfiguracionesDeCopias.FirstOrDefaultAsync(c => c.Id == ConfiguracionDeCopias.IdUnico, cancellationToken);
            if (config != null) return config;
            config = new ConfiguracionDeCopias();
            _context.ConfiguracionesDeCopias.Add(config);
            await _context.SaveChangesAsync(cancellationToken);
            return config;
        }

        private static EstadoDeCopiasAutomaticas Estado(ConfiguracionDeCopias c)
            => new(c.Activa, c.Frecuencia.ToString(), c.Hora, c.Minuto, c.DiaDeLaSemana, c.DiaDelMes, c.Conservar,
                ReglasDeCopiasAutomaticas.ProximaCopia(c), c.UltimaCopia, c.UltimoIntento, c.UltimoError);

        // ------------------------------------------------------------------ hacer copias

        public async Task<CopiaGuardada> GuardarAhoraAsync(CancellationToken cancellationToken)
        {
            await Candado.WaitAsync(cancellationToken);
            try
            {
                var config = await ObtenerOCrearAsync(cancellationToken);
                CopiaGuardada copia;
                try
                {
                    copia = await GenerarYGuardarAsync(config, _reloj.GetUtcNow(), cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "No se pudo guardar la copia pedida desde la pantalla en {Carpeta}.", _carpeta);
                    _context.ChangeTracker.Clear();
                    throw new InvalidOperationException(MensajeDeFalla);
                }
                await _eventos.RegistrarAsync(TablaDeAuditoria, "GUARDADA", _usuario.Email ?? UsuarioDelSistema, cancellationToken);
                return copia;
            }
            finally
            {
                Candado.Release();
            }
        }

        public async Task<CopiaGuardada?> EjecutarSiTocaAsync(CancellationToken cancellationToken)
        {
            if (!await Candado.WaitAsync(0, cancellationToken)) return null;   // ya hay otra copia en curso
            try
            {
                ConfiguracionDeCopias config;
                try
                {
                    config = await ObtenerOCrearAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Por ejemplo, el arranque con la migración todavía sin aplicar: se vuelve a mirar en la vuelta siguiente.
                    _log.LogWarning(ex, "No se pudo leer la programación de las copias automáticas.");
                    _context.ChangeTracker.Clear();
                    return null;
                }

                var ahora = _reloj.GetUtcNow();
                if (!ReglasDeCopiasAutomaticas.Toca(config, ahora)) return null;

                try
                {
                    var copia = await GenerarYGuardarAsync(config, ahora, cancellationToken);
                    await _eventos.RegistrarAsync(TablaDeAuditoria, "AUTOMATICA", UsuarioDelSistema, cancellationToken);
                    return copia;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "Falló la copia automática en {Carpeta}; se reintenta en {Minutos} minutos.", _carpeta, ReglasDeCopiasAutomaticas.EsperaTrasUnaFalla.TotalMinutes);
                    await RegistrarFallaAsync(ahora, cancellationToken);
                    return null;
                }
            }
            finally
            {
                Candado.Release();
            }
        }

        private async Task RegistrarFallaAsync(DateTimeOffset ahora, CancellationToken cancellationToken)
        {
            try
            {
                _context.ChangeTracker.Clear();   // lo que quedó a medias en memoria no vale
                var config = await ObtenerOCrearAsync(cancellationToken);
                config.RegistrarFalla(ahora, MensajeDeFalla);
                await _context.SaveChangesAsync(cancellationToken);
                await _eventos.RegistrarAsync(TablaDeAuditoria, "FALLIDA", UsuarioDelSistema, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "No se pudo anotar la falla de la copia automática.");
            }
        }

        /// <summary>Genera la copia, la escribe (primero temporal, después con su nombre), la anota y borra las que sobran.</summary>
        private async Task<CopiaGuardada> GenerarYGuardarAsync(ConfiguracionDeCopias config, DateTimeOffset ahora, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(_carpeta);
            BorrarTemporalesHuerfanos();

            var zip = await _copias.GenerarAsync(cancellationToken);
            var nombre = ReglasDeCopiasAutomaticas.NombreDeArchivo(ahora);
            var destino = Path.Combine(_carpeta, nombre);
            var temporal = destino + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporal, zip, cancellationToken);
                File.Move(temporal, destino, overwrite: true);   // dos copias en el mismo segundo: queda la última
            }
            catch
            {
                if (File.Exists(temporal)) File.Delete(temporal);
                throw;
            }

            config.RegistrarCopia(ahora);
            await _context.SaveChangesAsync(cancellationToken);
            AplicarRetencion(config.Conservar);

            var info = new FileInfo(destino);
            return new CopiaGuardada(nombre, info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
        }

        // Los temporales que dejó un corte a mitad de una copia, y solo los de este servicio.
        private void BorrarTemporalesHuerfanos()
        {
            foreach (var archivo in Directory.EnumerateFiles(_carpeta, "gymstudio_auto_*.zip.tmp"))
            {
                try { File.Delete(archivo); }
                catch (Exception ex) { _log.LogWarning(ex, "No se pudo borrar el temporal {Archivo}.", archivo); }
            }
        }

        // Se conservan las últimas N entre los archivos que se llaman exactamente como una copia automática; nada más se toca.
        private void AplicarRetencion(int conservar)
        {
            var sobrantes = NombresDeCopias().Skip(conservar);
            foreach (var nombre in sobrantes)
            {
                try { File.Delete(Path.Combine(_carpeta, nombre)); }
                catch (Exception ex) { _log.LogWarning(ex, "No se pudo borrar la copia vieja {Copia}.", nombre); }
            }
        }

        // De la más nueva a la más vieja: el nombre lleva la fecha y la hora, así que ordenar por nombre es ordenar por fecha.
        private IEnumerable<string> NombresDeCopias()
            => Directory.Exists(_carpeta)
                ? Directory.EnumerateFiles(_carpeta).Select(f => Path.GetFileName(f)!)
                    .Where(ReglasDeCopiasAutomaticas.EsNombreDeCopiaAutomatica)
                    .OrderByDescending(n => n, StringComparer.Ordinal)
                    .ToList()
                : Enumerable.Empty<string>();

        // ------------------------------------------------------------------ listar y descargar

        public IReadOnlyList<CopiaGuardada> Listar()
            => NombresDeCopias().Select(n =>
            {
                var info = new FileInfo(Path.Combine(_carpeta, n));
                return new CopiaGuardada(n, info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
            }).ToList();

        public Stream? AbrirParaDescargar(string nombre)
        {
            if (!ReglasDeCopiasAutomaticas.EsNombreDeCopiaAutomatica(nombre)) return null;
            var ruta = Path.GetFullPath(Path.Combine(_carpeta, nombre));
            // Aunque el nombre ya pasó la regla, la ruta final tiene que seguir dentro de la carpeta.
            if (!ruta.StartsWith(_carpeta + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(ruta)) return null;
            return new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}
