using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Nombre de persona con solo letras y teléfono con código de país en Socios y Usuarios (los entrenadores están en EntrenadoresTests),
/// contra la API y la base reales: lo que la pantalla filtra, el servidor también lo exige.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class NombreYTelefonoDePersonasTests(ApiFixture api)
{
    private static string Dni() => Random.Shared.Next(20_000_000, 49_999_999).ToString();

    private static object Socio(string nombre, string apellido, string? telefono, string? dni = null)
        => new { nombre, apellido, dni = dni ?? Dni(), telefono };

    private static object Usuario(string nombre, string? telefono, string? email = null)
        => new { nombre, email = email ?? $"{Nombres.Aleatorio(12)}@gym.com", rol = "Recepcion", telefono, activo = true, password = ApiFixture.ClaveNueva };

    // ---------- socios ----------

    [Fact]
    public async Task Un_socio_con_nombre_de_letras_y_telefono_con_codigo_se_guarda_y_se_edita()
    {
        var jefe = await api.Jefe();
        var apellido = "Pérez-Gómez " + Nombres.Aleatorio(6);
        var alta = await jefe.PostAsJsonAsync("/api/socios", Socio("María José", apellido, "+54 1155551234"));
        Assert.True(alta.StatusCode == HttpStatusCode.Created || alta.StatusCode == HttpStatusCode.OK, await alta.Detalle());
        var id = (long)(await alta.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;

        var cambio = await jefe.PutAsJsonAsync($"/api/socios/{id}", Socio("Ana", apellido, "+598 91234567"));
        Assert.True(cambio.IsSuccessStatusCode, await cambio.Detalle());

        var lista = await jefe.GetFromJsonAsync<JsonArray>("/api/socios");
        var socio = lista!.Select(s => s!.AsObject()).Single(s => (long)s["id"]! == id);
        Assert.Equal("Ana", (string)socio["nombre"]!);
        Assert.Equal("+598 91234567", (string)socio["telefono"]!);
    }

    [Fact]
    public async Task Un_socio_sin_telefono_se_guarda()
    {
        var jefe = await api.Jefe();
        var alta = await jefe.PostAsJsonAsync("/api/socios", Socio("Ana", "Sin " + Nombres.Aleatorio(8), ""));
        Assert.True(alta.IsSuccessStatusCode, await alta.Detalle());
    }

    public static IEnumerable<object?[]> Socios_invalidos()
    {
        yield return new object?[] { "nombre con número", "Ana2", "Gómez", "+54 1155551234", "El nombre solo puede tener letras" };
        yield return new object?[] { "apellido con número", "Ana", "Gómez 3", "+54 1155551234", "El apellido solo puede tener letras" };
        yield return new object?[] { "nombre con símbolos", "Ana <b>", "Gómez", "+54 1155551234", "El nombre solo puede tener letras" };
        yield return new object?[] { "apellido de empresa", "Ana", "S.A. 2000", "+54 1155551234", "El apellido solo puede tener letras" };
        yield return new object?[] { "teléfono sin código de país", "Ana", "Gómez", "11 5555-1234", "El teléfono tiene que llevar el código del país" };
        yield return new object?[] { "teléfono con guion", "Ana", "Gómez", "+54 11 5555-1234", "El teléfono tiene que llevar el código del país" };
        yield return new object?[] { "teléfono con letras", "Ana", "Gómez", "+54 11abc5678", "El teléfono tiene que llevar el código del país" };
        yield return new object?[] { "teléfono con fórmula", "Ana", "Gómez", "=HYPERLINK(\"http://x\")", "El teléfono tiene que llevar el código del país" };
    }

    [Theory]
    [MemberData(nameof(Socios_invalidos))]
    public async Task Un_socio_invalido_da_400_con_el_motivo_y_no_cambia_nada(string caso, string nombre, string apellido, string telefono, string motivo)
    {
        var jefe = await api.Jefe();
        var apellidoValido = "Valido " + Nombres.Aleatorio(8);
        var alta = await jefe.PostAsJsonAsync("/api/socios", Socio("Ana", apellidoValido, "+54 1155551234"));
        var id = (long)(await alta.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        var cantidad = (await jefe.GetFromJsonAsync<JsonArray>("/api/socios"))!.Count;

        var nuevo = await jefe.PostAsJsonAsync("/api/socios", Socio(nombre, apellido, telefono));
        var edicion = await jefe.PutAsJsonAsync($"/api/socios/{id}", Socio(nombre, apellido, telefono));

        Assert.True(nuevo.StatusCode == HttpStatusCode.BadRequest, $"{caso} (alta): se esperaba 400 y fue {(int)nuevo.StatusCode}. {await nuevo.Detalle()}");
        Assert.Contains(motivo, await nuevo.Detalle());
        Assert.True(edicion.StatusCode == HttpStatusCode.BadRequest, $"{caso} (cambio): se esperaba 400 y fue {(int)edicion.StatusCode}. {await edicion.Detalle()}");
        Assert.Contains(motivo, await edicion.Detalle());

        var despues = (await jefe.GetFromJsonAsync<JsonArray>("/api/socios"))!;
        Assert.Equal(cantidad, despues.Count);
        var socio = despues.Select(s => s!.AsObject()).Single(s => (long)s["id"]! == id);
        Assert.Equal(("Ana", apellidoValido, "+54 1155551234"), ((string)socio["nombre"]!, (string)socio["apellido"]!, (string)socio["telefono"]!));
    }

    // ---------- usuarios ----------

    [Fact]
    public async Task Un_usuario_con_nombre_de_letras_y_telefono_con_codigo_se_guarda_y_se_edita()
    {
        var jefe = await api.Jefe();
        var email = $"{Nombres.Aleatorio(12)}@gym.com";
        var alta = await jefe.PostAsJsonAsync("/api/usuarios", Usuario("Lic. Ana D'Angelo", "+34 612345678", email));
        Assert.True(alta.IsSuccessStatusCode, await alta.Detalle());
        var id = (long)(await alta.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;

        var cambio = await jefe.PutAsJsonAsync($"/api/usuarios/{id}", Usuario("Ana María", "+1 2125551234", email));
        Assert.True(cambio.IsSuccessStatusCode, await cambio.Detalle());

        var usuarios = (await jefe.GetFromJsonAsync<JsonArray>("/api/usuarios"))!.Select(u => u!.AsObject());
        var usuario = usuarios.Single(u => (long)u["id"]! == id);
        Assert.Equal(("Ana María", "+1 2125551234"), ((string)usuario["nombre"]!, (string)usuario["telefono"]!));
    }

    public static IEnumerable<object?[]> Usuarios_invalidos()
    {
        yield return new object?[] { "nombre con número", "Jefe 2", "+54 1155551234", "El nombre solo puede tener letras" };
        yield return new object?[] { "nombre con arroba", "jefe@gym.com", "+54 1155551234", "El nombre solo puede tener letras" };
        yield return new object?[] { "teléfono sin código de país", "Persona", "11 5555-1234", "El teléfono tiene que llevar el código del país" };
        yield return new object?[] { "teléfono con letras", "Persona", "abc", "El teléfono tiene que llevar el código del país" };
    }

    [Theory]
    [MemberData(nameof(Usuarios_invalidos))]
    public async Task Un_usuario_invalido_da_400_con_el_motivo_y_no_cambia_nada(string caso, string nombre, string telefono, string motivo)
    {
        var jefe = await api.Jefe();
        var email = $"{Nombres.Aleatorio(12)}@gym.com";
        var alta = await jefe.PostAsJsonAsync("/api/usuarios", Usuario("Valido", "+54 1155551234", email));
        var id = (long)(await alta.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;

        var nuevo = await jefe.PostAsJsonAsync("/api/usuarios", Usuario(nombre, telefono));
        var edicion = await jefe.PutAsJsonAsync($"/api/usuarios/{id}", Usuario(nombre, telefono, email));

        Assert.True(nuevo.StatusCode == HttpStatusCode.BadRequest, $"{caso} (alta): se esperaba 400 y fue {(int)nuevo.StatusCode}. {await nuevo.Detalle()}");
        Assert.Contains(motivo, await nuevo.Detalle());
        Assert.True(edicion.StatusCode == HttpStatusCode.BadRequest, $"{caso} (cambio): se esperaba 400 y fue {(int)edicion.StatusCode}. {await edicion.Detalle()}");
        Assert.Contains(motivo, await edicion.Detalle());

        var usuario = (await jefe.GetFromJsonAsync<JsonArray>("/api/usuarios"))!.Select(u => u!.AsObject()).Single(u => (long)u["id"]! == id);
        Assert.Equal(("Valido", "+54 1155551234"), ((string)usuario["nombre"]!, (string)usuario["telefono"]!));
    }

    [Fact]
    public async Task Los_usuarios_de_ejemplo_arrancan_con_nombre_y_telefono_validos()
    {
        var jefe = await api.Jefe();
        var usuarios = (await jefe.GetFromJsonAsync<JsonArray>("/api/usuarios"))!.Select(u => u!.AsObject()).ToList();

        var contador = usuarios.Single(u => (string)u["email"]! == "contador@gymstudio.com");
        Assert.Equal("Lic. Roberto Gómez", (string)contador["nombre"]!);
        Assert.Equal("+54 1155550202", (string)contador["telefono"]!);
        Assert.Equal("+54 1155550101", (string)usuarios.Single(u => (string)u["email"]! == "jefe@gym.com")["telefono"]!);
    }
}
