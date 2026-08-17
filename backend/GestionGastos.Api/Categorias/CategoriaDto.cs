namespace GestionGastos.Api.Categorias;

/// <summary>
/// Categoría del catálogo. <paramref name="Tipo"/> viaja como <c>"gasto"</c> o <c>"ingreso"</c>: el
/// número del enum es un detalle de persistencia y filtrar por él en el frontend obligaría a
/// duplicar la tabla de equivalencias.
/// </summary>
public sealed record CategoriaDto(int Id, string Nombre, string Tipo);
