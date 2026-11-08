using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Altas y cambios de entrenadores directo en la base, para las pruebas que no son del módulo de entrenadores (el calendario)
/// o que necesitan estados que la API no permite.
/// </summary>
public static class EntrenadoresEnBase
{
    /// <summary>Deja un entrenador activo con ese nombre (lo crea si no existe, sin distinguir mayúsculas) y devuelve su id.</summary>
    public static async Task<long> Asegurar(this ApiFixture api, string nombre)
    {
        await using var conexion = new NpgsqlConnection(api.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand("""
            INSERT INTO contabilidad."Entrenadores" ("NombreCompleto", "Activo", "FechaCreacion")
            VALUES (@nombre, true, now())
            ON CONFLICT ((lower("NombreCompleto"))) DO UPDATE SET "Activo" = true
            RETURNING "Id"
            """, conexion);
        comando.Parameters.AddWithValue("nombre", nombre);
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    public static async Task PonerActivo(this ApiFixture api, long id, bool activo)
    {
        await using var conexion = new NpgsqlConnection(api.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand("""UPDATE contabilidad."Entrenadores" SET "Activo" = @activo WHERE "Id" = @id""", conexion);
        comando.Parameters.AddWithValue("activo", activo);
        comando.Parameters.AddWithValue("id", id);
        await comando.ExecuteNonQueryAsync();
    }
}
