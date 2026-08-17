namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Respuesta del listado. Envuelve los items en vez de devolver el arreglo pelado porque el recorte
/// del techo (mitigación R-07) tiene que viajar en la respuesta: un arreglo de 500 elementos no
/// puede decir si había 500 o 5000, y truncar en silencio es mentir por omisión.
/// </summary>
/// <param name="Items">
/// Los movimientos del propietario, ordenados por <c>fecha DESC, id DESC</c>, como máximo
/// <see cref="MovimientosEndpoints.TechoDeItems"/>.
/// </param>
/// <param name="Recortado">
/// <c>true</c> cuando el propietario tiene más movimientos de los que entran en el techo, es decir
/// cuando <paramref name="Total"/> supera <see cref="MovimientosEndpoints.TechoDeItems"/>.
/// </param>
/// <param name="Total">
/// Cuántos movimientos tiene el propietario en total, NO cuántos trae <paramref name="Items"/>.
/// Cuando hay recorte los dos números difieren a propósito: es lo que le permite al frontend avisar
/// "estás viendo 500 de 1200" en vez de dar por completo un listado que no lo está. Sin recorte,
/// <c>Total == Items.Count</c>.
/// </param>
public sealed record ListadoMovimientosResponse(
    IReadOnlyList<MovimientoDto> Items,
    bool Recortado,
    int Total);
