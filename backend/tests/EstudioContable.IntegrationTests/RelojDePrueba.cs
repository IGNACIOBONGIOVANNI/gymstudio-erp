namespace EstudioContable.IntegrationTests;

/// <summary>
/// El reloj del servidor en las pruebas: el real mientras nadie lo fije, y el instante que la prueba elija cuando lo fija. Así se
/// prueba «a las 22:00 toca» sin esperar a las 22:00.
/// </summary>
public sealed class RelojDePrueba : TimeProvider
{
    private DateTimeOffset? _fijo;

    public void Fijar(DateTimeOffset ahora) => _fijo = ahora;

    /// <summary>Vuelve al reloj real.</summary>
    public void Liberar() => _fijo = null;

    public override DateTimeOffset GetUtcNow() => (_fijo ?? base.GetUtcNow()).ToUniversalTime();
}
