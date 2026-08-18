using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using GestionGastos.Api.Tests.Infra;

namespace GestionGastos.Api.Tests.Movimientos;

[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class RendimientoAltaTests(BaseDeDatosFixture baseDeDatos)
{
    private const int CategoriaComidaId = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// AC-17 / NFR-01: el alta se confirma en menos de 1 s en el percentil 95, medida sobre 100
    /// ejecuciones y con la tabla ya poblada — un alta contra una tabla vacía no dice nada del
    /// costo real del índice.
    /// </summary>
    [Fact]
    public async Task Alta_PercentilNoventaYCincoMenorAUnSegundo()
    {
        await baseDeDatos.LimpiarAsync();
        await MedicionDeRendimiento.SembrarMovimientosAsync(baseDeDatos, CategoriaComidaId);
        await MedicionDeRendimiento.ConfirmarSembradoAsync(costoQueSeMide: "del índice");

        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        // Descarta el costo de arranque (JIT, primer plan de consulta, pool de conexiones), que no
        // es el tiempo de guardado que mide el criterio.
        using (var calentamiento = await cliente.PostAsync("/api/movimientos", Cuerpo()))
        {
            Assert.Equal(HttpStatusCode.Created, calentamiento.StatusCode);
        }

        var duraciones = new List<TimeSpan>(MedicionDeRendimiento.Ejecuciones);
        for (var i = 0; i < MedicionDeRendimiento.Ejecuciones; i++)
        {
            var reloj = Stopwatch.StartNew();
            using var respuesta = await cliente.PostAsync("/api/movimientos", Cuerpo());
            reloj.Stop();

            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
            duraciones.Add(reloj.Elapsed);
        }

        var p95 = MedicionDeRendimiento.Percentil(duraciones, 95);
        Assert.True(
            p95 < MedicionDeRendimiento.PresupuestoP95,
            $"El p95 del alta fue {p95.TotalMilliseconds:F0} ms sobre {MedicionDeRendimiento.Ejecuciones} ejecuciones; " +
            $"el presupuesto de NFR-01 es {MedicionDeRendimiento.PresupuestoP95.TotalMilliseconds:F0} ms.");
    }

    private static StringContent Cuerpo() => new(
        JsonSerializer.Serialize(
            new { categoriaId = CategoriaComidaId, monto = 10.5m, fecha = "2026-03-01", nota = "medición" },
            Json),
        Encoding.UTF8,
        "application/json");
}
