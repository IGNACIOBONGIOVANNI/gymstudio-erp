using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static EstudioContable.IntegrationTests.BasesDePrueba;

namespace EstudioContable.IntegrationTests;

/// <summary>
/// Una cuota cobrada de noche (después de las 21, cuando en UTC ya es el día siguiente) queda con la fecha de Argentina en la
/// factura, el asiento y la Caja, igual que una factura cargada a mano. Se fija el reloj del servidor para probarlo.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class FechaDelCobroDeCuotasTests(ApiFixture api)
{
    // 13/11/2026 a las 23:30 en Argentina = 14/11/2026 a las 02:30 UTC.
    private static readonly DateTimeOffset DeNoche = new(2026, 11, 13, 23, 30, 0, TimeSpan.FromHours(-3));

    private static async Task<long> Id(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
        return (long)(await r.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    private static async Task<long> SocioNuevo(HttpClient jefe)
        => await Id(await jefe.PostAsJsonAsync("/api/socios", new
        {
            nombre = "Nora", apellido = "Fecha " + Nombres.Aleatorio(10),
            dni = Random.Shared.Next(20_000_000, 49_999_999).ToString(), telefono = ""
        }));

    private static async Task<long> PlanNuevo(HttpClient jefe)
        => await Id(await jefe.PostAsJsonAsync("/api/planes", new { nombre = "Plan " + Nombres.Aleatorio(10), precioMensual = 1000, duracionMeses = 1 }));

    /// <summary>Fecha de la última factura del socio, de su asiento y de su movimiento de Caja, como texto aaaa-mm-dd.</summary>
    private async Task<string[]> FechasDelCobro(long socio) => (await Leer(api.ConnectionString, $"""
        SELECT to_char(f."Fecha" AT TIME ZONE 'UTC', 'YYYY-MM-DD'), a.fecha::text, c."Fecha"::text
        FROM contabilidad."Facturas" f
        JOIN contabilidad.asientos_contables a ON a."Id" = f."AsientoContableId"
        JOIN caja.movimientos_caja c ON c."FacturaId" = f."Id"
        WHERE f."SocioId" = {socio}
        ORDER BY f."Id" DESC LIMIT 1
        """)).Single().Split('|');

    [Fact]
    public async Task Una_cuota_cobrada_a_las_23_30_queda_con_la_fecha_de_ese_dia()
    {
        var jefe = await api.Jefe();
        var socio = await SocioNuevo(jefe);
        var plan = await PlanNuevo(jefe);
        api.Reloj.Fijar(DeNoche);
        try
        {
            await Id(await jefe.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", new { socioId = socio, planId = plan, metodoPago = "caja" }));
        }
        finally { api.Reloj.Liberar(); }

        Assert.Equal(new[] { "2026-11-13", "2026-11-13", "2026-11-13" }, await FechasDelCobro(socio));
    }

    [Fact]
    public async Task El_cobro_repetido_enseguida_se_sigue_rechazando_de_noche()
    {
        var jefe = await api.Jefe();
        var socio = await SocioNuevo(jefe);
        var plan = await PlanNuevo(jefe);
        api.Reloj.Fijar(DeNoche);
        try
        {
            await Id(await jefe.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", new { socioId = socio, planId = plan, metodoPago = "caja" }));

            api.Reloj.Fijar(DeNoche.AddSeconds(5));
            var repetido = await jefe.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", new { socioId = socio, planId = plan, metodoPago = "caja" });
            Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);
            Assert.Contains("hace instantes", await repetido.Content.ReadAsStringAsync());

            // Pasados los 10 segundos se puede cobrar otro período.
            api.Reloj.Fijar(DeNoche.AddSeconds(15));
            await Id(await jefe.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", new { socioId = socio, planId = plan, metodoPago = "caja" }));
        }
        finally { api.Reloj.Liberar(); }
    }

    [Fact]
    public async Task Cobrar_una_membresia_pendiente_de_noche_tambien_queda_con_la_fecha_de_ese_dia()
    {
        var jefe = await api.Jefe();
        var socio = await SocioNuevo(jefe);
        var plan = await PlanNuevo(jefe);
        var membresia = (await Leer(api.ConnectionString, $"""
            INSERT INTO contabilidad."Membresias" ("SocioId", "PlanId", "FechaInicio", "FechaVencimiento", "EstaPagada")
            VALUES ({socio}, {plan}, now(), now() + interval '1 month', false) RETURNING "Id"
            """)).Single();

        api.Reloj.Fijar(DeNoche);
        try
        {
            var cobro = await jefe.PostAsJsonAsync($"/api/membresias/{membresia}/cobrar", new { membresiaId = long.Parse(membresia), metodoPago = "caja" });
            Assert.True(cobro.IsSuccessStatusCode, await cobro.Content.ReadAsStringAsync());
        }
        finally { api.Reloj.Liberar(); }

        Assert.Equal(new[] { "2026-11-13", "2026-11-13", "2026-11-13" }, await FechasDelCobro(socio));
    }

    [Fact]
    public async Task Una_cuota_cobrada_de_dia_no_cambia()
    {
        var jefe = await api.Jefe();
        var socio = await SocioNuevo(jefe);
        var plan = await PlanNuevo(jefe);
        api.Reloj.Fijar(new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.FromHours(-3)));
        try
        {
            await Id(await jefe.PostAsJsonAsync("/api/membresias/asignar-y-cobrar", new { socioId = socio, planId = plan, metodoPago = "caja" }));
        }
        finally { api.Reloj.Liberar(); }

        Assert.Equal(new[] { "2026-11-13", "2026-11-13", "2026-11-13" }, await FechasDelCobro(socio));
    }
}
