using System.Globalization;
using GestionGastos.Api.Common;
using GestionGastos.Api.Data;
using GestionGastos.Api.Data.Entidades;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Movimientos;

public static class MovimientosEndpoints
{
    /// <summary>
    /// Mismo título para "no existe" y para "es de otro propietario": distinguirlos confirmaría la
    /// existencia de una fila ajena.
    /// </summary>
    public const string TituloNoEncontrado = "Movimiento no encontrado";

    /// <summary>
    /// Techo de filas del listado (mitigaciones R-07 y R-14), que FEAT-001b conserva junto a los
    /// filtros. Los filtros achican el universo consultado, pero no acotan nada por sí solos: son
    /// opcionales, el default del mes actual lo pone el cliente y no el endpoint, y un rango
    /// deliberadamente ancho vuelve a pedir la tabla entera. El techo es la única cota superior del
    /// tamaño de la respuesta; la paginación sigue fuera de alcance por PRD, y quien queda afuera
    /// se anuncia con <c>recortado</c>.
    /// </summary>
    public const int TechoDeItems = 500;

    public static IEndpointRouteBuilder MapMovimientosEndpoints(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPost("/api/movimientos", CrearAsync).WithName("CrearMovimiento");
        rutas.MapGet("/api/movimientos", ListarAsync).WithName("ListarMovimientos");
        rutas.MapGet("/api/movimientos/{id:int}", ObtenerPorIdAsync).WithName("ObtenerMovimiento");
        rutas.MapPut("/api/movimientos/{id:int}", ModificarAsync).WithName("ModificarMovimiento");
        return rutas;
    }

    private static async Task<Results<Created<MovimientoDto>, ValidationProblem>> CrearAsync(
        CrearMovimientoRequest solicitud,
        AppDbContext datos,
        IUsuarioActual usuarioActual,
        CancellationToken cancelacion)
    {
        var validacion = ValidadorMovimiento.Validar(solicitud, out var validados);
        // El tipo esperado se valida aparte de las cuatro reglas comunes, y solo acá: es una entrada
        // no confiable del cliente que solo existe en el alta. Se acumula sobre el mismo resultado
        // para que un cuerpo con dos problemas siga devolviendo los dos errores juntos.
        var tipoEsperado = ValidadorMovimiento.ValidarTipoEsperado(solicitud.TipoEsperado, validacion);
        if (validados is not { } entrada || !validacion.EsValido)
        {
            return TypedResults.ValidationProblem(validacion.ComoDiccionario());
        }

        // La categoría se comprueba ANTES de insertar: dejar que reviente la FK devolvería un 500
        // donde AC-09 y AC-10 piden un error de validación.
        var categoria = await datos.Categorias
            .AsNoTracking()
            .Where(c => c.Id == entrada.CategoriaId)
            .Select(c => new { c.Id, c.Nombre, c.Tipo })
            .SingleOrDefaultAsync(cancelacion);

        if (categoria is null)
        {
            return TypedResults.ValidationProblem(ValidadorMovimiento.CategoriaInexistente().ComoDiccionario());
        }

        if (tipoEsperado is { } esperado && esperado != categoria.Tipo)
        {
            return TypedResults.ValidationProblem(
                ValidadorMovimiento.TipoCruzado(categoria.Nombre, categoria.Tipo, esperado).ComoDiccionario());
        }

        var movimiento = new Movimiento
        {
            // El propietario sale siempre de IUsuarioActual, nunca del cuerpo (mitigación R-04): el
            // filtro global protege las lecturas, pero EF no lo aplica a los INSERT.
            UsuarioId = await usuarioActual.ObtenerIdAsync(cancelacion),
            // El tipo se deriva de la categoría; el cliente no lo elige.
            Tipo = categoria.Tipo,
            CategoriaId = categoria.Id,
            Monto = entrada.Monto,
            // Dato del movimiento, tomado de la constante de dominio y no de un literal suelto acá
            // (FR-09).
            Moneda = Moneda.Predeterminada,
            Fecha = entrada.Fecha,
            Nota = entrada.Nota,
            CreadoEn = DateTime.UtcNow,
        };

        datos.Movimientos.Add(movimiento);
        await datos.SaveChangesAsync(cancelacion);

        var dto = ADto(
            movimiento.Id,
            movimiento.Tipo,
            categoria.Id,
            categoria.Nombre,
            movimiento.Monto,
            movimiento.Moneda,
            movimiento.Fecha,
            movimiento.Nota);

        return TypedResults.Created($"/api/movimientos/{movimiento.Id}", dto);
    }

