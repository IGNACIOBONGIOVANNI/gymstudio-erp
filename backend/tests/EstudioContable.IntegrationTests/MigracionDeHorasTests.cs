using static EstudioContable.IntegrationTests.BasesDePrueba;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// La migración que suma el valor de la hora a la ficha y la tabla de horas cumplidas, con datos de verdad de por medio: se migra una base
/// hasta la versión anterior (la de la foto y el contacto), se carga un entrenador, se migra a la última y se vuelve atrás. Solo agrega: las
/// fichas que ya estaban quedan como estaban, sin valor de hora.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class MigracionDeHorasTests(ApiFixture api)
{
    private const string Anterior = "20261108145843_FichaDeEntrenadorConContacto";

    private const string FichaVieja = """
        INSERT INTO contabilidad."Entrenadores" ("NombreCompleto", "Telefono", "Especialidad", "Email", "WhatsApp", "Activo", "FechaCreacion")
        VALUES ('Ana López', '11 1234-5678', 'Yoga', 'ana@gym.com', '11 5555-1234', true, now())
        """;

    [Fact]
    public async Task Las_fichas_existentes_quedan_sin_valor_de_hora_y_la_migracion_se_deshace_y_se_repite()
    {
        var cadena = await api.BaseNueva("migracion_horas");
        await Migrar(cadena, Anterior);
        await Ejecutar(cadena, FichaVieja);

        await Migrar(cadena);

        // La ficha sigue igual y sin valor de hora; la tabla de horas existe, vacía, con su auditoría y con la restricción que no deja borrar.
        Assert.Equal(new[] { "Ana López|11 1234-5678|ana@gym.com|" },
            await Leer(cadena, """SELECT "NombreCompleto", "Telefono", "Email", "ValorHora" FROM contabilidad."Entrenadores" """));
        Assert.Equal(new[] { "0" }, await Leer(cadena, """SELECT count(*) FROM contabilidad."HorasDeEntrenador" """));
        Assert.Equal(new[] { "1" }, await Leer(cadena, """
            SELECT count(*) FROM pg_trigger WHERE tgrelid = 'contabilidad."HorasDeEntrenador"'::regclass AND tgname = 'trg_auditar_horas_entrenador' AND NOT tgisinternal
            """));
        Assert.Equal(new[] { "r" }, await Leer(cadena, """
            SELECT confdeltype FROM pg_constraint WHERE conrelid = 'contabilidad."HorasDeEntrenador"'::regclass AND contype = 'f'
            """));
        Assert.Equal(new[] { "numeric(18,2)", "numeric(4,2)" }, await Leer(cadena, """
            SELECT format_type(atttypid, atttypmod) FROM pg_attribute
            WHERE attrelid = 'contabilidad."HorasDeEntrenador"'::regclass AND attname IN ('ValorHora', 'Horas') ORDER BY attname DESC
            """));

        // Hacia atrás: se van la columna y la tabla de horas, y la ficha sigue.
        await Migrar(cadena, Anterior);
        Assert.Empty(await Leer(cadena, """
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'contabilidad' AND table_name = 'Entrenadores' AND column_name = 'ValorHora'
            """));
        Assert.Equal(new[] { "" }, await Leer(cadena, """SELECT coalesce(to_regclass('contabilidad."HorasDeEntrenador"')::text, '')"""));
        Assert.Equal(new[] { "Ana López|ana@gym.com" },
            await Leer(cadena, """SELECT "NombreCompleto", "Email" FROM contabilidad."Entrenadores" """));

        // Y se puede volver a subir sin error.
        await Migrar(cadena);
        Assert.Equal(new[] { "1" }, await Leer(cadena, """SELECT count(*) FROM contabilidad."Entrenadores" """));
        Assert.Equal(new[] { "0" }, await Leer(cadena, """SELECT count(*) FROM contabilidad."HorasDeEntrenador" """));
    }

    [Fact]
    public async Task Con_horas_cargadas_volver_atras_las_descarta_sin_tocar_la_ficha()
    {
        var cadena = await api.BaseNueva("migracion_horas_con_datos");
        await Migrar(cadena);
        await Ejecutar(cadena, FichaVieja);
        await Ejecutar(cadena, """UPDATE contabilidad."Entrenadores" SET "ValorHora" = 1500.50""");
        await Ejecutar(cadena, """
            INSERT INTO contabilidad."HorasDeEntrenador" ("EntrenadorId", "Fecha", "Horas", "ValorHora")
            SELECT "Id", DATE '2026-11-10', 7.5, 1500.50 FROM contabilidad."Entrenadores"
            """);
        Assert.Equal(new[] { "7.50|1500.50" }, await Leer(cadena, """SELECT "Horas"::text, "ValorHora"::text FROM contabilidad."HorasDeEntrenador" """));

        await Migrar(cadena, Anterior);

        Assert.Equal(new[] { "Ana López" }, await Leer(cadena, """SELECT "NombreCompleto" FROM contabilidad."Entrenadores" """));
        Assert.Equal(new[] { "" }, await Leer(cadena, """SELECT coalesce(to_regclass('contabilidad."HorasDeEntrenador"')::text, '')"""));
    }
}
