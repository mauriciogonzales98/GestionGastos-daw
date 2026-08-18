import type {
  CategoriaDto,
  CrearMovimientoRequest,
  FiltrosDeMovimientos,
  ListadoMovimientosResponse,
  ModificarMovimientoRequest,
  MovimientoDto,
} from './tipos';

/** Un mensaje por campo, listo para mostrarse junto al control correspondiente. */
export type ErroresPorCampo = Record<string, string>;

/** Rechazo de validación del servidor (400 con extensión `errors` del RFC 9457). */
export class ErrorDeValidacion extends Error {
  constructor(readonly errores: ErroresPorCampo) {
    super('El servidor rechazó los datos enviados.');
    this.name = 'ErrorDeValidacion';
  }
}

/** La petición nunca llegó a destino: red caída, API apagada, DNS. */
export class ErrorDeRed extends Error {
  constructor(causa: unknown) {
    super('No pudimos conectar con el servidor.', { cause: causa });
    this.name = 'ErrorDeRed';
  }
}

/**
 * El recurso no está (404). No hereda de `ErrorDelServidor` a propósito: no es un fallo, es un
 * desenlace de dominio normal —el movimiento se borró desde otra pestaña, o el listado quedó
 * viejo—, y la vista tiene que poder responder "refrescá la lista" en vez de "algo se rompió".
 * Distinguirlo mirando el `title` sería un catch por mensaje, que es justo lo que un error tipado
 * evita.
 */
export class ErrorNoEncontrado extends Error {
  constructor(mensaje: string) {
    super(mensaje);
    this.name = 'ErrorNoEncontrado';
  }
}

/** Fallo del lado del servidor. El `traceId` es lo único que sirve para diagnosticarlo. */
export class ErrorDelServidor extends Error {
  constructor(
    mensaje: string,
    readonly traceId: string | null,
  ) {
    super(mensaje);
    this.name = 'ErrorDelServidor';
  }
}

/** Forma del `ProblemDetails` (RFC 9457) que emite ASP.NET Core. */
interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

const BASE = '/api';

export async function obtenerCategorias(): Promise<CategoriaDto[]> {
  return await pedir<CategoriaDto[]>('/categorias', {});
}

export async function obtenerMovimientos(
  filtros?: FiltrosDeMovimientos,
): Promise<ListadoMovimientosResponse> {
  return await pedir<ListadoMovimientosResponse>(`/movimientos${comoQueryString(filtros)}`, {});
}

export async function crearMovimiento(entrada: CrearMovimientoRequest): Promise<MovimientoDto> {
  return await pedir<MovimientoDto>('/movimientos', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(entrada),
  });
}

export async function modificarMovimiento(
  id: number,
  entrada: ModificarMovimientoRequest,
): Promise<MovimientoDto> {
  return await pedir<MovimientoDto>(`/movimientos/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(entrada),
  });
}

/**
 * No pasa por `pedir` porque el 204 del backend llega **sin cuerpo**: leerlo con `response.json()`
 * rompería el borrado exitoso con un error de parseo.
 */
export async function eliminarMovimiento(id: number): Promise<void> {
  await enviar(`/movimientos/${id}`, { method: 'DELETE' });
}

/**
 * Los filtros ausentes no viajan: `?desde=&hasta=` no es "sin filtro" sino ruido que esconde el
 * error de haber olvidado un valor. La cadena vacía cuenta como ausente porque es lo que entrega un
 * `<input type="date">` sin completar.
 */
function comoQueryString(filtros: FiltrosDeMovimientos | undefined): string {
  const parametros = new URLSearchParams();
  agregarSiTieneValor(parametros, 'categoriaId', filtros?.categoriaId);
  agregarSiTieneValor(parametros, 'desde', filtros?.desde);
  agregarSiTieneValor(parametros, 'hasta', filtros?.hasta);
  const consulta = parametros.toString();
  return consulta === '' ? '' : `?${consulta}`;
}

function agregarSiTieneValor(
  parametros: URLSearchParams,
  clave: string,
  valor: number | string | undefined,
): void {
  if (valor === undefined || valor === '') {
    return;
  }
  parametros.set(clave, String(valor));
}

async function pedir<T>(ruta: string, init: RequestInit): Promise<T> {
  const respuesta = await enviar(ruta, init);
  return (await respuesta.json()) as T;
}

/**
 * Hace la petición y convierte cualquier desenlace que no sea 2xx en un error tipado. Devuelve la
 * respuesta **sin leerla**: si el cuerpo se lee, y con qué, lo decide cada llamador — un 204 no
 * tiene ninguno.
 */
async function enviar(ruta: string, init: RequestInit): Promise<Response> {
  let respuesta: Response;
  try {
    respuesta = await fetch(`${BASE}${ruta}`, init);
  } catch (causa) {
    // El error no se traga: se convierte en un tipo propio para que la vista pueda ofrecer
    // reintentar en vez de mostrar un mensaje del navegador.
    throw new ErrorDeRed(causa);
  }

  if (respuesta.ok) {
    return respuesta;
  }

  const problema = await leerProblema(respuesta);

  if (respuesta.status === 400 && problema?.errors) {
    throw new ErrorDeValidacion(traducirErrores(problema.errors));
  }

  if (respuesta.status === 404) {
    throw new ErrorNoEncontrado(problema?.title ?? 'El movimiento ya no existe.');
  }

  throw new ErrorDelServidor(
    problema?.title ?? `La API respondió ${respuesta.status}.`,
    problema?.traceId ?? null,
  );
}

/**
 * Traduce la extensión `errors` del RFC 9457 —campo → lista de mensajes— a un mensaje por campo.
 * Las claves son las que emite el servidor (`monto`, `categoriaId`, `fecha`, `nota`,
 * `tipoEsperado` en el alta; `categoriaId`, `desde` y `hasta` en los filtros del listado); quién
 * las muestra y dónde es decisión de la vista.
 */
export function traducirErrores(errores: Record<string, string[]>): ErroresPorCampo {
  const traducidos: ErroresPorCampo = {};
  for (const [campo, mensajes] of Object.entries(errores)) {
    const texto = mensajes.filter((mensaje) => mensaje.trim() !== '').join(' ');
    if (texto !== '') {
      traducidos[campo] = texto;
    }
  }
  return traducidos;
}

/**
 * Un cuerpo de error que no sea JSON —un 502 de un proxy, por ejemplo— no puede a su vez romper el
 * manejo del error: se degrada a `null` y el llamador usa el mensaje genérico. No es un catch
 * silencioso: el desenlace sigue siendo una excepción tipada.
 */
async function leerProblema(respuesta: Response): Promise<ProblemDetails | null> {
  try {
    return (await respuesta.json()) as ProblemDetails;
  } catch {
    return null;
  }
}
