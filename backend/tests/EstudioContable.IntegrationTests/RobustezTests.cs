using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Datos hostiles o absurdos: la API debe contestar 400 con un mensaje claro, nunca 500 (que además
/// es una puerta a que alguien tire abajo una pantalla con un solo pedido mal armado).
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class RobustezTests(ApiFixture api)
{
    private static object Renglon(long ejercicioId) => new { ejercicioId, dia = 1, series = 3, repeticiones = "10", descansoSegundos = 60, indicaciones = "" };

    private async Task<long> Ejercicio(HttpClient c)
    {
        var r = await c.PostAsJsonAsync("/api/ejercicios", new { nombre = $"Robustez {Guid.NewGuid():N}"[..24], grupoMuscular = "Piernas" });
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    [Fact]
    public async Task Un_caracter_nulo_en_un_texto_se_rechaza_en_cualquier_pantalla()
    {
        var jefe = await api.Jefe();
        var nulo = "con\u0000nulo";

        foreach (var (ruta, cuerpo) in new (string, object)[]
        {
            ("/api/ejercicios", new { nombre = nulo, grupoMuscular = "Pecho" }),
            ("/api/clientes", new { razonSocial = nulo, direccion = "Calle 1", localidad = "Haedo", cuit = "20123456786", condicionIva = "Responsable Inscripto" }),
            ("/api/socios", new { nombre = nulo, apellido = "Perez", dni = "30111222", telefono = "" }),
            ("/api/productos", new { nombre = nulo, precioUnitario = 10, precioVenta = 20, stockInicial = 1 }),
        })
        {
            var r = await jefe.PostAsJsonAsync(ruta, cuerpo);
            Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{ruta} respondió {(int)r.StatusCode}: {await r.Detalle()}");
        }
    }

    [Fact]
    public async Task Una_rutina_con_un_renglon_vacio_o_con_demasiados_renglones_se_rechaza()
    {
        var jefe = await api.Jefe();
        var ej = await Ejercicio(jefe);

        var conNulo = await jefe.PostAsJsonAsync("/api/rutinas", new { nombre = $"Nulo {Guid.NewGuid():N}"[..20], nivel = "Principiante", ejercicios = new object?[] { Renglon(ej), null } });
        Assert.Equal(HttpStatusCode.BadRequest, conNulo.StatusCode);

        var demasiados = await jefe.PostAsJsonAsync("/api/rutinas", new { nombre = $"Enorme {Guid.NewGuid():N}"[..20], nivel = "Principiante", ejercicios = Enumerable.Repeat(Renglon(ej), 151).ToArray() });
        Assert.Equal(HttpStatusCode.BadRequest, demasiados.StatusCode);
        Assert.Contains("150", await demasiados.Detalle());

        var justo = await jefe.PostAsJsonAsync("/api/rutinas", new { nombre = $"Justa {Guid.NewGuid():N}"[..20], nivel = "Principiante", ejercicios = Enumerable.Repeat(Renglon(ej), 150).ToArray() });
        Assert.True(justo.IsSuccessStatusCode, await justo.Detalle());
    }

    [Theory]
    [InlineData("/api/dashboard?hoy=9999-12-31")]
    [InlineData("/api/dashboard?hoy=0001-01-01")]
    [InlineData("/api/dashboard?mes=9999-12")]
    [InlineData("/api/cuentas-corrientes/clientes?hoy=9999-12-31")]
    [InlineData("/api/cuentas-corrientes/proveedores?hoy=0001-01-01")]
    public async Task Las_fechas_absurdas_de_los_resumenes_se_rechazan(string ruta)
    {
        var jefe = await api.Jefe();
        Assert.Equal(HttpStatusCode.BadRequest, (await jefe.GetAsync(ruta)).StatusCode);
    }
}
