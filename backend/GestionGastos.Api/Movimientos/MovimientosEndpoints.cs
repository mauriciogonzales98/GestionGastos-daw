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

    public static IEndpointRouteBuilder MapMovimientosEndpoints(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPost("/api/movimientos", CrearAsync).WithName("CrearMovimiento");
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
