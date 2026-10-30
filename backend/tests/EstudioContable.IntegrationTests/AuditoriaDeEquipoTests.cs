using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Npgsql;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// La auditoría guarda desde qué equipo se trabajó (IP, sistema operativo y navegador) y deja constancia de
/// cada inicio de sesión. La IP solo se toma de un proxy de confianza: el encabezado no puede falsearse.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class AuditoriaDeEquipoTests(ApiFixture api)
{
    private const string Chrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
    private const string Firefox = "Mozilla/5.0 (X11; Linux x86_64; rv:132.0) Gecko/20100101 Firefox/132.0";

    private async Task<NpgsqlConnection> Conectar()
    {
        var conexion = new NpgsqlConnection(api.ConnectionString);
        await conexion.OpenAsync();
        return conexion;
    }

    private record Equipo(string Usuario, string? Ip, string? SistemaOperativo, string? Navegador);

    /// <summary>Fila de auditoría del alta de un plan con ese nombre.</summary>
    private async Task<Equipo> AuditoriaDelPlan(string nombre)
    {
        await using var c = await Conectar();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT usuario, ip, sistema_operativo, navegador FROM contabilidad.auditoria
            WHERE tabla = 'contabilidad.Planes' AND datos_nuevos ->> 'Nombre' = @nombre
            ORDER BY id DESC LIMIT 1
            """, c);
        cmd.Parameters.AddWithValue("nombre", nombre);
        await using var lector = await cmd.ExecuteReaderAsync();
        Assert.True(await lector.ReadAsync(), "No se encontró el registro de auditoría");
        return new Equipo(lector.GetString(0),
            lector.IsDBNull(1) ? null : lector.GetString(1),
            lector.IsDBNull(2) ? null : lector.GetString(2),
            lector.IsDBNull(3) ? null : lector.GetString(3));
    }

    private async Task<Equipo> CrearPlanDesde(HttpClient cliente, string nombre)
    {
        var alta = await cliente.PostAsJsonAsync("/api/planes", new { nombre, precioMensual = 1000, duracionMeses = 1 });
        Assert.True(alta.IsSuccessStatusCode, await alta.Detalle());
        return await AuditoriaDelPlan(nombre);
    }

    private async Task<string> CrearUsuario()
    {
        var jefe = await api.Jefe();
        var email = $"acc.{Guid.NewGuid():N}"[..18] + "@gym.com";
        var alta = await jefe.PostAsJsonAsync("/api/usuarios", new { nombre = "Acceso de prueba", email, rol = "Recepcion", telefono = (string?)null, activo = true, password = ApiFixture.ClaveNueva });
        Assert.True(alta.IsSuccessStatusCode, await alta.Detalle());
        return email;
    }

    private async Task<List<(string Operacion, string? Ip, string? Sistema, string? Navegador)>> Accesos(string email)
    {
        await using var c = await Conectar();
        await using var cmd = new NpgsqlCommand(
            "SELECT operacion, ip, sistema_operativo, navegador FROM contabilidad.auditoria WHERE tabla = 'acceso' AND usuario = @u ORDER BY id", c);
        cmd.Parameters.AddWithValue("u", email);
        await using var lector = await cmd.ExecuteReaderAsync();
        var filas = new List<(string, string?, string?, string?)>();
        while (await lector.ReadAsync())
            filas.Add((lector.GetString(0), lector.IsDBNull(1) ? null : lector.GetString(1), lector.IsDBNull(2) ? null : lector.GetString(2), lector.IsDBNull(3) ? null : lector.GetString(3)));
        return filas;
    }

    [Fact]
    public async Task Un_cambio_hecho_desde_la_api_queda_auditado_con_la_ip_el_sistema_y_el_navegador()
    {
        // La conexión llega desde un proxy de red privada (el del frontend) que informa la IP real del equipo.
        var cliente = await api.IniciarSesion(api.ClienteDesde(Chrome, ipDeConexion: "172.18.0.5", reenviado: "203.0.113.9"), "jefe@gym.com");

        var fila = await CrearPlanDesde(cliente, $"Plan equipo {Guid.NewGuid():N}");

        Assert.Equal("jefe@gym.com", fila.Usuario);
        Assert.Equal("203.0.113.9", fila.Ip);
        Assert.Equal("Windows 10/11", fila.SistemaOperativo);
        Assert.Equal("Chrome 141", fila.Navegador);
    }

    [Fact]
    public async Task Sin_proxy_se_guarda_la_ip_de_la_conexion_y_otro_equipo_se_distingue()
    {
        var cliente = await api.IniciarSesion(api.ClienteDesde(Firefox, ipDeConexion: "192.168.1.50"), "contador@gymstudio.com");

        var fila = await CrearPlanDesde(cliente, $"Plan equipo {Guid.NewGuid():N}");

        Assert.Equal("192.168.1.50", fila.Ip);
        Assert.Equal("Linux", fila.SistemaOperativo);
        Assert.Equal("Firefox 132", fila.Navegador);
    }

    [Fact]
    public async Task Quien_no_es_un_proxy_de_confianza_no_puede_falsear_su_ip_con_el_encabezado()
    {
        // Una conexión desde internet (IP pública) escribe X-Forwarded-For a mano: se ignora.
        var cliente = await api.IniciarSesion(api.ClienteDesde(Chrome, ipDeConexion: "8.8.8.8", reenviado: "1.2.3.4"), "jefe@gym.com");

        var fila = await CrearPlanDesde(cliente, $"Plan equipo {Guid.NewGuid():N}");

        Assert.Equal("8.8.8.8", fila.Ip);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("1.2.3.4'; DROP TABLE contabilidad.auditoria;--")]
    [InlineData("no-es-una-ip")]
    public async Task Un_encabezado_con_basura_no_se_guarda_como_ip(string reenviado)
    {
        var cliente = await api.IniciarSesion(api.ClienteDesde(Chrome, ipDeConexion: "172.18.0.5", reenviado: reenviado), "jefe@gym.com");

        var fila = await CrearPlanDesde(cliente, $"Plan equipo {Guid.NewGuid():N}");

        Assert.Equal("172.18.0.5", fila.Ip);   // lo que no es una IP se descarta y queda la de la conexión
    }

    [Fact]
    public async Task Una_herramienta_que_no_es_un_navegador_se_nombra_como_tal()
    {
        var cliente = await api.IniciarSesion(api.ClienteDesde("curl/8.4.0", ipDeConexion: "10.0.0.9"), "jefe@gym.com");

        var fila = await CrearPlanDesde(cliente, $"Plan equipo {Guid.NewGuid():N}");

        Assert.Equal("Desconocido", fila.SistemaOperativo);
        Assert.Equal("curl 8", fila.Navegador);
    }

    [Fact]
    public async Task Un_cambio_hecho_directo_en_la_base_no_tiene_equipo_y_un_dato_enorme_no_lo_hace_fallar()
    {
        var nombre = $"Plan directo {Guid.NewGuid():N}";
        (await (await api.Jefe()).PostAsJsonAsync("/api/planes", new { nombre, precioMensual = 1000, duracionMeses = 1 })).EnsureSuccessStatusCode();

        await using var c = await Conectar();
        // Sin variables de sesión: el equipo queda vacío y el usuario es el de PostgreSQL.
        await using (var sinEquipo = new NpgsqlCommand("UPDATE contabilidad.\"Planes\" SET \"Nombre\" = @nuevo WHERE \"Nombre\" = @viejo", c))
        {
            sinEquipo.Parameters.AddWithValue("nuevo", nombre + " (a)");
            sinEquipo.Parameters.AddWithValue("viejo", nombre);
            Assert.Equal(1, await sinEquipo.ExecuteNonQueryAsync());
        }
        var directo = await UltimoCambioDePlan(c, nombre + " (a)");
        Assert.Null(directo.Ip);
        Assert.Null(directo.SistemaOperativo);
        Assert.Null(directo.Navegador);

        // Variables de sesión absurdas: se recortan y el cambio auditado no falla.
        await using (var enormes = new NpgsqlCommand(
            "SELECT set_config('app.ip', repeat('9', 400), false), set_config('app.sistema_operativo', repeat('s', 400), false), set_config('app.navegador', repeat('n', 400), false)", c))
            await enormes.ExecuteNonQueryAsync();
        await using (var cambio = new NpgsqlCommand("UPDATE contabilidad.\"Planes\" SET \"Nombre\" = @nuevo WHERE \"Nombre\" = @viejo", c))
        {
            cambio.Parameters.AddWithValue("nuevo", nombre + " (b)");
            cambio.Parameters.AddWithValue("viejo", nombre + " (a)");
            Assert.Equal(1, await cambio.ExecuteNonQueryAsync());
        }
        var recortado = await UltimoCambioDePlan(c, nombre + " (b)");
        Assert.Equal(45, recortado.Ip!.Length);
        Assert.Equal(60, recortado.SistemaOperativo!.Length);
        Assert.Equal(60, recortado.Navegador!.Length);
    }

    private static async Task<Equipo> UltimoCambioDePlan(NpgsqlConnection c, string nombreNuevo)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT usuario, ip, sistema_operativo, navegador FROM contabilidad.auditoria WHERE tabla = 'contabilidad.Planes' AND datos_nuevos ->> 'Nombre' = @n ORDER BY id DESC LIMIT 1", c);
        cmd.Parameters.AddWithValue("n", nombreNuevo);
        await using var l = await cmd.ExecuteReaderAsync();
        Assert.True(await l.ReadAsync(), "No se encontró el registro de auditoría");
        return new Equipo(l.GetString(0), l.IsDBNull(1) ? null : l.GetString(1), l.IsDBNull(2) ? null : l.GetString(2), l.IsDBNull(3) ? null : l.GetString(3));
    }

    [Fact]
    public async Task Cada_inicio_de_sesion_queda_registrado_con_su_equipo_y_el_bloqueo_tambien()
    {
        var email = await CrearUsuario();
        var cliente = api.ClienteDesde(Chrome, ipDeConexion: "10.9.8.7");

        (await cliente.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFixture.ClaveNueva })).EnsureSuccessStatusCode();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/auth/login", new { email, password = "incorrecta" + i })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await cliente.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFixture.ClaveNueva })).StatusCode);

        var accesos = await Accesos(email);
        Assert.Equal(new[] { "INGRESO", "FALLIDO", "FALLIDO", "FALLIDO", "FALLIDO", "FALLIDO", "BLOQUEADO" }, accesos.Select(a => a.Operacion));
        Assert.All(accesos, a =>
        {
            Assert.Equal("10.9.8.7", a.Ip);
            Assert.Equal("Windows 10/11", a.Sistema);
            Assert.Equal("Chrome 141", a.Navegador);
        });
    }

    [Fact]
    public async Task Jamas_se_guarda_una_contrasena_en_la_auditoria()
    {
        var email = await CrearUsuario();
        var cliente = api.ClienteDesde(Chrome, ipDeConexion: "10.9.8.7");
        var secreta = "secreta-" + Guid.NewGuid().ToString("N");
        await cliente.PostAsJsonAsync("/api/auth/login", new { email, password = secreta });
        await cliente.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFixture.ClaveNueva });

        await using var c = await Conectar();
        await using var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM contabilidad.auditoria a WHERE a::text ILIKE '%' || @s || '%' OR a::text ILIKE '%' || @c || '%'", c);
        cmd.Parameters.AddWithValue("s", secreta);
        cmd.Parameters.AddWithValue("c", ApiFixture.ClaveNueva);
        Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Las_entradas_absurdas_del_login_se_rechazan_sin_error_y_el_email_hostil_se_guarda_como_texto()
    {
        var cliente = api.ClienteDesde(Chrome, ipDeConexion: "10.9.8.7");

        var emailEnorme = new string('a', 100_000) + "@gym.com";
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/auth/login", new { email = emailEnorme, password = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/auth/login", new { email = "jefe@gym.com", password = new string('p', 100_000) })).StatusCode);

        var hostil = $"'; DROP TABLE contabilidad.auditoria;--{Guid.NewGuid():N}\u0000\r\n@x.com";
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/auth/login", new { email = hostil, password = "x" })).StatusCode);
        var nulos = (await cliente.PostAsJsonAsync("/api/auth/login", new { email = (string?)null, password = (string?)null })).StatusCode;
        Assert.True(nulos is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized, $"Con datos nulos el login respondió {nulos}");

        await using var c = await Conectar();
        await using var largo = new NpgsqlCommand("SELECT max(length(usuario)) FROM contabilidad.auditoria WHERE tabla = 'acceso'", c);
        Assert.True((int)(await largo.ExecuteScalarAsync())! <= 254, "el usuario de un acceso supera el tope de 254 caracteres");

        // El texto hostil quedó como texto (sin caracteres de control) y la tabla sigue existiendo.
        await using var buscar = new NpgsqlCommand("SELECT usuario FROM contabilidad.auditoria WHERE tabla = 'acceso' AND usuario ILIKE '%drop table%' ORDER BY id DESC LIMIT 1", c);
        var guardado = (string)(await buscar.ExecuteScalarAsync())!;
        Assert.Contains("drop table contabilidad.auditoria", guardado);
        Assert.DoesNotContain('\u0000', guardado);
        Assert.DoesNotContain('\n', guardado);
    }

    [Fact]
    public async Task La_pantalla_de_auditoria_recibe_el_equipo_de_cada_registro()
    {
        var nombre = $"Plan equipo {Guid.NewGuid():N}";
        var cliente = await api.IniciarSesion(api.ClienteDesde(Chrome, ipDeConexion: "172.18.0.5", reenviado: "203.0.113.77"), "jefe@gym.com");
        await CrearPlanDesde(cliente, nombre);

        var registros = (await cliente.GetJson("/api/auditoria?tabla=contabilidad.Planes&cantidad=500")).AsArray();
        var registro = registros.First(r => (string?)r!["datosNuevos"]?["Nombre"] == nombre)!;
        Assert.Equal("203.0.113.77", (string?)registro["ip"]);
        Assert.Equal("Windows 10/11", (string?)registro["sistemaOperativo"]);
        Assert.Equal("Chrome 141", (string?)registro["navegador"]);

        var accesos = (await cliente.GetJson("/api/auditoria?tabla=acceso&cantidad=50")).AsArray();
        Assert.Contains(accesos, a => (string?)a!["operacion"] == "INGRESO" && (string?)a["usuario"] == "jefe@gym.com");

        // Recepción no ve la auditoría.
        var recepcion = await api.Recepcion();
        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.GetAsync("/api/auditoria")).StatusCode);
    }
}
