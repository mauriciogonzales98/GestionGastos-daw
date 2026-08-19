import { hoyComoIso } from './fecha';

/** Un rango cerrado de fechas en `yyyy-MM-dd`, con los dos extremos incluidos. */
export interface RangoDeFechas {
  desde: string;
  hasta: string;
}

/**
 * Primer y último día del mes calendario en curso, en formato `yyyy-MM-dd` y **en hora local del
 * navegador**: es la hora en la que el usuario pregunta "cómo vengo este mes", y la única con la
 * que la respuesta significa eso.
 *
 * El último día se obtiene pidiendo el día 0 del mes siguiente —que es el último del actual— en vez
 * de una tabla de largos, que se olvidaría del febrero bisiesto. El formateo se delega en
 * `hoyComoIso`, que ya lee los componentes locales: `toISOString()` convertiría a UTC y, en un huso
 * negativo como el nuestro, el 31 a las 23:00 devolvería el mes siguiente entero.
 */
export function mesActual(ahora: Date = new Date()): RangoDeFechas {
  const anio = ahora.getFullYear();
  const mes = ahora.getMonth();
  return {
    desde: hoyComoIso(new Date(anio, mes, 1)),
    hasta: hoyComoIso(new Date(anio, mes + 1, 0)),
  };
}
