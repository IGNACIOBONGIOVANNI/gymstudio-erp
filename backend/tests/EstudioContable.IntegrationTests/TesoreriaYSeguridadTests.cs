using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace EstudioContable.IntegrationTests;

/// <summary>Movimientos manuales de Caja y Banco, transferencias y saldo de caja.</summary>
[Collection(ColeccionApi.Nombre)]
public class TesoreriaTests(ApiFixture api)
{
    private static object Movimiento(int tipo, decimal importe, long? contrapartida = null) => new
    {
        clienteId = "1", fecha = "2026-10-20", detalle = "Movimiento de prueba",
        tipoMovimiento = tipo, importe, cuentaContrapartidaId = contrapartida
    };

    private async Task AsegurarEfectivoEnCaja(HttpClient cliente, decimal monto)
    {
        var r = await cliente.PostAsJsonAsync("/api/caja", Movimiento(tipo: 1, monto));
        Assert.True(r.IsSuccessStatusCode, await r.Detalle());
    }

    [Fact]
    public async Task Un_movimiento_manual_genera_su_asiento_y_eliminarlo_lo_revierte()
    {
        var jefe = await api.Jefe();
        await AsegurarEfectivoEnCaja(jefe, 1000);
        var gastos0 = await jefe.SaldoMayor("Gastos Generales");

        var creado = await jefe.PostAsJsonAsync("/api/caja", Movimiento(tipo: 2, importe: 300));
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var id = (long)(await creado.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;

        Assert.Equal(gastos0 + 300, await jefe.SaldoMayor("Gastos Generales"));
        Assert.Equal(await jefe.SaldoLibro("caja"), await jefe.SaldoMayor("Caja"));

        var eliminado = await jefe.DeleteAsync($"/api/caja/{id}");
        Assert.True(eliminado.IsSuccessStatusCode, await eliminado.Detalle());

        Assert.Equal(gastos0, await jefe.SaldoMayor("Gastos Generales"));
        Assert.Equal(await jefe.SaldoLibro("caja"), await jefe.SaldoMayor("Caja"));
    }

    [Fact]
    public async Task La_caja_puede_quedar_en_negativo_y_sigue_coincidiendo_con_el_mayor()
    {
        // Caso del ejercicio: se cargan las compras de contado antes que las ventas.
        var jefe = await api.Jefe();
        var saldo = await jefe.SaldoLibro("caja");

        var egreso = await jefe.PostAsJsonAsync("/api/caja", Movimiento(tipo: 2, importe: Math.Max(saldo, 0) + 1000));

        Assert.True(egreso.IsSuccessStatusCode, await egreso.Detalle());
        Assert.True(await jefe.SaldoLibro("caja") < 0);
        Assert.Equal(await jefe.SaldoLibro("caja"), await jefe.SaldoMayor("Caja"));

        await AsegurarEfectivoEnCaja(jefe, Math.Max(saldo, 0) + 1000);   // deja la caja como estaba
        Assert.Equal(saldo, await jefe.SaldoLibro("caja"));
    }

    [Fact]
    public async Task Una_transferencia_mueve_los_dos_libros_y_eliminarla_borra_ambos_movimientos()
    {
        var jefe = await api.Jefe();
        await AsegurarEfectivoEnCaja(jefe, 500);
        var caja0 = await jefe.SaldoLibro("caja");
        var banco0 = await jefe.SaldoLibro("banco");

        var r = await jefe.PostAsJsonAsync("/api/tesoreria/transferencias", new { origen = "Caja", fecha = "2026-10-20", importe = 500 });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);

        Assert.Equal(caja0 - 500, await jefe.SaldoLibro("caja"));
        Assert.Equal(banco0 + 500, await jefe.SaldoLibro("banco"));
        Assert.Equal(await jefe.SaldoLibro("caja"), await jefe.SaldoMayor("Caja"));
        Assert.Equal(await jefe.SaldoLibro("banco"), await jefe.SaldoMayor("Banco"));

        var libroCaja = await jefe.GetJson("/api/caja?clienteId=1");
        var transferencia = libroCaja["listaMovimientos"]!.AsArray().Last(m => (bool)m!["esTransferencia"]!)!;

        var edicion = await jefe.PutAsJsonAsync($"/api/caja/{(long)transferencia["id"]!}",
            new { id = (long)transferencia["id"]!, fecha = "2026-10-20", detalle = "x", ingreso = (decimal?)null, egreso = 1m });
        Assert.Equal(HttpStatusCode.BadRequest, edicion.StatusCode);

        var eliminada = await jefe.DeleteAsync($"/api/caja/{(long)transferencia["id"]!}");
        Assert.True(eliminada.IsSuccessStatusCode, await eliminada.Detalle());
        Assert.Equal(caja0, await jefe.SaldoLibro("caja"));
        Assert.Equal(banco0, await jefe.SaldoLibro("banco"));
    }