    /// <summary>
    /// Listado del propietario, con tres filtros opcionales e independientes: categoría, fecha
    /// desde y fecha hasta. Un parámetro ausente significa "sin ese filtro", así que sin ninguno el
    /// endpoint devuelve todo lo del propietario, igual que en FEAT-001a. Los parámetros que la API
    /// no conoce se siguen ignorando; los que sí conoce, si vienen mal, se rechazan.
    /// </summary>
    /// <remarks>
    /// Devuelve un <c>Results&lt;…&gt;</c> porque desde FEAT-001b el contrato tiene un segundo
    /// desenlace propio: una entrada de filtro inválida es un 400 con <c>errors</c> por campo. Los
    /// otros dos siguen sin serlo — una lista vacía es un 200 válido y no un 404, y el fallo de base
    /// se propaga al manejador global, que lo convierte en <c>ProblemDetails</c> 500.
    /// </remarks>
    private static async Task<Results<Ok<ListadoMovimientosResponse>, ValidationProblem>> ListarAsync(
        int? categoriaId,
        string? desde,
        string? hasta,
        AppDbContext datos,
        CancellationToken cancelacion)
    {
        var validacion = FiltrosDeListado.Parsear(categoriaId, desde, hasta, out var validados);
        if (validados is not { } filtros)
        {
            // El listado no llega a ejecutarse: un rango invertido no se consulta y después se
            // descarta, se rechaza antes de tocar la base.
            return TypedResults.ValidationProblem(validacion.ComoDiccionario());
        }

        // Sin cláusula de propietario: la aplica el filtro global (mitigación R-03).
        var consulta = datos.Movimientos.AsNoTracking();

        // Los tres filtros se incorporan al IQueryable ANTES del CountAsync y del Take, de modo que
        // se resuelvan en la base y nunca se materialice una fila fuera del rango (mitigación
        // R-13). Filtrar la lista ya traída daría la misma respuesta y sería otra cosa.
        if (filtros.CategoriaId is { } categoria)
        {
            consulta = consulta.Where(m => m.CategoriaId == categoria);
        }

        if (filtros.Desde is { } inicio)
        {
            consulta = consulta.Where(m => m.Fecha >= inicio);
        }

        if (filtros.Hasta is { } fin)
        {
            // Comparación entre DateOnly, sin hora de por medio: el extremo superior queda incluido
            // (AC-10), que es donde todo filtro por rango se equivoca.
            consulta = consulta.Where(m => m.Fecha <= fin);
        }

        // El total se cuenta aparte y sobre el universo YA FILTRADO: si contara todo lo del
        // propietario, `recortado` mentiría con un filtro angosto (mitigación R-14). Aparte del
        // conteo de items porque el techo hace que la cantidad devuelta ya no sirva para contar.
        var total = await consulta.CountAsync(cancelacion);

        var movimientos = await consulta
            // El desempate por id evita que dos movimientos del mismo día salgan en orden distinto
            // entre llamadas, y es lo que vuelve determinista cuál queda afuera del techo.
            .OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.Id)
            .Take(TechoDeItems)
            .Select(m => new
            {
                m.Id,
                m.Tipo,
                CategoriaId = m.Categoria!.Id,
                CategoriaNombre = m.Categoria!.Nombre,
                m.Monto,
                m.Moneda,
                m.Fecha,
                m.Nota,
            })
            .ToListAsync(cancelacion);

        var items = movimientos
            .Select(m => ADto(
                m.Id,
                m.Tipo,
                m.CategoriaId,
                m.CategoriaNombre,
                m.Monto,
                m.Moneda,
                m.Fecha,
                m.Nota))
            .ToList();

