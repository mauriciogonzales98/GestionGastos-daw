import { afterEach, describe, expect, it, vi } from 'vitest';
import type { Mock } from 'vitest';
import {
  ErrorDeRed,
  ErrorDeValidacion,
  ErrorDelServidor,
  ErrorNoEncontrado,
  crearMovimiento,
  eliminarMovimiento,
  modificarMovimiento,
  obtenerCategorias,
  obtenerMovimientos,
  obtenerResumen,
} from './cliente';
import type { CrearMovimientoRequest, ModificarMovimientoRequest, ResumenMensual } from './tipos';
import { json } from '../test/infra';

const ALTA_VALIDA: CrearMovimientoRequest = {
  categoriaId: 1,
  monto: 1500.5,
  fecha: '2026-08-17',
  nota: null,
  tipoEsperado: 'gasto',
};

/** El cuerpo del `PUT`: los cuatro campos del contrato y ninguno más — sin `tipoEsperado`. */
const CAMBIO_VALIDO: ModificarMovimientoRequest = {
  categoriaId: 4,
  monto: 2500,
  fecha: '2026-08-18',
  nota: 'Corregido',
};

const MOVIMIENTO_MODIFICADO = {
  id: 7,
  tipo: 'gasto',
  categoria: { id: 4, nombre: 'Transporte' },
  monto: 2500,
  moneda: 'ARS',
  fecha: '2026-08-18',
  nota: 'Corregido',
};

const LISTADO_VACIO = { items: [], recortado: false, total: 0 };

const NO_ENCONTRADO = { title: 'Movimiento no encontrado', status: 404 };

/**
 * El resumen tal como lo emite `GET /api/resumen`. Va tipado a propósito: si `ResumenMensual` deja
 * de espejar al `ResumenMensualDto` del backend, el desajuste se ve acá al compilar y no en runtime
 * como un `undefined` en pantalla.
 */
const RESUMEN_DE_AGOSTO: ResumenMensual = {
  mes: 8,
  anio: 2026,
  totalIngresado: 300000,
  totalGastado: 125500.75,
  balance: 174499.25,
  desglose: [
    { categoriaId: 1, categoriaNombre: 'Comida', total: 100000.5 },
    { categoriaId: 4, categoriaNombre: 'Transporte', total: 25500.25 },
  ],
};

/** Un mes sin movimientos: ceros y desglose vacío, pero con el período igual de presente. */
const RESUMEN_VACIO: ResumenMensual = {
  mes: 8,
  anio: 2026,
  totalIngresado: 0,
  totalGastado: 0,
  balance: 0,
  desglose: [],
};

/**
 * Simula `fetch` con una **fábrica** de respuestas y no con una instancia: el cuerpo de una
 * `Response` se puede leer una sola vez, así que reutilizar la misma en dos llamadas fallaría por
 * el cuerpo consumido y no por lo que el test quiere probar.
 */
function fetchQueResponde(fabrica: () => Response): Mock {
  const fetchFalso = vi.fn(() => Promise.resolve(fabrica()));
  vi.stubGlobal('fetch', fetchFalso);
  return fetchFalso;
}

