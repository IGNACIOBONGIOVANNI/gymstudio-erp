using EstudioContable.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// La migración que convierte el texto «Encargado» de cada turno en una ficha de entrenador, con datos de verdad de por medio:
/// se migra una base hasta la versión anterior, se cargan turnos, se migra a la última y se vuelve atrás.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class MigracionDeEntrenadoresTests(ApiFixture api)
{
    private const string Anterior = "20261106182940_TurnosDeEncargado";

    /// <summary>Crea (o recrea) una base vacía en el mismo contenedor y devuelve su cadena de conexión.</summary>
    private async Task<string> BaseNueva(string nombre)
    {
        var cadena = new NpgsqlConnectionStringBuilder(api.ConnectionString);
        await using (var admin = new NpgsqlConnection(cadena.ConnectionString))
        {
            await admin.OpenAsync();
            await using var borrar = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{nombre}\" WITH (FORCE)", admin);
            await borrar.ExecuteNonQueryAsync();
            await using var crear = new NpgsqlCommand($"CREATE DATABASE \"{nombre}\"", admin);
            await crear.ExecuteNonQueryAsync();
        }
        cadena.Database = nombre;
        return cadena.ConnectionString;
    }

    private static AppDbContext Contexto(string cadena)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cadena).Options);

    private static async Task Migrar(string cadena, string? destino = null)
    {
        await using var contexto = Contexto(cadena);
        await contexto.GetService<IMigrator>().MigrateAsync(destino);
    }

    private static async Task Ejecutar(string cadena, string sql)
    {
        await using var c = new NpgsqlConnection(cadena);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> Leer(string cadena, string sql)
    {
        await using var c = new NpgsqlConnection(cadena);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await using var lector = await cmd.ExecuteReaderAsync();
        var filas = new List<string>();
        while (await lector.ReadAsync()) filas.Add(string.Join("|", Enumerable.Range(0, lector.FieldCount).Select(i => lector.GetValue(i)?.ToString())));
        return filas;
    }

    private const string TurnosViejos = """
        INSERT INTO contabilidad."TurnosEncargado" ("DiaSemana", "Hora", "Encargado") VALUES
            (1, 6, 'Ana'), (1, 7, '  ana  '), (2, 6, 'Ana  López'), (2, 7, 'ana lópez'), (3, 6, 'Beto')
        """;

    [Fact]
    public async Task Los_nombres_ya_cargados_pasan_a_ser_una_ficha_por_persona_y_la_migracion_se_deshace()
    {
        var cadena = await BaseNueva("migracion_entrenadores");
        await Migrar(cadena, Anterior);
        await Ejecutar(cadena, TurnosViejos);

        await Migrar(cadena);

        // Una ficha por persona, con la primera grafía (por día y hora) ya limpia, y todas activas.
        Assert.Equal(new[] { "Ana|True", "Ana López|True", "Beto|True" },
            await Leer(cadena, """SELECT "NombreCompleto", "Activo" FROM contabilidad."Entrenadores" ORDER BY "NombreCompleto" """));
        // Cada turno apunta a la ficha de su persona.
        Assert.Equal(new[] { "1|6|Ana", "1|7|Ana", "2|6|Ana López", "2|7|Ana López", "3|6|Beto" },
            await Leer(cadena, """
                SELECT t."DiaSemana", t."Hora", e."NombreCompleto"
                FROM contabilidad."TurnosEncargado" t JOIN contabilidad."Entrenadores" e ON e."Id" = t."EntrenadorId"
                ORDER BY t."DiaSemana", t."Hora"
                """));
        Assert.Empty(await Leer(cadena, """
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'contabilidad' AND table_name = 'TurnosEncargado' AND column_name = 'Encargado'
            """));

        // Hacia atrás: vuelve el texto, con el nombre de la ficha, y no queda la tabla de entrenadores.
        await Migrar(cadena, Anterior);
        Assert.Equal(new[] { "1|6|Ana", "1|7|Ana", "2|6|Ana López", "2|7|Ana López", "3|6|Beto" },
            await Leer(cadena, """SELECT "DiaSemana", "Hora", "Encargado" FROM contabilidad."TurnosEncargado" ORDER BY "DiaSemana", "Hora" """));
        Assert.Equal(new[] { "" }, await Leer(cadena, """SELECT coalesce(to_regclass('contabilidad."Entrenadores"')::text, '')"""));
    }

    [Fact]
    public async Task Una_base_sin_turnos_migra_hacia_arriba_y_hacia_abajo_sin_error()
    {
        var cadena = await BaseNueva("migracion_entrenadores_vacia");
        await Migrar(cadena, Anterior);

        await Migrar(cadena);
        Assert.Equal(new[] { "0" }, await Leer(cadena, """SELECT count(*) FROM contabilidad."Entrenadores" """));

        await Migrar(cadena, Anterior);
        Assert.Equal(new[] { "0" }, await Leer(cadena, """SELECT count(*) FROM contabilidad."TurnosEncargado" """));
    }
}
