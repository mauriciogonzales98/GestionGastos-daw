namespace GestionGastos.Api.Tests.Contrato;

/// <summary>
/// Un tipo del contrato tal como lo declara el frontend en <c>frontend/src/api/tipos.ts</c>.
/// </summary>
/// <remarks>
/// Este modelo es la razón de ser del ticket: el archivo del frontend deja de ser documentación y
/// pasa a ser la especificación ejecutable contra la que se compara lo que el backend emite. Antes
/// había dos definiciones del contrato —los <c>record</c> de C# y estas interfaces— y nada las
/// comparaba.
/// </remarks>
public sealed record TipoDelFrontend(string Nombre, IReadOnlyList<CampoDelFrontend> Campos);

/// <summary>Un campo declarado dentro de un tipo del contrato.</summary>
/// <param name="Nombre">El nombre tal como viaja en el JSON.</param>
/// <param name="Tipo">
/// El tipo declarado, ya sin el <c>| null</c>: <c>number</c>, <c>string</c>, <c>boolean</c>, el
/// nombre de otra interfaz del archivo, el de un alias de unión de literales, o cualquiera de esos
/// con sufijo <c>[]</c>.
/// </param>
/// <param name="AdmiteNull">Si la declaración incluye <c>| null</c>.</param>
/// <param name="EsOpcional">Si la declaración usa <c>nombre?:</c>.</param>
public sealed record CampoDelFrontend(
    string Nombre,
    string Tipo,
    bool AdmiteNull,
    bool EsOpcional)
{
    /// <summary>Si el tipo declarado es un arreglo.</summary>
    public bool EsArreglo => Tipo.EndsWith("[]", StringComparison.Ordinal);

    /// <summary>
    /// El tipo sin el sufijo <c>[]</c>. Para un campo que no es arreglo coincide con
    /// <see cref="Tipo"/>, de modo que quien recorre la forma no necesita preguntar primero.
    /// </summary>
    public string TipoBase => EsArreglo ? Tipo[..^2] : Tipo;
}

/// <summary>
/// Lo que el frontend declara del contrato: sus interfaces y los alias de unión de literales que
/// esas interfaces usan.
/// </summary>
/// <remarks>
/// Los alias viajan aparte y no degradados a <c>string</c> porque la comparación necesita sus
/// valores: <c>TipoMovimiento</c> no es "una cadena cualquiera", es exactamente
/// <c>"gasto"</c> o <c>"ingreso"</c>, y un backend que emitiera <c>"Gasto"</c> estaría rompiendo el
/// contrato aunque el tipo JSON siguiera siendo <c>String</c>.
/// </remarks>
public sealed record ContratoDelFrontend(
    IReadOnlyList<TipoDelFrontend> Tipos,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AliasesDeLiterales);
