/**
 * Tipos del contrato HTTP con la API. Los nombres y los valores son los que el backend emite de
 * verdad: `"gasto"`/`"ingreso"` en minúscula (`TipoMovimientoTexto`), la fecha como `yyyy-MM-dd` y
 * la nota como `null` —nunca cadena vacía— cuando no hay nota. `FiltrosDeMovimientos` es la
 * excepción: no espeja ningún tipo del backend porque esos tres valores viajan en la query string
 * del listado y no en un cuerpo.
 */
export type TipoMovimiento = 'gasto' | 'ingreso';

/** `CategoriaDto` del backend: catálogo global, sin propietario. */
export interface CategoriaDto {
  id: number;
  nombre: string;
  tipo: TipoMovimiento;
}

/** La categoría tal como viaja dentro de un movimiento: solo id y nombre. */
export interface CategoriaDeMovimiento {
  id: number;
  nombre: string;
}

/** `MovimientoDto` del backend, compartido por el 201 del alta y el listado. */
export interface MovimientoDto {
  id: number;
  tipo: TipoMovimiento;
  categoria: CategoriaDeMovimiento;
  monto: number;
  moneda: string;
  fecha: string;
  nota: string | null;
}

/**
 * `CrearMovimientoRequest` del backend. No lleva `id`, `usuarioId`, `moneda`, `tipo` ni `creadoEn`
 * (mitigación R-04). `tipoEsperado` no se persiste: el servidor lo compara con el tipo de la
 * categoría para detectar el cruce de AC-10.
 */
export interface CrearMovimientoRequest {
  categoriaId: number;
  monto: number;
  fecha: string;
  nota: string | null;
  tipoEsperado: TipoMovimiento;
}

/** Respuesta de `GET /api/movimientos`: el techo de 500 filas se señala, no se oculta. */
export interface ListadoMovimientosResponse {
  items: MovimientoDto[];
  recortado: boolean;
  total: number;
}

/**
 * Los tres filtros de `GET /api/movimientos`, opcionales e independientes: el que no está no viaja,
 * y ausente significa "sin ese filtro" —no "ninguno"—. El `| undefined` explícito es por
 * `exactOptionalPropertyTypes`: la vista mantiene los filtros en un objeto único y necesita poder
 * asignar `undefined` para volver a "todas las categorías" sin rearmarlo.
 */
export interface FiltrosDeMovimientos {
  categoriaId?: number | undefined;
  desde?: string | undefined;
  hasta?: string | undefined;
}

/**
 * `ModificarMovimientoRequest` del backend, el cuerpo del `PUT`. No lleva `id` —viaja en la ruta—
 * ni `usuarioId`, `tipo`, `moneda` o `creadoEn` (mitigación R-15), y tampoco `tipoEsperado`: el
 * tipo del movimiento ya está persistido y el servidor lo lee de ahí, así que no hay nada que el
 * cliente tenga que declarar.
 */
export interface ModificarMovimientoRequest {
  categoriaId: number;
  monto: number;
  fecha: string;
  nota: string | null;
}
