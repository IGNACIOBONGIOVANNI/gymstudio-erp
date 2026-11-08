using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Npgsql;
using static EstudioContable.IntegrationTests.BasesDePrueba;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// La tabla donde vive la programación de las copias automáticas: una sola fila con valores iniciales, restricciones que repiten
/// las reglas del dominio aunque alguien escriba directo en la base, auditoría y copia de seguridad.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class ConfiguracionDeCopiasBaseTests(ApiFixture api)
{
    private async Task<PostgresException> Rechazada(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var c = new NpgsqlConnection(api.ConnectionString);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        foreach (var (nombre, valor) in parametros) cmd.Parameters.AddWithValue(nombre, valor);
        return await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task La_migracion_deja_la_fila_inicial_inactiva_diaria_a_las_22_y_con_14_copias()
    {
        // Una base nueva: la fila que deja la migración, sin lo que otras pruebas hayan cambiado en la base compartida.
        var cadena = await api.BaseNueva("config_copias_inicial");
        await Migrar(cadena);

        Assert.Equal(new[] { "1|False|Diaria|22|0|||14||" }, await Leer(cadena, """
            SELECT "Id", "Activa", "Frecuencia", "Hora", "Minuto", "DiaDeLaSemana", "DiaDelMes", "Conservar", "UltimaCopia", "UltimoError"
            FROM contabilidad."ConfiguracionDeCopias"
            """));
        Assert.Equal(new[] { "1" }, await Leer(cadena, """SELECT count(*) FROM contabilidad."ConfiguracionDeCopias" WHERE "ActivaDesde" IS NOT NULL"""));
    }

    [Theory]
    [InlineData("Hora", 24, "CK_ConfiguracionDeCopias_Hora")]
    [InlineData("Hora", -1, "CK_ConfiguracionDeCopias_Hora")]
    [InlineData("Minuto", 60, "CK_ConfiguracionDeCopias_Minuto")]
    [InlineData("Minuto", -1, "CK_ConfiguracionDeCopias_Minuto")]
    [InlineData("Conservar", 0, "CK_ConfiguracionDeCopias_Conservar")]
    [InlineData("Conservar", 61, "CK_ConfiguracionDeCopias_Conservar")]
    [InlineData("DiaDeLaSemana", 0, "CK_ConfiguracionDeCopias_DiaSemana")]
    [InlineData("DiaDeLaSemana", 8, "CK_ConfiguracionDeCopias_DiaSemana")]
    [InlineData("DiaDelMes", 0, "CK_ConfiguracionDeCopias_DiaMes")]
    [InlineData("DiaDelMes", 29, "CK_ConfiguracionDeCopias_DiaMes")]
    public async Task La_base_rechaza_valores_imposibles_aunque_se_escriba_directo(string columna, int valor, string restriccion)
    {
        var error = await Rechazada($"UPDATE contabilidad.\"ConfiguracionDeCopias\" SET \"{columna}\" = @valor WHERE \"Id\" = 1", ("valor", valor));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(restriccion, error.ConstraintName);
    }

    [Fact]
    public async Task La_base_rechaza_una_frecuencia_que_no_existe()
    {
        var error = await Rechazada("""UPDATE contabilidad."ConfiguracionDeCopias" SET "Frecuencia" = @valor WHERE "Id" = 1""", ("valor", "Cada5Minutos"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("CK_ConfiguracionDeCopias_Frecuencia", error.ConstraintName);
    }

    private const string InsertarFila = """
        INSERT INTO contabilidad."ConfiguracionDeCopias" ("Id", "Activa", "Frecuencia", "Hora", "Minuto", "Conservar", "ActivaDesde")
        VALUES (@id, false, 'Diaria', 22, 0, 14, now())
        """;

    [Fact]
    public async Task No_admite_una_segunda_fila_ni_repetir_la_primera()
    {
        var otra = await Rechazada(InsertarFila, ("id", 2));
        Assert.Equal(PostgresErrorCodes.CheckViolation, otra.SqlState);
        Assert.Equal("CK_ConfiguracionDeCopias_Unica", otra.ConstraintName);

        var repetida = await Rechazada(InsertarFila, ("id", 1));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, repetida.SqlState);
        Assert.Equal("PK_ConfiguracionDeCopias", repetida.ConstraintName);
    }

    [Fact]
    public async Task Los_cambios_de_la_programacion_quedan_en_la_auditoria()
    {
        var jefe = await api.Jefe();
        var antes = (await jefe.GetJson("/api/auditoria?cantidad=1")).AsArray();
        var desde = antes.Count == 0 ? 0 : (long)antes[0]!["id"]!;
        var conservar = (await Leer(api.ConnectionString, """SELECT "Conservar" FROM contabilidad."ConfiguracionDeCopias" WHERE "Id" = 1""")).Single();
        var otroValor = conservar == "15" ? 16 : 15;

        await Ejecutar(api.ConnectionString, $"UPDATE contabilidad.\"ConfiguracionDeCopias\" SET \"Conservar\" = {otroValor} WHERE \"Id\" = 1");
        await Ejecutar(api.ConnectionString, $"UPDATE contabilidad.\"ConfiguracionDeCopias\" SET \"Conservar\" = {conservar} WHERE \"Id\" = 1");

        var filas = (await jefe.GetJson("/api/auditoria?cantidad=50&tabla=contabilidad.ConfiguracionDeCopias")).AsArray()
            .Where(f => (long)f!["id"]! > desde).Select(f => (string)f!["operacion"]!).ToArray();
        Assert.Equal(new[] { "UPDATE", "UPDATE" }, filas);
    }

    [Fact]
    public async Task La_copia_de_seguridad_incluye_la_programacion()
    {
        var jefe = await api.Jefe();

        var zip = await jefe.GetByteArrayAsync("/api/copias/descargar");
        using var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        Assert.NotNull(archivo.GetEntry("datos/contabilidad.ConfiguracionDeCopias.csv"));

        var formulario = new MultipartFormDataContent { { new ByteArrayContent(zip), "file", "copia.zip" } };
        var comparacion = (await (await jefe.PostAsync("/api/copias/comparar", formulario)).Content.ReadFromJsonAsync<JsonObject>())!;
        var tabla = comparacion["tablas"]!.AsArray().First(t => (string?)t!["tabla"] == "contabilidad.ConfiguracionDeCopias")!;
        Assert.Equal("Programación de copias automáticas", (string)tabla["etiqueta"]!);
        Assert.Equal("Sistema", (string)tabla["grupo"]!);
        Assert.Equal("igual", (string)tabla["estado"]!);
    }
}
