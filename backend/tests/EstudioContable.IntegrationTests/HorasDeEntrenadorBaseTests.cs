using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Las reglas de las horas cumplidas que la base hace cumplir aunque alguien escriba directo en ella: horas de a cuarto de hora, valores
/// sin negativos, un registro por entrenador y día, no se borra a quien tiene horas, cada fila queda auditada y entra en la copia.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class HorasDeEntrenadorBaseTests(ApiFixture api)
{
    private static readonly DateOnly Dia = new(2026, 11, 10);

    private static string Nuevo(string baseDelNombre) => $"{baseDelNombre} {Nombres.Aleatorio(8)}";

    private async Task<long> CrearEntrenador(string baseDelNombre = "Base De Horas")
    {
        var jefe = await api.Jefe();
        var r = await jefe.PostAsJsonAsync("/api/entrenadores", new { nombre = Nuevo(baseDelNombre) });
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private async Task<object?> EjecutarAsync(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = new NpgsqlConnection(api.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros) comando.Parameters.AddWithValue(nombre, valor);
        return await comando.ExecuteScalarAsync();
    }

    private Task<object?> InsertarDia(long entrenadorId, decimal horas, decimal valor, DateOnly? fecha = null) => EjecutarAsync(
        """
        INSERT INTO contabilidad."HorasDeEntrenador" ("EntrenadorId", "Fecha", "Horas", "ValorHora") VALUES (@id, @fecha, @horas, @valor)
        RETURNING "Id"
        """, ("id", entrenadorId), ("fecha", fecha ?? Dia), ("horas", horas), ("valor", valor));

    private Task QuitarDias(long entrenadorId) => EjecutarAsync("""DELETE FROM contabilidad."HorasDeEntrenador" WHERE "EntrenadorId" = @id""", ("id", entrenadorId));

    [Fact]
    public async Task La_base_rechaza_horas_imposibles_valores_negativos_y_un_valor_de_hora_negativo_en_la_ficha()
    {
        var id = await CrearEntrenador();

        foreach (var horas in new[] { 24.25m, 0.1m, 0.3m, -1m })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => InsertarDia(id, horas, 1000m));
            Assert.Equal("CK_HorasDeEntrenador_Horas", error.ConstraintName);
        }

        var valor = await Assert.ThrowsAsync<PostgresException>(() => InsertarDia(id, 8m, -1m));
        Assert.Equal("CK_HorasDeEntrenador_ValorHora", valor.ConstraintName);

        var ficha = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(
            """UPDATE contabilidad."Entrenadores" SET "ValorHora" = -1 WHERE "Id" = @id""", ("id", id)));
        Assert.Equal("CK_Entrenadores_ValorHora", ficha.ConstraintName);

        // Lo que sí acepta: 0 (faltó), un cuarto de hora, 24 horas y un valor de hora de 0.
        foreach (var (horas, fecha) in new[] { (0m, 1), (0.25m, 2), (24m, 3) })
            await InsertarDia(id, horas, 0m, Dia.AddDays(-fecha));
        await QuitarDias(id);
    }

    [Fact]
    public async Task No_admite_dos_registros_del_mismo_entrenador_y_dia()
    {
        var id = await CrearEntrenador();
        var otro = await CrearEntrenador("Otro De Horas");
        try
        {
            await InsertarDia(id, 8m, 1000m);
            var error = await Assert.ThrowsAsync<PostgresException>(() => InsertarDia(id, 4m, 1000m));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
            Assert.Equal("ux_horas_entrenador_fecha", error.ConstraintName);

            // Otro entrenador el mismo día, y el mismo entrenador otro día, sí.
            await InsertarDia(otro, 8m, 1000m);
            await InsertarDia(id, 8m, 1000m, Dia.AddDays(-1));
        }
        finally { await QuitarDias(id); await QuitarDias(otro); }
    }

    [Fact]
    public async Task No_deja_borrar_a_un_entrenador_con_horas()
    {
        var id = await CrearEntrenador();
        try
        {
            await InsertarDia(id, 8m, 1000m);

            var error = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync("""DELETE FROM contabilidad."Entrenadores" WHERE "Id" = @id""", ("id", id)));

            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
            Assert.Equal("FK_HorasDeEntrenador_Entrenadores_EntrenadorId", error.ConstraintName);
        }
        finally { await QuitarDias(id); }
    }

    [Fact]
    public async Task Las_horas_quedan_en_la_auditoria()
    {
        var id = await CrearEntrenador();
        try
        {
            var dia = (long)(await InsertarDia(id, 8m, 1000m))!;
            await EjecutarAsync("""UPDATE contabilidad."HorasDeEntrenador" SET "Horas" = 9.5 WHERE "Id" = @dia""", ("dia", dia));

            var operaciones = new List<string>();
            await using var conexion = new NpgsqlConnection(api.ConnectionString);
            await conexion.OpenAsync();
            await using var comando = new NpgsqlCommand(
                """SELECT operacion FROM contabilidad.auditoria WHERE tabla = 'contabilidad.HorasDeEntrenador' AND registro_id = @dia ORDER BY id""", conexion);
            comando.Parameters.AddWithValue("dia", dia.ToString());
            await using (var lector = await comando.ExecuteReaderAsync())
                while (await lector.ReadAsync()) operaciones.Add(lector.GetString(0));

            Assert.Equal(new[] { "INSERT", "UPDATE" }, operaciones);
        }
        finally { await QuitarDias(id); }
    }

    [Fact]
    public async Task Las_horas_entran_en_la_copia_de_seguridad()
    {
        var jefe = await api.Jefe();
        var id = await CrearEntrenador();
        try
        {
            await InsertarDia(id, 8m, 1000m);
            var zip = await jefe.GetByteArrayAsync("/api/copias/descargar");

            using (var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
            {
                var entrada = archivo.GetEntry("datos/contabilidad.HorasDeEntrenador.csv");
                Assert.NotNull(entrada);
                using var lector = new StreamReader(entrada!.Open());
                Assert.Contains("1000", await lector.ReadToEndAsync());
            }

            var contenido = new MultipartFormDataContent();
            var parte = new ByteArrayContent(zip);
            parte.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            contenido.Add(parte, "file", "copia.zip");
            var comparar = await jefe.PostAsync("/api/copias/comparar", contenido);
            Assert.True(comparar.IsSuccessStatusCode, await comparar.Detalle());
            var comparacion = (await comparar.Content.ReadFromJsonAsync<JsonObject>())!;
            var tabla = comparacion["tablas"]!.AsArray().Single(t => (string?)t!["tabla"] == "contabilidad.HorasDeEntrenador")!;
            Assert.Equal("Horas de los entrenadores", (string)tabla["etiqueta"]!);
            Assert.Equal("Gimnasio", (string)tabla["grupo"]!);
            Assert.Equal("igual", (string)tabla["estado"]!);
        }
        finally { await QuitarDias(id); }
    }
}
