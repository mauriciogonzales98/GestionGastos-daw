using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;
using GestionGastos.Api.Tests.Infra;

namespace GestionGastos.Api.Tests.Movimientos;

[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class RendimientoAltaTests(BaseDeDatosFixture baseDeDatos)
{
    private const int CategoriaComidaId = 1;
    private const int MovimientosSembrados = 1000;
    private const int Ejecuciones = 100;
    private static readonly TimeSpan PresupuestoP95 = TimeSpan.FromSeconds(1);

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
        await SembrarMovimientosAsync();

        // El sembrado es la premisa de la medición, no un detalle del arreglo: si las filas no
        // quedaron, el p95 se mide contra una tabla chica y el test da verde sin haber probado
        // NFR-01. Falla acá, con el número real, en vez de mentir más abajo.
        var sembradas = await MovimientosEnLaBase.CantidadAsync();
        Assert.True(
            sembradas == MovimientosSembrados,
            $"El sembrado dejó {sembradas} movimientos y la medición necesita {MovimientosSembrados}: " +
            "sin la tabla poblada el p95 no dice nada del costo real del índice.");

        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        // Descarta el costo de arranque (JIT, primer plan de consulta, pool de conexiones), que no
        // es el tiempo de guardado que mide el criterio.
        using (var calentamiento = await cliente.PostAsync("/api/movimientos", Cuerpo()))
        {
            Assert.Equal(HttpStatusCode.Created, calentamiento.StatusCode);
        }

        var duraciones = new List<TimeSpan>(Ejecuciones);
        for (var i = 0; i < Ejecuciones; i++)
        {
            var reloj = Stopwatch.StartNew();
            using var respuesta = await cliente.PostAsync("/api/movimientos", Cuerpo());
            reloj.Stop();

            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
            duraciones.Add(reloj.Elapsed);
        }

        var p95 = Percentil(duraciones, 95);
        Assert.True(
            p95 < PresupuestoP95,
            $"El p95 del alta fue {p95.TotalMilliseconds:F0} ms sobre {Ejecuciones} ejecuciones; " +
            $"el presupuesto de NFR-01 es {PresupuestoP95.TotalMilliseconds:F0} ms.");
    }

    private static TimeSpan Percentil(IReadOnlyCollection<TimeSpan> duraciones, int percentil)
    {
        var ordenadas = duraciones.Order().ToList();
        // Método del rango más cercano: el menor valor por debajo del cual cae al menos el
        // percentil pedido de las muestras.
        var indice = (int)Math.Ceiling(percentil / 100d * ordenadas.Count) - 1;
        return ordenadas[Math.Clamp(indice, 0, ordenadas.Count - 1)];
    }

    private static StringContent Cuerpo() => new(
        JsonSerializer.Serialize(
            new { categoriaId = CategoriaComidaId, monto = 10.5m, fecha = "2026-03-01", nota = "medición" },
            Json),
        Encoding.UTF8,
        "application/json");

    private async Task SembrarMovimientosAsync()
    {
        await using var contexto = baseDeDatos.CrearContexto();
        var usuarioId = await new UsuarioSemillaActual(contexto).ObtenerIdAsync();
        contexto.UsuarioActualId = usuarioId;

        for (var i = 0; i < MovimientosSembrados; i++)
        {
            contexto.Movimientos.Add(new Movimiento
            {
                UsuarioId = usuarioId,
                CategoriaId = CategoriaComidaId,
                Tipo = TipoMovimiento.Gasto,
                Monto = 100m + i,
                Moneda = Moneda.Predeterminada,
                Fecha = new DateOnly(2026, 1, 1).AddDays(i % 365),
                Nota = null,
                CreadoEn = DateTime.UtcNow,
            });
        }

        await contexto.SaveChangesAsync();
    }
}
