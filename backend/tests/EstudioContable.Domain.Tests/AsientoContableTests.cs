using EstudioContable.Domain.Entities;
using EstudioContable.Domain.Exceptions;

namespace EstudioContable.Domain.Tests;

/// <summary>
/// Reglas de la partida doble y del ciclo de vida de un asiento.
/// </summary>
public class AsientoContableTests
{
    private const long Caja = 101, Ventas = 104, IvaDebito = 201;

    private static AsientoContable NuevoAsiento(string descripcion = "Venta mostrador")
        => new(clienteId: 1, numeroAsiento: 1, new DateOnly(2026, 10, 20), descripcion, usuarioCreadorId: 1);

    [Fact]
    public void Confirmar_asiento_balanceado_lo_deja_confirmado_con_auditoria()
    {
        var asiento = NuevoAsiento();
        asiento.AgregarLinea(Caja, 121, 0);
        asiento.AgregarLinea(Ventas, 0, 100);
        asiento.AgregarLinea(IvaDebito, 0, 21);

        asiento.Confirmar(usuarioConfirmacionId: 7);

        Assert.Equal(EstadoAsiento.Confirmado, asiento.Estado);
        Assert.Equal(7, asiento.UsuarioConfirmacionId);
        Assert.NotNull(asiento.FechaConfirmacion);
        Assert.Equal(121, asiento.TotalDebe);
        Assert.Equal(121, asiento.TotalHaber);
    }

    [Fact]
    public void Confirmar_asiento_desbalanceado_falla_y_queda_en_borrador()
    {
        var asiento = NuevoAsiento();
        asiento.AgregarLinea(Caja, 100, 0);
        asiento.AgregarLinea(Ventas, 0, 90);

        var error = Assert.Throws<AsientoDesbalanceadoException>(() => asiento.Confirmar(1));

        Assert.Equal(10, error.Diferencia);
        Assert.Equal(EstadoAsiento.Borrador, asiento.Estado);
        Assert.Null(asiento.FechaConfirmacion);
    }

    [Fact]
    public void Confirmar_requiere_al_menos_dos_lineas()
    {
        var asiento = NuevoAsiento();
        asiento.AgregarLinea(Caja, 100, 0);

        Assert.Throws<InvalidOperationException>(() => asiento.Confirmar(1));
    }

    [Theory]
    [InlineData(10, 10)]   // en Debe y en Haber a la vez
    [InlineData(0, 0)]     // sin importe
    [InlineData(-5, 0)]    // negativo
    public void Linea_invalida_es_rechazada(decimal debe, decimal haber)
    {
        var asiento = NuevoAsiento();

        Assert.Throws<ArgumentException>(() => asiento.AgregarLinea(Caja, debe, haber));
    }

    [Fact]
    public void Asiento_confirmado_es_inmutable()
    {
        var asiento = NuevoAsiento();
        asiento.AgregarLinea(Caja, 50, 0);
        asiento.AgregarLinea(Ventas, 0, 50);
        asiento.Confirmar(1);

        Assert.Throws<InvalidOperationException>(() => asiento.AgregarLinea(Caja, 1, 0));
        Assert.Throws<InvalidOperationException>(() => asiento.Confirmar(1));
    }

    [Fact]
    public void Contraasiento_invierte_debe_y_haber_y_apunta_al_original()
    {
        var original = NuevoAsiento("Factura de Venta N° 3");
        original.AgregarLinea(Caja, 121, 0, "Total");
        original.AgregarLinea(Ventas, 0, 100, "Ventas");
        original.AgregarLinea(IvaDebito, 0, 21, "IVA");
        original.Confirmar(1);

        var reverso = original.CrearContraasiento(numeroAsiento: 2, new DateOnly(2026, 10, 26), usuarioCreadorId: 1);
        reverso.Confirmar(1);

        Assert.Equal(original.Id, reverso.AsientoRevertidoId);
        Assert.StartsWith("Reverso del asiento N° 1", reverso.Descripcion);
        // Cada cuenta queda con saldo cero sumando original + reverso.
        foreach (var cuenta in new[] { Caja, Ventas, IvaDebito })
        {
            var saldo = original.Lineas.Concat(reverso.Lineas)
                .Where(l => l.CuentaId == cuenta)
                .Sum(l => l.Debe - l.Haber);
            Assert.Equal(0, saldo);
        }
    }

    [Fact]
    public void No_se_puede_revertir_un_borrador_ni_un_contraasiento()
    {
        var borrador = NuevoAsiento();
        borrador.AgregarLinea(Caja, 10, 0);
        borrador.AgregarLinea(Ventas, 0, 10);
        Assert.Throws<InvalidOperationException>(() => borrador.CrearContraasiento(2, new DateOnly(2026, 10, 26), 1));

        borrador.Confirmar(1);
        var reverso = borrador.CrearContraasiento(2, new DateOnly(2026, 10, 26), 1);
        reverso.Confirmar(1);
        Assert.Throws<InvalidOperationException>(() => reverso.CrearContraasiento(3, new DateOnly(2026, 10, 26), 1));
    }

    [Fact]
    public void Descripcion_obligatoria()
    {
        Assert.Throws<ArgumentException>(() => NuevoAsiento("   "));
    }
}
