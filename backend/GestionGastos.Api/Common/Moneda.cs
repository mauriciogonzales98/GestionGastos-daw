namespace GestionGastos.Api.Common;

/// <summary>
/// La moneda es un dato del movimiento, no un literal repartido por los handlers (FR-09): dejarla
/// acá es lo que permite agregar más monedas sin migrar datos.
/// </summary>
public static class Moneda
{
    public const string Predeterminada = "ARS";

    public const int Largo = 3;
}