/** La ruta y las opciones con las que se llamó a `fetch` en la enésima llamada. */
function llamada(fetchFalso: Mock, indice = 0): [string, RequestInit] {
  return fetchFalso.mock.calls[indice] as [string, RequestInit];
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
    fetchQueResponde(() => json(problema, 400, 'application/problem+json'));

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
    const fetchFalso = fetchQueResponde(() => json(creado, 201));

    await expect(crearMovimiento(ALTA_VALIDA)).resolves.toEqual(creado);

    const [ruta, opciones] = llamada(fetchFalso);
    expect(ruta).toBe('/api/movimientos');
    expect(opciones.method).toBe('POST');
    expect(JSON.parse(String(opciones.body))).toEqual(ALTA_VALIDA);
  });

  it('cliente_ErrorDeRed_LanzaErrorDeRedTipado', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));

    await expect(obtenerCategorias()).rejects.toBeInstanceOf(ErrorDeRed);
  });

  it('cliente_ErrorQuinientos_LanzaErrorDelServidorConTraceId', async () => {
    fetchQueResponde(() =>
      json({ status: 500, traceId: 'traza-123' }, 500, 'application/problem+json'),
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
    fetchQueResponde(() => json(catalogo, 200));

    await expect(obtenerCategorias()).resolves.toEqual(catalogo);
  });

  it('obtenerMovimientos_ConFiltros_ArmaLaQueryString', async () => {
    const fetchFalso = fetchQueResponde(() => json(LISTADO_VACIO, 200));

    await obtenerMovimientos({ categoriaId: 3, desde: '2026-08-01', hasta: '2026-08-31' });

    const [ruta] = llamada(fetchFalso);
    expect(ruta).toBe('/api/movimientos?categoriaId=3&desde=2026-08-01&hasta=2026-08-31');
  });

  it('obtenerMovimientos_SinFiltros_NoMandaParametrosVacios', async () => {
    const fetchFalso = fetchQueResponde(() => json(LISTADO_VACIO, 200));

    await obtenerMovimientos();
    await obtenerMovimientos({ desde: '2026-08-01' });

    const [sinFiltros] = llamada(fetchFalso);
    expect(sinFiltros).toBe('/api/movimientos');

    // Los tres filtros son independientes: el que no se pide no viaja, ni siquiera vacío.
    const [soloDesde] = llamada(fetchFalso, 1);
    expect(soloDesde).toBe('/api/movimientos?desde=2026-08-01');
    expect(soloDesde).not.toContain('hasta=');
    expect(soloDesde).not.toContain('categoriaId=');
  });

  it('obtenerMovimientos_ConCadenasVacias_LasOmite', async () => {
    const fetchFalso = fetchQueResponde(() => json(LISTADO_VACIO, 200));

    // Un `<input type="date">` sin completar entrega cadena vacía: eso es "sin filtro", no un
    // filtro vacío. La categoría viaja igual, para que el test no pueda pasar con un cliente que
    // simplemente ignore los filtros.
    await obtenerMovimientos({ categoriaId: 5, desde: '', hasta: '' });

    const [ruta] = llamada(fetchFalso);
    expect(ruta).toBe('/api/movimientos?categoriaId=5');
  });

  it('obtenerMovimientos_ConRangoInvertido_LanzaErrorDeValidacionConElCampo', async () => {
    const problema = {
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { desde: ['La fecha desde no puede ser posterior a la fecha hasta'] },
    };
    const fetchFalso = fetchQueResponde(() => json(problema, 400, 'application/problem+json'));

    const fallo = await obtenerMovimientos({ desde: '2026-08-31', hasta: '2026-08-01' }).catch(
      (error: unknown) => error,
    );

    // El rango llegó a viajar: el rechazo es del servidor, no del cliente, que no valida nada acá.
    const [ruta] = llamada(fetchFalso);
    expect(ruta).toBe('/api/movimientos?desde=2026-08-31&hasta=2026-08-01');
    expect(fallo).toBeInstanceOf(ErrorDeValidacion);
    expect((fallo as ErrorDeValidacion).errores).toEqual({
      desde: 'La fecha desde no puede ser posterior a la fecha hasta',
    });
  });

  it('modificarMovimiento_MandaPutConElCuerpo', async () => {
    const fetchFalso = fetchQueResponde(() => json(MOVIMIENTO_MODIFICADO, 200));

    await expect(modificarMovimiento(7, CAMBIO_VALIDO)).resolves.toEqual(MOVIMIENTO_MODIFICADO);

    const [ruta, opciones] = llamada(fetchFalso);
    expect(ruta).toBe('/api/movimientos/7');
    expect(opciones.method).toBe('PUT');
    expect(new Headers(opciones.headers).get('Content-Type')).toBe('application/json');
    expect(JSON.parse(String(opciones.body))).toEqual(CAMBIO_VALIDO);
  });

  it('eliminarMovimiento_MandaDeleteYNoLeeCuerpo', async () => {
    // Un 204 de verdad: sin cuerpo. `json()` sobre esto rechaza, así que si el cliente lo llamara
    // el test lo delataría con el fallo de parseo además del espía.
    const sinCuerpo = new Response(null, { status: 204 });
    const espiaJson = vi.spyOn(sinCuerpo, 'json');
    const fetchFalso = vi.fn(() => Promise.resolve(sinCuerpo));
    vi.stubGlobal('fetch', fetchFalso);

    await expect(eliminarMovimiento(7)).resolves.toBeUndefined();

    const [ruta, opciones] = llamada(fetchFalso, 0);
    expect(ruta).toBe('/api/movimientos/7');
    expect(opciones.method).toBe('DELETE');
    expect(opciones.body).toBeUndefined();
    expect(espiaJson).not.toHaveBeenCalled();
  });

  it('cliente_Ante404_LanzaErrorNoEncontrado', async () => {
    fetchQueResponde(() => json(NO_ENCONTRADO, 404, 'application/problem+json'));

    const falloAlModificar = await modificarMovimiento(7, CAMBIO_VALIDO).catch(
      (error: unknown) => error,
    );
    const falloAlEliminar = await eliminarMovimiento(7).catch((error: unknown) => error);

    expect(falloAlModificar).toBeInstanceOf(ErrorNoEncontrado);
    expect(falloAlEliminar).toBeInstanceOf(ErrorNoEncontrado);
    // El 404 es un desenlace de dominio, no un fallo del servidor: la vista tiene que poder
    // distinguirlo sin mirar el `title` a mano.
    expect(falloAlModificar).not.toBeInstanceOf(ErrorDelServidor);
    expect(falloAlEliminar).not.toBeInstanceOf(ErrorDelServidor);
  });

  it('cliente_Ante500_SigueLanzandoErrorDelServidor', async () => {
    fetchQueResponde(() =>
      json({ title: 'Se produjo un error inesperado.', status: 500, traceId: 'traza-500' }, 500),
    );

    const falloAlModificar = await modificarMovimiento(7, CAMBIO_VALIDO).catch(
      (error: unknown) => error,
    );
    const falloAlEliminar = await eliminarMovimiento(7).catch((error: unknown) => error);

    expect(falloAlModificar).toBeInstanceOf(ErrorDelServidor);
    expect(falloAlEliminar).toBeInstanceOf(ErrorDelServidor);
    // El tipo nuevo no se puede comer los fallos reales.
    expect(falloAlModificar).not.toBeInstanceOf(ErrorNoEncontrado);
    expect((falloAlModificar as ErrorDelServidor).traceId).toBe('traza-500');
  });

  it('cliente_AnteRedCaida_LanzaErrorDeRed', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));

    await expect(modificarMovimiento(7, CAMBIO_VALIDO)).rejects.toBeInstanceOf(ErrorDeRed);
    await expect(eliminarMovimiento(7)).rejects.toBeInstanceOf(ErrorDeRed);
    await expect(obtenerMovimientos({ categoriaId: 3 })).rejects.toBeInstanceOf(ErrorDeRed);
  });

  it('cliente_Ante400ConErrors_LanzaErrorDeValidacion', async () => {
    const problema = {
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: {
        categoriaId: ['La categoría "Sueldo" es de ingreso y el movimiento es de gasto'],
        monto: ['El monto debe ser mayor a cero'],
      },
    };
    fetchQueResponde(() => json(problema, 400, 'application/problem+json'));

    const fallo = await modificarMovimiento(7, CAMBIO_VALIDO).catch((error: unknown) => error);

    expect(fallo).toBeInstanceOf(ErrorDeValidacion);
    expect(fallo).not.toBeInstanceOf(ErrorNoEncontrado);
    expect((fallo as ErrorDeValidacion).errores).toEqual({
      categoriaId: 'La categoría "Sueldo" es de ingreso y el movimiento es de gasto',
      monto: 'El monto debe ser mayor a cero',
    });
  });

  it('cliente_ConCuerpoDeErrorQueNoEsJson_LanzaErrorDelServidorGenerico', async () => {
    // El 502 de un proxy llega como HTML: leer el problema no puede a su vez romper el manejo del
    // error. Este es el caso que dejaba vivo al mutante de `leerProblema`.
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(
          new Response('<html><body>502 Bad Gateway</body></html>', {
            status: 502,
            headers: { 'Content-Type': 'text/html' },
          }),
        ),
      ),
    );

    const fallo = await eliminarMovimiento(7).catch((error: unknown) => error);

    expect(fallo).toBeInstanceOf(ErrorDelServidor);
    expect((fallo as ErrorDelServidor).message).toBe('La API respondió 502.');
    expect((fallo as ErrorDelServidor).traceId).toBeNull();
  });
  it('ObtenerResumen_DevuelveLosTotalesYElDesglose', async () => {
    const fetchFalso = fetchQueResponde(() => json(RESUMEN_DE_AGOSTO, 200));

    const resumen = await obtenerResumen();

    // El período lo fija el servidor (FR-03): la función no recibe parámetros y no arma query
    // string, así que la ruta es exactamente esta y sin `?`.
    const [ruta, opciones] = llamada(fetchFalso);
    expect(ruta).toBe('/api/resumen');
    expect(ruta).not.toContain('?');
    expect(opciones.method).toBeUndefined();
    expect(opciones.body).toBeUndefined();

    // Campo por campo y no un `toEqual` del objeto entero: un nombre que no coincida con el DTO no
    // rompe la compilación, llega como `undefined`, y un `toEqual` contra el mismo objeto que se
    // sirvió no lo delataría.
    expect(resumen.mes).toBe(8);
    expect(resumen.anio).toBe(2026);
    expect(resumen.totalIngresado).toBe(300000);
    expect(resumen.totalGastado).toBe(125500.75);
    expect(resumen.balance).toBe(174499.25);
    expect(resumen.desglose).toHaveLength(2);
    expect(resumen.desglose[0]?.categoriaId).toBe(1);
    expect(resumen.desglose[0]?.categoriaNombre).toBe('Comida');
    expect(resumen.desglose[0]?.total).toBe(100000.5);
    expect(resumen.desglose[1]?.categoriaNombre).toBe('Transporte');
  });

  it('ObtenerResumen_ConMesVacio_DevuelveCerosYDesgloseVacio', async () => {
    fetchQueResponde(() => json(RESUMEN_VACIO, 200));

    const resumen = await obtenerResumen();

    // El mes sin movimientos no es un caso de error ni una lista ausente: son ceros y un desglose
    // vacío, y el período sigue viniendo para que la vista pueda rotularlo.
    expect(resumen.totalIngresado).toBe(0);
    expect(resumen.totalGastado).toBe(0);
    expect(resumen.balance).toBe(0);
    expect(resumen.desglose).toEqual([]);
    expect(resumen.mes).toBe(8);
    expect(resumen.anio).toBe(2026);
  });

  it('ObtenerResumen_ConErrorDelServidor_LanzaErrorDelServidorConTraceId', async () => {
    fetchQueResponde(() =>
      json(
        { title: 'Se produjo un error inesperado.', status: 500, traceId: 'traza-resumen' },
        500,
        'application/problem+json',
      ),
    );

    const fallo = await obtenerResumen().catch((error: unknown) => error);

    // El manejo de errores es el heredado de `pedir`, no una segunda implementación: el `traceId`
    // tiene que llegar igual que en el resto del cliente.
    expect(fallo).toBeInstanceOf(ErrorDelServidor);
    expect((fallo as ErrorDelServidor).message).toBe('Se produjo un error inesperado.');
    expect((fallo as ErrorDelServidor).traceId).toBe('traza-resumen');
  });

  it('ObtenerResumen_ConRedCaida_LanzaErrorDeRed', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));

    const fallo = await obtenerResumen().catch((error: unknown) => error);

    expect(fallo).toBeInstanceOf(ErrorDeRed);
    expect(fallo).not.toBeInstanceOf(ErrorDelServidor);
  });
});
