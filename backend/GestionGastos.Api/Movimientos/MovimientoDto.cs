using GestionGastos.Api.Data.Entidades;

namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Forma de salida de un movimiento. La comparten el 201 del alta y el listado (Block 3) para que no
/// puedan divergir.
/// </summary>
public sealed record MovimientoDto(
    int Id,
    string Tipo,
    CategoriaDeMovimientoDto Categoria,
    decimal Monto,
    string Moneda,
    string Fecha,
    string? Nota);

/// <summary>Solo id y nombre: la fila del listado no necesita más y el tipo ya viaja arriba.</summary>
public sealed record CategoriaDeMovimientoDto(int Id, string Nombre);

/// <summary>
/// Traducción entre el enum persistido y su nombre en la API. Vive en un solo lugar porque el
/// contrato HTTP dice <c>"gasto"</c>/<c>"ingreso"</c> y el enum dice <c>1</c>/<c>2</c>: repartir esa
/// conversión por los handlers es cómo aparecen las respuestas que no coinciden entre endpoints.
/// </summary>
public static class TipoMovimientoTexto
{
    public const string Gasto = "gasto";

    public const string Ingreso = "ingreso";

    public static string Nombre(TipoMovimiento tipo) => tipo switch
    {
        TipoMovimiento.Gasto => Gasto,
        TipoMovimiento.Ingreso => Ingreso,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de movimiento desconocido."),
    };

    public static bool TryParse(string texto, out TipoMovimiento tipo)
    {
        switch (texto.Trim().ToLowerInvariant())
        {
            case Gasto:
                tipo = TipoMovimiento.Gasto;
                return true;
            case Ingreso:
                tipo = TipoMovimiento.Ingreso;
                return true;
            default:
                tipo = default;
                return false;
        }
    }
}
