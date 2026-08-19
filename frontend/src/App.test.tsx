import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from './App';
import type { CategoriaDto, MovimientoDto } from './api/tipos';
import { json } from './test/infra';

/** Instante fijo: el rango por defecto es el mes en curso y no puede depender del día de la corrida. */
const AHORA = new Date(2026, 7, 18, 12, 0, 0);

/** El rango que `App` tiene que pedir sola al abrirse, derivado de `AHORA`. */
const URL_DEL_MES_ACTUAL = '/api/movimientos?desde=2026-08-01&hasta=2026-08-31';

const CATEGORIAS: CategoriaDto[] = [
  { id: 1, nombre: 'Comida', tipo: 'gasto' },
  { id: 8, nombre: 'Sueldo', tipo: 'ingreso' },
];

const YA_CARGADO: MovimientoDto = {
  id: 1,
  tipo: 'ingreso',
  categoria: { id: 8, nombre: 'Sueldo' },
  monto: 250000,
  moneda: 'ARS',
  fecha: '2026-08-01',
  nota: null,
};

const GASTO_CREADO: MovimientoDto = {
  id: 2,
  tipo: 'gasto',
  categoria: { id: 1, nombre: 'Comida' },
  monto: 1500.5,
  moneda: 'ARS',
  fecha: '2026-08-17',
  nota: 'Supermercado',
};

const INGRESO_CREADO: MovimientoDto = {
  id: 3,
  tipo: 'ingreso',
  categoria: { id: 8, nombre: 'Sueldo' },
  monto: 900000,
  moneda: 'ARS',
  fecha: '2026-08-16',
  nota: 'Sueldo de agosto',
};

/**
 * Enruta el `fetch` global por endpoint —no por orden de llamadas, como ya decidió
 * `FormularioMovimiento.test.tsx`— y mantiene el estado del servidor falso: el listado devuelve
 * `recienCreado` solo DESPUÉS del POST. Si las dos lecturas del listado devolvieran lo mismo, el
 * test daría verde aunque la recarga nunca ocurriera, que es exactamente el agujero que cierra.
 *
 * **Honra la query string**, además: filtra por categoría y por rango con los dos extremos
 * incluidos, y guarda cada URL pedida. Ignorarla —como hacía este doble hasta FEAT-001b— dejaba
 * pasar un default del mes actual que nunca se aplicara.
 */
function prepararFetch(recienCreado: MovimientoDto): {
  lecturasDelListado: () => number;
  urlsDelListado: () => string[];
} {
  let creado = false;
  const urls: string[] = [];
  const falso = vi.fn(async (ruta: string, init?: RequestInit) => {
    if (ruta.startsWith('/api/categorias')) {
      return json(CATEGORIAS);
    }
    if (ruta.startsWith('/api/movimientos') && init?.method === 'POST') {
      creado = true;
      return json(recienCreado, 201);
    }
    if (ruta.startsWith('/api/movimientos')) {
      urls.push(ruta);
      const url = new URL(ruta, 'http://localhost');
      const categoriaId = url.searchParams.get('categoriaId');
      const desde = url.searchParams.get('desde');
      const hasta = url.searchParams.get('hasta');
      const todos = creado ? [recienCreado, YA_CARGADO] : [YA_CARGADO];
      const items = todos.filter(
        (movimiento) =>
          (categoriaId === null || movimiento.categoria.id === Number(categoriaId)) &&
          (desde === null || movimiento.fecha >= desde) &&
          (hasta === null || movimiento.fecha <= hasta),
      );
      return json({ items, recortado: false, total: items.length });
    }
    throw new Error(`Ruta no esperada en el test: ${ruta}`);
  });
  vi.stubGlobal('fetch', falso);
  return { lecturasDelListado: () => urls.length, urlsDelListado: () => urls };
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

describe('App', () => {
  it('App_TrasUnAltaDeGasto_ElListadoMuestraElMovimientoNuevo', async () => {
    const usuario = userEvent.setup();
    const { lecturasDelListado, urlsDelListado } = prepararFetch(GASTO_CREADO);

    render(<App />);

    // La ausencia previa es la mitad que importa: sin ella, un listado que ya traía la fila daría
    // verde sin que la recarga hubiera pasado.
    await screen.findByText('01/08/2026');
    expect(screen.queryByText('17/08/2026')).toBeNull();
    expect(lecturasDelListado()).toBe(1);
    // AC-09: la primera lectura ya pide el mes en curso, no todo el historial.
    expect(urlsDelListado()).toEqual([URL_DEL_MES_ACTUAL]);

    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.type(screen.getByLabelText('Monto'), '1500.50');
    await usuario.click(screen.getByRole('button', { name: /guardar/i }));

    // Esta espera es la que mata al mutante que borra `onCreado` de App.tsx (AC-05): sin la costura
    // formulario → version → listado, el POST responde 201 pero la tabla nunca se recarga.
    expect(await screen.findByText('17/08/2026')).not.toBeNull();
    expect(screen.getByText('ARS 1.500,50')).not.toBeNull();
    expect(screen.getByText('Supermercado')).not.toBeNull();

    // Cota superior, y lo único que la vigila: `cargar` es un `useCallback` sobre los filtros, así
    // que unas deps mal puestas darían un fetch en bucle que la aserción del DOM no vería —la fila
    // aparecería igual—. Sincrónica a propósito: `waitFor` resuelve en el primer poll que no lanza,
    // y a esta altura ya vale 2, así que nunca observaría una tercera lectura posterior.
    expect(lecturasDelListado()).toBe(2);
    // Y la recarga tampoco se olvida del rango: las dos lecturas lo llevan.
    expect(urlsDelListado()).toEqual([URL_DEL_MES_ACTUAL, URL_DEL_MES_ACTUAL]);
  });

  it('App_TrasUnAltaDeIngreso_ElListadoMuestraElMovimientoNuevo', async () => {
    const usuario = userEvent.setup();
    const { lecturasDelListado, urlsDelListado } = prepararFetch(INGRESO_CREADO);

    render(<App />);

    await screen.findByText('01/08/2026');
    expect(screen.queryByText('16/08/2026')).toBeNull();
    expect(urlsDelListado()).toEqual([URL_DEL_MES_ACTUAL]);

    // AC-06 es la otra mitad del criterio y se ejerce, no se infiere: cambiar el tipo a ingreso
    // reinicia la categoría (AC-10), así que el orden de estos tres pasos importa.
    await usuario.click(screen.getByLabelText('Ingreso'));
    await usuario.selectOptions(screen.getByLabelText('Categoría'), '8');
    await usuario.type(screen.getByLabelText('Monto'), '900000');
    await usuario.click(screen.getByRole('button', { name: /guardar/i }));

    // Sobre fecha, monto y nota, que son únicos: 'Sueldo' aparece también en la fila ya cargada.
    expect(await screen.findByText('16/08/2026')).not.toBeNull();
    expect(screen.getByText('ARS 900.000,00')).not.toBeNull();
    expect(screen.getByText('Sueldo de agosto')).not.toBeNull();
    expect(lecturasDelListado()).toBe(2);
    expect(urlsDelListado()).toEqual([URL_DEL_MES_ACTUAL, URL_DEL_MES_ACTUAL]);
  });
});
