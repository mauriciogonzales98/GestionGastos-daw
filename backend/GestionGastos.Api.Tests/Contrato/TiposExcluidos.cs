namespace GestionGastos.Api.Tests.Contrato;

/// <summary>
/// Los tipos de <c>frontend/src/api/tipos.ts</c> que la verificación del contrato no compara, cada
/// uno con el motivo por el que no lo hace.
/// </summary>
/// <remarks>
/// Lista única y con motivo obligatorio (NFR-05). Una exclusión sin motivo es indistinguible de un
/// olvido, y un tipo olvidado queda pareciendo verificado — que es el modo de falla que este ticket
/// combate. Por eso hay un test que falla si algún motivo está vacío y otro que falla si
/// <c>tipos.ts</c> gana un tipo que no está ni verificado ni acá.
/// </remarks>
public static class TiposExcluidos
{
    /// <summary>Nombre del tipo → motivo de la exclusión.</summary>
    public static IReadOnlyDictionary<string, string> PorNombre { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FiltrosDeMovimientos"] =
                "No espeja ningún tipo del backend: sus tres valores viajan en la query string de " +
                "GET /api/movimientos, no en un cuerpo, así que no hay JSON emitido contra el cual " +
                "compararlo. El propio tipos.ts lo declara como la excepción del archivo.",
        };
}
