/**
 * Formato de monto con su moneda, para mostrar en la tabla del listado. `moneda` viene de la
 * respuesta —hoy siempre `"ARS"`— y no se asume hardcodeado.
 */
export function formatearMonto(monto: number, moneda: string): string {
  const numero = new Intl.NumberFormat('es-AR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(monto);
  return `${moneda} ${numero}`;
}

/**
 * `yyyy-MM-dd` a `dd/mm/yyyy` para mostrar. Deliberadamente no pasa por `Date`: construir un `Date`
 * a partir de `yyyy-MM-dd` lo interpreta como UTC medianoche, y en un huso negativo como el nuestro
 * (UTC-3) `toLocaleDateString` puede mostrar el día anterior. Se recorta el texto directamente.
 */
export function formatearFecha(fechaIso: string): string {
  const [anio, mes, dia] = fechaIso.split('-');
  return `${dia}/${mes}/${anio}`;
}
