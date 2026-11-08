using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Copias;
using EstudioContable.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace EstudioContable.Infrastructure.Copias
{
    /// <inheritdoc />
    /// <remarks>
    /// Una copia es un .zip con <c>manifiesto.json</c> y un CSV por tabla (<c>datos/esquema.tabla.csv</c>). El manifiesto trae
    /// la versión, la migración y, por tabla, la cantidad de filas, la huella de su contenido (la misma que calculan los scripts
    /// de PowerShell) y el SHA-256 de su CSV. Los nombres de tabla de un archivo no se usan jamás en una consulta si no existen
    /// en la base actual: así una copia manipulada no puede inyectar nada.
    /// </remarks>
    public sealed class CopiasDeSeguridadService : ICopiasDeSeguridadService
    {
        private const string Formato = "gymstudio-copia-v1";
        private const long BytesMaximosDeLaCopia = 50L * 1024 * 1024;
        private const long BytesMaximosDescomprimidos = 300L * 1024 * 1024;
        private const int EntradasMaximas = 100;

        private static readonly Regex NombreDeTabla = new(@"^[A-Za-z0-9_]{1,60}\.[A-Za-z0-9_]{1,60}$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex NombreDeArchivo = new(@"^datos/[A-Za-z0-9_]{1,60}\.[A-Za-z0-9_]{1,60}\.csv$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        private static readonly UTF8Encoding Utf8SinBom = new(false);

        // Por cada tabla: filas y huella (MD5 de todo su contenido, ordenado). Es la misma consulta que usan los scripts.
        private const string SqlTablas = """
            SELECT t.table_schema || '.' || t.table_name AS tabla,
                   (xpath('/row/n/text()', query_to_xml(format('SELECT count(*) AS n FROM %I.%I', t.table_schema, t.table_name), false, true, '')))[1]::text AS filas,
                   (xpath('/row/h/text()', query_to_xml(format('SELECT md5(coalesce(string_agg(x::text, ''|'' ORDER BY x::text), '''')) AS h FROM %I.%I x', t.table_schema, t.table_name), false, true, '')))[1]::text AS huella
            FROM information_schema.tables t
            WHERE t.table_type = 'BASE TABLE' AND t.table_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY 1
            """;

        private const string SqlMigracion = """SELECT coalesce(max("MigrationId"), '') || '|' || count(*) FROM public."__EFMigrationsHistory" """;

        // Después de cargar los datos con sus propios números de id, las secuencias tienen que seguir desde el mayor.
        private const string SqlSecuencias = """
            DO $$
            DECLARE r record;
            BEGIN
                FOR r IN
                    SELECT c.table_schema AS s, c.table_name AS t, c.column_name AS col,
                           pg_get_serial_sequence(format('%I.%I', c.table_schema, c.table_name), c.column_name) AS seq
                    FROM information_schema.columns c
                    JOIN information_schema.tables tb ON tb.table_schema = c.table_schema AND tb.table_name = c.table_name AND tb.table_type = 'BASE TABLE'
                    WHERE c.table_schema NOT IN ('pg_catalog', 'information_schema')
                LOOP
                    IF r.seq IS NOT NULL THEN
                        EXECUTE format('SELECT setval(%L, COALESCE((SELECT max(%I) FROM %I.%I), 0) + 1, false)', r.seq, r.col, r.s, r.t);
                    END IF;
                END LOOP;
            END $$;
            """;

        private readonly AppDbContext _context;
        private readonly string _version;
        private readonly byte[]? _claveDeFirma;

        public CopiasDeSeguridadService(AppDbContext context, IConfiguration configuracion)
        {
            _context = context;
            _version = configuracion["Sistema:Version"] ?? "1.0.0";
            // La firma usa su propia clave (Copias:ClaveDeFirma) o, si no hay, la de las sesiones (Jwt:Key): ninguna está en el archivo.
            var clave = configuracion["Copias:ClaveDeFirma"];
            if (string.IsNullOrWhiteSpace(clave)) clave = configuracion["Jwt:Key"];
            _claveDeFirma = string.IsNullOrWhiteSpace(clave) ? null : SHA256.HashData(Utf8SinBom.GetBytes("gymstudio-copia-v1|" + clave));
        }

        // ------------------------------------------------------------------ modelos del archivo

        private sealed class Manifiesto
        {
            public string Formato { get; set; } = "";
            public DateTimeOffset Fecha { get; set; }
            public string Version { get; set; } = "";
            public string Migracion { get; set; } = "";
            public int Migraciones { get; set; }
            public Dictionary<string, TablaDelManifiesto> Tablas { get; set; } = new();
            /// <summary>HMAC-SHA256 del resto del manifiesto: detecta que alguien lo editó y recalculó los SHA-256 de los archivos.</summary>
            public string? Firma { get; set; }
        }

        private sealed class TablaDelManifiesto
        {
            public long Filas { get; set; }
            public string Huella { get; set; } = "";
            public string Archivo { get; set; } = "";
            public string Sha256 { get; set; } = "";
        }

        private sealed record EstadoDeBase(SortedDictionary<string, (long Filas, string Huella)> Tablas, string Migracion, int Migraciones);

        private sealed record CopiaLeida(Manifiesto Manifiesto, Dictionary<string, byte[]> Archivos);

        // ------------------------------------------------------------------ generar

        public async Task<byte[]> GenerarAsync(CancellationToken cancellationToken)
        {
            var conexion = await AbrirAsync(cancellationToken);
            try
            {
                // Una foto consistente: la huella y el contenido de cada tabla salen del mismo instante.
                await using var transaccion = await conexion.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
                var estado = await LeerEstadoAsync(conexion, transaccion, cancellationToken);
                var manifiesto = new Manifiesto
                {
                    Formato = Formato, Fecha = DateTimeOffset.UtcNow, Version = _version,
                    Migracion = estado.Migracion, Migraciones = estado.Migraciones,
                };

                using var memoria = new MemoryStream();
                using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var (tabla, datos) in estado.Tablas)
                    {
                        var csv = await ExportarAsync(conexion, tabla, cancellationToken);
                        var archivo = $"datos/{tabla}.csv";
                        var entrada = zip.CreateEntry(archivo, CompressionLevel.Optimal);
                        await using (var salida = entrada.Open()) await salida.WriteAsync(csv, cancellationToken);
                        manifiesto.Tablas[tabla] = new TablaDelManifiesto
                        {
                            Filas = datos.Filas, Huella = datos.Huella, Archivo = archivo, Sha256 = Convert.ToHexString(SHA256.HashData(csv)),
                        };
                    }
                    manifiesto.Firma = Firmar(manifiesto);
                    var entradaManifiesto = zip.CreateEntry("manifiesto.json", CompressionLevel.Optimal);
                    await using (var salida = entradaManifiesto.Open())
                        await JsonSerializer.SerializeAsync(salida, manifiesto, OpcionesJson, cancellationToken);
                }
                await transaccion.CommitAsync(cancellationToken);
                return memoria.ToArray();
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        // ------------------------------------------------------------------ comparar

        public async Task<ComparacionDeCopia> CompararAsync(Stream zip, CancellationToken cancellationToken)
        {
            var copia = Leer(zip);
            var conexion = await AbrirAsync(cancellationToken);
            try
            {
                var actual = await LeerEstadoAsync(conexion, null, cancellationToken);
                var manifiesto = copia.Manifiesto;
                var mismoEsquema = manifiesto.Migracion == actual.Migracion;

                var tablas = new List<TablaComparada>();
                foreach (var nombre in manifiesto.Tablas.Keys.Union(actual.Tablas.Keys).OrderBy(n => n, StringComparer.Ordinal))
                {
                    var enCopia = manifiesto.Tablas.TryGetValue(nombre, out var c) ? c : null;
                    var ahora = actual.Tablas.TryGetValue(nombre, out var a) ? ((long Filas, string Huella)?)a : null;
                    var estado = enCopia == null ? "solo-ahora"
                        : ahora == null ? "solo-copia"
                        : enCopia.Filas == ahora.Value.Filas && enCopia.Huella == ahora.Value.Huella ? "igual" : "cambia";
                    var (etiqueta, grupo, orden) = EtiquetasDeTablas.De(nombre);
                    tablas.Add(new TablaComparada(nombre, etiqueta, grupo, orden, enCopia?.Filas, ahora?.Filas, estado, EtiquetasDeTablas.SeActualizaSola(nombre)));
                }
                tablas = tablas.OrderBy(t => t.Orden).ThenBy(t => t.Tabla, StringComparer.Ordinal).ToList();

                // La auditoría crece con cada acción (hasta con descargar la copia) y la programación de las copias anota la última copia después
                // de sacarla: no cuentan para decir si los datos son iguales (ver EtiquetasDeTablas.SeActualizaSola).
                var veredicto = !mismoEsquema ? "otra-version"
                    : tablas.Any(t => !t.EsAuditoria && t.Estado != "igual") ? "anterior"
                    : "identica";
                var firma = EstadoDeLaFirma(manifiesto);
                var motivo = MotivoQueImpideRestaurar(manifiesto, actual, firma);

                return new ComparacionDeCopia(
                    manifiesto.Fecha, new EstadoDeSistema(manifiesto.Version, manifiesto.Migracion, manifiesto.Migraciones),
                    new EstadoDeSistema(_version, actual.Migracion, actual.Migraciones),
                    mismoEsquema, tablas, veredicto, motivo == null, motivo, firma);
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        // ------------------------------------------------------------------ restaurar

        public async Task<ResultadoDeRestauracion> RestaurarAsync(Stream zip, CancellationToken cancellationToken)
        {
            var copia = Leer(zip);   // si no pasa la verificación de integridad, no se toca la base
            var conexion = await AbrirAsync(cancellationToken);
            try
            {
                // Todo en una transacción: si algo falla en cualquier punto, la base queda exactamente como estaba.
                await using var transaccion = await conexion.BeginTransactionAsync(cancellationToken);
                var actual = await LeerEstadoAsync(conexion, transaccion, cancellationToken);
                var problema = MotivoQueImpideRestaurar(copia.Manifiesto, actual, EstadoDeLaFirma(copia.Manifiesto));
                if (problema != null) throw new InvalidOperationException(problema);

                // Los datos de la copia ya son consistentes entre sí: se cargan sin que los disparadores y las claves foráneas
                // intervengan a mitad de camino (el orden de carga no importa). Eso pide permisos de superusuario.
                try { await EjecutarAsync(conexion, transaccion, "SET LOCAL session_replication_role = replica", cancellationToken); }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
                {
                    throw new InvalidOperationException(
                        "La base de datos no le permite a la aplicación restaurar copias (hacen falta permisos de superusuario). Restaurá con scripts\\restaurar.ps1, a partir de una copia .dump.");
                }

                var tablas = actual.Tablas.Keys.Where(t => t != EtiquetasDeTablas.TablaDeMigraciones).ToList();
                await EjecutarAsync(conexion, transaccion, "TRUNCATE TABLE " + string.Join(", ", tablas.Select(Citar)), cancellationToken);

                foreach (var tabla in tablas)
                {
                    var csv = Utf8SinBom.GetString(copia.Archivos[$"datos/{tabla}.csv"]);
                    await using var escritor = await conexion.BeginTextImportAsync(
                        $"COPY {Citar(tabla)} FROM STDIN (FORMAT csv, HEADER true, ENCODING 'UTF8')", cancellationToken);
                    await escritor.WriteAsync(csv.AsMemory(), cancellationToken);
                }

                await EjecutarAsync(conexion, transaccion, SqlSecuencias, cancellationToken);

                // Control final, antes de confirmar: lo que quedó tiene que ser lo que dice la copia, tabla por tabla.
                var despues = await LeerEstadoAsync(conexion, transaccion, cancellationToken);
                foreach (var tabla in tablas)
                {
                    var esperado = copia.Manifiesto.Tablas[tabla];
                    if (despues.Tablas[tabla].Filas != esperado.Filas || despues.Tablas[tabla].Huella != esperado.Huella)
                        throw new InvalidOperationException($"La restauración no dejó los datos de «{EtiquetasDeTablas.De(tabla).Etiqueta}» iguales a los de la copia. No se cambió nada.");
                }

                await transaccion.CommitAsync(cancellationToken);
                return new ResultadoDeRestauracion(tablas.Count, tablas.Sum(t => despues.Tablas[t].Filas));
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        // ------------------------------------------------------------------ lectura y verificación del archivo

        /// <summary>
        /// Lee el .zip con topes (tamaño, cantidad de archivos y tamaño descomprimido, que se cuenta de verdad porque el que
        /// declara el zip puede mentir), acepta solo los nombres esperados y verifica que cada CSV tenga el SHA-256 del manifiesto.
        /// </summary>
        private static CopiaLeida Leer(Stream origen)
        {
            using var memoria = new MemoryStream();
            var buffer = new byte[81920];
            int leidos;
            while ((leidos = origen.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (memoria.Length + leidos > BytesMaximosDeLaCopia)
                    throw new ArgumentException($"La copia supera los {BytesMaximosDeLaCopia / (1024 * 1024)} MB permitidos.");
                memoria.Write(buffer, 0, leidos);
            }
            if (memoria.Length < 4 || memoria.GetBuffer()[0] != 0x50 || memoria.GetBuffer()[1] != 0x4B || memoria.GetBuffer()[2] != 0x03 || memoria.GetBuffer()[3] != 0x04)
                throw new ArgumentException("El archivo no es una copia de seguridad de GymStudio (.zip).");
            memoria.Position = 0;

            var archivos = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            try
            {
                using var zip = new ZipArchive(memoria, ZipArchiveMode.Read);
                if (zip.Entries.Count > EntradasMaximas) throw new ArgumentException("El archivo no es una copia de seguridad de GymStudio válida.");

                long total = 0;
                foreach (var entrada in zip.Entries)
                {
                    var nombre = entrada.FullName;
                    if (nombre != "manifiesto.json" && !NombreDeArchivo.IsMatch(nombre))
                        throw new ArgumentException("La copia trae archivos que no le corresponden.");
                    using var datos = entrada.Open();
                    using var contenido = new MemoryStream();
                    int n;
                    while ((n = datos.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += n;
                        if (total > BytesMaximosDescomprimidos) throw new ArgumentException("La copia es demasiado grande para cargarla.");
                        contenido.Write(buffer, 0, n);
                    }
                    if (!archivos.TryAdd(nombre, contenido.ToArray())) throw new ArgumentException("La copia tiene archivos repetidos.");
                }
            }
            catch (InvalidDataException)
            {
                throw new ArgumentException("El archivo no es una copia de seguridad de GymStudio (.zip) válida.");
            }

            if (!archivos.TryGetValue("manifiesto.json", out var bytesDelManifiesto))
                throw new ArgumentException("El archivo no es una copia de seguridad de GymStudio: falta el manifiesto.");

            Manifiesto? manifiesto;
            try { manifiesto = JsonSerializer.Deserialize<Manifiesto>(bytesDelManifiesto, OpcionesJson); }
            catch (JsonException) { manifiesto = null; }
            if (manifiesto == null || manifiesto.Formato != Formato || manifiesto.Tablas == null || manifiesto.Tablas.Count is 0 or > 200)
                throw new ArgumentException("El archivo no es una copia de seguridad de GymStudio: el manifiesto no se puede leer.");

            foreach (var (tabla, datos) in manifiesto.Tablas)
            {
                if (!NombreDeTabla.IsMatch(tabla)) throw new ArgumentException("El manifiesto de la copia tiene nombres de tabla que no corresponden.");
                var archivo = $"datos/{tabla}.csv";
                if (datos.Archivo != archivo || !archivos.TryGetValue(archivo, out var csv))
                    throw new ArgumentException($"La copia está incompleta: falta el archivo de «{EtiquetasDeTablas.De(tabla).Etiqueta}».");
                if (!string.Equals(Convert.ToHexString(SHA256.HashData(csv)), datos.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"La copia está dañada o fue modificada: la huella del archivo de «{EtiquetasDeTablas.De(tabla).Etiqueta}» no coincide.");
            }
            if (archivos.Keys.Any(k => k != "manifiesto.json" && !manifiesto.Tablas.ContainsKey(k["datos/".Length..^".csv".Length])))
                throw new ArgumentException("La copia trae archivos que el manifiesto no declara.");

            return new CopiaLeida(manifiesto, archivos);
        }

        /// <summary>Por qué esta copia no se puede restaurar en este sistema, o null si se puede.</summary>
        // Lo que se firma: todo lo que el manifiesto afirma (versión, migración y, por tabla, filas, huella, archivo y SHA-256).
        private static string Canonico(Manifiesto m)
            => string.Join("\n",
                new[] { m.Formato, m.Fecha.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), m.Version, m.Migracion, m.Migraciones.ToString(CultureInfo.InvariantCulture) }
                    .Concat(m.Tablas.OrderBy(t => t.Key, StringComparer.Ordinal)
                        .Select(t => $"{t.Key}|{t.Value.Filas.ToString(CultureInfo.InvariantCulture)}|{t.Value.Huella}|{t.Value.Archivo}|{t.Value.Sha256}")));

        private string? Firmar(Manifiesto m)
            => _claveDeFirma == null ? null : Convert.ToHexString(HMACSHA256.HashData(_claveDeFirma, Utf8SinBom.GetBytes(Canonico(m))));

        private string EstadoDeLaFirma(Manifiesto m)
        {
            if (_claveDeFirma == null) return "no-disponible";
            if (string.IsNullOrEmpty(m.Firma)) return "sin-firma";
            var esperada = Firmar(m)!;
            return CryptographicOperations.FixedTimeEquals(Utf8SinBom.GetBytes(esperada), Utf8SinBom.GetBytes(m.Firma.ToUpperInvariant())) ? "valida" : "invalida";
        }

        private static string? MotivoQueImpideRestaurar(Manifiesto copia, EstadoDeBase actual, string firma)
        {
            if (copia.Migracion != actual.Migracion)
                return $"La copia es de otra versión de la base de datos (migración {copia.Migracion}) y este sistema está en la {actual.Migracion}. Desde esta pantalla solo se restauran copias hechas con la misma versión del sistema.";
            if (!actual.Tablas.Keys.ToHashSet().SetEquals(copia.Tablas.Keys))
                return "Las tablas de la copia no son las mismas que las del sistema actual.";
            if (firma == "invalida")
                return "La firma de la copia no coincide: el archivo se modificó después de hacerse o lo hizo otra instalación con otra clave. Por seguridad no se restaura.";
            if (firma == "sin-firma")
                return "La copia no está firmada por un sistema GymStudio, así que no se puede comprobar que nadie la modificó. Por seguridad no se restaura.";
            return null;
        }

        // ------------------------------------------------------------------ acceso a la base

        private async Task<NpgsqlConnection> AbrirAsync(CancellationToken cancellationToken)
        {
            await _context.Database.OpenConnectionAsync(cancellationToken);
            return (NpgsqlConnection)_context.Database.GetDbConnection();
        }

        private static async Task<EstadoDeBase> LeerEstadoAsync(NpgsqlConnection conexion, NpgsqlTransaction? transaccion, CancellationToken cancellationToken)
        {
            var tablas = new SortedDictionary<string, (long, string)>(StringComparer.Ordinal);
            await using (var comando = new NpgsqlCommand(SqlTablas, conexion, transaccion) { CommandTimeout = 120 })
            await using (var lector = await comando.ExecuteReaderAsync(cancellationToken))
            {
                while (await lector.ReadAsync(cancellationToken))
                    tablas[lector.GetString(0)] = (long.Parse(lector.GetString(1), CultureInfo.InvariantCulture), lector.GetString(2));
            }

            await using var consulta = new NpgsqlCommand(SqlMigracion, conexion, transaccion);
            var partes = ((string)(await consulta.ExecuteScalarAsync(cancellationToken))!).Split('|');
            return new EstadoDeBase(tablas, partes[0], int.Parse(partes[1], CultureInfo.InvariantCulture));
        }

        private static async Task<byte[]> ExportarAsync(NpgsqlConnection conexion, string tabla, CancellationToken cancellationToken)
        {
            using var lector = await conexion.BeginTextExportAsync($"COPY {Citar(tabla)} TO STDOUT (FORMAT csv, HEADER true, ENCODING 'UTF8')", cancellationToken);
            return Utf8SinBom.GetBytes(await lector.ReadToEndAsync(cancellationToken));
        }

        private static async Task EjecutarAsync(NpgsqlConnection conexion, NpgsqlTransaction transaccion, string sql, CancellationToken cancellationToken)
        {
            await using var comando = new NpgsqlCommand(sql, conexion, transaccion) { CommandTimeout = 300 };
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>"esquema.tabla" entre comillas. Solo se llama con nombres que ya se comprobaron contra la base.</summary>
        private static string Citar(string tabla)
        {
            var partes = tabla.Split('.', 2);
            return $"{CitarIdentificador(partes[0])}.{CitarIdentificador(partes[1])}";
        }

        private static string CitarIdentificador(string nombre) => "\"" + nombre.Replace("\"", "\"\"") + "\"";
    }
}
