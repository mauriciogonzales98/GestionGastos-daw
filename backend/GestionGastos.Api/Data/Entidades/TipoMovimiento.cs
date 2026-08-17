namespace GestionGastos.Api.Data.Entidades;

/// <summary>
/// Los valores son explícitos porque se persisten: renumerarlos cambiaría el significado de los
/// datos ya guardados.
/// </summary>
public enum TipoMovimiento : byte
{
    Gasto = 1,
    Ingreso = 2,
}
