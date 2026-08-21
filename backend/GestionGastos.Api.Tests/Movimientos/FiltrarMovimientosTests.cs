using System.Net;
using System.Text.Json;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;
using GestionGastos.Api.Tests.Infra;
using MySqlConnector;

namespace GestionGastos.Api.Tests.Movimientos;

[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class FiltrarMovimientosTests(BaseDeDatosFixture baseDeDatos)
{
    private const string Ruta = "/api/movimientos";
    private const int CategoriaComidaId = 1;
    private const int CategoriaTransporteId = 2;
    private const int CategoriaSueldoId = 8;

    /// <summary>Tabla que se busca en el SQL observado, para aislar las consultas del listado.</summary>
    private const string TablaDeMovimientos = "movimientos";

    /// <summary>Techo de la mitigación R-07, que este ticket conserva.</summary>
    private const int TechoEsperado = 500;

    // ---------------------------------------------------------------- camino feliz

    [Fact]
    public async Task Filtrar_PorCategoria_DevuelveSoloEsaCategoria()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 3, 1), "comida-uno"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaTransporteId, 20m, new DateOnly(2026, 3, 2), "transporte"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 30m, new DateOnly(2026, 3, 3), "comida-dos"));

        var listado = await ListarAsync($"?categoriaId={CategoriaComidaId}");

        // AC-07: únicamente movimientos de esa categoría.
        var items = Items(listado);
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(CategoriaComidaId, i.GetProperty("categoria").GetProperty("id").GetInt32()));
        Assert.DoesNotContain("transporte", items.Select(i => i.GetProperty("nota").GetString()!));
        // El total se cuenta sobre lo filtrado, no sobre los tres sembrados (mitigación R-14).
        Assert.Equal(2, listado.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Filtrar_SinCategoria_DevuelveTodasLasCategorias()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 3, 1), "comida"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaTransporteId, 20m, new DateOnly(2026, 3, 2), "transporte"),
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 30m, new DateOnly(2026, 3, 3), "sueldo"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 40m, new DateOnly(2026, 7, 1), "fuera-del-rango"));

        // Sin ningún parámetro, el endpoint sigue devolviendo todo lo del propietario, igual que en
        // FEAT-001a: ausencia de parámetro = sin ese filtro, nunca "categoría cero".
        var sinParametros = await ListarAsync();
        Assert.Equal(4, Items(sinParametros).Count);
        Assert.Equal(4, sinParametros.GetProperty("total").GetInt32());

        // AC-08: con un rango aplicado pero sin tocar el filtro de categoría, vienen movimientos de
        // TODAS las categorías. La omisión de `categoriaId` no puede arrastrar al otro filtro: si
        // fuera un test sin parámetros, daría verde con un endpoint que ignora los tres.
        var listado = await ListarAsync("?desde=2026-03-01&hasta=2026-03-31");

        var items = Items(listado);
        Assert.Equal(3, items.Count);
        Assert.Equal(
            new[] { CategoriaSueldoId, CategoriaTransporteId, CategoriaComidaId },
            items.Select(i => i.GetProperty("categoria").GetProperty("id").GetInt32()).ToArray());
        Assert.Equal(3, listado.GetProperty("total").GetInt32());
        Assert.DoesNotContain("fuera-del-rango", items.Select(i => i.GetProperty("nota").GetString()!));
    }

    [Fact]
    public async Task Filtrar_PorRango_IncluyeAmbosExtremos()
    {
        await baseDeDatos.LimpiarAsync();
        // Con fecha EXACTAMENTE igual a cada extremo: el error clásico del rango cerrado es dejar
        // afuera al de arriba por comparar contra una fecha con hora.
        await SembrarAsync(
            // Los vecinos inmediatos de cada extremo, que quedan afuera: sin ellos el test daría
            // verde también con un endpoint que ignora el rango.
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 5m, new DateOnly(2026, 2, 28), "vispera"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 3, 1), "extremo-inferior"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 20m, new DateOnly(2026, 3, 15), "del-medio"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 30m, new DateOnly(2026, 3, 31), "extremo-superior"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 40m, new DateOnly(2026, 4, 1), "dia-siguiente"));

        var listado = await ListarAsync("?desde=2026-03-01&hasta=2026-03-31");

        // AC-10: los dos extremos incluidos.
        var items = Items(listado);
        Assert.Equal(
            new[] { "extremo-superior", "del-medio", "extremo-inferior" },
            items.Select(i => i.GetProperty("nota").GetString()).ToArray());
        Assert.Equal(3, listado.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Filtrar_PorRango_ExcluyeLoDeAfuera()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 2, 28), "un-dia-antes"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 20m, new DateOnly(2026, 3, 10), "adentro"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 30m, new DateOnly(2026, 4, 1), "un-dia-despues"));

        var listado = await ListarAsync("?desde=2026-03-01&hasta=2026-03-31");

        // AC-14: ninguno de los excluidos aparece en la respuesta.
        var fila = Assert.Single(Items(listado));
        Assert.Equal("adentro", fila.GetProperty("nota").GetString());
        var notas = Items(listado).Select(i => i.GetProperty("nota").GetString()!).ToList();
        Assert.DoesNotContain("un-dia-antes", notas);
        Assert.DoesNotContain("un-dia-despues", notas);
        Assert.Equal(1, listado.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Filtrar_PorCategoriaYRango_AplicaLasDosCondiciones()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            // Cumple las dos.
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 3, 10), "cumple-las-dos"),
            // Cumple la categoría pero no el rango.
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 20m, new DateOnly(2026, 5, 10), "solo-categoria"),
            // Cumple el rango pero no la categoría.
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaTransporteId, 30m, new DateOnly(2026, 3, 12), "solo-rango"),
            // No cumple ninguna.
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 40m, new DateOnly(2026, 6, 1), "ninguna"));

        var listado = await ListarAsync($"?categoriaId={CategoriaComidaId}&desde=2026-03-01&hasta=2026-03-31");

        // AC-11: las dos condiciones a la vez, no una u otra.
        var fila = Assert.Single(Items(listado));
        Assert.Equal("cumple-las-dos", fila.GetProperty("nota").GetString());
        Assert.Equal(1, listado.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Filtrar_ElTotalYElRecorte_SeCuentanSobreLoFiltrado()
    {
        await baseDeDatos.LimpiarAsync();
        const int sembrados = TechoEsperado + 1;
        var primerDia = new DateOnly(2025, 1, 1);
        await SembrarAsync(Enumerable.Range(0, sembrados)
            .Select(i => NuevoMovimiento(
                TipoMovimiento.Gasto,
                CategoriaComidaId,
                10m + i,
                primerDia.AddDays(i),
                null))
            .ToArray());

        // Premisa: sin filtro, el universo del propietario SÍ supera el techo y `recortado` es true.
        // Sin esta mitad, un `total` que contara el universo sin filtrar no se distinguiría.
        var sinFiltro = await ListarAsync();
        Assert.Equal(sembrados, sinFiltro.GetProperty("total").GetInt32());
        Assert.True(sinFiltro.GetProperty("recortado").GetBoolean());

        var listado = await ListarAsync("?desde=2025-01-01&hasta=2025-01-05");

        // Cinco días, cinco movimientos: `total` describe el universo FILTRADO (mitigación R-14)…
        Assert.Equal(5, Items(listado).Count);
        Assert.Equal(5, listado.GetProperty("total").GetInt32());
        // …y por eso `recortado` no miente con un filtro angosto.
        Assert.False(listado.GetProperty("recortado").GetBoolean());
    }

    [Fact]
    public async Task Filtrar_EmiteElWhereEnSql()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 3, 10), "adentro"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaTransporteId, 20m, new DateOnly(2026, 8, 10), "afuera"));

        var sentencias = new RegistroDeSentencias();
        var listado = await ObservadorDeSql.GetObservandoElSqlAsync(
            $"{Ruta}?categoriaId={CategoriaComidaId}&desde=2026-03-01&hasta=2026-03-31",
            sentencias);

        Assert.Single(Items(listado));

        // Capa estructural (mitigación R-13): un filtrado hecho en memoria daría verde en las
        // aserciones conductuales de los otros tests, porque la respuesta sería la misma. Lo único
        // que distingue "se filtró en la base" de "se filtró después de traer todo" es el SQL.
        var consultas = ConsultasSobreMovimientos(sentencias);
        Assert.True(consultas.Count >= 2, $"Se esperaban al menos dos consultas (el COUNT y el SELECT); hubo {consultas.Count}.");

        foreach (var consulta in consultas)
        {
            // Cada consulta que toca la tabla —el COUNT del total incluido, porque `total` describe
            // el universo filtrado— lleva las tres condiciones.
            Assert.Matches(@"`categoria_id`\s*=", consulta);
            Assert.Matches(@"`fecha`\s*>=", consulta);
            Assert.Matches(@"`fecha`\s*<=", consulta);
        }
    }

    // ---------------------------------------------------------------- caminos tristes

    [Fact]
    public async Task Filtrar_ConDesdePosteriorAHasta_Devuelve400()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 3, 10), "uno"));

        var respuesta = await PedirAsync("?desde=2026-03-31&hasta=2026-03-01");

        // AC-12 y FR-06: el rango invertido se rechaza con el motivo, y el listado no se ejecuta.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.Estado);
        Assert.Equal("application/problem+json", respuesta.TipoDeContenido);
        var errores = respuesta.Cuerpo.GetProperty("errors");
        Assert.True(errores.TryGetProperty("desde", out var mensajes), $"El 400 no trae 'errors.desde': {respuesta.Texto}");
        Assert.NotEmpty(mensajes.EnumerateArray());
        Assert.False(respuesta.Cuerpo.TryGetProperty("items", out _), "Un rechazo de validación no devuelve listado.");
    }

    [Fact]
    public async Task Filtrar_ConFechaMalFormada_Devuelve400ConElCampo()
    {
        await baseDeDatos.LimpiarAsync();

        var desde = await PedirAsync("?desde=01/02/2026");

        Assert.Equal(HttpStatusCode.BadRequest, desde.Estado);
        var erroresDeDesde = desde.Cuerpo.GetProperty("errors");
        Assert.True(erroresDeDesde.TryGetProperty("desde", out var mensajeDeDesde), $"Falta 'errors.desde': {desde.Texto}");
        // El mensaje nombra el formato esperado y está redactado a mano: nunca el texto de la
        // excepción del parseo (mitigación R-21).
        Assert.Contains("yyyy-MM-dd", string.Join(" ", mensajeDeDesde.EnumerateArray().Select(m => m.GetString())), StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", desde.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", desde.Texto, StringComparison.Ordinal);

        // El campo del error es el que vino mal, no uno genérico.
        var hasta = await PedirAsync("?hasta=basura");
        Assert.Equal(HttpStatusCode.BadRequest, hasta.Estado);
        var erroresDeHasta = hasta.Cuerpo.GetProperty("errors");
        Assert.True(erroresDeHasta.TryGetProperty("hasta", out _), $"Falta 'errors.hasta': {hasta.Texto}");
        Assert.False(erroresDeHasta.TryGetProperty("desde", out _), "Se reportó un campo que llegó bien.");

        // Fuera del rango que el DATE de MySQL admite: parsea, pero la base lo rechazaría con un
        // 500 donde el contrato promete 400 (mitigación R-20).
        var fueraDeRango = await PedirAsync("?desde=0500-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, fueraDeRango.Estado);
        Assert.True(
            fueraDeRango.Cuerpo.GetProperty("errors").TryGetProperty("desde", out _),
            $"Falta 'errors.desde' para la fecha fuera de rango: {fueraDeRango.Texto}");
    }

    [Fact]
    public async Task Filtrar_ConFechaVacia_LaTrataComoAusente()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 3, 1), "uno"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaTransporteId, 20m, new DateOnly(2026, 7, 15), "dos"));

        // Decisión de contrato: `?desde=` sin valor es el extremo AUSENTE, no una fecha con formato
        // inválido. Es lo que manda un formulario con el campo vacío, y el Block 4 depende de que
        // limpiar el filtro en la UI no se convierta en un 400.
        var vacias = await PedirAsync("?desde=&hasta=");

        Assert.Equal(HttpStatusCode.OK, vacias.Estado);
        Assert.False(
            vacias.Cuerpo.TryGetProperty("errors", out _),
            $"El valor vacío se reportó como entrada inválida: {vacias.Texto}");

        // Y "ausente" significa sin recortar el universo: el mismo listado completo que sin
        // parámetros. Sin esta mitad, un endpoint que tratara el vacío como una fecha mínima
        // absurda —o que devolviera vacío— daría verde con solo el 200.
        var items = Items(vacias.Cuerpo);
        Assert.Equal(2, items.Count);
        Assert.Equal(
            new[] { "dos", "uno" },
            items.Select(i => i.GetProperty("nota").GetString()).ToArray());
        Assert.Equal(2, vacias.Cuerpo.GetProperty("total").GetInt32());

        // Los blancos son el mismo caso que el vacío: `IsNullOrWhiteSpace`, no `IsNullOrEmpty`.
        var enBlanco = await PedirAsync("?desde=%20&hasta=%20%20");
        Assert.Equal(HttpStatusCode.OK, enBlanco.Estado);
        Assert.Equal(2, Items(enBlanco.Cuerpo).Count);

        // Contraste: lo vacío pasa, pero lo que trae texto y no parsea sigue siendo 400. Sin este
        // par, "acepta todo" sería indistinguible de "trata el vacío como ausente".
        var conTexto = await PedirAsync("?desde=%20nada%20");
        Assert.Equal(HttpStatusCode.BadRequest, conTexto.Estado);
        Assert.True(
            conTexto.Cuerpo.GetProperty("errors").TryGetProperty("desde", out _),
            $"Falta 'errors.desde' para un valor no vacío que no parsea: {conTexto.Texto}");
    }

    [Fact]
    public async Task Filtrar_ConCategoriaInexistente_DevuelveListadoVacio()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 3, 10), "uno"));

        // Filtrar por algo que no existe no es una petición malformada: es una consulta sin
        // resultados. El 400 se reserva para la entrada inválida.
        var listado = await ListarAsync("?categoriaId=987654");

        Assert.Empty(Items(listado));
        Assert.Equal(0, listado.GetProperty("total").GetInt32());
        Assert.False(listado.GetProperty("recortado").GetBoolean());

        // Un id menor o igual a cero, en cambio, sí es entrada inválida.
        var cero = await PedirAsync("?categoriaId=0");
        Assert.Equal(HttpStatusCode.BadRequest, cero.Estado);
        Assert.True(
            cero.Cuerpo.GetProperty("errors").TryGetProperty("categoriaId", out _),
            $"Falta 'errors.categoriaId': {cero.Texto}");

        var negativo = await PedirAsync("?categoriaId=-1");
        Assert.Equal(HttpStatusCode.BadRequest, negativo.Estado);
        Assert.True(
            negativo.Cuerpo.GetProperty("errors").TryGetProperty("categoriaId", out _),
            $"Falta 'errors.categoriaId': {negativo.Texto}");
    }

    [Fact]
    public async Task Listar_ConFalloDeBase_DevuelveProblemDetailsCon500()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, new DateOnly(2026, 3, 10), "adentro"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaTransporteId, 20m, new DateOnly(2026, 9, 10), "afuera"));
        var consulta = $"{Ruta}?categoriaId={CategoriaComidaId}&desde=2026-03-01&hasta=2026-03-31";

        // Precondición: con la base sana esa misma query es un 200 Y aplicó los filtros. Sin la
        // segunda mitad, un endpoint que ni siquiera conoce los parámetros daría verde: con la base
        // caída todo termina en 500 igual, y el test no probaría la rama nueva.
        await using (var sana = new ApiFactory())
        {
            using var cliente = sana.CreateClient();
            using var ok = await cliente.GetAsync(consulta);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            var listado = await JsonDeRespuesta.LeerAsync(ok);
            var fila = Assert.Single(Items(listado));
            Assert.Equal("adentro", fila.GetProperty("nota").GetString());
        }

        await using var fabrica = new ApiFactory(ApiFactory.CadenaHaciaUnPuertoCerrado);
        using var clienteConBaseCaida = fabrica.CreateClient();

        using var respuesta = await clienteConBaseCaida.GetAsync(consulta);

        // La rama nueva de filtros no esquiva el manejador global: sigue saliendo ProblemDetails.
        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            fabrica.ExcepcionesRegistradas,
            e => Excepciones.Desenrollar(e).OfType<MySqlException>().Any());

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.True(JsonDeRespuesta.Raiz(cuerpo).TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
        Assert.DoesNotContain("stackTrace", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("MySql", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- utilidades

    private sealed record RespuestaObservada(
        HttpStatusCode Estado,
        string? TipoDeContenido,
        string Texto,
        JsonElement Cuerpo);

    private static async Task<JsonElement> ListarAsync(string consulta = "")
    {
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        using var respuesta = await cliente.GetAsync(Ruta + consulta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await JsonDeRespuesta.LeerAsync(respuesta);
    }

    private static async Task<RespuestaObservada> PedirAsync(string consulta)
    {
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        using var respuesta = await cliente.GetAsync(Ruta + consulta);

        var texto = await respuesta.Content.ReadAsStringAsync();
        return new RespuestaObservada(
            respuesta.StatusCode,
            respuesta.Content.Headers.ContentType?.MediaType,
            texto,
            JsonDeRespuesta.Raiz(texto));
    }

    /// <summary>Sentencias observadas que leyeron la tabla de movimientos.</summary>
    private static List<string> ConsultasSobreMovimientos(RegistroDeSentencias sentencias) =>
        sentencias.Sentencias
            .Where(s => s.Contains($"FROM `{TablaDeMovimientos}`", StringComparison.Ordinal))
            .ToList();

    private static List<JsonElement> Items(JsonElement listado)
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
}