        return TypedResults.Ok(new ListadoMovimientosResponse(items, total > TechoDeItems, total));
    }

    /// <summary>
    /// Modificación de un movimiento propio. El tipo NO se puede cambiar (fuera de alcance por PRD):
    /// la categoría nueva tiene que ser del mismo tipo que el movimiento persistido, y ese tipo sale
    /// de la fila y no del cuerpo, que en esto no es confiable.
    /// </summary>
    /// <remarks>
    /// Valida primero y toca la entidad al final: cualquier rechazo tiene que dejar el movimiento con
    /// todos sus valores anteriores (AC-04). La fila se localiza con una lectura sobre
    /// <c>DbSet&lt;Movimiento&gt;</c>, donde el filtro global de propietario ya aplica (mitigación
    /// R-15); un <c>ExecuteUpdate</c> sin esa lectura escribiría sobre filas ajenas.
    /// </remarks>
    private static async Task<Results<Ok<MovimientoDto>, ValidationProblem, ProblemHttpResult>> ModificarAsync(
        int id,
        ModificarMovimientoRequest solicitud,
        AppDbContext datos,
        CancellationToken cancelacion)
    {
        var validacion = ValidadorMovimiento.Validar(solicitud, out var validados);
        if (validados is not { } entrada)
        {
            return TypedResults.ValidationProblem(validacion.ComoDiccionario());
        }

        // Con seguimiento y sin cláusula de propietario: el filtro global es el que decide qué se
        // puede tocar, y hace indistinguibles "no existe" y "es de otro".
        var movimiento = await datos.Movimientos.SingleOrDefaultAsync(m => m.Id == id, cancelacion);
        if (movimiento is null)
        {
            return TypedResults.Problem(title: TituloNoEncontrado, statusCode: StatusCodes.Status404NotFound);
        }

        var categoria = await datos.Categorias
            .AsNoTracking()
            .Where(c => c.Id == entrada.CategoriaId)
            .Select(c => new { c.Id, c.Nombre, c.Tipo })
            .SingleOrDefaultAsync(cancelacion);

        if (categoria is null)
        {
            return TypedResults.ValidationProblem(ValidadorMovimiento.CategoriaInexistente().ComoDiccionario());
        }

        if (categoria.Tipo != movimiento.Tipo)
        {
            return TypedResults.ValidationProblem(
                ValidadorMovimiento.TipoCruzado(categoria.Nombre, categoria.Tipo, movimiento.Tipo).ComoDiccionario());
        }

        // Los cuatro campos del contrato y ninguno más: el propietario, el tipo, la moneda y la fecha
        // de creación no se tocan aunque el cuerpo los traiga (mitigación R-15).
        movimiento.CategoriaId = categoria.Id;
        movimiento.Monto = entrada.Monto;
        movimiento.Fecha = entrada.Fecha;
        movimiento.Nota = entrada.Nota;

        await datos.SaveChangesAsync(cancelacion);

        return TypedResults.Ok(ADto(
            movimiento.Id,
            movimiento.Tipo,
            categoria.Id,
            categoria.Nombre,
            movimiento.Monto,
            movimiento.Moneda,
            movimiento.Fecha,
            movimiento.Nota));
    }

    private static async Task<Results<Ok<MovimientoDto>, ProblemHttpResult>> ObtenerPorIdAsync(
        int id,
        AppDbContext datos,
        CancellationToken cancelacion)
    {
        // Sin cláusula de propietario: la aplica el filtro global (mitigación R-03), que además hace
        // indistinguibles "no existe" y "es de otro".
        var movimiento = await datos.Movimientos
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new
            {
                m.Id,
                m.Tipo,
                CategoriaId = m.Categoria!.Id,
                CategoriaNombre = m.Categoria!.Nombre,
                m.Monto,
                m.Moneda,
                m.Fecha,
                m.Nota,
            })
            .SingleOrDefaultAsync(cancelacion);

        if (movimiento is null)
        {
            return TypedResults.Problem(title: TituloNoEncontrado, statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(ADto(
            movimiento.Id,
            movimiento.Tipo,
            movimiento.CategoriaId,
            movimiento.CategoriaNombre,
            movimiento.Monto,
            movimiento.Moneda,
            movimiento.Fecha,
            movimiento.Nota));
    }

    private static MovimientoDto ADto(
        int id,
        TipoMovimiento tipo,
        int categoriaId,
        string categoriaNombre,
        decimal monto,
        string moneda,
        DateOnly fecha,
        string? nota) => new(
            id,
            TipoMovimientoTexto.Nombre(tipo),
            new CategoriaDeMovimientoDto(categoriaId, categoriaNombre),
            monto,
            moneda,
            // Formato invariante y explícito: la fecha del movimiento no lleva hora ni zona horaria,
            // y serializarla con la cultura del proceso la desplazaría de formato.
            fecha.ToString(ValidadorMovimiento.FormatoDeFecha, CultureInfo.InvariantCulture),
            nota);
}
