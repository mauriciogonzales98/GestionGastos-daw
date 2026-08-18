namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Los cuatro campos que el alta y la modificación validan igual. Existe para que
/// <see cref="ValidadorMovimiento.Validar"/> tenga UNA sola implementación de cada regla en vez de
/// una copia por endpoint: el PUT no puede ser una puerta de atrás a las validaciones del POST
/// (mitigación R-16).
/// </summary>
/// <remarks>
/// <c>TipoEsperado</c> queda deliberadamente afuera. En el alta es una entrada <b>no confiable</b>
/// del cliente, que solo sirve para detectar el cruce de tipo que de otro modo el servidor no vería;
/// en la modificación no hace falta ninguna entrada del cliente, porque el tipo del movimiento ya
/// está persistido y es confiable. Meter los dos casos bajo el mismo miembro mezclaría un dato
/// confiable con uno que no lo es.
/// </remarks>
public interface IEntradaDeMovimiento
{
    int? CategoriaId { get; }

    decimal? Monto { get; }

    string? Fecha { get; }

    string? Nota { get; }
}
