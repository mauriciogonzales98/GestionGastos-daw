using GestionGastos.Api.Data;
using GestionGastos.Api.Data.Entidades;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Resumen;

public static class ResumenEndpoints
{
    public static IEndpointRouteBuilder MapResumenEndpoints(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/resumen", ObtenerAsync).WithName("ObtenerResumen");
        return rutas;
    }

    /// <summary>
    /// Resumen del mes calendario en curso del propietario: los tres totales y el desglose de sus
    /// gastos por categoría.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No acepta ningún parámetro</b>, y eso es del contrato y no un olvido (mitigación R-27):
    /// el período lo fija el servidor, así que FR-03 —el resumen es siempre del mes en curso, pase
    /// lo que pase con los filtros del listado— queda garantizado acá y no depende de que el
    /// cliente se porte bien.
    /// </para>
    /// <para>
    /// Devuelve un <c>Ok</c> pelado porque el contrato tiene un solo desenlace propio: un mes sin
    /// movimientos es un 200 en cero y no un 404, y el fallo de base se propaga al manejador
    /// global, que lo convierte en <c>ProblemDetails</c> 500.
    /// </para>
    /// </remarks>
    private static async Task<Ok<ResumenMensualDto>> ObtenerAsync(
        AppDbContext datos,
        CancellationToken cancelacion)
    {
        var rango = RangoDelMes.De(DateOnly.FromDateTime(DateTime.Today));

        // Sin cláusula de propietario: la aplica el filtro global (mitigación R-24). Repetirlo a
        // mano acá sería una segunda copia del criterio del listado, que puede divergir de la
        // original justo donde nadie lo ve — una fuga por agregación no agrega una fila, agranda un
        // número.
        var delMes = datos.Movimientos
            .AsNoTracking()
            .Where(m => m.Fecha >= rango.PrimerDia && m.Fecha <= rango.UltimoDia);

        // Las dos agregaciones salen del MISMO IQueryable, así que no pueden aplicar cortes
        // distintos y desincronizar el desglose del total gastado (AC-05).
        var porTipo = await delMes
            .GroupBy(m => m.Tipo)
            .Select(g => new { Tipo = g.Key, Total = g.Sum(m => m.Monto) })
            .ToListAsync(cancelacion);

        var desglose = await delMes
            .Where(m => m.Tipo == TipoMovimiento.Gasto)
            .GroupBy(m => new { m.CategoriaId, m.Categoria!.Nombre })
            .Select(g => new { g.Key.CategoriaId, g.Key.Nombre, Total = g.Sum(m => m.Monto) })
            // De lo más caro a lo más barato, que es el orden en que se lee un desglose de gastos;
            // el nombre desempata para que dos categorías con el mismo total no salgan alternadas
            // entre llamadas.
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Nombre)
            .ToListAsync(cancelacion);

        // Un tipo sin movimientos en el mes no tiene grupo, y su total es cero (AC-02).
        var totalIngresado = TotalDe(porTipo.Select(t => (t.Tipo, t.Total)), TipoMovimiento.Ingreso);
        var totalGastado = TotalDe(porTipo.Select(t => (t.Tipo, t.Total)), TipoMovimiento.Gasto);

        return TypedResults.Ok(new ResumenMensualDto(
            rango.PrimerDia.Month,
            rango.PrimerDia.Year,
            totalIngresado,
            totalGastado,
            totalIngresado - totalGastado,
            desglose
                .Select(c => new CategoriaConTotalDto(c.CategoriaId, c.Nombre, c.Total))
                .ToList()));
    }

    private static decimal TotalDe(
        IEnumerable<(TipoMovimiento Tipo, decimal Total)> porTipo,
        TipoMovimiento tipo) =>
        porTipo.SingleOrDefault(t => t.Tipo == tipo).Total;
}
