namespace GestionGastos.Api.Data.Entidades;

public sealed class Categoria
{
    public int Id { get; set; }

    public required string Nombre { get; set; }

    /// <summary>Tipo del catálogo. No es el del movimiento: ver la nota del modelo de datos.</summary>
    public TipoMovimiento Tipo { get; set; }
}
