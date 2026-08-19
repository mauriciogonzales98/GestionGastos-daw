import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from '../App';
import type { CategoriaDto, MovimientoDto, ResumenMensual } from '../api/tipos';
import { json } from '../test/infra';

/**
 * Estos tests montan `App` y no `FiltrosMovimientos` a propósito: lo que el criterio de cierre pide
 * comprobar es **la URL que se pide**, y eso solo aparece si el filtro, el listado y el cliente HTTP
 * están conectados de verdad. Un spy sobre `onAplicar` daría verde con el listado desconectado, que
 * es justo el defecto que costó una ronda de verificación en FEAT-001a.
 */

/** Instante fijo para toda la suite: el mes por defecto no puede depender de qué día se corra. */
const AHORA = new Date(2026, 7, 18, 12, 0, 0);

const CATEGORIAS: CategoriaDto[] = [
  { id: 1, nombre: 'Comida', tipo: 'gasto' },
  { id: 2, nombre: 'Transporte', tipo: 'gasto' },
  { id: 8, nombre: 'Sueldo', tipo: 'ingreso' },
];

const COMIDA: MovimientoDto = {
  id: 1,
  tipo: 'gasto',
  categoria: { id: 1, nombre: 'Comida' },
  monto: 1500.5,
  moneda: 'ARS',
  fecha: '2026-08-17',
  nota: 'Supermercado',
};

const TRANSPORTE: MovimientoDto = {
  id: 2,
  tipo: 'gasto',
  categoria: { id: 2, nombre: 'Transporte' },
  monto: 300,
  moneda: 'ARS',
  fecha: '2026-08-03',
  nota: 'Colectivo',
};

const SUELDO_DE_JULIO: MovimientoDto = {
  id: 3,
  tipo: 'ingreso',
  categoria: { id: 8, nombre: 'Sueldo' },
  monto: 250000,
  moneda: 'ARS',
  fecha: '2026-07-01',
  nota: 'Sueldo de julio',
};

const MOVIMIENTOS = [COMIDA, TRANSPORTE, SUELDO_DE_JULIO];

/**
 * `App` monta también el resumen del mes, que pide `/api/resumen` al abrirse. Estos tests son de
 * los filtros y no lo miran, pero el doble tiene que atenderlo igual: sin esta rama el resumen
 * queda mostrando su aviso de error y el `findByRole('alert')` del test de red caída deja de saber
 * de cuál de los dos habla. Va en cero porque ninguno de estos tests afirma nada sobre sus números.
 */
const RESUMEN_EN_CERO: ResumenMensual = {
  mes: 8,
  anio: 2026,
  totalIngresado: 0,
  totalGastado: 0,
  balance: 0,
  desglose: [],
};

interface ServidorFalso {
  urlsDelListado: () => string[];
  ultimaUrlDelListado: () => string;
}

/**
 * Servidor falso que **honra la query string**: filtra igual que el backend, con los dos extremos
 * incluidos. Si ignorara los parámetros, un filtro que nunca se envía daría el mismo DOM que uno
 * que sí, y el test no probaría nada.
 */
function responderListado(url: URL): Response {
  const categoriaId = url.searchParams.get('categoriaId');
  const desde = url.searchParams.get('desde');
  const hasta = url.searchParams.get('hasta');
  const items = MOVIMIENTOS.filter(
    (movimiento) =>
      (categoriaId === null || movimiento.categoria.id === Number(categoriaId)) &&
      (desde === null || movimiento.fecha >= desde) &&
      (hasta === null || movimiento.fecha <= hasta),
  );
  return json({ items, recortado: false, total: items.length });
}

function prepararServidor(responder: (url: URL) => Response = responderListado): ServidorFalso {
  const urls: string[] = [];
  const falso = vi.fn(async (ruta: string) => {
    if (ruta.startsWith('/api/categorias')) {
      return json(CATEGORIAS);
    }
    if (ruta.startsWith('/api/resumen')) {
      return json(RESUMEN_EN_CERO);
    }
    if (ruta.startsWith('/api/movimientos')) {
      urls.push(ruta);
      return responder(new URL(ruta, 'http://localhost'));
    }
    throw new Error(`Ruta no esperada en el test: ${ruta}`);
  });
  vi.stubGlobal('fetch', falso);
  return { urlsDelListado: () => urls, ultimaUrlDelListado: () => ultima(urls) };
}

