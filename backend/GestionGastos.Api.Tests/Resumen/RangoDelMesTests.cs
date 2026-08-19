using GestionGastos.Api.Resumen;

namespace GestionGastos.Api.Tests.Resumen;

/// <summary>
/// El corte del mes se prueba sin base (mitigación R-28): los bordes —diciembre y febrero
/// bisiesto— son aritmética de calendario, y una base de por medio solo agregaría ruido a la
/// afirmación.
/// </summary>
public sealed class RangoDelMesTests
{
    [Fact]
    public void RangoDelMes_DevuelveElPrimeroYElUltimoDiaDelMes()
    {
        // Un día cualquiera del medio del mes: el rango no depende de la fecha que se le pase, solo
        // de su mes.
        var marzo = RangoDelMes.De(new DateOnly(2026, 3, 15));

        Assert.Equal(new DateOnly(2026, 3, 1), marzo.PrimerDia);
        Assert.Equal(new DateOnly(2026, 3, 31), marzo.UltimoDia);

        // Un mes de 30 días: sin este contraste, un cálculo que sumara 30 días fijos daría verde en
        // marzo y mentiría en abril.
        var abril = RangoDelMes.De(new DateOnly(2026, 4, 17));

        Assert.Equal(new DateOnly(2026, 4, 1), abril.PrimerDia);
        Assert.Equal(new DateOnly(2026, 4, 30), abril.UltimoDia);

        // Los extremos son idempotentes: partir del primero o del último día da el mismo rango.
        Assert.Equal(marzo, RangoDelMes.De(new DateOnly(2026, 3, 1)));
        Assert.Equal(marzo, RangoDelMes.De(new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void RangoDelMes_EnDiciembre_NoSeCorreAlAnioSiguiente()
    {
        var diciembre = RangoDelMes.De(new DateOnly(2026, 12, 10));

        // El error clásico es calcular el último día como "el día anterior al primero del mes
        // siguiente" y perder el año en el camino.
        Assert.Equal(new DateOnly(2026, 12, 1), diciembre.PrimerDia);
        Assert.Equal(new DateOnly(2026, 12, 31), diciembre.UltimoDia);
        Assert.Equal(2026, diciembre.UltimoDia.Year);
        Assert.Equal(12, diciembre.UltimoDia.Month);
    }

    [Fact]
    public void RangoDelMes_EnFebreroDeAnioBisiesto_TerminaEl29()
    {
        var bisiesto = RangoDelMes.De(new DateOnly(2024, 2, 5));

        Assert.Equal(new DateOnly(2024, 2, 1), bisiesto.PrimerDia);
        Assert.Equal(new DateOnly(2024, 2, 29), bisiesto.UltimoDia);

        // El contraste con un febrero común: un último día fijo en 28 —o en 29— acierta uno de los
        // dos casos y no los dos.
        var comun = RangoDelMes.De(new DateOnly(2026, 2, 5));

        Assert.Equal(new DateOnly(2026, 2, 1), comun.PrimerDia);
        Assert.Equal(new DateOnly(2026, 2, 28), comun.UltimoDia);
    }
}
