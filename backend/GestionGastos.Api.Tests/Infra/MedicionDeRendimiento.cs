using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;

namespace GestionGastos.Api.Tests.Infra;

/// <summary>
/// Lo que comparten las mediciones de NFR-01: el presupuesto, el tamaño de la muestra, el cálculo
/// del percentil y la tabla poblada contra la que hay que medir. Vive acá y no en cada test porque
/// un p95 calculado de dos maneras, o dos sembrados que se van desincronizando, harían incomparables
/// dos números que el criterio trata como el mismo presupuesto.
/// </summary>
/// <remarks>
/// Lo que NO se comparte es qué se cronometra: el alta mide un POST y el listado mide un GET
/// filtrado. Eso es el test, no el arreglo, y cada uno lo escribe entero.
/// </remarks>
public static class MedicionDeRendimiento
{
    /// <summary>
    /// Filas en la tabla al momento de medir: con la tabla vacía el número no diría nada del costo
    /// real del índice ni del filtro.
    /// </summary>
    public const int MovimientosSembrados = 1000;

    /// <summary>Muestras por medición, las que hacen que hablar de un percentil 95 signifique algo.</summary>
    public const int Ejecuciones = 100;

    /// <summary>Presupuesto de NFR-01 para el p95 de UNA petición: el alta, o el listado.</summary>
    public static readonly TimeSpan PresupuestoP95 = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Presupuesto del p95 de la pantalla principal completa, que AC-11 mide como el listado y el
    /// resumen juntos.
    /// </summary>
    /// <remarks>
    /// Son dos constantes y no una a propósito: <see cref="PresupuestoP95"/> vale 1 s y es el
    /// presupuesto de una sola petición, mientras que este cubre las dos que la pantalla hace.
    /// Reusar la de 1 s para la pantalla mediría contra un criterio que nadie escribió —ni el de
    /// AC-11, que da 2 s, ni el del listado solo—, y el número resultante no sería comparable con
    /// ninguno de los dos.
    /// </remarks>
    public static readonly TimeSpan PresupuestoP95Pantalla = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Las fechas de los movimientos sembrados, repartidas sobre el año en curso. Se declaran una
    /// sola vez porque son a la vez lo que se siembra y de donde sale cuántas filas tiene que
    /// devolver un rango: un número copiado a mano dejaría de valer en cuanto alguien tocara el
    /// sembrado.
    /// </summary>
    public static readonly IReadOnlyList<DateOnly> FechasSembradas =
        GenerarFechasSembradas(DateOnly.FromDateTime(DateTime.Today));

    /// <summary>
    /// Reparte <see cref="MovimientosSembrados"/> fechas sobre los 365 días que siguen al 1 de enero
    /// del año de <paramref name="hoy"/>.
    /// </summary>
    /// <param name="hoy">La fecha de ejecución. Sólo se usa su año.</param>
    /// <remarks>
    /// <para>
    /// <b>La propiedad que estas fechas deben cumplir, y que no puede quedar implícita:</b> tienen
    /// que cubrir <b>el mes en curso, sea cual sea</b>, porque <c>RendimientoResumenTests</c> mide
    /// contra el mes que el servidor fija con el reloj real. Por eso el ancla **no puede llevar un
    /// año escrito literalmente**.
    /// </para>
    /// <para>
    /// Eso fue exactamente el defecto de FIX-004: la versión anterior decía
    /// <c>new DateOnly(2026, 1, 1)</c>, que significaba "el 1 de enero del año en curso" el día en
    /// que se escribió y quedó absoluta en el archivo. Desde el 2027-01-01 dejaba el mes en curso
    /// con cero filas, y para siempre.
    /// </para>
    /// <para>
    /// El ancla es el año completo y no una ventana alrededor de hoy para no tener bordes de
    /// calendario que demostrar de a uno: con <c>i % 365</c> sobre 1000 elementos, 270 días llevan 3
    /// filas y 95 llevan 2, así que el mínimo por día es 2 y el peor mes posible —un febrero común
    /// de 28 días— deja 56 filas.
    /// </para>
    /// <para>
    /// Es función pura y parametrizada por fecha siguiendo el patrón que este proyecto ya usa para
    /// la lógica de calendario: <c>RangoDelMes.De(DateOnly)</c>, que producción llama con
    /// <c>DateTime.Today</c> y que <c>RangoDelMesTests</c> prueba con fechas simuladas. Así los
    /// bordes se comprueban sin esperar a que el calendario llegue.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<DateOnly> GenerarFechasSembradas(DateOnly hoy)
    {
        var primerDiaDelAnio = new DateOnly(hoy.Year, 1, 1);

        return Enumerable
            .Range(0, MovimientosSembrados)
            .Select(i => primerDiaDelAnio.AddDays(i % 365))
            .ToList();
    }

    /// <summary>
    /// Método del rango más cercano: el menor valor por debajo del cual cae al menos el percentil
    /// pedido de las muestras.
    /// </summary>
    public static TimeSpan Percentil(IReadOnlyCollection<TimeSpan> duraciones, int percentil)
    {
        var ordenadas = duraciones.Order().ToList();
        var indice = (int)Math.Ceiling(percentil / 100d * ordenadas.Count) - 1;
        return ordenadas[Math.Clamp(indice, 0, ordenadas.Count - 1)];
    }

    /// <summary>
    /// Puebla la tabla con <see cref="MovimientosSembrados"/> movimientos del usuario semilla, todos
    /// de la misma categoría y con las fechas de <see cref="FechasSembradas"/>.
    /// </summary>
    public static async Task SembrarMovimientosAsync(BaseDeDatosFixture baseDeDatos, int categoriaId)
    {
        await using var contexto = baseDeDatos.CrearContexto();
        var usuarioId = await new UsuarioSemillaActual(contexto).ObtenerIdAsync();
        contexto.UsuarioActualId = usuarioId;

        var monto = 100m;
        foreach (var fecha in FechasSembradas)
        {
            contexto.Movimientos.Add(new Movimiento
            {
                UsuarioId = usuarioId,
                CategoriaId = categoriaId,
                Tipo = TipoMovimiento.Gasto,
                Monto = monto++,
                Moneda = Moneda.Predeterminada,
                Fecha = fecha,
                Nota = null,
                CreadoEn = DateTime.UtcNow,
            });
        }

        await contexto.SaveChangesAsync();
    }

    /// <summary>
    /// El sembrado es la premisa de la medición, no un detalle del arreglo: si las filas no
    /// quedaron, el p95 se mide contra una tabla chica y el test da verde sin haber probado NFR-01.
    /// Falla acá, con el número real, en vez de mentir más abajo.
    /// </summary>
    public static async Task ConfirmarSembradoAsync(string costoQueSeMide)
    {
        var sembradas = await MovimientosEnLaBase.CantidadAsync();
        Assert.True(
            sembradas == MovimientosSembrados,
            $"El sembrado dejó {sembradas} movimientos y la medición necesita {MovimientosSembrados}: " +
            $"sin la tabla poblada el p95 no dice nada del costo real {costoQueSeMide}.");
    }
}
