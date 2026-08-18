using System.Globalization;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;

namespace GestionGastos.Api.Movimientos;

/// <summary>
/// Entrada de un movimiento ya validada y convertida a los tipos del dominio, la misma para el alta
/// y para la modificación. Que exista este tipo es lo que permite que el handler no vuelva a
/// preguntarse si el monto es positivo o si la fecha parsea.
/// </summary>
public sealed record DatosDeMovimiento(
    int CategoriaId,
    decimal Monto,
    DateOnly Fecha,
    string? Nota);

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

    /// <summary>
    /// Primera fecha que el tipo <c>DATE</c> de MySQL admite. <c>DateOnly</c> arranca en
    /// <c>0001-01-01</c>, así que hay un rango que parsea acá y que la base rechaza: sin este límite
    /// la solicitud llega al INSERT y el error del proveedor sale como 500, donde el contrato promete
    /// 400.
    /// </summary>
    public static readonly DateOnly FechaMinima = new(1000, 1, 1);

    /// <summary>Última fecha que el tipo <c>DATE</c> de MySQL admite.</summary>
    public static readonly DateOnly FechaMaxima = new(9999, 12, 31);

    /// <summary>
    /// Las cuatro reglas que el alta y la modificación comparten, sobre la interfaz común y no sobre
    /// un tipo concreto: una sola implementación, imposible de dejar atrás en uno de los dos
    /// endpoints (mitigación R-16).
    /// </summary>
    public static ResultadoValidacion Validar(IEntradaDeMovimiento solicitud, out DatosDeMovimiento? datos)
    {
        var resultado = new ResultadoValidacion();

        var categoriaId = ValidarCategoriaId(solicitud.CategoriaId, resultado);
        var monto = ValidarMonto(solicitud.Monto, resultado);
        var fecha = ValidarFecha(solicitud.Fecha, resultado);
        var nota = ValidarNota(solicitud.Nota, resultado);

        if (!resultado.EsValido)
        {
            datos = null;
            return resultado;
        }

        datos = new DatosDeMovimiento(categoriaId!.Value, monto!.Value, fecha!.Value, nota);
        return resultado;
    }

    /// <summary>
    /// El rechazo por categoría inexistente, redactado en un solo lugar para que el alta y la
    /// modificación no puedan responder cosas distintas ante lo mismo.
    /// </summary>
    public static ResultadoValidacion CategoriaInexistente() =>
        new ResultadoValidacion().Agregar("categoriaId", "La categoría no existe");

    /// <summary>
    /// El rechazo por tipo cruzado. En el alta el tipo del movimiento lo aporta <c>tipoEsperado</c>;
    /// en la modificación sale de la fila persistida. El mensaje nombra los dos tipos porque, sin
    /// eso, "no puede usarse" no dice qué corregir.
    /// </summary>
    public static ResultadoValidacion TipoCruzado(
        string nombreDeLaCategoria,
        TipoMovimiento tipoDeLaCategoria,
        TipoMovimiento tipoDelMovimiento) =>
        new ResultadoValidacion().Agregar(
            "categoriaId",
            $"La categoría '{nombreDeLaCategoria}' es de tipo {TipoMovimientoTexto.Nombre(tipoDeLaCategoria)} " +
            $"y no puede usarse en un movimiento de tipo {TipoMovimientoTexto.Nombre(tipoDelMovimiento)}");

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

        if (parseada < FechaMinima || parseada > FechaMaxima)
        {
            resultado.Agregar(
                "fecha",
                $"La fecha debe estar entre {FechaMinima.ToString(FormatoDeFecha, CultureInfo.InvariantCulture)} " +
                $"y {FechaMaxima.ToString(FormatoDeFecha, CultureInfo.InvariantCulture)}");
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

    /// <summary>
    /// Queda FUERA de <see cref="Validar"/> y la invoca solo el alta. <c>tipoEsperado</c> es una
    /// entrada no confiable del cliente que solo sirve para detectar el cruce de tipo en el POST; la
    /// modificación no la necesita, porque compara contra el tipo persistido del movimiento, que sí
    /// es confiable. Acumula sobre el mismo <paramref name="resultado"/> para que un cuerpo con dos
    /// problemas siga devolviendo los dos errores de una.
    /// </summary>
    public static TipoMovimiento? ValidarTipoEsperado(string? tipoEsperado, ResultadoValidacion resultado)
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
