using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Enums;

namespace EstudioContable.Domain.Tests;

public class CuitTests
{
    [Theory]
    [InlineData("20-12345678-6")]
    [InlineData("20123456786")]
    [InlineData("30-11111111-8")]
    [InlineData("27-11111111-7")]
    public void Cuit_con_digito_verificador_correcto_es_valido(string cuit)
        => Assert.True(Cuit.EsValido(cuit));

    [Theory]
    [InlineData("20-12345678-9")]   // dígito verificador incorrecto
    [InlineData("2012345678")]      // 10 dígitos
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void Cuit_invalido_es_rechazado(string? cuit)
        => Assert.False(Cuit.EsValido(cuit));

    [Fact]
    public void Formatear_agrega_los_guiones()
        => Assert.Equal("20-12345678-6", Cuit.Formatear(Cuit.Normalizar("20.12345678.6")));
}

public class SocioTests
{
    private static Socio NuevoSocio(string dni = "30.111.222")
        => new("Ana", "Gómez", dni, telefono: null, fechaAlta: new DateTime(2026, 1, 1));

    [Fact]
    public void El_dni_se_guarda_sin_puntos()
        => Assert.Equal("30111222", NuevoSocio().Dni);

    [Theory]
    [InlineData("")]
    [InlineData("123")]          // muy corto
    [InlineData("123456789")]    // muy largo
    [InlineData("30A11222")]     // con letras
    public void Dni_invalido_es_rechazado(string dni)
        => Assert.Throws<ArgumentException>(() => NuevoSocio(dni));

    [Fact]
    public void Renovar_un_plan_vencido_cubre_desde_hoy_y_el_vencimiento_no_queda_antes_del_inicio()
    {
        // Regresión: antes se tomaba la hora actual dos veces y el vencimiento
        // podía quedar microsegundos antes del inicio de la membresía.
        var socio = NuevoSocio();

        var periodo = socio.RenovarPlan(planId: 1, duracionMeses: 1);

        Assert.Equal(periodo.Inicio.AddMonths(1), periodo.Vencimiento);
        Assert.Equal(periodo.Vencimiento, socio.FechaVencimiento);
        Assert.Equal("Activo", socio.Estado);
    }

    [Fact]
    public void Renovar_estando_al_dia_empieza_cuando_termina_el_periodo_actual()
    {
        var socio = NuevoSocio();
        var primero = socio.RenovarPlan(1, duracionMeses: 1);

        var segundo = socio.RenovarPlan(1, duracionMeses: 3);

        Assert.Equal(primero.Vencimiento, segundo.Inicio);
        Assert.Equal(primero.Vencimiento.AddMonths(3), segundo.Vencimiento);
    }

    [Fact]
    public void Plan_con_duracion_cero_se_toma_como_un_mes()
    {
        var periodo = NuevoSocio().RenovarPlan(1, duracionMeses: 0);
        Assert.Equal(periodo.Inicio.AddMonths(1), periodo.Vencimiento);
    }

    [Fact]
    public void Membresia_con_vencimiento_anterior_al_inicio_es_rechazada()
        => Assert.Throws<ArgumentException>(() =>
            new Membresia(1, 1, new DateTime(2026, 10, 2), new DateTime(2026, 10, 1)));
}

public class ProductoTests
{
    [Fact]
    public void El_stock_no_puede_quedar_negativo()
    {
        var producto = new Producto("Proteína", precioUnitario: 1000, precioVenta: 1500, stockInicial: 3);

        producto.ActualizarStock(-3);
        Assert.Equal(0, producto.StockActual);

        var error = Assert.Throws<InvalidOperationException>(() => producto.ActualizarStock(-1));
        Assert.Contains("Stock insuficiente", error.Message);
        Assert.Equal(0, producto.StockActual);
    }

    [Fact]
    public void Editar_no_acepta_precios_negativos()
    {
        var producto = new Producto("Proteína", 1000, 1500);
        Assert.Throws<ArgumentException>(() =>
            producto.ActualizarDatos("Proteína", -1, 1500, null, null, null, null, null, null));
    }
}

public class MovimientoTesoreriaTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void El_importe_debe_ser_positivo(decimal importe)
        => Assert.Throws<ArgumentException>(() =>
            new MovimientoCaja("1", new DateTime(2026, 10, 20), "Pago", TipoMovimientoCaja.Egreso, importe));

    [Fact]
    public void Tipo_de_movimiento_inexistente_es_rechazado()
        => Assert.Throws<ArgumentException>(() =>
            new MovimientoBanco("1", new DateTime(2026, 10, 20), "x", (TipoMovimientoBanco)7, 10));

    [Fact]
    public void Fecha_vacia_es_rechazada()
        => Assert.Throws<ArgumentException>(() =>
            new MovimientoCaja("1", default, "x", TipoMovimientoCaja.Ingreso, 10));

    [Fact]
    public void Actualizar_datos_cambia_el_sentido_del_movimiento()
    {
        var movimiento = new MovimientoCaja("1", new DateTime(2026, 10, 20), "Cobro", TipoMovimientoCaja.Ingreso, 100);

        movimiento.ActualizarDatos(new DateTime(2026, 10, 21), "Pago", esIngreso: false, importe: 80);

        Assert.False(movimiento.EsIngreso);
        Assert.Equal(80, movimiento.Importe);
    }
}

public class UsuarioTests
{
    [Fact]
    public void El_email_se_normaliza()
    {
        var usuario = new Usuario("Ana", "  Ana@GYM.com ", "hash", RolUsuario.Recepcion);
        Assert.Equal("ana@gym.com", usuario.Email);
    }

    [Fact]
    public void El_administrador_principal_no_se_desactiva_ni_cambia_de_rol()
    {
        var jefe = new Usuario("Jefe", "jefe@gym.com", "hash", RolUsuario.Administrador, esPrincipal: true);

        Assert.Throws<InvalidOperationException>(() => jefe.Desactivar());
        Assert.Throws<InvalidOperationException>(() => jefe.ActualizarDatos("Jefe", "jefe@gym.com", RolUsuario.Recepcion, null));
        Assert.True(jefe.Activo);
    }
}
