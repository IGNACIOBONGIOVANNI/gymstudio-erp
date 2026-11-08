using static EstudioContable.IntegrationTests.BasesDePrueba;

namespace EstudioContable.IntegrationTests;

/// <summary>El punto de partida común de las pruebas de copias automáticas, que comparten la base y la carpeta.</summary>
public static class AyudasDeCopias
{
    /// <summary>Programación apagada y por defecto, carpeta de copias vacía (o inexistente) y reloj real.</summary>
    public static async Task ReiniciarCopias(this ApiFixture api)
    {
        api.Reloj.Liberar();
        if (Directory.Exists(api.CarpetaDeCopias)) Directory.Delete(api.CarpetaDeCopias, recursive: true);
        else if (File.Exists(api.CarpetaDeCopias)) File.Delete(api.CarpetaDeCopias);
        await Ejecutar(api.ConnectionString, """
            UPDATE contabilidad."ConfiguracionDeCopias"
            SET "Activa" = false, "Frecuencia" = 'Diaria', "Hora" = 22, "Minuto" = 0, "DiaDeLaSemana" = NULL, "DiaDelMes" = NULL,
                "Conservar" = 14, "ActivaDesde" = now(), "UltimaCopia" = NULL, "UltimoIntento" = NULL, "UltimoError" = NULL
            WHERE "Id" = 1
            """);
    }
}
