namespace EstudioContable.IntegrationTests;

/// <summary>
/// Arma un JPEG de mentira (no se puede abrir como imagen) con la estructura que mira <c>ReglasDeFoto</c>: SOI, comentarios de relleno,
/// la cabecera de tamaño (SOF) y EOI. Solo para pruebas. Es copia de la de EstudioContable.Domain.Tests.
/// </summary>
public static class JpegDePrueba
{
    public static byte[] Crear(int ancho, int alto, int bytesTotales = 400, byte marcadorSof = 0xC0)
    {
        var sof = new byte[]
        {
            0xFF, marcadorSof, 0x00, 0x11, 0x08, (byte)(alto >> 8), (byte)alto, (byte)(ancho >> 8), (byte)ancho,
            0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
        };
        using var jpeg = new MemoryStream();
        jpeg.WriteByte(0xFF); jpeg.WriteByte(0xD8);

        // Relleno con segmentos de comentario (FF FE + largo + datos) hasta llegar al tamaño pedido.
        var restante = Math.Max(0, bytesTotales - (2 + sof.Length + 2));
        while (restante > 0)
        {
            var datos = Math.Max(0, Math.Min(restante - 4, 60_000));
            jpeg.WriteByte(0xFF); jpeg.WriteByte(0xFE);
            jpeg.WriteByte((byte)((datos + 2) >> 8)); jpeg.WriteByte((byte)(datos + 2));
            jpeg.Write(new byte[datos]);
            restante -= datos + 4;
        }

        jpeg.Write(sof);
        jpeg.WriteByte(0xFF); jpeg.WriteByte(0xD9);
        return jpeg.ToArray();
    }
}
