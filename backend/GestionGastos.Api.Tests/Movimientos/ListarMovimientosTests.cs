using System.Globalization;
using System.Net;
using System.Text.Json;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;
using GestionGastos.Api.Tests.Infra;
using MySqlConnector;

namespace GestionGastos.Api.Tests.Movimientos;

[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class ListarMovimientosTests(BaseDeDatosFixture baseDeDatos)
{
    private const string Ruta = "/api/movimientos";
    private const int CategoriaComidaId = 1;
    private const int CategoriaSueldoId = 8;

    /// <summary>Tabla que se busca en el SQL observado, para aislar el ORDER BY del listado.</summary>
    private const string TablaDeMovimientos = "movimientos";

    /// <summary>Techo de la mitigación R-07: el listado no devuelve más que esto.</summary>
    private const int TechoEsperado = 500;

    // ---------------------------------------------------------------- camino feliz

    [Fact]
    public async Task Listar_DevuelveGastosEIngresosJuntos()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 1500.50m, new DateOnly(2026, 3, 2), "Almuerzo"),
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 900000m, new DateOnly(2026, 3, 1), "Sueldo"));

        var listado = await ListarAsync();

        var items = Items(listado);
        Assert.Equal(2, items.Count);
        // Los dos tipos en la MISMA respuesta, sin segundo endpoint ni filtro (AC-15).
        Assert.Equal(new[] { "gasto", "ingreso" }, items.Select(i => i.GetProperty("tipo").GetString()).ToArray());
        Assert.Equal(2, listado.GetProperty("total").GetInt32());
        Assert.False(listado.GetProperty("recortado").GetBoolean());
    }

    [Fact]
    public async Task Listar_OrdenaPorFechaDescendente()
    {
        await baseDeDatos.LimpiarAsync();
        // Se siembran desordenadas a propósito: si el endpoint no ordena, salen en orden de id.
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 1, 5), "enero"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 20m, new DateOnly(2026, 3, 10), "marzo"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 30m, new DateOnly(2026, 2, 1), "febrero"));

        var sentencias = new RegistroDeSentencias();
        var items = Items(await ObservadorDeSql.GetObservandoElSqlAsync(Ruta, sentencias));

        Assert.Equal(
            new[] { "2026-03-10", "2026-02-01", "2026-01-05" },
            items.Select(i => i.GetProperty("fecha").GetString()).ToArray());
        Assert.Equal(
            new[] { "marzo", "febrero", "enero" },
            items.Select(i => i.GetProperty("nota").GetString()).ToArray());

        // El orden por fecha tiene que estar EN LA CONSULTA, por lo mismo que se explica en
        // Listar_DesempataPorIdDescendente: con estas tres filas el índice
        // ix_movimientos_usuario_fecha_id ya las entrega en fecha descendente, así que las
        // aserciones de arriba pasarían aunque el endpoint no pidiera orden alguno. Se afirma acá y
        // no de rebote en el test del desempate: apoyarse en el guard de OTRO test es un
        // acoplamiento implícito que se pierde en cuanto alguien toca aquel.
        Assert.Matches(@"`fecha`\s+DESC", OrdenamientoEmitido(sentencias));
    }

    [Fact]
    public async Task Listar_DesempataPorIdDescendente()
    {
        await baseDeDatos.LimpiarAsync();
        var mismoDia = new DateOnly(2026, 3, 1);
        // Los tres del mismo día van INTERCALADOS entre otros de fechas distintas, y no seguidos:
        // sembrados en bloque, el orden natural del motor los devuelve por id descendente de casualidad
        // y el test daría verde aunque el endpoint no pidiera desempate alguno. Intercalados, la
        // ausencia del ThenByDescending deja el grupo en orden ascendente y se nota.
        var ids = await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, mismoDia, "primero"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 40m, new DateOnly(2026, 3, 5), "otro-dia-nuevo"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 20m, mismoDia, "segundo"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 50m, new DateOnly(2026, 3, 3), "otro-dia-medio"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 30m, mismoDia, "tercero"));

        var sentencias = new RegistroDeSentencias();
        var items = Items(await ObservadorDeSql.GetObservandoElSqlAsync(Ruta, sentencias));

        // Valor exacto y no "está ordenado": el desempate por id descendente es el contrato.
        Assert.Equal(
            new[] { "otro-dia-nuevo", "otro-dia-medio", "tercero", "segundo", "primero" },
            items.Select(i => i.GetProperty("nota").GetString()).ToArray());

        // Solo el grupo empatado, comparado contra sus ids reales en descendente.
        var idsDelMismoDia = new[] { ids[0], ids[2], ids[4] };
        var idsDevueltosDelMismoDia = items
            .Where(i => i.GetProperty("fecha").GetString() == "2026-03-01")
            .Select(i => i.GetProperty("id").GetInt32())
            .ToArray();
        Assert.Equal(idsDelMismoDia.OrderByDescending(id => id).ToArray(), idsDevueltosDelMismoDia);

        // Y el desempate tiene que estar EN LA CONSULTA. Las aserciones de arriba, solas, no lo
        // prueban a esta escala: el índice ix_movimientos_usuario_fecha_id es
        // (usuario_id, fecha DESC, id DESC), así que con pocas filas MySQL sirve el ORDER BY
        // recorriéndolo sin filesort y devuelve los empates en id descendente aunque la consulta no
        // lo pida — el verde lo firmaría el plan de ejecución y no el contrato.
        //
        // Se PODRÍA matar por comportamiento: pasadas ~120 filas del propietario el optimizador
        // abandona el índice y cae a scan + filesort, y ahí la ausencia del desempate se ve en la
        // respuesta. Pero eso obliga a apoyarse en el orden que el filesort le da a los empates, que
        // MySQL no especifica y que se observó inestable según el tamaño (ascendente con 13 filas,
        // descendente con 23/33/53, ascendente otra vez con 103). Un test así falla ABIERTO: el día
        // que el plan cambie vuelve a verde solo, sin que nadie se entere. La aserción sobre la
        // consulta no depende del plan, y es la que cierra el "orden estable entre llamadas" de la
        // spec.
        var ordenamiento = OrdenamientoEmitido(sentencias);
        Assert.Matches(@"`fecha`\s+DESC", ordenamiento);
        Assert.Matches(@"`id`\s+DESC", ordenamiento);
        Assert.True(
            ordenamiento.IndexOf("`fecha`", StringComparison.Ordinal)
            < ordenamiento.IndexOf("`id`", StringComparison.Ordinal),
            $"La fecha tiene que ordenar antes que el id; el ORDER BY emitido fue: {ordenamiento}");
    }

    [Fact]
    public async Task Listar_CadaFilaTraeLosCincoDatos()
    {
        await baseDeDatos.LimpiarAsync();
        var ids = await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 1234.56m, new DateOnly(2026, 3, 1), "Almuerzo"));

        var fila = Assert.Single(Items(await ListarAsync()));

        // AC-16: fecha, tipo, nombre de categoría, monto con moneda y nota. Valores exactos, porque
        // verificar que el campo existe no verifica que traiga lo que corresponde.
        Assert.Equal(ids[0], fila.GetProperty("id").GetInt32());
        Assert.Equal("2026-03-01", fila.GetProperty("fecha").GetString());
        Assert.Equal("gasto", fila.GetProperty("tipo").GetString());
        Assert.Equal(CategoriaComidaId, fila.GetProperty("categoria").GetProperty("id").GetInt32());
        Assert.Equal("Comida", fila.GetProperty("categoria").GetProperty("nombre").GetString());
        Assert.Equal(1234.56m, fila.GetProperty("monto").GetDecimal());
        Assert.Equal("ARS", fila.GetProperty("moneda").GetString());
        Assert.Equal(Moneda.Predeterminada, fila.GetProperty("moneda").GetString());
        Assert.Equal("Almuerzo", fila.GetProperty("nota").GetString());
    }

    [Fact]
    public async Task Listar_ExcluyeMovimientosDeOtroPropietario()
    {
        await baseDeDatos.LimpiarAsync();
        var propios = await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 3, 1), "propio"));
        var idAjeno = await SembrarDeOtroPropietarioAsync();

        // La fila ajena está en la tabla: sin esto, el test daría verde con la base vacía.
        Assert.Equal(2, await CantidadDeMovimientosAsync());

        var listado = await ListarAsync();

        var fila = Assert.Single(Items(listado));
        Assert.Equal(propios[0], fila.GetProperty("id").GetInt32());
        Assert.NotEqual(idAjeno, fila.GetProperty("id").GetInt32());
        // `total` es el del propietario, no el de la tabla (AC-02).
        Assert.Equal(1, listado.GetProperty("total").GetInt32());
        // Sobre las propiedades y no sobre la cadena cruda: el monto ajeno como subcadena podría
        // coincidir con un id autoincremental y haría fallar el test sin que nada esté roto.
        Assert.Equal(10m, fila.GetProperty("monto").GetDecimal());
        Assert.DoesNotContain(999m, Items(listado).Select(i => i.GetProperty("monto").GetDecimal()));
        Assert.DoesNotContain("ajeno", Items(listado).Select(i => i.GetProperty("nota").GetString()!));
    }

    // ---------------------------------------------------------------- caminos tristes

    [Fact]
    public async Task Listar_SinMovimientos_DevuelveListaVaciaNo404()
    {
        await baseDeDatos.LimpiarAsync();
        Assert.Equal(0, await CantidadDeMovimientosAsync());

        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        using var respuesta = await cliente.GetAsync(Ruta);

        // Cero movimientos es una respuesta válida, nunca un error.
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var listado = await LeerJsonAsync(respuesta);
        Assert.Empty(Items(listado));
        Assert.False(listado.GetProperty("recortado").GetBoolean());
        Assert.Equal(0, listado.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Listar_ConMasDeQuinientos_RecortaYLoSeñala()
    {
        await baseDeDatos.LimpiarAsync();
        const int sembrados = TechoEsperado + 1;
        var primerDia = new DateOnly(2025, 1, 1);
        // Una fecha distinta por movimiento: el orden queda inequívoco y se puede afirmar CUÁLES son
        // los 500 devueltos, no solo cuántos.
        await SembrarAsync(Enumerable.Range(0, sembrados)
            .Select(i => NuevoMovimiento(
                TipoMovimiento.Gasto,
                CategoriaComidaId,
                10m + i,
                primerDia.AddDays(i),
                null))
            .ToArray());

        var listado = await ListarAsync();

        var items = Items(listado);
        Assert.Equal(TechoEsperado, items.Count);
        Assert.True(listado.GetProperty("recortado").GetBoolean());
        // `total` cuenta lo que el propietario tiene, no lo que se devolvió: es lo que permite al
        // frontend avisar del recorte en vez de mentir por omisión.
        Assert.Equal(sembrados, listado.GetProperty("total").GetInt32());

        // Los devueltos son los MÁS RECIENTES, no 500 cualesquiera.
        var esperadas = Enumerable.Range(0, sembrados)
            .Select(i => primerDia.AddDays(i))
            .OrderByDescending(f => f)
            .Take(TechoEsperado)
            .Select(f => f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .ToArray();
        Assert.Equal(esperadas, items.Select(i => i.GetProperty("fecha").GetString()).ToArray());
        // El único recortado es el más viejo.
        Assert.DoesNotContain(
            primerDia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            items.Select(i => i.GetProperty("fecha").GetString()!));
    }

    [Fact]
    public async Task Listar_ConNotaNula_DevuelveNullNoCadenaVacia()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 3, 1), null));

        var fila = Assert.Single(Items(await ListarAsync()));

        Assert.Equal(JsonValueKind.Null, fila.GetProperty("nota").ValueKind);
    }

    [Fact]
    public async Task Listar_FalloDeBase_DevuelveProblemDetails500SinStackTrace()
    {
        await baseDeDatos.LimpiarAsync();

        // Precondición: la ruta existe. Con la base caída todo termina en 500, así que sin esto el
        // test daría verde incluso sin endpoint.
        await using (var sana = new ApiFactory())
        {
            using var cliente = sana.CreateClient();
            using var ok = await cliente.GetAsync(Ruta);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        await using var fabrica = new ApiFactory(ApiFactory.CadenaHaciaUnPuertoCerrado);
        using var clienteConBaseCaida = fabrica.CreateClient();

        using var respuesta = await clienteConBaseCaida.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        // Correlación: sin esto el test daría verde con cualquier 500 de otra causa.
        Assert.Contains(
            fabrica.ExcepcionesRegistradas,
            e => Desenrollar(e).OfType<MySqlException>().Any());

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        using var documento = JsonDocument.Parse(cuerpo);
        Assert.True(documento.RootElement.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));

        Assert.DoesNotContain("stackTrace", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("MySql", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Listar_ConParametrosDesconocidos_LosIgnora()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 3, 2), "uno"),
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 20m, new DateOnly(2026, 3, 1), "dos"));

        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        using var respuesta = await cliente.GetAsync($"{Ruta}?foo=bar&desde=basura&limite=1");

        // Los filtros llegan en FEAT-001b: hasta entonces se ignoran, no se rechaza la petición ni
        // se recorta el listado.
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var listado = await LeerJsonAsync(respuesta);
        var items = Items(listado);
        Assert.Equal(2, items.Count);
        Assert.Equal(
            new[] { "uno", "dos" },
            items.Select(i => i.GetProperty("nota").GetString()).ToArray());
        Assert.Equal(2, listado.GetProperty("total").GetInt32());
    }

    // ---------------------------------------------------------------- utilidades

    private static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        // Clone: el JsonDocument se descarta al salir y el elemento quedaría apuntando a memoria
        // devuelta al pool.
        using var documento = JsonDocument.Parse(cuerpo);
        return documento.RootElement.Clone();
    }

    private static async Task<JsonElement> ListarAsync()
    {
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        using var respuesta = await cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await LeerJsonAsync(respuesta);
    }

    /// <summary>Cláusula <c>ORDER BY</c> que el listado emitió en el request observado.</summary>
    private static string OrdenamientoEmitido(RegistroDeSentencias sentencias) =>
        ObservadorDeSql.OrderByDe(sentencias, TablaDeMovimientos);

    private static IReadOnlyList<JsonElement> Items(JsonElement listado)
    {
        Assert.True(listado.TryGetProperty("items", out var items), "La respuesta no trae 'items'.");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        return items.EnumerateArray().ToList();
    }

    private static Movimiento NuevoMovimiento(
        TipoMovimiento tipo,
        int categoriaId,
        decimal monto,
        DateOnly fecha,
        string? nota) => new()
        {
            Tipo = tipo,
            CategoriaId = categoriaId,
            Monto = monto,
            Moneda = Moneda.Predeterminada,
            Fecha = fecha,
            Nota = nota,
            CreadoEn = DateTime.UtcNow,
        };

    /// <summary>Siembra movimientos del usuario semilla y devuelve sus ids en el orden dado.</summary>
    private async Task<IReadOnlyList<int>> SembrarAsync(params Movimiento[] movimientos)
    {
        await using var contexto = baseDeDatos.CrearContexto();
        var usuarioId = await new UsuarioSemillaActual(contexto).ObtenerIdAsync();
        contexto.UsuarioActualId = usuarioId;

        foreach (var movimiento in movimientos)
        {
            movimiento.UsuarioId = usuarioId;
        }

        contexto.Movimientos.AddRange(movimientos);
        await contexto.SaveChangesAsync();
        return movimientos.Select(m => m.Id).ToList();
    }

    private async Task<int> SembrarDeOtroPropietarioAsync()
    {
        await using var contexto = baseDeDatos.CrearContexto();
        var otro = new Usuario { Email = "otro@gestiongastos.local", CreadoEn = DateTime.UtcNow };
        contexto.Usuarios.Add(otro);
        await contexto.SaveChangesAsync();

        contexto.UsuarioActualId = otro.Id;
        var ajeno = NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 999m, new DateOnly(2026, 12, 31), "ajeno");
        ajeno.UsuarioId = otro.Id;
        contexto.Movimientos.Add(ajeno);
        await contexto.SaveChangesAsync();
        return ajeno.Id;
    }

    /// <summary>
    /// Cuenta la tabla entera, sin el filtro global de propietario. SQL constante, sin
    /// interpolación de nada que venga de afuera (mitigación R-05).
    /// </summary>
    private static async Task<int> CantidadDeMovimientosAsync()
    {
        await using var conexion = new MySqlConnection(BaseDeDatosFixture.CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand("SELECT COUNT(*) FROM movimientos", conexion);
        return Convert.ToInt32(await comando.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static IEnumerable<Exception> Desenrollar(Exception excepcion)
    {
        for (var actual = excepcion; actual is not null; actual = actual.InnerException)
        {
            yield return actual;
        }
    }
}

