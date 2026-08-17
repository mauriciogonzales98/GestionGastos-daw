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
    /// Techo de filas del listado (mitigación R-07). La paginación está fuera de alcance por PRD y
    /// FEAT-001b la reemplaza por el filtro del mes actual; hasta entonces un listado sin límite es
    /// un vector de degradación gratuito.
    /// </summary>
    public const int TechoDeItems = 500;

    public static IEndpointRouteBuilder MapMovimientosEndpoints(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPost("/api/movimientos", CrearAsync).WithName("CrearMovimiento");
        rutas.MapGet("/api/movimientos", ListarAsync).WithName("ListarMovimientos");
        rutas.MapGet("/api/movimientos/{id:int}", ObtenerPorIdAsync).WithName("ObtenerMovimiento");
        return rutas;
    }

    private static async Task<Results<Created<MovimientoDto>, ValidationProblem>> CrearAsync(
        CrearMovimientoRequest solicitud,
        AppDbContext datos,
        IUsuarioActual usuarioActual,
        CancellationToken cancelacion)
    {
        var validacion = ValidadorMovimiento.Validar(solicitud, out var validados);
        if (validados is not { } entrada)
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
            return TypedResults.ValidationProblem(
                new ResultadoValidacion().Agregar("categoriaId", "La categoría no existe").ComoDiccionario());
        }

        if (entrada.TipoEsperado is { } esperado && esperado != categoria.Tipo)
        {
            return TypedResults.ValidationProblem(
                new ResultadoValidacion()
                    .Agregar(
                        "categoriaId",
                        $"La categoría '{categoria.Nombre}' es de tipo {TipoMovimientoTexto.Nombre(categoria.Tipo)} " +
                        $"y no puede usarse en un movimiento de tipo {TipoMovimientoTexto.Nombre(esperado)}")
                    .ComoDiccionario());
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
    /// Listado del propietario. No recibe parámetros: los filtros llegan en FEAT-001b y hasta
    /// entonces cualquier query string se ignora en vez de rechazarse.
    /// </summary>
    /// <remarks>
    /// Devuelve un único resultado tipado y no un <c>Results&lt;…&gt;</c> porque el contrato tiene
    /// un solo desenlace propio: una lista vacía es un 200 válido, no un 404. El fallo de base no es
    /// un segundo desenlace de este handler — se propaga al manejador global, que lo convierte en
    /// <c>ProblemDetails</c> 500.
    /// </remarks>
    private static async Task<Ok<ListadoMovimientosResponse>> ListarAsync(
        AppDbContext datos,
        CancellationToken cancelacion)
    {
        // Sin cláusula de propietario: la aplica el filtro global (mitigación R-03).
        var consulta = datos.Movimientos.AsNoTracking();

        // El total se cuenta aparte y sobre TODO lo del propietario: con recorte, el techo hace que
        // la cantidad de items ya no sirva para contar.
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
