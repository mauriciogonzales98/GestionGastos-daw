using GestionGastos.Api.Data;
using GestionGastos.Api.Movimientos;
using Microsoft.EntityFrameworkCore;

namespace GestionGastos.Api.Categorias;

public static class CategoriasEndpoints
{
    public static IEndpointRouteBuilder MapCategoriasEndpoints(this IEndpointRouteBuilder rutas)
    {
        // El catálogo es global (FR-02): no lleva propietario y no se filtra por usuario.
        rutas.MapGet("/api/categorias", async (AppDbContext datos, CancellationToken cancelacion) =>
        {
            var categorias = await datos.Categorias
                .AsNoTracking()
                .OrderBy(c => c.Tipo)
                .ThenBy(c => c.Nombre)
                .Select(c => new { c.Id, c.Nombre, c.Tipo })
                .ToListAsync(cancelacion);

            // La traducción del tipo se hace en memoria: EF no sabe traducir el switch a SQL, y
            // hacerlo con un CASE en la consulta duplicaría la tabla de equivalencias.
            return TypedResults.Ok(categorias
                .Select(c => new CategoriaDto(c.Id, c.Nombre, TipoMovimientoTexto.Nombre(c.Tipo)))
                .ToList());
        })
        .WithName("ObtenerCategorias");

        return rutas;
    }
}
