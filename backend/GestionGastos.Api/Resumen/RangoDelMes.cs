namespace GestionGastos.Api.Resumen;

/// <summary>
/// Corte del mes calendario al que pertenece una fecha, con los dos extremos incluidos.
/// </summary>
/// <remarks>
/// Vive en su propio archivo, y no adentro del handler, para poder probar los bordes sin base
/// (mitigación R-28): diciembre y febrero bisiesto son aritmética de calendario, y un rango mal
/// calculado no falla, incluye o excluye montos en silencio.
/// </remarks>
public readonly record struct RangoDelMes(DateOnly PrimerDia, DateOnly UltimoDia)
{
    /// <summary>
    /// El primer y el último día del mes de <paramref name="fecha"/>. El último día se deriva del
    /// primero del mes siguiente: contar días fijos acierta en unos meses y miente en otros.
    /// </summary>
    public static RangoDelMes De(DateOnly fecha)
    {
        var primerDia = new DateOnly(fecha.Year, fecha.Month, 1);
        return new RangoDelMes(primerDia, primerDia.AddMonths(1).AddDays(-1));
    }
}
