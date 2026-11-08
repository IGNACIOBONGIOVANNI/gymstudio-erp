using System.Text;
using EstudioContable.Domain;
using EstudioContable.Domain.Entities;

namespace EstudioContable.Domain.Tests;

public class ReglasDeFotoTests
{
    [Fact]
    public void Un_jpeg_de_256_por_256_vale_y_tambien_uno_progresivo()
    {
        ReglasDeFoto.Validar(JpegDePrueba.Crear(256, 256));
        ReglasDeFoto.Validar(JpegDePrueba.Crear(256, 256, marcadorSof: 0xC2));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(150_000)]
    public void Los_limites_de_peso_valen(int bytes)
    {
        var foto = JpegDePrueba.Crear(256, 256, bytes);
        Assert.Equal(bytes, foto.Length);
        ReglasDeFoto.Validar(foto);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(150_001)]
    public void Muy_chico_o_muy_pesado_se_rechaza(int bytes)
    {
        var foto = JpegDePrueba.Crear(256, 256, bytes);
        Assert.Equal(bytes, foto.Length);
        Assert.Throws<ArgumentException>(() => ReglasDeFoto.Validar(foto));
    }

    [Theory]
    [InlineData(16, 16)]
    [InlineData(512, 512)]
    public void Los_limites_de_tamano_valen(int ancho, int alto) => ReglasDeFoto.Validar(JpegDePrueba.Crear(ancho, alto));

    [Theory]
    [InlineData(15, 256)]
    [InlineData(256, 15)]
    [InlineData(513, 256)]
    [InlineData(256, 513)]
    [InlineData(20_000, 20_000)]
    public void Un_tamano_fuera_de_rango_se_rechaza_aunque_pese_poco(int ancho, int alto)
        => Assert.Throws<ArgumentException>(() => ReglasDeFoto.Validar(JpegDePrueba.Crear(ancho, alto)));

    public static IEnumerable<object?[]> Archivos_que_no_son_una_foto()
    {
        yield return new object?[] { "nulo", null };
        yield return new object?[] { "vacío", Array.Empty<byte>() };
        yield return new object?[] { "PNG", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.Concat(new byte[300]).ToArray() };
        yield return new object?[] { "SVG", Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>" + new string(' ', 200)) };
        yield return new object?[] { "HTML", Encoding.UTF8.GetBytes("<html><body><script>alert(1)</script></body></html>" + new string(' ', 200)) };
        yield return new object?[] { "JPEG truncado, sin fin", JpegDePrueba.Crear(256, 256)[..^2] };
        yield return new object?[] { "solo la firma", new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 } };
        yield return new object?[] { "JPEG sin cabecera de tamaño", new byte[] { 0xFF, 0xD8 }.Concat(new byte[] { 0xFF, 0xFE, 0x00, 0x7A }).Concat(new byte[120]).Concat(new byte[] { 0xFF, 0xD9 }).ToArray() };
        yield return new object?[] { "DHT (C4) que no es una cabecera de tamaño", JpegDePrueba.Crear(256, 256, marcadorSof: 0xC4) };
        yield return new object?[] { "DAC (CC) que no es una cabecera de tamaño", JpegDePrueba.Crear(256, 256, marcadorSof: 0xCC) };
    }

    [Theory]
    [MemberData(nameof(Archivos_que_no_son_una_foto))]
    public void Lo_que_no_es_un_jpeg_valido_se_rechaza_con_un_mensaje(string caso, byte[]? bytes)
    {
        var error = Assert.Throws<ArgumentException>(() => ReglasDeFoto.Validar(bytes));
        Assert.False(string.IsNullOrWhiteSpace(error.Message), caso);
    }

    [Fact]
    public void El_tamano_se_lee_de_la_cabecera_y_sin_cabecera_es_null()
    {
        Assert.Equal((300, 200), ReglasDeFoto.TamanoDelJpeg(JpegDePrueba.Crear(300, 200)));
        Assert.Null(ReglasDeFoto.TamanoDelJpeg(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }));
        Assert.Null(ReglasDeFoto.TamanoDelJpeg(Encoding.UTF8.GetBytes("no es un jpeg")));
    }
}

public class FotoDeEntrenadorTests
{
    private static readonly DateTimeOffset Antes = new(2026, 11, 8, 10, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset Despues = Antes.AddHours(2);

    [Fact]
    public void Una_foto_valida_se_crea_con_su_fecha()
    {
        var bytes = JpegDePrueba.Crear(256, 256);
        var foto = new FotoDeEntrenador(7, bytes, Antes);
        Assert.Equal((7L, Antes), (foto.EntrenadorId, foto.Actualizada));
        Assert.Equal(bytes, foto.Foto);
    }

    [Fact]
    public void Reemplazar_cambia_los_bytes_y_la_fecha()
    {
        var foto = new FotoDeEntrenador(7, JpegDePrueba.Crear(256, 256, 400), Antes);
        var nueva = JpegDePrueba.Crear(300, 300, 500);

        foto.Reemplazar(nueva, Despues);

        Assert.Equal(nueva, foto.Foto);
        Assert.Equal(Despues, foto.Actualizada);
    }

    [Fact]
    public void Reemplazar_con_un_archivo_invalido_no_cambia_nada()
    {
        var original = JpegDePrueba.Crear(256, 256, 400);
        var foto = new FotoDeEntrenador(7, original, Antes);

        Assert.Throws<ArgumentException>(() => foto.Reemplazar(new byte[] { 1, 2, 3 }, Despues));

        Assert.Equal(original, foto.Foto);
        Assert.Equal(Antes, foto.Actualizada);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Una_foto_sin_entrenador_o_con_un_archivo_invalido_no_se_crea(long entrenadorId)
    {
        Assert.Throws<ArgumentException>(() => new FotoDeEntrenador(entrenadorId, JpegDePrueba.Crear(256, 256), Antes));
        Assert.Throws<ArgumentException>(() => new FotoDeEntrenador(7, new byte[] { 1, 2, 3 }, Antes));
    }
}
