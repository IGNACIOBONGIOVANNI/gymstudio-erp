using System.Net.Http.Json;
using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Reglas que viven en PostgreSQL (triggers y auditoría): se prueban con SQL directo,
/// sin pasar por la API, para demostrar que la base se defiende sola.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class BaseDeDatosTests(ApiFixture api)
{
    private async Task<NpgsqlConnection> Conectar()
    {
        var conexion = new NpgsqlConnection(api.ConnectionString);
        await conexion.OpenAsync();
        return conexion;
    }

    private static async Task<long> InsertarAsiento(NpgsqlConnection c, NpgsqlTransaction tx, string estado, long numero)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO contabilidad.asientos_contables
                (cliente_id, numero_asiento, fecha, descripcion, estado, usuario_creador_id, fecha_creacion)
            VALUES (1, @numero, current_date, 'Prueba SQL', @estado, 1, now()) RETURNING "Id"
            """, c, tx);
        cmd.Parameters.AddWithValue("numero", numero);
        cmd.Parameters.AddWithValue("estado", estado);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static Task InsertarLinea(NpgsqlConnection c, NpgsqlTransaction tx, long asientoId, long cuenta, decimal debe, decimal haber)
    {
        var cmd = new NpgsqlCommand(
            "INSERT INTO contabilidad.lineas_asiento (asiento_id, cuenta_id, debe, haber, orden) VALUES (@a, @c, @d, @h, 0)", c, tx);
        cmd.Parameters.AddWithValue("a", asientoId);
        cmd.Parameters.AddWithValue("c", cuenta);
        cmd.Parameters.AddWithValue("d", debe);
        cmd.Parameters.AddWithValue("h", haber);
        return cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task El_trigger_rechaza_al_hacer_commit_un_asiento_confirmado_desbalanceado()
    {
        await using var c = await Conectar();
        await using var tx = await c.BeginTransactionAsync();
        var id = await InsertarAsiento(c, tx, "Confirmado", 900_001);
        await InsertarLinea(c, tx, id, 107, 100, 0);
        await InsertarLinea(c, tx, id, 104, 0, 90);

        var error = await Assert.ThrowsAsync<PostgresException>(() => tx.CommitAsync());

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Contains("partida doble", error.MessageText);
    }

    [Fact]
    public async Task Un_asiento_confirmado_no_se_puede_borrar_ni_modificar()
    {
        await using var c = await Conectar();
        await using var tx = await c.BeginTransactionAsync();
        var id = await InsertarAsiento(c, tx, "Confirmado", 900_002);
        await InsertarLinea(c, tx, id, 107, 50, 0);
        await InsertarLinea(c, tx, id, 104, 0, 50);

        await using var borrar = new NpgsqlCommand("DELETE FROM contabilidad.asientos_contables WHERE \"Id\" = @id", c, tx);
        borrar.Parameters.AddWithValue("id", id);
        var error = await Assert.ThrowsAsync<PostgresException>(() => borrar.ExecuteNonQueryAsync());

        Assert.Contains("no puede eliminarse", error.MessageText);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task La_auditoria_registra_al_usuario_logueado_en_la_api()
    {
        var nombre = $"Plan auditado {Guid.NewGuid():N}";
        var jefe = await api.Jefe();
        var alta = await jefe.PostAsJsonAsync("/api/planes", new { nombre, precioMensual = 1000, duracionMeses = 1 });
        alta.EnsureSuccessStatusCode();

        await using var c = await Conectar();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT usuario, operacion FROM contabilidad.auditoria
            WHERE tabla = 'contabilidad.Planes' AND datos_nuevos ->> 'Nombre' = @nombre
            """, c);
        cmd.Parameters.AddWithValue("nombre", nombre);
        await using var lector = await cmd.ExecuteReaderAsync();

        Assert.True(await lector.ReadAsync(), "No se encontró el registro de auditoría");
        Assert.Equal("jefe@gym.com", lector.GetString(0));
        Assert.Equal("INSERT", lector.GetString(1));
    }

    [Fact]
    public async Task La_auditoria_no_se_puede_borrar()
    {
        await using var c = await Conectar();
        await using var cmd = new NpgsqlCommand("DELETE FROM contabilidad.auditoria", c);

        var error = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());

        Assert.Contains("sólo lectura", error.MessageText);
    }
}
