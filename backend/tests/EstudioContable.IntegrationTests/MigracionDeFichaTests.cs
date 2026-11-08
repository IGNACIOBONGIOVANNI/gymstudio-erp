using static EstudioContable.IntegrationTests.BasesDePrueba;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// La migración que suma el email, el WhatsApp y la tabla de fotos a la ficha del entrenador, con datos de verdad de por medio:
/// se migra una base hasta la versión anterior, se carga un entrenador, se migra a la última y se vuelve atrás. Solo agrega: las
/// fichas que ya estaban quedan como estaban.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class MigracionDeFichaTests(ApiFixture api)
{
    private const string Anterior = "20261108020654_CopiasAutomaticas";

    private const string FichaVieja = """
        INSERT INTO contabilidad."Entrenadores" ("NombreCompleto", "Telefono", "Especialidad", "Activo", "FechaCreacion")
        VALUES ('Ana López', '11 1234-5678', 'Yoga', true, now())
        """;

    [Fact]
    public async Task Las_fichas_existentes_quedan_igual_con_el_contacto_vacio_y_la_migracion_se_deshace_y_se_repite()
    {
        var cadena = await api.BaseNueva("migracion_ficha");
        await Migrar(cadena, Anterior);
        await Ejecutar(cadena, FichaVieja);

        await Migrar(cadena);

        // La ficha sigue igual y las columnas nuevas están vacías; la tabla de fotos existe, sin filas ni auditoría.
        Assert.Equal(new[] { "Ana López|11 1234-5678|Yoga||" },
            await Leer(cadena, """SELECT "NombreCompleto", "Telefono", "Especialidad", "Email", "WhatsApp" FROM contabilidad."Entrenadores" """));
        Assert.Equal(new[] { "0" }, await Leer(cadena, """SELECT count(*) FROM contabilidad."FotosDeEntrenador" """));
        Assert.Equal(new[] { "0" }, await Leer(cadena, """
            SELECT count(*) FROM pg_trigger WHERE tgrelid = 'contabilidad."FotosDeEntrenador"'::regclass AND NOT tgisinternal
            """));
        // Si se borra la ficha, se va con su foto (la API no borra fichas; esto es la red de seguridad de la base).
        Assert.Equal(new[] { "c" }, await Leer(cadena, """
            SELECT confdeltype FROM pg_constraint WHERE conrelid = 'contabilidad."FotosDeEntrenador"'::regclass AND contype = 'f'
            """));

        // Hacia atrás: se van las columnas y la tabla de fotos, y la ficha sigue.
        await Migrar(cadena, Anterior);
        Assert.Empty(await Leer(cadena, """
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'contabilidad' AND table_name = 'Entrenadores' AND column_name IN ('Email', 'WhatsApp')
            """));
        Assert.Equal(new[] { "" }, await Leer(cadena, """SELECT coalesce(to_regclass('contabilidad."FotosDeEntrenador"')::text, '')"""));
        Assert.Equal(new[] { "Ana López|11 1234-5678|Yoga" },
            await Leer(cadena, """SELECT "NombreCompleto", "Telefono", "Especialidad" FROM contabilidad."Entrenadores" """));

        // Y se puede volver a subir sin error.
        await Migrar(cadena);
        Assert.Equal(new[] { "1" }, await Leer(cadena, """SELECT count(*) FROM contabilidad."Entrenadores" """));
        Assert.Equal(new[] { "0" }, await Leer(cadena, """SELECT count(*) FROM contabilidad."FotosDeEntrenador" """));
    }

    [Fact]
    public async Task Con_una_foto_cargada_la_migracion_hacia_atras_la_descarta_sin_tocar_la_ficha()
    {
        var cadena = await api.BaseNueva("migracion_ficha_con_foto");
        await Migrar(cadena);
        await Ejecutar(cadena, FichaVieja);
        await Ejecutar(cadena, """
            INSERT INTO contabilidad."FotosDeEntrenador" ("EntrenadorId", "Foto", "Actualizada")
            SELECT "Id", decode(repeat('ff', 200), 'hex'), now() FROM contabilidad."Entrenadores"
            """);
        Assert.Equal(new[] { "200" }, await Leer(cadena, """SELECT octet_length("Foto") FROM contabilidad."FotosDeEntrenador" """));

        await Migrar(cadena, Anterior);

        Assert.Equal(new[] { "Ana López" }, await Leer(cadena, """SELECT "NombreCompleto" FROM contabilidad."Entrenadores" """));
        Assert.Equal(new[] { "" }, await Leer(cadena, """SELECT coalesce(to_regclass('contabilidad."FotosDeEntrenador"')::text, '')"""));
    }
}
