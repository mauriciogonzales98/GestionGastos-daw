using System.Net;
using System.Text.Json;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;
using GestionGastos.Api.Tests.Infra;
using MySqlConnector;

namespace GestionGastos.Api.Tests.Resumen;

[Collection(BaseDeDatosCollection.NombreDeLaColeccion)]
public sealed class ResumenTests(BaseDeDatosFixture baseDeDatos)
{
    private const string Ruta = "/api/resumen";
    private const int CategoriaComidaId = 1;
    private const int CategoriaTransporteId = 2;
    private const int CategoriaSaludId = 5;
    private const int CategoriaSueldoId = 8;

    /// <summary>Tabla que se busca en el SQL observado, para aislar las consultas del resumen.</summary>
    private const string TablaDeMovimientos = "movimientos";

    /// <summary>
    /// El mes del resumen lo fija el servidor, así que los tests siembran contra el mes en curso.
    /// El rango se calcula acá con <see cref="DateTime.DaysInMonth"/> y NO con <c>RangoDelMes</c>:
    /// usar la misma pieza que se está probando haría que un corte equivocado quedara verde por
    /// coincidir consigo mismo.
    /// </summary>
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.Today);

    private static readonly DateOnly PrimerDiaDelMes = new(Hoy.Year, Hoy.Month, 1);

    private static readonly DateOnly UltimoDiaDelMes =
        new(Hoy.Year, Hoy.Month, DateTime.DaysInMonth(Hoy.Year, Hoy.Month));

    // ---------------------------------------------------------------- camino feliz

    [Fact]
    public async Task Resumen_ConIngresosYGastos_DevuelveLosTresTotales()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 1000m, PrimerDiaDelMes, "sueldo"),
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 500.50m, PrimerDiaDelMes.AddDays(3), "extra"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 200.25m, PrimerDiaDelMes.AddDays(1), "comida"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaTransporteId, 99.75m, PrimerDiaDelMes.AddDays(2), "transporte"));

        var resumen = await ObtenerAsync();

        // AC-01: los tres números, y el balance ya resuelto por el servidor.
        Assert.Equal(1500.50m, TotalIngresado(resumen));
        Assert.Equal(300.00m, TotalGastado(resumen));
        Assert.Equal(1200.50m, Balance(resumen));
    }

    [Fact]
    public async Task Resumen_SinMovimientosEnElMes_DevuelveCerosYDesgloseVacio()
    {
        await baseDeDatos.LimpiarAsync();
        // El propietario TIENE movimientos, pero ninguno de este mes: sin ellos, el test daría verde
        // con un endpoint que devuelve ceros porque la tabla está vacía.
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 80m, PrimerDiaDelMes.AddDays(-1), "mes-anterior"),
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 900m, UltimoDiaDelMes.AddDays(1), "mes-siguiente"));

        var respuesta = await PedirAsync();

        // AC-02: un mes vacío es un 200 con ceros, no un 404 ni un error.
        Assert.Equal(HttpStatusCode.OK, respuesta.Estado);
        Assert.Equal(0m, TotalIngresado(respuesta.Cuerpo));
        Assert.Equal(0m, TotalGastado(respuesta.Cuerpo));
        Assert.Equal(0m, Balance(respuesta.Cuerpo));
        Assert.Empty(Desglose(respuesta.Cuerpo));
        // Y sigue siendo un resumen bien formado: rotulado con su período (FR-04) y sin errores.
        Assert.Equal(Hoy.Month, respuesta.Cuerpo.GetProperty("mes").GetInt32());
        Assert.Equal(Hoy.Year, respuesta.Cuerpo.GetProperty("anio").GetInt32());
        Assert.False(respuesta.Cuerpo.TryGetProperty("errors", out _), $"Un mes vacío no es un error: {respuesta.Texto}");
        Assert.DoesNotContain("mes-anterior", respuesta.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("mes-siguiente", respuesta.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resumen_ConGastosMayoresQueIngresos_DevuelveBalanceNegativo()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 100m, PrimerDiaDelMes, "sueldo"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 250.50m, PrimerDiaDelMes.AddDays(1), "comida"));

        var resumen = await ObtenerAsync();

        // AC-03 del lado del contrato: el signo lo pone el servidor, no el cliente restando.
        Assert.Equal(-150.50m, Balance(resumen));
        Assert.True(Balance(resumen) < 0m, "El balance deficitario tiene que viajar negativo.");
        Assert.Equal(100m, TotalIngresado(resumen));
        Assert.Equal(250.50m, TotalGastado(resumen));
    }

    [Fact]
    public async Task Resumen_ConVariasCategorias_DevuelveUnTotalPorCategoriaConGastos()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, PrimerDiaDelMes, "comida-uno"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 30m, PrimerDiaDelMes.AddDays(1), "comida-dos"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaTransporteId, 20m, PrimerDiaDelMes.AddDays(2), "transporte"));

        var resumen = await ObtenerAsync();

        // AC-04: una fila por categoría con gastos, con la suma de sus montos del mes.
        var porCategoria = DesglosePorNombre(resumen);
        Assert.Equal(2, porCategoria.Count);
        Assert.Equal(40m, porCategoria["Comida"]);
        Assert.Equal(20m, porCategoria["Transporte"]);
        // El id de la categoría viaja junto al nombre: el frontend no tiene que resolverlo de nuevo.
        Assert.Contains(Desglose(resumen), c => c.GetProperty("categoriaId").GetInt32() == CategoriaComidaId);
        Assert.Contains(Desglose(resumen), c => c.GetProperty("categoriaId").GetInt32() == CategoriaTransporteId);
    }

    [Fact]
    public async Task Resumen_ConCategoriaSinGastosEnElMes_NoLaIncluyeEnElDesglose()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, PrimerDiaDelMes, "comida-del-mes"),
            // Categoría con gastos, pero de otro mes: no se lista.
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaTransporteId, 20m, PrimerDiaDelMes.AddDays(-1), "transporte-viejo"),
            // Categoría de ingreso: el desglose es solo de gastos (FR-02).
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 900m, PrimerDiaDelMes.AddDays(1), "sueldo"));

        var resumen = await ObtenerAsync();

        // AC-04, la mitad que se olvida: no aparece la categoría sin gastos EN EL MES.
        var porCategoria = DesglosePorNombre(resumen);
        var unica = Assert.Single(porCategoria);
        Assert.Equal("Comida", unica.Key);
        Assert.Equal(10m, unica.Value);
        Assert.DoesNotContain("Transporte", porCategoria.Keys);
        // Ni la categoría de un ingreso, aunque el ingreso sí cuente para el total.
        Assert.DoesNotContain("Sueldo", porCategoria.Keys);
        Assert.Equal(900m, TotalIngresado(resumen));
        // Y las categorías del catálogo que no tuvieron ningún movimiento tampoco aparecen en cero.
        Assert.DoesNotContain(Desglose(resumen), c => c.GetProperty("categoriaId").GetInt32() == CategoriaSaludId);
    }

    [Fact]
    public async Task Resumen_LaSumaDelDesglose_EsIgualAlTotalGastado()
    {
        await baseDeDatos.LimpiarAsync();
        // Montos con centavos que no cierran en redondo: si la agregación sumara en punto flotante,
        // la identidad se rompería por centavos y no por diseño.
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 33.33m, PrimerDiaDelMes, "uno"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 66.67m, PrimerDiaDelMes.AddDays(1), "dos"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaTransporteId, 0.01m, PrimerDiaDelMes.AddDays(2), "tres"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaSaludId, 1234.56m, UltimoDiaDelMes, "cuatro"),
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 5000m, PrimerDiaDelMes, "sueldo"));

        var resumen = await ObtenerAsync();

        // AC-05: la identidad exacta entre el desglose y el total, que es lo que delata dos
        // consultas desincronizadas.
        var sumaDelDesglose = Desglose(resumen).Sum(c => c.GetProperty("total").GetDecimal());
        Assert.Equal(1334.57m, TotalGastado(resumen));
        Assert.Equal(TotalGastado(resumen), sumaDelDesglose);
        Assert.Equal(3, Desglose(resumen).Count);
        // El ingreso no se coló en el desglose ni en el total gastado.
        Assert.Equal(5000m, TotalIngresado(resumen));
    }

    [Fact]
    public async Task Resumen_ConMovimientosDeOtrosMeses_LosExcluye()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            // Los vecinos inmediatos del rango, que son donde el corte se equivoca.
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 500m, PrimerDiaDelMes.AddDays(-1), "vispera"),
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 700m, PrimerDiaDelMes.AddDays(-1), "ingreso-viejo"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaTransporteId, 900m, UltimoDiaDelMes.AddDays(1), "dia-siguiente"),
            // Y bien lejos, para el caso del año entero.
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaSaludId, 1100m, PrimerDiaDelMes.AddYears(-1), "el-ano-pasado"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 25m, PrimerDiaDelMes.AddDays(2), "de-este-mes"));

        var resumen = await ObtenerAsync();

        // AC-07: ni en los totales, ni en el balance, ni en el desglose.
        Assert.Equal(25m, TotalGastado(resumen));
        Assert.Equal(0m, TotalIngresado(resumen));
        Assert.Equal(-25m, Balance(resumen));
        var unica = Assert.Single(Desglose(resumen));
        Assert.Equal(25m, unica.GetProperty("total").GetDecimal());
        Assert.Equal(CategoriaComidaId, unica.GetProperty("categoriaId").GetInt32());
    }

    [Fact]
    public async Task Resumen_ConMovimientosElPrimeroYElUltimoDiaDelMes_LosIncluye()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 11m, PrimerDiaDelMes, "primer-dia"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaTransporteId, 22m, UltimoDiaDelMes, "ultimo-dia"),
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 33m, UltimoDiaDelMes, "ingreso-ultimo-dia"));

        var resumen = await ObtenerAsync();

        // AC-08: los dos extremos adentro. Un rango medio abierto dejaría afuera al último día.
        Assert.Equal(33m, TotalGastado(resumen));
        Assert.Equal(33m, TotalIngresado(resumen));
        Assert.Equal(0m, Balance(resumen));
        var porCategoria = DesglosePorNombre(resumen);
        Assert.Equal(11m, porCategoria["Comida"]);
        Assert.Equal(22m, porCategoria["Transporte"]);
    }

    [Fact]
    public async Task Resumen_DevuelveElMesYElAnioDelPeriodo()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, PrimerDiaDelMes, "comida"));

        var resumen = await ObtenerAsync();

        // AC-09 del lado del contrato: el rótulo sale del mismo lugar que los números, y el período
        // lo fija el servidor (FR-03), no un parámetro.
        Assert.Equal(Hoy.Month, resumen.GetProperty("mes").GetInt32());
        Assert.Equal(Hoy.Year, resumen.GetProperty("anio").GetInt32());
        Assert.InRange(resumen.GetProperty("mes").GetInt32(), 1, 12);
    }

    [Fact]
    public async Task Resumen_NoDevuelveLaListaDeMovimientos()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, PrimerDiaDelMes, "nota-reveladora-uno"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 20m, PrimerDiaDelMes.AddDays(1), "nota-reveladora-dos"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 30m, PrimerDiaDelMes.AddDays(2), "nota-reveladora-tres"),
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 40m, PrimerDiaDelMes, "nota-reveladora-cuatro"));

        var respuesta = await PedirAsync();

        // AC-12: tres gastos de la misma categoría son UNA fila, no tres.
        var unica = Assert.Single(Desglose(respuesta.Cuerpo));
        Assert.Equal(60m, unica.GetProperty("total").GetDecimal());

        // La respuesta tiene exactamente los seis campos del contrato: nada que se parezca a un
        // listado de movimientos.
        var campos = respuesta.Cuerpo.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(
            new[] { "anio", "balance", "desglose", "mes", "totalGastado", "totalIngresado" },
            campos.ToArray());
        Assert.False(respuesta.Cuerpo.TryGetProperty("items", out _), "El resumen no devuelve items.");
        Assert.False(respuesta.Cuerpo.TryGetProperty("movimientos", out _), "El resumen no devuelve movimientos.");

        // Y ninguna nota ni ningún id de movimiento viajan en el cuerpo: son datos de fila, y el
        // resumen solo transporta agregados.
        Assert.DoesNotContain("nota", respuesta.Texto, StringComparison.OrdinalIgnoreCase);
        var camposDelDesglose = unica.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(new[] { "categoriaId", "categoriaNombre", "total" }, camposDelDesglose.ToArray());
    }

    [Fact]
    public async Task Resumen_EmiteLaAgregacionEnSql()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, PrimerDiaDelMes, "uno"),
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 20m, PrimerDiaDelMes.AddDays(1), "dos"),
            NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 90m, PrimerDiaDelMes, "tres"));

        var sentencias = new RegistroDeSentencias();
        var resumen = await ObservadorDeSql.GetObservandoElSqlAsync(Ruta, sentencias);

        // Premisa conductual: los números están bien. Es lo que hace que la capa estructural sea la
        // única que distingue "sumó la base" de "sumó el servidor con todo en memoria".
        Assert.Equal(30m, TotalGastado(resumen));
        Assert.Equal(90m, TotalIngresado(resumen));

        var consultas = ConsultasSobreMovimientos(sentencias);
        Assert.True(consultas.Count > 0, "Ninguna consulta observada leyó la tabla de movimientos.");

        foreach (var consulta in consultas)
        {
            // NFR-02 y mitigación R-25: la suma ocurre en la base. Sin esto, una agregación que EF
            // no logra traducir se evalúa en el cliente, trae todas las filas del propietario y
            // devuelve exactamente los mismos números.
            Assert.Contains("SUM(", consulta, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("GROUP BY", consulta, StringComparison.OrdinalIgnoreCase);
            // Y el corte del mes también viaja a la base, no se filtra después.
            Assert.Matches(@"`fecha`\s*>=", consulta);
            Assert.Matches(@"`fecha`\s*<=", consulta);
            // Ninguna consulta trae columnas de fila: si se seleccionara la nota o el id, estaría
            // materializando movimientos.
            Assert.DoesNotContain("`nota`", consulta, StringComparison.Ordinal);
        }
    }

    // ---------------------------------------------------------------- caminos tristes

    [Fact]
    public async Task Resumen_DeOtroPropietario_NoEntraEnLosTotales()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, PrimerDiaDelMes, "propio"));
        await SembrarDeOtroPropietarioAsync();

        // Las filas ajenas están en la tabla: sin esto, el test daría verde con la base vacía.
        Assert.Equal(3, await MovimientosEnLaBase.CantidadAsync());

        var respuesta = await PedirAsync();

        // AC-10 y mitigación R-24. La fuga por agregación no se ve como una fila de más sino como un
        // número más grande, así que se afirma sobre los tres totales y sobre el desglose.
        Assert.Equal(10m, TotalGastado(respuesta.Cuerpo));
        Assert.Equal(0m, TotalIngresado(respuesta.Cuerpo));
        Assert.Equal(-10m, Balance(respuesta.Cuerpo));

        var unica = Assert.Single(Desglose(respuesta.Cuerpo));
        Assert.Equal(CategoriaComidaId, unica.GetProperty("categoriaId").GetInt32());
        Assert.Equal(10m, unica.GetProperty("total").GetDecimal());
        // La categoría que solo el ajeno usó no aparece siquiera como fila.
        Assert.DoesNotContain(Desglose(respuesta.Cuerpo), c => c.GetProperty("categoriaId").GetInt32() == CategoriaSaludId);
        Assert.DoesNotContain("Salud", respuesta.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resumen_ConLaBaseCaida_Devuelve500SinDetalleInterno()
    {
        await baseDeDatos.LimpiarAsync();
        await SembrarAsync(
            NuevoMovimiento(TipoMovimiento.Gasto, CategoriaComidaId, 10m, PrimerDiaDelMes, "propio"));

        // Precondición: con la base sana la misma ruta es un 200 con los números. Sin esta mitad, un
        // endpoint inexistente daría verde igual, porque con la base caída todo termina en error.
        await using (var sana = new ApiFactory())
        {
            using var cliente = sana.CreateClient();
            using var ok = await cliente.GetAsync(Ruta);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            Assert.Equal(10m, TotalGastado(await JsonDeRespuesta.LeerAsync(ok)));
        }

        await using var fabrica = new ApiFactory(ApiFactory.CadenaHaciaUnPuertoCerrado);
        using var clienteConBaseCaida = fabrica.CreateClient();

        using var respuesta = await clienteConBaseCaida.GetAsync(Ruta);

        // Mitigación R-26: el fallo de base sale por el manejador global, como ProblemDetails.
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
        // Ni la cadena de conexión ni el puerto al que no se pudo conectar.
        Assert.DoesNotContain("3399", cuerpo, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- utilidades

    private sealed record RespuestaObservada(
        HttpStatusCode Estado,
        string? TipoDeContenido,
        string Texto,
        JsonElement Cuerpo);

    private static async Task<JsonElement> ObtenerAsync()
    {
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        using var respuesta = await cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await JsonDeRespuesta.LeerAsync(respuesta);
    }

    private static async Task<RespuestaObservada> PedirAsync()
    {
        await using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        using var respuesta = await cliente.GetAsync(Ruta);

        var texto = await respuesta.Content.ReadAsStringAsync();
        return new RespuestaObservada(
            respuesta.StatusCode,
            respuesta.Content.Headers.ContentType?.MediaType,
            texto,
            JsonDeRespuesta.Raiz(texto));
    }

    private static decimal TotalIngresado(JsonElement resumen) => resumen.GetProperty("totalIngresado").GetDecimal();

    private static decimal TotalGastado(JsonElement resumen) => resumen.GetProperty("totalGastado").GetDecimal();

    private static decimal Balance(JsonElement resumen) => resumen.GetProperty("balance").GetDecimal();

    private static IReadOnlyList<JsonElement> Desglose(JsonElement resumen)
    {
        Assert.True(resumen.TryGetProperty("desglose", out var desglose), "La respuesta no trae 'desglose'.");
        Assert.Equal(JsonValueKind.Array, desglose.ValueKind);
        return desglose.EnumerateArray().ToList();
    }

    /// <summary>
    /// El desglose indexado por nombre de categoría. Falla si una categoría aparece dos veces, que
    /// es como se vería una agregación que no agrupó (AC-12).
    /// </summary>
    private static IReadOnlyDictionary<string, decimal> DesglosePorNombre(JsonElement resumen)
    {
        var filas = Desglose(resumen)
            .Select(c => (Nombre: c.GetProperty("categoriaNombre").GetString()!, Total: c.GetProperty("total").GetDecimal()))
            .ToList();
        Assert.Equal(filas.Select(f => f.Nombre).Distinct(StringComparer.Ordinal).Count(), filas.Count);
        return filas.ToDictionary(f => f.Nombre, f => f.Total, StringComparer.Ordinal);
    }

    /// <summary>Sentencias observadas que leyeron la tabla de movimientos.</summary>
    private static IReadOnlyList<string> ConsultasSobreMovimientos(RegistroDeSentencias sentencias) =>
        sentencias.Sentencias
            .Where(s => s.Contains($"FROM `{TablaDeMovimientos}`", StringComparison.Ordinal))
            .ToList();

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

    private async Task SembrarAsync(params Movimiento[] movimientos)
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
    }

    /// <summary>
    /// Siembra un gasto y un ingreso de OTRO usuario, los dos dentro del mes en curso y con montos
    /// grandes: si el filtro global no mordiera, los tres totales cambiarían de valor y no habría
    /// una fila de más que lo delate. No hace falta publicar <c>UsuarioActualId</c>: el filtro
    /// global se aplica a las consultas, no a los INSERT.
    /// </summary>
    private async Task SembrarDeOtroPropietarioAsync()
    {
        await using var contexto = baseDeDatos.CrearContexto();
        var otro = new Usuario { Email = "otro@gestiongastos.local", CreadoEn = DateTime.UtcNow };
        contexto.Usuarios.Add(otro);
        await contexto.SaveChangesAsync();

        var gastoAjeno = NuevoMovimiento(TipoMovimiento.Gasto, CategoriaSaludId, 999m, PrimerDiaDelMes, "ajeno");
        gastoAjeno.UsuarioId = otro.Id;
        var ingresoAjeno = NuevoMovimiento(TipoMovimiento.Ingreso, CategoriaSueldoId, 777m, UltimoDiaDelMes, "ajeno");
        ingresoAjeno.UsuarioId = otro.Id;

        contexto.Movimientos.AddRange(gastoAjeno, ingresoAjeno);
        await contexto.SaveChangesAsync();
    }
}
