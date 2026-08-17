/**
 * Día de hoy en formato `yyyy-MM-dd`, armado con los componentes **locales** de la fecha.
 *
 * Deliberadamente no usa `toISOString()`: ese método convierte a UTC y, en cualquier huso negativo
 * —el nuestro es UTC-3—, después de las 21:00 devuelve el día siguiente. La fecha de un movimiento
 * no tiene hora ni zona horaria en ninguna de las tres capas, así que el desplazamiento sería un
 * error silencioso de un día.
 */
export function hoyComoIso(ahora: Date = new Date()): string {
  const anio = String(ahora.getFullYear()).padStart(4, '0');
  const mes = String(ahora.getMonth() + 1).padStart(2, '0');
  const dia = String(ahora.getDate()).padStart(2, '0');
  return `${anio}-${mes}-${dia}`;
}
