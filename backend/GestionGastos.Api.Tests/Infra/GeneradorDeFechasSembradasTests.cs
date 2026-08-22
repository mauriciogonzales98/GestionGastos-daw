namespace GestionGastos.Api.Tests.Infra;

/// <summary>
/// La comprobación ejecutable de FIX-004: el sembrado de rendimiento cubre el mes en curso en
/// cualquier fecha de ejecución.
/// </summary>
/// <remarks>
/// <para>
/// Estos tests existen porque la alternativa era verificar el arreglo <b>leyendo el código</b>, y esa
/// es exactamente la razón por la que la verificación ronda 1 de FIX-001 salió BLOCKED: un arreglo
/// que sólo se puede comprobar mirándolo no está comprobado.
/// </para>
/// <para>
/// El patrón —función pura, fechas simuladas como entrada, aserciones sobre la salida, sin tocar el
/// reloj del sistema— es el que <c>RangoDelMesTests</c> ya usa para <c>RangoDelMes.De(DateOnly)</c>,
/// incluido su caso de febrero bisiesto. Se copia a propósito.
/// </para>
/// </remarks>
public sealed class GeneradorDeFechasSembradasTests
{
    /// <summary>
    /// El defecto original, reproducido: antes de FIX-004 el sembrado llevaba el año 2026 escrito, y
    /// en cualquier fecha de 2027 en adelante dejaba el mes en curso con cero filas. Este es el test
    /// que fallaba antes del arreglo y pasa después.
    /// </summary>
    [Theory]
    [InlineData(2027, 1, 1)]   // el día exacto en que el sembrado viejo vencía
    [InlineData(2027, 6, 15)]
    [InlineData(2030, 12, 31)] // muy en el futuro: el arreglo no tiene fecha de caducidad propia
    public void Generador_EnFechasPosterioresAlVencimientoViejo_CubreElMesDeEsaFecha(
        int anio, int mes, int dia)
    {
        var hoySimulado = new DateOnly(anio, mes, dia);

        var enElMes = FechasDelMesDe(hoySimulado);

        Assert.NotEmpty(enElMes);
        Assert.All(enElMes, fecha =>
        {
            Assert.Equal(hoySimulado.Year, fecha.Year);
            Assert.Equal(hoySimulado.Month, fecha.Month);
        });
    }

    /// <summary>
    /// AC-01 pide un piso de 2 movimientos en el mes <b>en cualquier fecha</b>, no un promedio. Se
    /// recorren los 12 meses en vez de confiar en el mes en que corra el CI.
    /// </summary>
    [Fact]
    public void Generador_EnCualquierMesDelAnio_DejaAlMenosDosFilasEnEseMes()
    {
        foreach (var mes in Enumerable.Range(1, 12))
        {
            var hoySimulado = new DateOnly(2029, mes, 1);

            Assert.True(
                FechasDelMesDe(hoySimulado).Count >= 2,
                $"El mes {mes} de 2029 quedó con menos de 2 movimientos sembrados, y la medición " +
                "necesita el mes poblado para decir algo.");
        }
    }

    /// <summary>
    /// El borde que el propio repositorio ya aprendió a probar: <c>RangoDelMesTests</c> ejercita un
    /// febrero bisiesto porque un último día fijo en 28 —o en 29— acierta uno de los dos casos y
    /// falla el otro. El sembrado no debería tener ese borde, y esto lo comprueba en vez de
    /// suponerlo.
    /// </summary>
    [Theory]
    [InlineData(2028)] // bisiesto
    [InlineData(2029)] // común
    public void Generador_EnFebrero_CubreElMesEnBisiestoYEnComun(int anio)
    {
        var enFebrero = FechasDelMesDe(new DateOnly(anio, 2, 14));

        Assert.NotEmpty(enFebrero);
        Assert.All(enFebrero, fecha => Assert.Equal(2, fecha.Month));
    }

    /// <summary>
    /// El día del mes en que se ejecute no debe cambiar lo que se siembra: el ancla es el año, no la
    /// fecha. Si esto fallara, el arnés mediría distinto según el día, que es la clase de defecto
    /// que FIX-004 corrige.
    /// </summary>
    [Fact]
    public void Generador_ConDistintosDiasDelMismoAnio_ProduceExactamenteLoMismo()
    {
        var desdeElPrimero = MedicionDeRendimiento.GenerarFechasSembradas(new DateOnly(2029, 1, 1));
        var desdeElUltimo = MedicionDeRendimiento.GenerarFechasSembradas(new DateOnly(2029, 12, 31));

        Assert.Equal(desdeElPrimero, desdeElUltimo);
    }

    /// <summary>
    /// NFR-01 y AC-09: el volumen del sembrado no cambia con este ticket. Un arnés que midiera sobre
    /// menos filas daría números más bajos por el motivo equivocado.
    /// </summary>
    [Fact]
    public void Generador_SiempreProduceElVolumenQueElCriterioFijo()
    {
        var fechas = MedicionDeRendimiento.GenerarFechasSembradas(new DateOnly(2031, 7, 9));

        Assert.Equal(MedicionDeRendimiento.MovimientosSembrados, fechas.Count);
        Assert.Equal(365, fechas.Distinct().Count());
    }

    private static List<DateOnly> FechasDelMesDe(DateOnly hoySimulado) =>
        MedicionDeRendimiento
            .GenerarFechasSembradas(hoySimulado)
            .Where(f => f.Year == hoySimulado.Year && f.Month == hoySimulado.Month)
            .ToList();
}
