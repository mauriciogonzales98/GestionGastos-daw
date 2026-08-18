using System.Globalization;
using GestionGastos.Api.Common;

namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Los filtros del listado ya validados y convertidos a los tipos del dominio. Cada uno es opcional
/// por separado: <c>null</c> significa "sin ese filtro", nunca "categoría cero" ni "fecha mínima".
/// </summary>
public sealed record FiltrosValidados(int? CategoriaId, DateOnly? Desde, DateOnly? Hasta);

/// <summary>
/// Parseo y validación de los tres parámetros de query del listado. Mismo patrón que
/// <see cref="ValidadorMovimiento"/>: no lanza excepciones para el flujo esperado, devuelve un
/// <see cref="ResultadoValidacion"/> que el endpoint traduce a <c>ValidationProblem</c>.
/// </summary>
public static class FiltrosDeListado
{
    public static ResultadoValidacion Parsear(
        int? categoriaId,
        string? desde,
        string? hasta,
        out FiltrosValidados? filtros)
    {
        var resultado = new ResultadoValidacion();

        var categoria = ValidarCategoriaId(categoriaId, resultado);
        var inicio = ValidarFecha(desde, "desde", resultado);
        var fin = ValidarFecha(hasta, "hasta", resultado);

        // El rango solo se compara si las dos fechas parsearon: contra una que no parseó, la
        // comparación sumaría un segundo error que no describe nada nuevo.
        if (inicio is { } desdeValido && fin is { } hastaValido && desdeValido > hastaValido)
        {
            resultado.Agregar(
                "desde",
                "La fecha de inicio no puede ser posterior a la de fin");
        }

        if (!resultado.EsValido)
        {
            filtros = null;
            return resultado;
        }

        filtros = new FiltrosValidados(categoria, inicio, fin);
        return resultado;
    }

    private static int? ValidarCategoriaId(int? categoriaId, ResultadoValidacion resultado)
    {
        if (categoriaId is not { } valor)
        {
            return null;
        }

        if (valor <= 0)
        {
            // Una categoría que no existe NO es un error —devuelve listado vacío—, pero un id que
            // ninguna categoría puede tener es entrada malformada.
            resultado.Agregar("categoriaId", "La categoría debe ser un número mayor a cero");
            return null;
        }

        return valor;
    }

    private static DateOnly? ValidarFecha(string? fecha, string campo, ResultadoValidacion resultado)
    {
        if (string.IsNullOrWhiteSpace(fecha))
        {
            // Ausente o en blanco es "sin ese extremo", no un rechazo: los tres filtros son
            // opcionales e independientes.
            return null;
        }

        if (!DateOnly.TryParseExact(
                fecha,
                ValidadorMovimiento.FormatoDeFecha,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parseada))
        {
            // Mensaje redactado a mano, nunca el detalle de la excepción del parseo (mitigación
            // R-21). Formato exacto y cultura invariante para que "01/02" no signifique cosas
            // distintas según el entorno (mitigación R-20).
            resultado.Agregar(campo, $"La fecha debe tener formato {ValidadorMovimiento.FormatoDeFecha}");
            return null;
        }

        if (parseada < ValidadorMovimiento.FechaMinima || parseada > ValidadorMovimiento.FechaMaxima)
        {
            resultado.Agregar(
                campo,
                $"La fecha debe estar entre {Texto(ValidadorMovimiento.FechaMinima)} y {Texto(ValidadorMovimiento.FechaMaxima)}");
            return null;
        }

        return parseada;
    }

    private static string Texto(DateOnly fecha) =>
        fecha.ToString(ValidadorMovimiento.FormatoDeFecha, CultureInfo.InvariantCulture);
}
