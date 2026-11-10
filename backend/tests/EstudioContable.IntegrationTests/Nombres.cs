namespace EstudioContable.IntegrationTests;

/// <summary>
/// Nombres que no se repiten entre pruebas (la base es la misma para todas) y que sirven para una persona: solo letras,
/// porque el nombre de un socio, un usuario o un entrenador no puede llevar números.
/// </summary>
internal static class Nombres
{
    /// <summary>Un grupo de letras minúsculas al azar, por ejemplo «qwhrtzaa».</summary>
    public static string Aleatorio(int largo)
        => string.Create(largo, 0, static (letras, _) =>
        {
            for (var i = 0; i < letras.Length; i++) letras[i] = (char)('a' + Random.Shared.Next(26));
        });
}
