using EstudioContable.Domain.Entities;

namespace EstudioContable.Domain.Tests;

public class MedicionSocioTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 27);

    private static MedicionSocio Nueva(decimal peso = 80m, DateOnly? fecha = null, decimal? grasa = null, decimal? cintura = null, decimal? brazo = null)
        => new(1, fecha ?? Hoy, peso, grasa, cintura, null, null, brazo, null, null, Hoy);

    [Fact]
    public void Solo_el_peso_es_obligatorio_y_se_redondea_a_un_decimal()
    {
        var m = Nueva(peso: 80.456m);
        Assert.Equal(80.5m, m.PesoKg);
        Assert.Null(m.GrasaCorporalPct);
        Assert.Null(m.CinturaCm);
    }

    [Theory]
    [InlineData(19.9)]
    [InlineData(400.1)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Un_peso_fuera_de_rango_es_rechazado(double peso)
        => Assert.Throws<ArgumentException>(() => Nueva(peso: (decimal)peso));

    [Fact]
    public void Los_porcentajes_y_perimetros_opcionales_tienen_su_rango()
    {
        Assert.Throws<ArgumentException>(() => Nueva(grasa: 1.9m));
        Assert.Throws<ArgumentException>(() => Nueva(grasa: 70.1m));
        Assert.Throws<ArgumentException>(() => Nueva(cintura: 29m));
        Assert.Throws<ArgumentException>(() => Nueva(brazo: 101m));
        Assert.Equal(70m, Nueva(grasa: 70m).GrasaCorporalPct);          // los extremos valen
        Assert.Equal(100m, Nueva(brazo: 100m).BrazoCm);
    }

    [Fact]
    public void La_fecha_no_puede_ser_futura_pero_se_tolera_un_dia_por_la_diferencia_horaria()
    {
        Assert.Throws<ArgumentException>(() => Nueva(fecha: Hoy.AddDays(2)));
        Assert.Equal(Hoy.AddDays(1), Nueva(fecha: Hoy.AddDays(1)).Fecha);
        Assert.Throws<ArgumentException>(() => Nueva(fecha: new DateOnly(1999, 1, 1)));
    }

    [Fact]
    public void Las_notas_largas_son_rechazadas()
        => Assert.Throws<ArgumentException>(() => new MedicionSocio(1, Hoy, 80, null, null, null, null, null, null, new string('x', 301), Hoy));

    [Fact]
    public void Actualizar_corrige_los_valores()
    {
        var m = Nueva(peso: 80m, cintura: 90m);
        m.Actualizar(Hoy, 78.2m, 20m, null, null, null, null, null, "  en ayunas ", Hoy);

        Assert.Equal(78.2m, m.PesoKg);
        Assert.Equal(20m, m.GrasaCorporalPct);
        Assert.Null(m.CinturaCm);          // lo que se deja vacío se borra
        Assert.Equal("en ayunas", m.Notas);
    }
}
