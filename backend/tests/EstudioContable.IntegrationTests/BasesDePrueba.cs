using EstudioContable.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Bases de datos descartables dentro del mismo contenedor de PostgreSQL, para probar migraciones y valores iniciales sin tocar
/// los datos que usan las demás pruebas.
/// </summary>
public static class BasesDePrueba
{
    /// <summary>Crea (o recrea) una base vacía y devuelve su cadena de conexión.</summary>
    public static async Task<string> BaseNueva(this ApiFixture api, string nombre)
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

    public static AppDbContext Contexto(string cadena)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cadena).Options);

    /// <summary>Migra hasta la última migración o hasta la indicada (hacia adelante o hacia atrás).</summary>
    public static async Task Migrar(string cadena, string? destino = null)
    {
        await using var contexto = Contexto(cadena);
        await contexto.GetService<IMigrator>().MigrateAsync(destino);
    }

    public static async Task Ejecutar(string cadena, string sql)
    {
        await using var c = new NpgsqlConnection(cadena);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Las filas del resultado, con las columnas unidas por «|».</summary>
    public static async Task<List<string>> Leer(string cadena, string sql)
    {
        await using var c = new NpgsqlConnection(cadena);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await using var lector = await cmd.ExecuteReaderAsync();
        var filas = new List<string>();
        while (await lector.ReadAsync()) filas.Add(string.Join("|", Enumerable.Range(0, lector.FieldCount).Select(i => lector.GetValue(i)?.ToString())));
        return filas;
    }
}
