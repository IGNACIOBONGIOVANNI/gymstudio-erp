using static EstudioContable.IntegrationTests.BasesDePrueba;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// La migración que pasa las repeticiones guardadas con texto («30 seg», «10 por pierna») al formato nuevo (un número o un rango):
/// el número queda en las repeticiones y el texto original pasa a las indicaciones, así no se pierde nada.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class MigracionDeRepeticionesTests(ApiFixture api)
{
    private const string Anterior = "20261109235048_SociosDniUnico";

    private static string Renglones(params (string Reps, string Indicaciones)[] renglones) => $"""
        INSERT INTO contabilidad."Ejercicios" ("Nombre", "GrupoMuscular", "Activo") VALUES ('Plancha', 'Abdomen', true);
        INSERT INTO contabilidad."Rutinas" ("Nombre", "Objetivo", "Nivel", "Activa", "ModificadaEn") VALUES ('Prueba', '', 'Principiante', true, now());
        INSERT INTO contabilidad."RutinaEjercicios" ("RutinaId", "EjercicioId", "Dia", "Orden", "Series", "Repeticiones", "DescansoSegundos", "Indicaciones")
        SELECT r."Id", e."Id", 1, v.orden, 3, v.reps, 60, v.ind
        FROM contabilidad."Rutinas" r, contabilidad."Ejercicios" e,
             (VALUES {string.Join(", ", renglones.Select((x, i) => $"({i + 1}, '{x.Reps}', '{x.Indicaciones}')"))}) AS v(orden, reps, ind);
        """;

    private const string Resultado = """
        SELECT "Repeticiones" || '|' || "Indicaciones" FROM contabilidad."RutinaEjercicios" ORDER BY "Orden"
        """;

    [Fact]
    public async Task Los_textos_pasan_a_numero_y_lo_escrito_queda_en_las_indicaciones()
    {
        var cadena = await api.BaseNueva("migracion_repeticiones");
        await Migrar(cadena, Anterior);
        await Ejecutar(cadena, Renglones(
            ("30 seg", ""),
            ("10 min", "Entrada en calor"),
            ("10 por pierna", ""),
            ("15 min", "Vuelta a la calma"),
            ("8 - 10", "Con buena técnica"),
            ("12", "Sin cambios"),
            ("8-10", ""),
            ("al fallo", ""),
            ("12-8", ""),
            ("0", "")));

        await Migrar(cadena);

        Assert.Equal(new[]
        {
            "30|30 segundos",
            "10|Entrada en calor. 10 minutos",
            "10|10 por pierna",
            "15|Vuelta a la calma. 15 minutos",
            "8-10|Con buena técnica",     // solo tenía espacios de más: se acomoda y no se toca la indicación
            "12|Sin cambios",
            "8-10|",
            "1|al fallo",                  // sin número: queda 1 y el texto en la indicación
            "12|12-8",                     // rango al revés: el primer número
            "1|0",                         // cero repeticiones: queda 1
        }, await Leer(cadena, Resultado));
    }

    [Fact]
    public async Task Todas_quedan_en_el_formato_que_exige_el_dominio()
    {
        var cadena = await api.BaseNueva("migracion_repeticiones_formato");
        await Migrar(cadena, Anterior);
        await Ejecutar(cadena, Renglones(("30 seg", ""), ("45\"", ""), ("2 x 10", ""), ("10 — 12", ""), ("999-1000", "")));

        await Migrar(cadena);

        foreach (var fila in await Leer(cadena, Resultado))
            Assert.Equal(fila.Split('|')[0], Domain.Entities.RutinaEjercicio.NormalizarRepeticiones(fila.Split('|')[0]));
    }

    [Fact]
    public async Task Una_indicacion_larga_no_pasa_los_200_caracteres()
    {
        var cadena = await api.BaseNueva("migracion_repeticiones_largo");
        await Migrar(cadena, Anterior);
        await Ejecutar(cadena, Renglones(("30 seg", new string('x', 198))));

        await Migrar(cadena);

        var fila = (await Leer(cadena, Resultado)).Single();
        Assert.StartsWith("30|", fila);
        Assert.True(fila.Length - 3 <= 200);
    }

    [Fact]
    public async Task Volver_atras_no_cambia_nada_y_se_puede_volver_a_subir()
    {
        var cadena = await api.BaseNueva("migracion_repeticiones_atras");
        await Migrar(cadena, Anterior);
        await Ejecutar(cadena, Renglones(("30 seg", "")));
        await Migrar(cadena);

        await Migrar(cadena, Anterior);
        Assert.Equal(new[] { "30|30 segundos" }, await Leer(cadena, Resultado));

        await Migrar(cadena);
        Assert.Equal(new[] { "30|30 segundos" }, await Leer(cadena, Resultado));
    }
}
