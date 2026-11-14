using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Cambio de contraseña: cada usuario cambia la suya dando la actual y la nueva dos veces; el Administrador, desde Usuarios,
/// cambia la de otro sin saber la actual (la saltea), pero también escribe la nueva dos veces.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CambioDeClaveTests(ApiFixture api)
{
    private const string OtraClave = "Otra-Clave-Valida-29?";

    /// <summary>Crea un usuario de Recepción nuevo (para no tocar las claves de los usuarios que comparten las demás pruebas).</summary>
    private async Task<(long Id, string Email)> NuevoUsuario()
    {
        var jefe = await api.Jefe();
        var email = $"clave.{Guid.NewGuid():N}"[..18] + "@gym.com";
        var alta = await jefe.PostAsJsonAsync("/api/usuarios", new
        {
            nombre = "Cambio de clave", email, rol = "Recepcion", telefono = (string?)null, activo = true,
            password = ApiFixture.ClaveNueva, confirmacionPassword = ApiFixture.ClaveNueva
        });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        return ((long)(await alta.Content.ReadFromJsonAsync<JsonObject>())!["id"]!, email);
    }

    private async Task<HttpStatusCode> Ingresar(string email, string clave)
        => (await api.ClienteAnonimo().PostAsJsonAsync("/api/auth/login", new { email, password = clave })).StatusCode;

    [Fact]
    public async Task Cada_usuario_cambia_su_contrasena_dando_la_actual_y_la_nueva_dos_veces()
    {
        var (_, email) = await NuevoUsuario();
        var usuario = await api.ClienteComo(email, ApiFixture.ClaveNueva);

        var cambio = await usuario.PutAsJsonAsync("/api/auth/mi-clave",
            new { claveActual = ApiFixture.ClaveNueva, claveNueva = OtraClave, confirmacionClave = OtraClave });

        Assert.Equal(HttpStatusCode.NoContent, cambio.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await Ingresar(email, OtraClave));
        Assert.Equal(HttpStatusCode.Unauthorized, await Ingresar(email, ApiFixture.ClaveNueva));
    }

    [Fact]
    public async Task Con_la_contrasena_actual_equivocada_no_se_cambia()
    {
        var (_, email) = await NuevoUsuario();
        var usuario = await api.ClienteComo(email, ApiFixture.ClaveNueva);

        var cambio = await usuario.PutAsJsonAsync("/api/auth/mi-clave",
            new { claveActual = "No-Es-La-Clave-1!", claveNueva = OtraClave, confirmacionClave = OtraClave });

        Assert.Equal(HttpStatusCode.BadRequest, cambio.StatusCode);
        Assert.Contains("actual no es correcta", await cambio.Detalle());
        Assert.Equal(HttpStatusCode.OK, await Ingresar(email, ApiFixture.ClaveNueva));
    }

    [Fact]
    public async Task Si_las_dos_contrasenas_nuevas_no_coinciden_no_se_cambia()
    {
        var (_, email) = await NuevoUsuario();
        var usuario = await api.ClienteComo(email, ApiFixture.ClaveNueva);

        var cambio = await usuario.PutAsJsonAsync("/api/auth/mi-clave",
            new { claveActual = ApiFixture.ClaveNueva, claveNueva = OtraClave, confirmacionClave = OtraClave + "x" });

        Assert.Equal(HttpStatusCode.BadRequest, cambio.StatusCode);
        Assert.Contains("no coinciden", await cambio.Detalle());
        Assert.Equal(HttpStatusCode.OK, await Ingresar(email, ApiFixture.ClaveNueva));
    }

    [Theory]
    [InlineData("corta1!")]          // menos de 8 caracteres
    [InlineData("Clave-Con-123!")]   // números consecutivos
    public async Task Una_contrasena_nueva_debil_se_rechaza(string debil)
    {
        var (_, email) = await NuevoUsuario();
        var usuario = await api.ClienteComo(email, ApiFixture.ClaveNueva);

        var cambio = await usuario.PutAsJsonAsync("/api/auth/mi-clave",
            new { claveActual = ApiFixture.ClaveNueva, claveNueva = debil, confirmacionClave = debil });

        Assert.Equal(HttpStatusCode.BadRequest, cambio.StatusCode);
        Assert.Contains("política", await cambio.Detalle());
    }

    [Fact]
    public async Task La_contrasena_nueva_tiene_que_ser_distinta_de_la_actual()
    {
        var (_, email) = await NuevoUsuario();
        var usuario = await api.ClienteComo(email, ApiFixture.ClaveNueva);

        var cambio = await usuario.PutAsJsonAsync("/api/auth/mi-clave",
            new { claveActual = ApiFixture.ClaveNueva, claveNueva = ApiFixture.ClaveNueva, confirmacionClave = ApiFixture.ClaveNueva });

        Assert.Equal(HttpStatusCode.BadRequest, cambio.StatusCode);
        Assert.Contains("distinta", await cambio.Detalle());
    }

    [Fact]
    public async Task Sin_sesion_no_se_puede_cambiar_ninguna_contrasena()
    {
        var cambio = await api.ClienteAnonimo().PutAsJsonAsync("/api/auth/mi-clave",
            new { claveActual = ApiFixture.Clave, claveNueva = OtraClave, confirmacionClave = OtraClave });
        Assert.Equal(HttpStatusCode.Unauthorized, cambio.StatusCode);
    }

    [Fact]
    public async Task El_administrador_cambia_la_de_otro_sin_saber_la_actual_pero_repitiendo_la_nueva()
    {
        var (id, email) = await NuevoUsuario();
        var jefe = await api.Jefe();
        object Datos(string? clave, string? confirmacion) => new
        {
            nombre = "Cambio de clave", email, rol = "Recepcion", telefono = (string?)null, activo = true,
            password = clave, confirmacionPassword = confirmacion
        };

        var distinta = await jefe.PutAsJsonAsync($"/api/usuarios/{id}", Datos(OtraClave, OtraClave + "x"));
        Assert.Equal(HttpStatusCode.BadRequest, distinta.StatusCode);
        Assert.Contains("no coinciden", await distinta.Detalle());

        var sinRepetir = await jefe.PutAsJsonAsync($"/api/usuarios/{id}", Datos(OtraClave, null));
        Assert.Equal(HttpStatusCode.BadRequest, sinRepetir.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await Ingresar(email, ApiFixture.ClaveNueva));

        var bien = await jefe.PutAsJsonAsync($"/api/usuarios/{id}", Datos(OtraClave, OtraClave));
        Assert.Equal(HttpStatusCode.NoContent, bien.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await Ingresar(email, OtraClave));

        // Editar los datos sin escribir contraseña no la cambia ni pide repetirla.
        var sinClave = await jefe.PutAsJsonAsync($"/api/usuarios/{id}", Datos(null, null));
        Assert.Equal(HttpStatusCode.NoContent, sinClave.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await Ingresar(email, OtraClave));
    }

    [Fact]
    public async Task El_alta_de_un_usuario_tambien_pide_la_contrasena_dos_veces()
    {
        var jefe = await api.Jefe();
        var email = $"alta.{Guid.NewGuid():N}"[..17] + "@gym.com";
        var alta = await jefe.PostAsJsonAsync("/api/usuarios", new
        {
            nombre = "Sin repetir", email, rol = "Recepcion", telefono = (string?)null, activo = true,
            password = ApiFixture.ClaveNueva, confirmacionPassword = "Distinta-Clave-29?"
        });
        Assert.Equal(HttpStatusCode.BadRequest, alta.StatusCode);
        Assert.Contains("no coinciden", await alta.Detalle());
    }

    [Fact]
    public async Task El_cambio_queda_en_la_auditoria_sin_la_contrasena()
    {
        var (id, email) = await NuevoUsuario();
        var usuario = await api.ClienteComo(email, ApiFixture.ClaveNueva);
        await usuario.PutAsJsonAsync("/api/auth/mi-clave", new { claveActual = ApiFixture.ClaveNueva, claveNueva = OtraClave, confirmacionClave = OtraClave });

        var jefe = await api.Jefe();
        var registros = (await jefe.GetJson($"/api/auditoria?tabla=contabilidad.Usuario&usuario={Uri.EscapeDataString(email)}")).AsArray();
        var cambio = registros.Select(r => r!.AsObject()).First(r => (string?)r["registroId"] == id.ToString() && (string)r["operacion"]! == "UPDATE");
        Assert.Contains("Contraseña", cambio["camposModificados"]!.AsArray().Select(c => (string)c!));
        Assert.DoesNotContain(OtraClave, cambio.ToJsonString());
    }
}
