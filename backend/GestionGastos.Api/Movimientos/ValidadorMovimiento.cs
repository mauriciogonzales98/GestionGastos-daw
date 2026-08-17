using System.Globalization;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;

namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Entrada del alta ya validada y convertida a los tipos del dominio. Que exista este tipo es lo que
/// permite que el handler no vuelva a preguntarse si el monto es positivo o si la fecha parsea.
/// </summary>
public sealed record DatosDeMovimiento(
    int CategoriaId,
    decimal Monto,
    DateOnly Fecha,
    string? Nota,
    TipoMovimiento? TipoEsperado);

/// <summary>
/// Validación a mano, sin dependencias nuevas. No lanza excepciones para el flujo esperado: devuelve
/// un <see cref="ResultadoValidacion"/> que el endpoint traduce a <c>ValidationProblem</c>.
/// </summary>
public static class ValidadorMovimiento
{
    public const int LargoMaximoDeNota = 120;

    /// <summary>13 dígitos enteros y 2 decimales, que es lo que entra en <c>decimal(15,2)</c>.</summary>
    public const decimal MontoMaximo = 9999999999999.99m;

    public const int DecimalesDelMonto = 2;

    public const string FormatoDeFecha = "yyyy-MM-dd";

    public static ResultadoValidacion Validar(CrearMovimientoRequest solicitud, out DatosDeMovimiento? datos)
    {
        var resultado = new ResultadoValidacion();

        var categoriaId = ValidarCategoriaId(solicitud.CategoriaId, resultado);
        var monto = ValidarMonto(solicitud.Monto, resultado);
        var fecha = ValidarFecha(solicitud.Fecha, resultado);
        var nota = ValidarNota(solicitud.Nota, resultado);
        var tipoEsperado = ValidarTipoEsperado(solicitud.TipoEsperado, resultado);

        if (!resultado.EsValido)
        {
            datos = null;
            return resultado;
        }

        datos = new DatosDeMovimiento(categoriaId!.Value, monto!.Value, fecha!.Value, nota, tipoEsperado);
        return resultado;
    }

    private static int? ValidarCategoriaId(int? categoriaId, ResultadoValidacion resultado)
    {
        // El 0 se trata como ausente a propósito: es el valor que manda un selector sin elegir.
        if (categoriaId is null or 0)
        {
            resultado.Agregar("categoriaId", "La categoría es obligatoria");
            return null;
        }

        if (categoriaId < 0)
        {
            resultado.Agregar("categoriaId", "La categoría no existe");
            return null;
        }

        return categoriaId;
    }

    private static decimal? ValidarMonto(decimal? monto, ResultadoValidacion resultado)
    {
        if (monto is not { } valor)
        {
            resultado.Agregar("monto", "El monto es obligatorio y debe ser un número");
            return null;
        }

        if (valor <= 0)
        {
            resultado.Agregar("monto", "El monto debe ser mayor a cero");
            return null;
        }

        // Comparación por valor y no por escala: 10.50 y 10.5 son el mismo monto, y rechazar el
        // primero por traer un cero de más sería un rechazo sin motivo.
        if (decimal.Round(valor, DecimalesDelMonto) != valor)
        {
            resultado.Agregar("monto", $"El monto admite como máximo {DecimalesDelMonto} decimales");
            return null;
        }

        if (valor > MontoMaximo)
        {
            resultado.Agregar(
                "monto",
                $"El monto no puede superar {MontoMaximo.ToString(CultureInfo.InvariantCulture)}");
            return null;
        }

        return valor;
    }

    private static DateOnly? ValidarFecha(string? fecha, ResultadoValidacion resultado)
    {
        if (string.IsNullOrWhiteSpace(fecha))
        {
            resultado.Agregar("fecha", "La fecha es obligatoria");
            return null;
        }

        if (!DateOnly.TryParseExact(fecha, FormatoDeFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parseada))
        {
            resultado.Agregar("fecha", $"La fecha debe tener formato {FormatoDeFecha}");
            return null;
        }

        return parseada;
    }

    private static string? ValidarNota(string? nota, ResultadoValidacion resultado)
    {
        if (string.IsNullOrWhiteSpace(nota))
        {
            // Vacía o en blanco es ausencia de nota, no una nota de espacios (AC-12).
            return null;
        }

        if (nota.Length > LargoMaximoDeNota)
        {
            resultado.Agregar("nota", $"La nota no puede superar los {LargoMaximoDeNota} caracteres");
            return null;
        }

        // Se persiste tal cual la escribió el usuario: escapar al guardar corrompe el dato, y el
        // escape corresponde al renderizar (mitigación R-06).
        return nota;
    }

    private static TipoMovimiento? ValidarTipoEsperado(string? tipoEsperado, ResultadoValidacion resultado)
    {
        if (string.IsNullOrWhiteSpace(tipoEsperado))
        {
            return null;
        }

        if (!TipoMovimientoTexto.TryParse(tipoEsperado, out var tipo))
        {
            resultado.Agregar(
                "tipoEsperado",
                $"El tipo debe ser {TipoMovimientoTexto.Gasto} o {TipoMovimientoTexto.Ingreso}");
            return null;
        }

        return tipo;
    }
}
