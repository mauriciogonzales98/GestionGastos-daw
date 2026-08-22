using System.Diagnostics;
using System.Globalization;
using System.Net;
using GestionGastos.Api.Tests.Infra;

namespace GestionGastos.Api.Tests.Movimientos;

[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class RendimientoListadoTests(BaseDeDatosFixture baseDeDatos)
{
    private const int CategoriaComidaId = 1;

    /// <summary>
    /// El rango que se mide: marzo del año en curso. Se deriva del mismo reloj que
    /// <see cref="MedicionDeRendimiento.FechasSembradas"/> y no de un año literal, porque un rango
    /// fijo deja de coincidir con el sembrado en cuanto cambia el año — que es el defecto que
    /// FIX-004 corrige del otro lado.
    /// </summary>
    private static readonly int AnioEnCurso = DateTime.Today.Year;

    private static readonly DateOnly Desde = new(AnioEnCurso, 3, 1);
    private static readonly DateOnly Hasta = new(AnioEnCurso, 3, 31);

    /// <summary>El listado filtrado, que es lo que la aplicación pide de entrada: categoría más rango.</summary>
    private static readonly string RutaFiltrada =
        $"/api/movimientos?categoriaId={CategoriaComidaId}" +
        $"&desde={Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" +
        $"&hasta={Hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// AC-13 / NFR-01: el listado filtrado responde en menos de 1 s en el percentil 95, medido
    /// sobre 100 ejecuciones con 1000 movimientos en la cuenta — con la tabla vacía la medición no
    /// diría nada del costo del filtro ni del índice.
    /// </summary>
    [Fact]
    public async Task Listado_ConMilMovimientos_RespondeBajoUnSegundo()
    {
        await baseDeDatos.LimpiarAsync();
        await MedicionDeRendimiento.SembrarMovimientosAsync(baseDeDatos, CategoriaComidaId);
        await MedicionDeRendimiento.ConfirmarSembradoAsync(costoQueSeMide: "del filtro");

        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        // Descarta el costo de arranque (JIT, primer plan de consulta, pool de conexiones), que no
        // es el tiempo de respuesta que mide el criterio. Y de paso comprueba que lo que se mide es
        // el listado FILTRADO: cronometrar una consulta que devuelve las 1000 filas mediría otra
        // cosa y podría dar verde igual. Las esperadas salen de las fechas realmente sembradas, no
        // de un número a mano.
        var esperadas = MedicionDeRendimiento.FechasSembradas.Count(f => f >= Desde && f <= Hasta);

        // El otro lado del mismo modo de falla que ConfirmarQueElMesTieneFilas() cubre en
        // RendimientoResumenTests: si el rango dejara de coincidir con el sembrado, `esperadas`
        // valdría 0, la API devolvería 0, y las dos aserciones de abajo compararían 0 con 0. El test
        // daría VERDE cronometrando una consulta vacía — que es el defecto que FIX-004 corrige.
        Assert.True(
            esperadas > 0,
            $"El rango {Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.." +
            $"{Hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} no contiene ninguna fila " +
            "del sembrado, así que la medición cronometraría una consulta vacía comparando 0 con 0.");

        using (var calentamiento = await cliente.GetAsync(RutaFiltrada))
        {
            Assert.Equal(HttpStatusCode.OK, calentamiento.StatusCode);
            var listado = await JsonDeRespuesta.LeerAsync(calentamiento);
            Assert.Equal(esperadas, listado.GetProperty("total").GetInt32());
            Assert.Equal(esperadas, listado.GetProperty("items").EnumerateArray().Count());
        }

        var duraciones = new List<TimeSpan>(MedicionDeRendimiento.Ejecuciones);
        for (var i = 0; i < MedicionDeRendimiento.Ejecuciones; i++)
        {
            var reloj = Stopwatch.StartNew();
            using var respuesta = await cliente.GetAsync(RutaFiltrada);
            reloj.Stop();

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            duraciones.Add(reloj.Elapsed);
        }

        var p95 = MedicionDeRendimiento.Percentil(duraciones, 95);
        Assert.True(
            p95 < MedicionDeRendimiento.PresupuestoP95,
            $"El p95 del listado filtrado fue {p95.TotalMilliseconds:F0} ms sobre {MedicionDeRendimiento.Ejecuciones} ejecuciones; " +
            $"el presupuesto de NFR-01 es {MedicionDeRendimiento.PresupuestoP95.TotalMilliseconds:F0} ms.");
    }
}
