using System.Text.Json.Serialization;

namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Entrada de la modificación. **No tiene** <c>id</c> —viaja en la ruta—, ni <c>usuarioId</c>,
/// <c>tipo</c>, <c>moneda</c>, <c>creadoEn</c> o <c>tipoEsperado</c> (mitigación R-15, que extiende
/// R-04 al PUT): un cuerpo que los incluya los ve ignorados, no aplicados. El propietario y el tipo
/// del movimiento no se pueden cambiar; la categoría nueva tiene que ser del mismo tipo que el
/// movimiento.
/// </summary>
/// <param name="CategoriaId">Categoría del catálogo, del mismo tipo que el movimiento.</param>
/// <param name="Monto">Mayor a cero, con hasta dos decimales.</param>
/// <param name="Fecha">Fecha del movimiento en formato <c>yyyy-MM-dd</c>, sin hora ni zona horaria.</param>
/// <param name="Nota">Opcional, hasta 120 caracteres. Vacía o en blanco se normaliza a <c>null</c>.</param>
/// <remarks>
/// El <see cref="MontoJsonConverter"/> se repite acá a propósito: los atributos no se heredan de
/// <see cref="IEntradaDeMovimiento"/>, y sin él un <c>"monto": "abc"</c> reventaría el
/// deserializador y saldría como un 400 genérico sin <c>errors.monto</c>.
/// </remarks>
public sealed record ModificarMovimientoRequest(
    int? CategoriaId,
    [property: JsonConverter(typeof(MontoJsonConverter))] decimal? Monto,
    string? Fecha,
    string? Nota) : IEntradaDeMovimiento;
