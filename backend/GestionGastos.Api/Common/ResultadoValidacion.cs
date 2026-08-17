namespace GestionGastos.Api.Common;

/// <summary>
/// Un rechazo de validación, atado al campo que lo provoca. Es un dato, no una excepción: el camino
/// esperado de una entrada inválida es responder 400, y usar excepciones para eso convierte lo
/// normal en excepcional.
/// </summary>
public readonly record struct ErrorDeValidacion(string Campo, string Mensaje);

/// <summary>
/// Resultado de validar una entrada. Acumula todos los errores en vez de cortar en el primero: quien
/// carga el formulario merece ver de una todo lo que tiene que corregir.
/// </summary>
public sealed class ResultadoValidacion
{
    private readonly List<ErrorDeValidacion> _errores = [];

    public bool EsValido => _errores.Count == 0;

    public IReadOnlyList<ErrorDeValidacion> Errores => _errores;

    public ResultadoValidacion Agregar(string campo, string mensaje)
    {
        _errores.Add(new ErrorDeValidacion(campo, mensaje));
        return this;
    }

    /// <summary>
    /// La forma que espera la extensión <c>errors</c> del <c>ProblemDetails</c> (RFC 9457): un
    /// diccionario de campo a mensajes.
    /// </summary>
    public Dictionary<string, string[]> ComoDiccionario() =>
        _errores
            .GroupBy(e => e.Campo, StringComparer.Ordinal)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.Select(e => e.Mensaje).ToArray(),
                StringComparer.Ordinal);
}
