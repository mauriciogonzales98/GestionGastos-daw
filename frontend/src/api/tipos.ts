/**
 * Tipos del contrato HTTP con la API. Los nombres y los valores son los que el backend emite de
 * verdad: `"gasto"`/`"ingreso"` en minúscula (`TipoMovimientoTexto`), la fecha como `yyyy-MM-dd` y
 * la nota como `null` —nunca cadena vacía— cuando no hay nota.
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
