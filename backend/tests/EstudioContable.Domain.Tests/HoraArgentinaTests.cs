namespace EstudioContable.Domain.Tests;

public class HoraArgentinaTests
{
    [Theory]
    [InlineData("2026-11-14T02:30:00Z", "2026-11-13T23:30:00")]   // de noche: en UTC ya es el día siguiente
    [InlineData("2026-11-13T13:00:00Z", "2026-11-13T10:00:00")]   // de día
    [InlineData("2026-11-14T03:00:00Z", "2026-11-14T00:00:00")]   // medianoche en Argentina
    public void El_comprobante_lleva_la_hora_de_Argentina_marcada_como_UTC(string utc, string esperado)
    {
        var fecha = HoraArgentina.ParaComprobante(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(DateTime.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), fecha);
        Assert.Equal(DateTimeKind.Utc, fecha.Kind);
    }
}
