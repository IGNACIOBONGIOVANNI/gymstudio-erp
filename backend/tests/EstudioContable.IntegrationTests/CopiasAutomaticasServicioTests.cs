using System.IO.Compression;
using EstudioContable.Application.Copias;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static EstudioContable.IntegrationTests.BasesDePrueba;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// El servicio de copias automáticas, con el reloj en la mano: guarda la copia cuando toca y no antes, no se repite, recupera lo que
/// se perdió con el sistema apagado, conserva solo las últimas N sin tocar otros archivos, sobrevive a una carpeta rota y no deja
/// salir nada de su carpeta.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CopiasAutomaticasServicioTests(ApiFixture api)
{
    private const string MensajeDeFalla = "No se pudo guardar la copia en la carpeta de copias automáticas. Revisá los permisos y el espacio en disco.";

    // Todas las horas son de Argentina (UTC-3).
    private static DateTimeOffset Fecha(int anio, int mes, int dia, int hora = 0, int minuto = 0, int segundo = 0)
        => new(anio, mes, dia, hora, minuto, segundo, TimeSpan.FromHours(-3));

    private async Task<T> Con<T>(Func<ICopiasAutomaticasService, Task<T>> accion)
    {
        using var alcance = api.Servicios.CreateScope();
        return await accion(alcance.ServiceProvider.GetRequiredService<ICopiasAutomaticasService>());
    }

    private static ProgramacionDeCopias Diaria(int hora = 22, int conservar = 14) => new(true, "Diaria", hora, 0, null, null, conservar);

    private static readonly string[] Nombres = { "gymstudio_auto_20261101_220000.zip", "gymstudio_auto_20261102_220000.zip", "gymstudio_auto_20261103_220000.zip" };

    private Task Reiniciar() => api.ReiniciarCopias();

    /// <summary>Programa con el reloj fijado en <paramref name="ahora"/> (desde ahí se cuenta la próxima copia).</summary>
    private async Task Programar(DateTimeOffset ahora, ProgramacionDeCopias pedido)
    {
        api.Reloj.Fijar(ahora);
        await Con(s => s.ProgramarAsync(pedido, CancellationToken.None));
    }

    private async Task<CopiaGuardada?> EjecutarEn(DateTimeOffset ahora)
    {
        api.Reloj.Fijar(ahora);
        return await Con(s => s.EjecutarSiTocaAsync(CancellationToken.None));
    }

    private string[] EnLaCarpeta() => Directory.Exists(api.CarpetaDeCopias)
        ? Directory.EnumerateFileSystemEntries(api.CarpetaDeCopias).Select(p => Path.GetFileName(p)!).OrderBy(n => n, StringComparer.Ordinal).ToArray()
        : Array.Empty<string>();

    private void Crear(params string[] archivos)
    {
        Directory.CreateDirectory(api.CarpetaDeCopias);
        foreach (var a in archivos) File.WriteAllText(Path.Combine(api.CarpetaDeCopias, a), "contenido");
    }

    private async Task PonerUltimaCopia(DateTimeOffset cuando)
    {
        await using var c = new NpgsqlConnection(api.ConnectionString);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand("""UPDATE contabilidad."ConfiguracionDeCopias" SET "UltimaCopia" = @cuando WHERE "Id" = 1""", c);
        cmd.Parameters.AddWithValue("cuando", cuando.ToUniversalTime());
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Hace que la carpeta no se pueda crear: en su lugar hay un archivo.</summary>
    private void RomperLaCarpeta()
    {
        if (Directory.Exists(api.CarpetaDeCopias)) Directory.Delete(api.CarpetaDeCopias, recursive: true);
        File.WriteAllText(api.CarpetaDeCopias, "no soy una carpeta");
    }

    // ---------- cuándo guarda ----------

    [Fact]
    public async Task No_hace_nada_si_esta_inactiva()
    {
        await Reiniciar();

        Assert.Null(await EjecutarEn(Fecha(2026, 11, 7, 22)));
        Assert.Empty(EnLaCarpeta());
    }

    [Fact]
    public async Task Hace_la_copia_cuando_toca_y_no_antes_y_queda_firmada()
    {
        await Reiniciar();
        await Programar(Fecha(2026, 11, 7, 15), Diaria());

        Assert.Null(await EjecutarEn(Fecha(2026, 11, 7, 21, 59)));
        Assert.Empty(EnLaCarpeta());

        var copia = await EjecutarEn(Fecha(2026, 11, 7, 22));
        Assert.NotNull(copia);
        Assert.Equal("gymstudio_auto_20261107_220000.zip", copia!.Nombre);
        Assert.Equal(new[] { "gymstudio_auto_20261107_220000.zip" }, EnLaCarpeta());
        Assert.True(copia.Bytes > 0);

        // Es una copia como las demás: se verifica con la firma del servidor.
        using var alcance = api.Servicios.CreateScope();
        var copias = alcance.ServiceProvider.GetRequiredService<ICopiasDeSeguridadService>();
        await using var flujo = File.OpenRead(Path.Combine(api.CarpetaDeCopias, copia.Nombre));
        Assert.Equal("valida", (await copias.CompararAsync(flujo, CancellationToken.None)).Firma);

        var estado = await Con(s => s.ObtenerAsync(CancellationToken.None));
        Assert.Equal(Fecha(2026, 11, 7, 22), estado.UltimaCopia);
        Assert.Null(estado.UltimoError);
        Assert.Equal(Fecha(2026, 11, 8, 22), estado.ProximaCopia);
    }

    [Fact]
    public async Task No_repite_dentro_del_mismo_periodo()
    {
        await Reiniciar();
        await Programar(Fecha(2026, 11, 7, 15), Diaria());
        Assert.NotNull(await EjecutarEn(Fecha(2026, 11, 7, 22)));

        Assert.Null(await EjecutarEn(Fecha(2026, 11, 7, 22, 0, 30)));
        Assert.Null(await EjecutarEn(Fecha(2026, 11, 8, 21, 59)));
        Assert.Single(EnLaCarpeta());
    }

    [Fact]
    public async Task Si_estuvo_apagado_varios_periodos_hace_una_sola_copia_al_volver()
    {
        await Reiniciar();
        await Programar(Fecha(2026, 11, 1, 12), Diaria());
        await PonerUltimaCopia(Fecha(2026, 11, 4, 22));   // la última copia, hace tres días

        Assert.NotNull(await EjecutarEn(Fecha(2026, 11, 7, 23)));
        Assert.Null(await EjecutarEn(Fecha(2026, 11, 7, 23, 1)));
        Assert.Equal(new[] { "gymstudio_auto_20261107_230000.zip" }, EnLaCarpeta());
    }

    [Fact]
    public async Task Hacer_una_copia_ahora_guarda_cuenta_como_ultima_y_no_mueve_la_proxima_programada()
    {
        await Reiniciar();
        await Programar(Fecha(2026, 11, 7, 12), Diaria());
        api.Reloj.Fijar(Fecha(2026, 11, 7, 15));

        var copia = await Con(s => s.GuardarAhoraAsync(CancellationToken.None));

        Assert.Equal("gymstudio_auto_20261107_150000.zip", copia.Nombre);
        Assert.Equal(new[] { copia.Nombre }, EnLaCarpeta());
        var estado = await Con(s => s.ObtenerAsync(CancellationToken.None));
        Assert.Equal(Fecha(2026, 11, 7, 15), estado.UltimaCopia);
        Assert.Equal(Fecha(2026, 11, 7, 22), estado.ProximaCopia);   // la de las 22:00 sigue en su lugar
    }

    // ---------- retención y archivos ajenos ----------

    [Fact]
    public async Task La_retencion_borra_solo_las_copias_propias_y_deja_todo_lo_demas()
    {
        await Reiniciar();
        await Programar(Fecha(2026, 11, 1, 12), Diaria(conservar: 2));
        Crear(Nombres);
        Crear("notas.zip", "gymstudio_auto_x.zip", "copia.dump", "gymstudio_auto_20261104_220000.zip.bak");
        Directory.CreateDirectory(Path.Combine(api.CarpetaDeCopias, "gymstudio_auto_20250101_000000.zip"));   // una carpeta con nombre de copia

        Assert.NotNull(await EjecutarEn(Fecha(2026, 11, 7, 22)));

        Assert.Equal(new[]
        {
            "copia.dump", "gymstudio_auto_20250101_000000.zip", "gymstudio_auto_20261103_220000.zip", "gymstudio_auto_20261104_220000.zip.bak",
            "gymstudio_auto_20261107_220000.zip", "gymstudio_auto_x.zip", "notas.zip",
        }, EnLaCarpeta());
    }

    [Fact]
    public async Task Un_temporal_huerfano_de_un_corte_se_borra_y_otros_temporales_no()
    {
        await Reiniciar();
        await Programar(Fecha(2026, 11, 1, 12), Diaria());
        Crear("gymstudio_auto_20261101_220000.zip.tmp", "otro.tmp");

        Assert.NotNull(await EjecutarEn(Fecha(2026, 11, 7, 22)));

        Assert.Equal(new[] { "gymstudio_auto_20261107_220000.zip", "otro.tmp" }, EnLaCarpeta());
    }

    [Fact]
    public async Task Listar_ordena_de_la_mas_nueva_a_la_mas_vieja_e_ignora_lo_ajeno()
    {
        await Reiniciar();
        Crear(Nombres[0], Nombres[2], Nombres[1], "notas.zip", "gymstudio_auto_20261104_220000.zip.tmp");

        var lista = await Con(s => Task.FromResult(s.Listar()));

        Assert.Equal(new[] { Nombres[2], Nombres[1], Nombres[0] }, lista.Select(c => c.Nombre).ToArray());
        Assert.All(lista, c => Assert.Equal("contenido".Length, c.Bytes));

        await Reiniciar();   // sin carpeta, la lista es vacía
        Assert.Empty(await Con(s => Task.FromResult(s.Listar())));
    }

    // ---------- fallas ----------

    [Fact]
    public async Task Una_falla_se_guarda_no_tumba_nada_y_se_reintenta_a_los_30_minutos()
    {
        await Reiniciar();
        await Programar(Fecha(2026, 11, 6, 23), Diaria());
        RomperLaCarpeta();

        Assert.Null(await EjecutarEn(Fecha(2026, 11, 7, 22, 5)));   // no lanza
        var tras = await Con(s => s.ObtenerAsync(CancellationToken.None));
        Assert.Equal(MensajeDeFalla, tras.UltimoError);
        Assert.Equal(Fecha(2026, 11, 7, 22, 5), tras.UltimoIntento);
        Assert.Null(tras.UltimaCopia);

        Assert.Null(await EjecutarEn(Fecha(2026, 11, 7, 22, 34, 59)));   // todavía no: no se reintenta cada minuto
        Assert.Equal(Fecha(2026, 11, 7, 22, 5), (await Con(s => s.ObtenerAsync(CancellationToken.None))).UltimoIntento);

        Assert.Null(await EjecutarEn(Fecha(2026, 11, 7, 22, 35)));       // a los 30 minutos reintenta (y vuelve a fallar)
        Assert.Equal(Fecha(2026, 11, 7, 22, 35), (await Con(s => s.ObtenerAsync(CancellationToken.None))).UltimoIntento);

        File.Delete(api.CarpetaDeCopias);                              // se arregla la carpeta
        Assert.NotNull(await EjecutarEn(Fecha(2026, 11, 7, 23, 6)));
        var arreglado = await Con(s => s.ObtenerAsync(CancellationToken.None));
        Assert.Null(arreglado.UltimoError);
        Assert.Equal(Fecha(2026, 11, 7, 23, 6), arreglado.UltimaCopia);
    }

    [Fact]
    public async Task Hacer_una_copia_ahora_con_la_carpeta_rota_avisa_con_un_mensaje_claro_y_no_cambia_la_programacion()
    {
        await Reiniciar();
        await Programar(Fecha(2026, 11, 7, 12), Diaria());
        RomperLaCarpeta();
        api.Reloj.Fijar(Fecha(2026, 11, 7, 15));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Con(s => s.GuardarAhoraAsync(CancellationToken.None)));

        Assert.Equal(MensajeDeFalla, error.Message);
        var estado = await Con(s => s.ObtenerAsync(CancellationToken.None));
        Assert.Null(estado.UltimaCopia);
        Assert.True(estado.Activa);
    }

    [Fact]
    public async Task Una_corrida_automatica_y_una_copia_ahora_a_la_vez_no_dejan_archivos_rotos()
    {
        await Reiniciar();
        await Programar(Fecha(2026, 11, 7, 15), Diaria());
        api.Reloj.Fijar(Fecha(2026, 11, 7, 22));

        var automatica = Con(s => s.EjecutarSiTocaAsync(CancellationToken.None));
        var ahora = Con(s => s.GuardarAhoraAsync(CancellationToken.None));
        await Task.WhenAll(automatica, ahora);

        var archivos = EnLaCarpeta();
        Assert.NotEmpty(archivos);
        Assert.DoesNotContain(archivos, a => a.EndsWith(".tmp"));
        foreach (var archivo in archivos)
        {
            using var zip = ZipFile.OpenRead(Path.Combine(api.CarpetaDeCopias, archivo));   // se abre: está entero
            Assert.NotNull(zip.GetEntry("manifiesto.json"));
        }
    }

    // ---------- programar ----------

    public static IEnumerable<object?[]> Programaciones_imposibles()
    {
        yield return new object?[] { "semanal sin día", "Semanal", 22, 0, null, null, 14 };
        yield return new object?[] { "semanal día 8", "Semanal", 22, 0, 8, null, 14 };
        yield return new object?[] { "mensual día 0", "Mensual", 22, 0, null, 0, 14 };
        yield return new object?[] { "mensual día 29", "Mensual", 22, 0, null, 29, 14 };
        yield return new object?[] { "mensual día 31", "Mensual", 22, 0, null, 31, 14 };
        yield return new object?[] { "hora 24", "Diaria", 24, 0, null, null, 14 };
        yield return new object?[] { "minuto 60", "Diaria", 22, 60, null, null, 14 };
        yield return new object?[] { "conservar 0", "Diaria", 22, 0, null, null, 0 };
        yield return new object?[] { "conservar 1000", "Diaria", 22, 0, null, null, 1000 };
        yield return new object?[] { "frecuencia nula", null, 22, 0, null, null, 14 };
        yield return new object?[] { "frecuencia vacía", "", 22, 0, null, null, 14 };
        yield return new object?[] { "frecuencia numérica 99", "99", 22, 0, null, null, 14 };
        yield return new object?[] { "frecuencia numérica que existe", "3", 22, 0, null, null, 14 };
        yield return new object?[] { "frecuencia que no existe", "Cada5Minutos", 22, 0, null, null, 14 };
        yield return new object?[] { "frecuencias combinadas", "CadaSeisHoras,CadaDoceHoras", 22, 0, null, null, 14 };
        yield return new object?[] { "frecuencia con otra escritura", "diaria", 22, 0, null, null, 14 };
    }

    [Theory]
    [MemberData(nameof(Programaciones_imposibles))]
    public async Task Programar_rechaza_lo_imposible_y_no_cambia_lo_guardado(string caso, string? frecuencia, int hora, int minuto, int? diaDeLaSemana, int? diaDelMes, int conservar)
    {
        await Reiniciar();
        var antes = await Con(s => s.ObtenerAsync(CancellationToken.None));

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => Con(s => s.ProgramarAsync(new ProgramacionDeCopias(true, frecuencia, hora, minuto, diaDeLaSemana, diaDelMes, conservar), CancellationToken.None)));

        Assert.False(string.IsNullOrWhiteSpace(error.Message), caso);
        Assert.Equal(antes with { ProximaCopia = null }, (await Con(s => s.ObtenerAsync(CancellationToken.None))) with { ProximaCopia = null });
    }

    [Fact]
    public async Task Programar_guarda_la_programacion_y_calcula_la_proxima_copia()
    {
        await Reiniciar();
        api.Reloj.Fijar(Fecha(2026, 11, 7, 15));

        var estado = await Con(s => s.ProgramarAsync(new ProgramacionDeCopias(true, "Semanal", 8, 30, 1, null, 20), CancellationToken.None));

        Assert.Equal(("Semanal", 8, 30, 1, (int?)null, 20, true), (estado.Frecuencia, estado.Hora, estado.Minuto, estado.DiaDeLaSemana, estado.DiaDelMes, estado.Conservar, estado.Activa));
        Assert.Equal(Fecha(2026, 11, 9, 8, 30), estado.ProximaCopia);   // el lunes siguiente
        Assert.Equal(estado, await Con(s => s.ObtenerAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task Una_programacion_inactiva_no_tiene_proxima_copia()
    {
        await Reiniciar();
        var estado = await Con(s => s.ProgramarAsync(new ProgramacionDeCopias(false, "Diaria", 22, 0, null, null, 14), CancellationToken.None));
        Assert.Null(estado.ProximaCopia);
    }

    // ---------- descargas ----------

    [Fact]
    public async Task AbrirParaDescargar_solo_abre_copias_de_la_carpeta()
    {
        await Reiniciar();
        Crear("gymstudio_auto_20261107_220000.zip");
        // Un archivo con nombre de copia pero afuera de la carpeta: ninguna ruta tiene que llegar hasta él.
        var afuera = Path.Combine(Path.GetDirectoryName(api.CarpetaDeCopias)!, "gymstudio_auto_20261107_230000.zip");
        File.WriteAllText(afuera, "secreto");
        try
        {
            await using (var flujo = await Con(s => Task.FromResult(s.AbrirParaDescargar("gymstudio_auto_20261107_220000.zip"))))
            {
                Assert.NotNull(flujo);
                Assert.Equal("contenido", await new StreamReader(flujo!).ReadToEndAsync());
            }

            foreach (var hostil in new string?[]
            {
                null, "", "../gymstudio_auto_20261107_230000.zip", "..\\gymstudio_auto_20261107_230000.zip", "/etc/passwd", "C:\\Windows\\win.ini",
                "%2e%2e%2fgymstudio_auto_20261107_230000.zip", "gymstudio_auto_20261107_220000.zip\n", "gymstudio_auto_20261107_220000.zip.tmp",
                "gymstudio_auto_20250101_000000.zip",   // un nombre válido que no existe
                "gymstudio_auto_20261107_230000.zip",   // existe, pero afuera de la carpeta
            })
            {
                await using var flujo = await Con(s => Task.FromResult(s.AbrirParaDescargar(hostil!)));
                Assert.True(flujo == null, $"«{hostil}» no tendría que abrir nada.");
            }
        }
        finally
        {
            File.Delete(afuera);
        }
    }

    // ---------- auditoría ----------

    [Fact]
    public async Task Quedan_en_la_auditoria_la_programacion_cada_copia_y_cada_falla()
    {
        await Reiniciar();
        var jefe = await api.Jefe();
        var ultimas = (await jefe.GetJson("/api/auditoria?cantidad=1")).AsArray();
        var desde = ultimas.Count == 0 ? 0 : (long)ultimas[0]!["id"]!;

        await Programar(Fecha(2026, 11, 7, 15), Diaria());
        Assert.NotNull(await EjecutarEn(Fecha(2026, 11, 7, 22)));                       // AUTOMATICA
        await Con(s => s.GuardarAhoraAsync(CancellationToken.None));                  // GUARDADA
        RomperLaCarpeta();
        Assert.Null(await EjecutarEn(Fecha(2026, 11, 8, 22, 5)));                       // FALLIDA

        var filas = (await jefe.GetJson("/api/auditoria?cantidad=200&tabla=copia")).AsArray()
            .Where(f => (long)f!["id"]! > desde).ToList();
        Assert.Equal(new[] { "AUTOMATICA", "FALLIDA", "GUARDADA", "PROGRAMADA" }, filas.Select(f => (string)f!["operacion"]!).OrderBy(o => o).ToArray());
        Assert.All(filas, f => Assert.Equal("sistema", (string)f!["usuario"]!));   // fuera de una request no hay usuario
    }
}
