using System.Net;
using System.Text;
using System.Text.Json;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;
using GestionGastos.Api.Tests.Infra;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Tests.Movimientos;

/// <summary>
/// <c>DELETE /api/movimientos/{id}</c>. La eliminación es definitiva —el PRD descarta baja lógica,
/// historial y papelera (riesgo R-19)— y localiza la fila con una lectura filtrada por propietario,
/// de modo que un movimiento ajeno sea indistinguible de uno inexistente (mitigaciones R-15 y R-17).
/// </summary>
[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class EliminarMovimientoTests(BaseDeDatosFixture baseDeDatos)
{
    private const int CategoriaComidaId = 1;
    private const int CategoriaTransporteId = 2;

    private const decimal MontoOriginal = 1234.56m;
    private const string FechaOriginal = "2026-03-05";
    private const string NotaOriginal = "Nota original";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------------------------------------------------------------- camino feliz (AC-05)

    [Fact]
    public async Task Eliminar_UnMovimientoPropio_Devuelve204()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        var id = await SembrarGastoAsync(cliente);
        Assert.Equal(1, await MovimientosEnLaBase.CantidadAsync());

        using var respuesta = await cliente.DeleteAsync($"/api/movimientos/{id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        // 204 es "sin cuerpo": un ProblemDetails o un DTO acá romperían el contrato que el cliente
        // del Block 4 asume al no llamar a response.json().
        Assert.Equal(string.Empty, await respuesta.Content.ReadAsStringAsync());

        // Definitiva, no baja lógica: la fila deja de existir en la tabla, mirada sin el filtro
        // global.
        Assert.Equal(0, await MovimientosEnLaBase.CantidadAsync());
    }

    [Fact]
    public async Task Eliminar_UnMovimientoPropio_DejaDeAparecerEnElListado()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        var idBorrado = await SembrarGastoAsync(cliente);
        var idSobreviviente = await SembrarGastoAsync(cliente, CategoriaTransporteId, 77m, "2026-03-06");

        using var eliminacion = await cliente.DeleteAsync($"/api/movimientos/{idBorrado}");
        Assert.Equal(HttpStatusCode.NoContent, eliminacion.StatusCode);

        // La mitad que importa de AC-05: "dejar de devolverlo en consultas posteriores".
        using var listado = await cliente.GetAsync("/api/movimientos");
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        var raiz = await JsonDeRespuesta.LeerAsync(listado);
        var ids = raiz.GetProperty("items").EnumerateArray()
            .Select(m => m.GetProperty("id").GetInt32())
            .ToList();
        Assert.DoesNotContain(idBorrado, ids);
        // Y solo desaparece el eliminado: el otro movimiento sigue ahí.
        Assert.Equal(new[] { idSobreviviente }, ids);
        Assert.Equal(1, raiz.GetProperty("total").GetInt32());

        using var porId = await cliente.GetAsync($"/api/movimientos/{idBorrado}");
        Assert.Equal(HttpStatusCode.NotFound, porId.StatusCode);
    }

    // ---------------------------------------------------------------- caminos tristes

    [Fact]
    public async Task Eliminar_DosVeces_DevuelveNotFoundLaSegunda()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        var id = await SembrarGastoAsync(cliente);

        using var primera = await cliente.DeleteAsync($"/api/movimientos/{id}");
        Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);

        using var segunda = await cliente.DeleteAsync($"/api/movimientos/{id}");

        // 404 y no 500: borrar lo ya borrado es un desenlace de dominio normal, no un fallo.
        Assert.Equal(HttpStatusCode.NotFound, segunda.StatusCode);
        Assert.Equal("application/problem+json", segunda.Content.Headers.ContentType?.MediaType);
        await AssertTituloDeNoEncontradoAsync(segunda);
        Assert.Empty(fabrica.ExcepcionesRegistradas);
    }

    [Fact]
    public async Task Eliminar_Inexistente_Devuelve404()
    {
        await baseDeDatos.LimpiarAsync();
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.DeleteAsync("/api/movimientos/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        // El título es el del endpoint y no el que produce el ruteo por falta de ruta: sin esta
        // aserción el test daría verde con el DELETE sin implementar.
        await AssertTituloDeNoEncontradoAsync(respuesta);
    }

    // ---------------------------------------------------------------- ajeno (AC-06, R-15)

    [Fact]
    public async Task Eliminar_DeOtroPropietario_Devuelve404YNoLoBorra()
    {
        await baseDeDatos.LimpiarAsync();
        var (otroUsuarioId, idAjeno) = await SembrarMovimientoDeOtroPropietarioAsync();

        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.DeleteAsync($"/api/movimientos/{idAjeno}");

        // Indistinguible de "no existe": mismo estado y mismo título que el inexistente.
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        await AssertTituloDeNoEncontradoAsync(respuesta);

        // Y la fila ajena sigue existiendo, intacta (mitigación R-15). Un ExecuteDelete sin la
        // lectura previa la habría borrado devolviendo el mismo 404.
        Assert.Equal(1, await MovimientosEnLaBase.CantidadAsync());
        var ajeno = await MovimientoDeAsync(otroUsuarioId, idAjeno);
        Assert.Equal(999m, ajeno.Monto);
        Assert.Equal(CategoriaComidaId, ajeno.CategoriaId);
        Assert.Equal(new DateOnly(2026, 3, 1), ajeno.Fecha);
        Assert.Equal(otroUsuarioId, ajeno.UsuarioId);
    }

    // ---------------------------------------------------------------- utilidades

    private static StringContent Cuerpo(object carga) =>
        new(JsonSerializer.Serialize(carga, Json), Encoding.UTF8, "application/json");

    /// <summary>
    /// Alta por el endpoint del alta, para que el movimiento a eliminar sea uno de verdad y no una
    /// fila armada a mano que podría no parecerse a lo que la API produce.
    /// </summary>
    private static async Task<int> SembrarGastoAsync(
        HttpClient cliente,
        int categoriaId = CategoriaComidaId,
        decimal monto = MontoOriginal,
        string fecha = FechaOriginal)
    {
        using var creacion = await cliente.PostAsync(
            "/api/movimientos",
            Cuerpo(new
            {
                categoriaId,
                monto,
                fecha,
                nota = NotaOriginal,
            }));

        Assert.Equal(HttpStatusCode.Created, creacion.StatusCode);
        return (await JsonDeRespuesta.LeerAsync(creacion)).GetProperty("id").GetInt32();
    }

    private static async Task AssertTituloDeNoEncontradoAsync(HttpResponseMessage respuesta)
    {
        var raiz = await JsonDeRespuesta.LeerAsync(respuesta);
        Assert.True(raiz.TryGetProperty("title", out var titulo), "El ProblemDetails no trae 'title'.");
        Assert.Equal("Movimiento no encontrado", titulo.GetString());
    }

    /// <summary>
    /// Lee la fila con el filtro global publicado para ese propietario, que es la única forma de
    /// mirar un movimiento ajeno sin <c>IgnoreQueryFilters</c> (ADR-003).
    /// </summary>
    private async Task<Movimiento> MovimientoDeAsync(int usuarioId, int id)
    {
        await using var contexto = baseDeDatos.CrearContexto();
        contexto.UsuarioActualId = usuarioId;
        return await contexto.Movimientos.AsNoTracking().SingleAsync(m => m.Id == id);
    }

    /// <summary>
    /// No publica <c>UsuarioActualId</c> para insertar: el filtro global se aplica a las consultas,
    /// no a los INSERT, y el propietario se fija a mano acá.
    /// </summary>
    private async Task<(int UsuarioId, int MovimientoId)> SembrarMovimientoDeOtroPropietarioAsync()
    {
        await using var contexto = baseDeDatos.CrearContexto();
        var otro = new Usuario { Email = "otro@gestiongastos.local", CreadoEn = DateTime.UtcNow };
        contexto.Usuarios.Add(otro);
        await contexto.SaveChangesAsync();

        var ajeno = new Movimiento
        {
            UsuarioId = otro.Id,
            CategoriaId = CategoriaComidaId,
            Tipo = TipoMovimiento.Gasto,
            Monto = 999m,
            Moneda = Moneda.Predeterminada,
            Fecha = new DateOnly(2026, 3, 1),
            CreadoEn = DateTime.UtcNow,
        };
        contexto.Movimientos.Add(ajeno);
        await contexto.SaveChangesAsync();
        return (otro.Id, ajeno.Id);
    }
}
