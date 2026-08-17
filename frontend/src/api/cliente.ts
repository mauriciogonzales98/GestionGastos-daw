import type { CategoriaDto, CrearMovimientoRequest, MovimientoDto } from './tipos';

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

export async function obtenerCategorias(senal?: AbortSignal): Promise<CategoriaDto[]> {
  return await pedir<CategoriaDto[]>('/categorias', senal ? { signal: senal } : {});
}

export async function crearMovimiento(entrada: CrearMovimientoRequest): Promise<MovimientoDto> {
  return await pedir<MovimientoDto>('/movimientos', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(entrada),
  });
}

async function pedir<T>(ruta: string, init: RequestInit): Promise<T> {
  let respuesta: Response;
  try {
    respuesta = await fetch(`${BASE}${ruta}`, init);
  } catch (causa) {
    // El error no se traga: se convierte en un tipo propio para que la vista pueda ofrecer
    // reintentar en vez de mostrar un mensaje del navegador.
    throw new ErrorDeRed(causa);
  }

  if (respuesta.ok) {
    return (await respuesta.json()) as T;
  }

  const problema = await leerProblema(respuesta);

  if (respuesta.status === 400 && problema?.errors) {
    throw new ErrorDeValidacion(traducirErrores(problema.errors));
  }

  throw new ErrorDelServidor(
    problema?.title ?? `La API respondió ${respuesta.status}.`,
    problema?.traceId ?? null,
  );
}

/**
 * Traduce la extensión `errors` del RFC 9457 —campo → lista de mensajes— a un mensaje por campo.
 * Las claves son las que emite el servidor (`monto`, `categoriaId`, `fecha`, `nota`,
 * `tipoEsperado`); quién las muestra y dónde es decisión de la vista.
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
