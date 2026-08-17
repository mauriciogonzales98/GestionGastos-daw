namespace GestionGastos.Api.Data.Entidades;

public sealed class Movimiento
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    /// <summary>Se deriva siempre de la categoría elegida; el cliente nunca lo envía.</summary>
    public TipoMovimiento Tipo { get; set; }

    public int CategoriaId { get; set; }

    public Categoria? Categoria { get; set; }

    /// <summary><c>decimal</c> de punta a punta, nunca punto flotante (NFR-03).</summary>
    public decimal Monto { get; set; }

    public string Moneda { get; set; } = Common.Moneda.Predeterminada;

    /// <summary>Fecha sin hora ni zona horaria, para que ninguna capa la desplace de día.</summary>
    public DateOnly Fecha { get; set; }

    public string? Nota { get; set; }

    /// <summary>Siempre en UTC.</summary>
    public DateTime CreadoEn { get; set; }
}
