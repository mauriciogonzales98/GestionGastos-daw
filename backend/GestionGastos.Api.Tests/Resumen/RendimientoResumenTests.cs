using System.Diagnostics;
using System.Globalization;
using System.Net;
using GestionGastos.Api.Tests.Infra;
using Xunit.Abstractions;

namespace GestionGastos.Api.Tests.Resumen;

/// <summary>
/// NFR-01 y AC-11 del lado de la pantalla principal: lo que el usuario espera no es una petición
/// sino dos —el listado del mes y el resumen del mes—, y el presupuesto de 2 s es el de las dos
/// juntas.
/// </summary>
[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class RendimientoResumenTests(BaseDeDatosFixture baseDeDatos, ITestOutputHelper salida)
{
    private const int CategoriaComidaId = 1;

    private const string RutaDelResumen = "/api/resumen";

    /// <summary>
    /// El período del resumen lo fija el servidor, así que la medición es contra el mes en curso.
    /// El rango se calcula acá con <see cref="DateTime.DaysInMonth"/> y NO con <c>RangoDelMes</c>,
    /// por el mismo motivo que en <c>ResumenTests</c>: usar la pieza que el endpoint usa la haría
    /// coincidir consigo misma.
    /// </summary>
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.Today);

    private static readonly DateOnly PrimerDiaDelMes = new(Hoy.Year, Hoy.Month, 1);

    private static readonly DateOnly UltimoDiaDelMes =
        new(Hoy.Year, Hoy.Month, DateTime.DaysInMonth(Hoy.Year, Hoy.Month));

    /// <summary>
    /// El listado tal como lo pide la pantalla principal: el mes en curso y sin categoría, que es
    /// el default que `mesActual.ts` arma en el frontend. Medir el listado sin filtro sería medir
    /// una petición que la pantalla no hace.
    /// </summary>
    private static readonly string RutaDelListado =
        $"/api/movimientos?desde={Iso(PrimerDiaDelMes)}&hasta={Iso(UltimoDiaDelMes)}";

    /// <summary>
    /// Posiciones del sembrado que caen dentro del mes en curso. Salen de las fechas realmente
    /// sembradas y no de un número a mano, que dejaría de valer en cuanto alguien tocara el
    /// sembrado.
    /// </summary>
    private static readonly IReadOnlyList<int> IndicesSembradosDelMes = MedicionDeRendimiento.FechasSembradas
        .Select((fecha, indice) => (Fecha: fecha, Indice: indice))
        .Where(f => f.Fecha >= PrimerDiaDelMes && f.Fecha <= UltimoDiaDelMes)
        .Select(f => f.Indice)
        .ToList();

    /// <summary>
    /// Lo que el resumen tiene que devolver como total gastado del mes. Se deriva del contrato del
    /// sembrado —<c>SembrarMovimientosAsync</c> arranca en 100 y sube de a uno siguiendo el orden
    /// de <c>FechasSembradas</c>, así que la fila i vale 100 + i— y es el único oráculo que
    /// distingue "sumó el mes entero" de "sumó una parte".
    /// </summary>
    private static readonly decimal TotalGastadoEsperado = IndicesSembradosDelMes.Sum(i => 100m + i);

    /// <summary>
    /// AC-11 / NFR-01: la pantalla principal responde en menos de 2 s en el percentil 95, medida
    /// sobre 100 ejecuciones con 1000 movimientos en la cuenta. Se cronometran las DOS peticiones
    /// que la pantalla hace y se suman: el usuario no espera a la más rápida.
    /// </summary>
    /// <remarks>
    /// El p95 del resumen incluye sus dos round trips a la base —totales por tipo y desglose por
    /// categoría—, que es como está escrito el endpoint. El número no es el de una sola consulta.
    /// </remarks>
    [Fact]
    public async Task Pantalla_ConMilMovimientos_ListadoYResumenRespondenBajoDosSegundos()
    {
        await baseDeDatos.LimpiarAsync();
        await MedicionDeRendimiento.SembrarMovimientosAsync(baseDeDatos, CategoriaComidaId);
        await MedicionDeRendimiento.ConfirmarSembradoAsync(costoQueSeMide: "de la pantalla");
        ConfirmarQueElMesTieneFilas();

        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        // Descarta el costo de arranque (JIT, primer plan de consulta, pool de conexiones), que no
        // es el tiempo de respuesta que mide el criterio. Y de paso comprueba que lo que se
        // cronometra tiene datos adentro: un listado vacío y un resumen en cero también responden
        // rápido, y darían verde midiendo otra cosa.
        using (var calentamientoDelListado = await cliente.GetAsync(RutaDelListado))
        {
            Assert.Equal(HttpStatusCode.OK, calentamientoDelListado.StatusCode);
            var listado = await JsonDeRespuesta.LeerAsync(calentamientoDelListado);
            Assert.Equal(IndicesSembradosDelMes.Count, listado.GetProperty("total").GetInt32());
        }

        using (var calentamientoDelResumen = await cliente.GetAsync(RutaDelResumen))
        {
            Assert.Equal(HttpStatusCode.OK, calentamientoDelResumen.StatusCode);
            var resumen = await JsonDeRespuesta.LeerAsync(calentamientoDelResumen);
            Assert.Equal(TotalGastadoEsperado, resumen.GetProperty("totalGastado").GetDecimal());
        }

        var delListado = new List<TimeSpan>(MedicionDeRendimiento.Ejecuciones);
        var delResumen = new List<TimeSpan>(MedicionDeRendimiento.Ejecuciones);
        var deLaPantalla = new List<TimeSpan>(MedicionDeRendimiento.Ejecuciones);

        for (var i = 0; i < MedicionDeRendimiento.Ejecuciones; i++)
        {
            var relojDelListado = Stopwatch.StartNew();
            using (var respuestaDelListado = await cliente.GetAsync(RutaDelListado))
            {
                relojDelListado.Stop();
                Assert.Equal(HttpStatusCode.OK, respuestaDelListado.StatusCode);
            }

            var relojDelResumen = Stopwatch.StartNew();
            using (var respuestaDelResumen = await cliente.GetAsync(RutaDelResumen))
            {
                relojDelResumen.Stop();
                Assert.Equal(HttpStatusCode.OK, respuestaDelResumen.StatusCode);
            }

            delListado.Add(relojDelListado.Elapsed);
            delResumen.Add(relojDelResumen.Elapsed);
            // El criterio es de la pantalla, no de cada endpoint por separado: lo que se espera es
            // a que estén las dos respuestas.
            deLaPantalla.Add(relojDelListado.Elapsed + relojDelResumen.Elapsed);
        }

        var p95DelListado = MedicionDeRendimiento.Percentil(delListado, 95);
        var p95DelResumen = MedicionDeRendimiento.Percentil(delResumen, 95);
        var p95DeLaPantalla = MedicionDeRendimiento.Percentil(deLaPantalla, 95);

        // Los tres números quedan en la salida del test, no solo en el mensaje de una falla: el
        // criterio del bloque es que el p95 medido se pueda registrar, y un assert verde no dice
        // cuánto margen quedaba.
        salida.WriteLine($"p95 listado del mes:     {p95DelListado.TotalMilliseconds:F0} ms");
        salida.WriteLine($"p95 resumen (2 queries): {p95DelResumen.TotalMilliseconds:F0} ms");
        salida.WriteLine($"p95 pantalla completa:   {p95DeLaPantalla.TotalMilliseconds:F0} ms");
        salida.WriteLine(
            $"({MedicionDeRendimiento.Ejecuciones} ejecuciones, {MedicionDeRendimiento.MovimientosSembrados} " +
            $"movimientos sembrados, {IndicesSembradosDelMes.Count} de ellos en el mes medido)");

        Assert.True(
            p95DeLaPantalla < MedicionDeRendimiento.PresupuestoP95Pantalla,
            $"El p95 de la pantalla fue {p95DeLaPantalla.TotalMilliseconds:F0} ms sobre {MedicionDeRendimiento.Ejecuciones} " +
            $"ejecuciones (listado {p95DelListado.TotalMilliseconds:F0} ms + resumen {p95DelResumen.TotalMilliseconds:F0} ms, " +
            $"este último con sus dos consultas); el presupuesto de AC-11 es " +
            $"{MedicionDeRendimiento.PresupuestoP95Pantalla.TotalMilliseconds:F0} ms.");
    }

    /// <summary>
    /// NFR-02 a escala: con 1000 movimientos sembrados la respuesta del resumen sigue trayendo a lo
    /// sumo una fila por categoría. Es la mitad conductual que el test estructural del Block 1 no
    /// cubre — aquel mira el SQL con tres filas sembradas, este mira el cuerpo con mil.
    /// </summary>
    [Fact]
    public async Task Resumen_ConMilMovimientos_NoMaterializaFilasDeMovimientos()
    {
        await baseDeDatos.LimpiarAsync();
        await MedicionDeRendimiento.SembrarMovimientosAsync(baseDeDatos, CategoriaComidaId);
        await MedicionDeRendimiento.ConfirmarSembradoAsync(costoQueSeMide: "de la agregación");
        ConfirmarQueElMesTieneFilas();

        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuestaDelResumen = await cliente.GetAsync(RutaDelResumen);
        Assert.Equal(HttpStatusCode.OK, respuestaDelResumen.StatusCode);
        var textoDelResumen = await respuestaDelResumen.Content.ReadAsStringAsync();
        var resumen = JsonDeRespuesta.Raiz(textoDelResumen);

        // Todos los movimientos sembrados son gastos de la misma categoría: decenas de filas en el
        // mes son UNA fila en el desglose, con la suma completa.
        var desglose = resumen.GetProperty("desglose").EnumerateArray().ToList();
        var unica = Assert.Single(desglose);
        Assert.Equal(CategoriaComidaId, unica.GetProperty("categoriaId").GetInt32());
        Assert.Equal(TotalGastadoEsperado, unica.GetProperty("total").GetDecimal());
        Assert.Equal(TotalGastadoEsperado, resumen.GetProperty("totalGastado").GetDecimal());

        // La forma general del criterio, no la del sembrado: a lo sumo una fila POR CATEGORÍA del
        // catálogo, y ninguna categoría repetida. Escrito así, el test sigue diciendo lo mismo si
        // mañana el sembrado usara varias categorías.
        var categorias = await ObtenerCategoriasAsync(cliente);
        Assert.True(
            desglose.Count <= categorias,
            $"El desglose trae {desglose.Count} filas y el catálogo tiene {categorias} categorías.");
        Assert.Equal(
            desglose.Select(c => c.GetProperty("categoriaId").GetInt32()).Distinct().Count(),
            desglose.Count);
        // Y una fila por categoría es un orden de magnitud menos que una fila por movimiento: con
        // 93 gastos en el mes, la diferencia entre agregar y no agregar es visible en el conteo.
        Assert.True(
            IndicesSembradosDelMes.Count > desglose.Count * 10,
            $"El mes tiene {IndicesSembradosDelMes.Count} movimientos y el desglose {desglose.Count} filas: " +
            "con tan poca diferencia el conteo no distingue una agregación de un listado.");

        // Y nada con forma de fila viaja al lado de los agregados.
        Assert.False(resumen.TryGetProperty("items", out _), $"El resumen no devuelve items: {textoDelResumen}");
        Assert.False(resumen.TryGetProperty("movimientos", out _), $"El resumen no devuelve movimientos: {textoDelResumen}");
        Assert.DoesNotContain("nota", textoDelResumen, StringComparison.OrdinalIgnoreCase);

        // El tamaño de la respuesta no crece con las filas del mes. El listado del MISMO mes es la
        // vara: trae una fila por movimiento y pesa un orden de magnitud más. Un `Single` sobre el
        // desglose no distingue "agregó" de "agregó y además mandó las filas al lado"; esto sí.
        using var respuestaDelListado = await cliente.GetAsync(RutaDelListado);
        Assert.Equal(HttpStatusCode.OK, respuestaDelListado.StatusCode);
        var textoDelListado = await respuestaDelListado.Content.ReadAsStringAsync();

        Assert.True(
            textoDelResumen.Length * 10 < textoDelListado.Length,
            $"El resumen pesa {textoDelResumen.Length} caracteres y el listado del mismo mes " +
            $"{textoDelListado.Length}, con {IndicesSembradosDelMes.Count} movimientos: el resumen " +
            "está transportando filas y no solo agregados.");
    }

    /// <summary>
    /// Premisa compartida: el sembrado deja movimientos DENTRO del mes que se mide. Sin filas en el
    /// mes en curso, la medición cronometraría un listado vacío y un resumen en cero, y "a lo sumo
    /// una fila por categoría" se cumpliría por no haber ninguna.
    /// </summary>
    private static void ConfirmarQueElMesTieneFilas() =>
        Assert.True(
            IndicesSembradosDelMes.Count > 1,
            $"El sembrado dejó {IndicesSembradosDelMes.Count} movimientos entre {Iso(PrimerDiaDelMes)} y " +
            $"{Iso(UltimoDiaDelMes)}, y la medición necesita el mes poblado para decir algo.");

    /// <summary>Cuántas categorías tiene el catálogo, que es el techo de filas del desglose (AC-12).</summary>
    private static async Task<int> ObtenerCategoriasAsync(HttpClient cliente)
    {
        using var respuesta = await cliente.GetAsync("/api/categorias");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var catalogo = await JsonDeRespuesta.LeerAsync(respuesta);
        return catalogo.EnumerateArray().Count();
    }

    private static string Iso(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
