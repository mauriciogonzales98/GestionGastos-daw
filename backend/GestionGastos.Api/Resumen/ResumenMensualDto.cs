namespace GestionGastos.Api.Resumen;

/// <summary>
/// Resumen del mes calendario en curso. Viaja ya agregado (NFR-02): los tres totales y a lo sumo
/// una fila por categoría, nunca la lista de movimientos.
/// </summary>
/// <param name="Mes">Mes del período, de 1 a 12. Lo fija el servidor, no el cliente (FR-03).</param>
/// <param name="Anio">Año del período, que junto a <paramref name="Mes"/> rotula el resumen (FR-04).</param>
/// <param name="TotalIngresado">Suma de los ingresos del mes. <c>decimal</c> y nunca punto flotante (NFR-03 de FEAT-001a).</param>
/// <param name="TotalGastado">Suma de los gastos del mes, igual a la suma de <paramref name="Desglose"/> (AC-05).</param>
/// <param name="Balance">
/// <paramref name="TotalIngresado"/> menos <paramref name="TotalGastado"/>, ya resuelto acá: la
/// resta hecha en el cliente es una segunda definición del balance que puede divergir de esta.
/// </param>
/// <param name="Desglose">
/// Los gastos del mes por categoría, únicamente las categorías con al menos un gasto (FR-02). Una
/// categoría sin gastos no aparece en cero: no aparece.
/// </param>
public sealed record ResumenMensualDto(
    int Mes,
    int Anio,
    decimal TotalIngresado,
    decimal TotalGastado,
    decimal Balance,
    IReadOnlyList<CategoriaConTotalDto> Desglose);

/// <summary>Una categoría y lo que se gastó en ella durante el mes.</summary>
public sealed record CategoriaConTotalDto(int CategoriaId, string CategoriaNombre, decimal Total);