    [Fact]
    public async Task Caja_y_Banco_no_se_usan_en_asientos_manuales()
    {
        var contador = await api.Contador();

        var r = await contador.PostAsJsonAsync("/api/asientos", new
        {
            clienteId = 1, fecha = "2026-10-20", descripcion = "Depósito",
            lineas = new[] { new { cuentaId = 102, debe = 100m, haber = 0m }, new { cuentaId = 101, debe = 0m, haber = 100m } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}

/// <summary>Autenticación y permisos por rol.</summary>
[Collection(ColeccionApi.Nombre)]
public class SeguridadTests(ApiFixture api)
{
    [Fact]
    public async Task Sin_token_la_api_responde_401()
    {
        var r = await api.ClienteAnonimo().GetAsync("/api/facturas");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Email_inexistente_y_contrasena_incorrecta_dan_el_mismo_mensaje()
    {
        var cliente = api.ClienteAnonimo();
        var malaClave = await cliente.PostAsJsonAsync("/api/auth/login", new { email = "jefe@gym.com", password = "otra" });
        var sinUsuario = await cliente.PostAsJsonAsync("/api/auth/login", new { email = "nadie@gym.com", password = "otra" });

        Assert.Equal(HttpStatusCode.Unauthorized, malaClave.StatusCode);
        Assert.Equal(await malaClave.Detalle(), await sinUsuario.Detalle());
    }

    [Fact]
    public async Task Recepcion_puede_facturar_pero_no_anular_ni_administrar_usuarios()
    {
        var recepcion = await api.Recepcion();
        var factura = await recepcion.PostAsJsonAsync("/api/facturas", new
        {
            fecha = "2026-10-20T12:00:00Z", tipoOperacion = "Venta", condicionPago = "ContadoCaja",
            tipoComprobante = "C", cliente = "Consumidor Final", subtotal = 1000m, iva = 0m, total = 1000m,
            items = new[] { new { productoId = 0, descripcion = "Clase suelta", cantidad = 1, precioUnitario = 1000m, subtotal = 1000m } }
        });
        Assert.Equal(HttpStatusCode.Created, factura.StatusCode);
        var id = (long)(await factura.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;

        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.PostAsync($"/api/facturas/{id}/anular", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.GetAsync("/api/usuarios")).StatusCode);

        var contador = await api.Contador();
        Assert.Equal(HttpStatusCode.NoContent, (await contador.PostAsync($"/api/facturas/{id}/anular", null)).StatusCode);
    }

    [Fact]
    public async Task Un_usuario_desactivado_no_puede_iniciar_sesion()
    {
        var jefe = await api.Jefe();
        var email = $"temporal.{Guid.NewGuid():N}"[..20] + "@gym.com";
        var alta = await jefe.PostAsJsonAsync("/api/usuarios", new { nombre = "Temporal", email, rol = "Recepcion", telefono = (string?)null, activo = true, password = ApiFixture.ClaveNueva });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var id = (long)(await alta.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;

        await jefe.PutAsJsonAsync($"/api/usuarios/{id}/estado", new { activo = false });

        var login = await api.ClienteAnonimo().PostAsJsonAsync("/api/auth/login", new { email, password = ApiFixture.ClaveNueva });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Contains("inactiva", await login.Detalle());
    }

    [Fact]
    public async Task Recepcion_no_puede_mover_dinero_ni_borrar_catalogos()
    {
        // Antes la API sólo ocultaba estas acciones en pantalla: Recepción podía llamarlas igual.
        var recepcion = await api.Recepcion();
        var vacio = new { };
        foreach (var ruta in new[] { "/api/caja", "/api/banco", "/api/tesoreria/transferencias" })
            Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.PostAsJsonAsync(ruta, vacio)).StatusCode);
        foreach (var ruta in new[] { "/api/planes/1", "/api/productos/1", "/api/clientes/1", "/api/proveedores/1", "/api/caja/1", "/api/banco/1" })
            Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.DeleteAsync(ruta)).StatusCode);
    }

    [Fact]
    public async Task El_token_deja_de_valer_cuando_el_usuario_se_da_de_baja()
    {
        var jefe = await api.Jefe();
        var email = $"baja.{Guid.NewGuid():N}"[..18] + "@gym.com";
        var alta = await jefe.PostAsJsonAsync("/api/usuarios", new { nombre = "Con token", email, rol = "Recepcion", telefono = (string?)null, activo = true, password = ApiFixture.ClaveNueva });
        var id = (long)(await alta.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        var sesion = await api.ClienteComo(email, ApiFixture.ClaveNueva);
        Assert.Equal(HttpStatusCode.OK, (await sesion.GetAsync("/api/facturas")).StatusCode);

        await jefe.PutAsJsonAsync($"/api/usuarios/{id}/estado", new { activo = false });

        Assert.Equal(HttpStatusCode.Unauthorized, (await sesion.GetAsync("/api/facturas")).StatusCode);
    }

    [Theory]
    [InlineData("abc123")]        // corta
    [InlineData("12345678")]      // sólo números
    [InlineData("abcdefgh")]      // sólo letras
    public async Task Las_contrasenas_debiles_se_rechazan(string clave)
    {
        var jefe = await api.Jefe();
        var email = $"debil.{Guid.NewGuid():N}"[..18] + "@gym.com";
        var respuesta = await jefe.PostAsJsonAsync("/api/usuarios", new { nombre = "Débil", email, rol = "Recepcion", telefono = (string?)null, activo = true, password = clave });
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Una_cuenta_se_bloquea_tras_cinco_claves_incorrectas()
    {
        var jefe = await api.Jefe();
        var email = $"bloq.{Guid.NewGuid():N}"[..18] + "@gym.com";
        await jefe.PostAsJsonAsync("/api/usuarios", new { nombre = "Bloqueable", email, rol = "Recepcion", telefono = (string?)null, activo = true, password = ApiFixture.ClaveNueva });
        var anonimo = api.ClienteAnonimo();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/auth/login", new { email, password = "incorrecta" + i })).StatusCode);

        // Bloqueada: ni siquiera la clave correcta entra.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await anonimo.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFixture.ClaveNueva })).StatusCode);
    }

    [Fact]
    public async Task Las_respuestas_de_la_api_llevan_cabeceras_de_seguridad()
    {
        var jefe = await api.Jefe();
        var respuesta = await jefe.GetAsync("/api/planes");
        Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", respuesta.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("no-store", respuesta.Headers.GetValues("Cache-Control").Single());
    }
}
