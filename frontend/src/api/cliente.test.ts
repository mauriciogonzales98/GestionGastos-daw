import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ErrorDeRed,
  ErrorDeValidacion,
  ErrorDelServidor,
  crearMovimiento,
  obtenerCategorias,
} from './cliente';
import type { CrearMovimientoRequest } from './tipos';

const ALTA_VALIDA: CrearMovimientoRequest = {
  categoriaId: 1,
  monto: 1500.5,
  fecha: '2026-08-17',
  nota: null,
  tipoEsperado: 'gasto',
};

function respuesta(cuerpo: unknown, estado: number, tipo = 'application/json'): Response {
  return new Response(JSON.stringify(cuerpo), {
    status: estado,
    headers: { 'Content-Type': tipo },
  });
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('cliente', () => {
  it('cliente_TraduceProblemDetailsAErroresPorCampo', async () => {
    const problema = {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: {
        monto: ['El monto debe ser mayor a cero'],
        categoriaId: ['La categoría es obligatoria'],
        nota: ['La nota no puede superar los 120 caracteres', 'Segundo motivo'],
      },
    };
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(respuesta(problema, 400, 'application/problem+json')),
    );

    const fallo = await crearMovimiento(ALTA_VALIDA).catch((error: unknown) => error);

    expect(fallo).toBeInstanceOf(ErrorDeValidacion);
    expect((fallo as ErrorDeValidacion).errores).toEqual({
      monto: 'El monto debe ser mayor a cero',
      categoriaId: 'La categoría es obligatoria',
      nota: 'La nota no puede superar los 120 caracteres Segundo motivo',
    });
  });

  it('cliente_AltaValida_DevuelveElMovimientoCreado', async () => {
    const creado = {
      id: 7,
      tipo: 'gasto',
      categoria: { id: 1, nombre: 'Comida' },
      monto: 1500.5,
      moneda: 'ARS',
      fecha: '2026-08-17',
      nota: null,
    };
    const fetchFalso = vi.fn().mockResolvedValue(respuesta(creado, 201));
    vi.stubGlobal('fetch', fetchFalso);

    await expect(crearMovimiento(ALTA_VALIDA)).resolves.toEqual(creado);

    const [ruta, opciones] = fetchFalso.mock.calls[0] as [string, RequestInit];
    expect(ruta).toBe('/api/movimientos');
    expect(opciones.method).toBe('POST');
    expect(JSON.parse(String(opciones.body))).toEqual(ALTA_VALIDA);
  });

  it('cliente_ErrorDeRed_LanzaErrorDeRedTipado', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));

    await expect(obtenerCategorias()).rejects.toBeInstanceOf(ErrorDeRed);
  });

  it('cliente_ErrorQuinientos_LanzaErrorDelServidorConTraceId', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(
          respuesta({ status: 500, traceId: 'traza-123' }, 500, 'application/problem+json'),
        ),
    );

    const fallo = await crearMovimiento(ALTA_VALIDA).catch((error: unknown) => error);

    expect(fallo).toBeInstanceOf(ErrorDelServidor);
    expect((fallo as ErrorDelServidor).traceId).toBe('traza-123');
  });

  it('cliente_ObtenerCategorias_DevuelveElCatalogo', async () => {
    const catalogo = [
      { id: 1, nombre: 'Comida', tipo: 'gasto' },
      { id: 8, nombre: 'Sueldo', tipo: 'ingreso' },
    ];
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta(catalogo, 200)));

    await expect(obtenerCategorias()).resolves.toEqual(catalogo);
  });
});
