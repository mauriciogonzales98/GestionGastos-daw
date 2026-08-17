namespace GestionGastos.Api.Data.Entidades;

public sealed class Usuario
{
    public int Id { get; set; }

    public required string Email { get; set; }

    /// <summary>Siempre en UTC.</summary>
    public DateTime CreadoEn { get; set; }
}