/** `noUncheckedIndexedAccess` exige descartar `undefined`; acá siempre hubo al menos una lectura. */
function ultima(urls: string[]): string {
  const url = urls[urls.length - 1];
  if (url === undefined) {
    throw new Error('El listado no pidió ninguna URL.');
  }
  return url;
}

/**
 * `<input type="date">` no se completa con `type()`: el control acepta el valor entero, no una
 * secuencia de teclas, y jsdom descarta los intermedios. `fireEvent.change` es la forma
 * determinista de fijarlo.
 */
function ponerFecha(etiqueta: string, valor: string): void {
  fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: valor } });
}

/** El texto del error que acompaña a un control, buscado por su `aria-describedby`. */
function mensajeDelControl(etiqueta: string): string {
  const control = screen.getByLabelText(etiqueta);
  const id = control.getAttribute('aria-describedby') ?? '';
  return document.getElementById(id)?.textContent ?? '';
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(AHORA);
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('FiltrosMovimientos', () => {
  it('Filtros_AlAbrir_PideElMesActual', async () => {
    // Marzo, y no el mes en que se corra la suite: la fecha del sistema se ancla acá mismo para que
    // el test signifique lo mismo en agosto que en marzo.
    vi.setSystemTime(new Date(2026, 2, 15, 10, 0, 0));
    const servidor = prepararServidor();

    render(<App />);

    await waitFor(() => {
      expect(servidor.urlsDelListado()).toHaveLength(1);
    });
    expect(servidor.ultimaUrlDelListado()).toBe(
      '/api/movimientos?desde=2026-03-01&hasta=2026-03-31',
    );
  });

  it('Filtros_SinTocarLaCategoria_NoMandaCategoriaId', async () => {
    const servidor = prepararServidor();

    render(<App />);

    // AC-08: "todas las categorías" es la ausencia del parámetro, no un `categoriaId` vacío. La
    // URL se compara entera y no solo por la ausencia de `categoriaId`: un cliente que no mandara
    // ningún filtro cumpliría "no contiene categoriaId" sin tener nada implementado.
    await waitFor(() => {
      expect(servidor.urlsDelListado()).toHaveLength(1);
    });
    expect(servidor.ultimaUrlDelListado()).toBe(
      '/api/movimientos?desde=2026-08-01&hasta=2026-08-31',
    );
    expect(await screen.findByText('Supermercado')).not.toBeNull();
    expect(screen.getByText('Colectivo')).not.toBeNull();
  });

  it('Filtros_AlElegirCategoria_LaPasaAlCliente', async () => {
    const usuario = userEvent.setup();
    const servidor = prepararServidor();

    render(<App />);
    await screen.findByText('Supermercado');

    await usuario.selectOptions(screen.getByLabelText('Filtrar por categoría'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar filtros' }));

    await waitFor(() => {
      expect(servidor.urlsDelListado()).toHaveLength(2);
    });
    expect(servidor.ultimaUrlDelListado()).toBe(
      '/api/movimientos?categoriaId=1&desde=2026-08-01&hasta=2026-08-31',
    );
    // AC-07: el listado queda con esa categoría y solo con esa.
    await waitFor(() => {
      expect(screen.queryByText('Colectivo')).toBeNull();
    });
    expect(screen.getByText('Supermercado')).not.toBeNull();
  });

  it('Filtros_AlAplicarUnRango_LoPasaAlCliente', async () => {
    const usuario = userEvent.setup();
    const servidor = prepararServidor();

    render(<App />);
    await screen.findByText('Supermercado');

    ponerFecha('Desde', '2026-07-01');
    ponerFecha('Hasta', '2026-07-31');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar filtros' }));

    await waitFor(() => {
      expect(servidor.urlsDelListado()).toHaveLength(2);
    });
    expect(servidor.ultimaUrlDelListado()).toBe(
      '/api/movimientos?desde=2026-07-01&hasta=2026-07-31',
    );
    // AC-10: entra el movimiento con fecha igual al extremo `desde` y salen los de agosto.
    expect(await screen.findByText('Sueldo de julio')).not.toBeNull();
    expect(screen.queryByText('Supermercado')).toBeNull();
  });

  it('Filtros_AlAplicarCategoriaYRango_PasaLosDos', async () => {
    const usuario = userEvent.setup();
    const servidor = prepararServidor();

    render(<App />);
    await screen.findByText('Supermercado');

    await usuario.selectOptions(screen.getByLabelText('Filtrar por categoría'), '2');
    ponerFecha('Desde', '2026-08-01');
    ponerFecha('Hasta', '2026-08-10');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar filtros' }));

    await waitFor(() => {
      expect(servidor.urlsDelListado()).toHaveLength(2);
    });
    expect(servidor.ultimaUrlDelListado()).toBe(
      '/api/movimientos?categoriaId=2&desde=2026-08-01&hasta=2026-08-10',
    );
    // AC-11: 'Supermercado' cumple el rango pero no la categoría; se va igual.
    expect(await screen.findByText('Colectivo')).not.toBeNull();
    expect(screen.queryByText('Supermercado')).toBeNull();
  });

  it('Filtros_ConDesdePosteriorAHasta_MuestraElMotivoYNoPide', async () => {
    const usuario = userEvent.setup();
    const servidor = prepararServidor();

    render(<App />);
    await screen.findByText('Supermercado');

    ponerFecha('Desde', '2026-08-31');
    ponerFecha('Hasta', '2026-08-01');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar filtros' }));

    // AC-12: el motivo aparece junto al control, y la petición no llega a salir.
    await waitFor(() => {
      expect(mensajeDelControl('Desde')).toBe(
        'La fecha de inicio no puede ser posterior a la de fin',
      );
    });
    expect(servidor.urlsDelListado()).toHaveLength(1);
  });

  it('Filtros_ConRangoInvalido_MantieneElListadoAnterior', async () => {
    const usuario = userEvent.setup();
    prepararServidor();

    render(<App />);
    await screen.findByText('Supermercado');

    ponerFecha('Desde', '2026-08-31');
    ponerFecha('Hasta', '2026-08-01');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar filtros' }));

    await waitFor(() => {
      expect(mensajeDelControl('Desde')).not.toBe('');
    });
    // La mitad que se suele olvidar: las filas y el rango vigente siguen siendo los de antes.
    expect(screen.getByText('Supermercado')).not.toBeNull();
    expect(screen.getByText('Colectivo')).not.toBeNull();
    expect(screen.getByText('Mostrando movimientos del 01/08/2026 al 31/08/2026.')).not.toBeNull();
  });

  it('Filtros_ConRedCaida_MuestraElErrorConReintento', async () => {
    const usuario = userEvent.setup();
    const urls: string[] = [];
    const falso = vi.fn(async (ruta: string) => {
      if (ruta.startsWith('/api/categorias')) {
        return json(CATEGORIAS);
      }
      if (ruta.startsWith('/api/resumen')) {
        return json(RESUMEN_EN_CERO);
      }
      urls.push(ruta);
      if (urls.length === 1) {
        throw new TypeError('Failed to fetch');
      }
      return responderListado(new URL(ruta, 'http://localhost'));
    });
    vi.stubGlobal('fetch', falso);

    render(<App />);

    const aviso = await screen.findByRole('alert');
    expect(aviso.textContent).toContain('No pudimos conectar con el servidor.');

    await usuario.click(screen.getByRole('button', { name: /reintentar/i }));

    expect(await screen.findByText('Supermercado')).not.toBeNull();
    // El reintento no puede perder el rango por el camino: las dos lecturas lo llevan. Sin esta
    // aserción el test daba verde contra el listado sin filtros de FEAT-001a.
    expect(urls).toEqual([
      '/api/movimientos?desde=2026-08-01&hasta=2026-08-31',
      '/api/movimientos?desde=2026-08-01&hasta=2026-08-31',
    ]);
  });

  it('Filtros_ConRechazoDelServidor_MuestraElMensajePorCampo', async () => {
    const usuario = userEvent.setup();
    // El cliente considera válido este rango; el servidor lo rechaza igual. La validación del
    // cliente es comodidad, no control: el mensaje del servidor tiene que llegar al control.
    const servidor = prepararServidor((url) =>
      url.searchParams.get('desde') === '2026-07-01'
        ? json(
            {
              title: 'Uno o más errores de validación.',
              status: 400,
              errors: { desde: ['El rango solicitado no es válido.'] },
            },
            400,
            'application/problem+json',
          )
        : responderListado(url),
    );

    render(<App />);
    await screen.findByText('Supermercado');

    ponerFecha('Desde', '2026-07-01');
    ponerFecha('Hasta', '2026-07-31');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar filtros' }));

    await waitFor(() => {
      expect(mensajeDelControl('Desde')).toBe('El rango solicitado no es válido.');
    });
    expect(servidor.urlsDelListado()).toHaveLength(2);
  });
});
