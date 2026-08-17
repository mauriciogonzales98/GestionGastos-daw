import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from './App';
import type { CategoriaDto, MovimientoDto } from './api/tipos';
import { json } from './test/infra';

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
 */
function prepararFetch(recienCreado: MovimientoDto): { lecturasDelListado: () => number } {
  let creado = false;
  let lecturas = 0;
  const falso = vi.fn(async (ruta: string, init?: RequestInit) => {
    if (ruta.startsWith('/api/categorias')) {
      return json(CATEGORIAS);
    }
    if (ruta.startsWith('/api/movimientos') && init?.method === 'POST') {
      creado = true;
      return json(recienCreado, 201);
    }
    if (ruta.startsWith('/api/movimientos')) {
      lecturas += 1;
      const items = creado ? [recienCreado, YA_CARGADO] : [YA_CARGADO];
      return json({ items, recortado: false, total: items.length });
    }
    throw new Error(`Ruta no esperada en el test: ${ruta}`);
  });
  vi.stubGlobal('fetch', falso);
  return { lecturasDelListado: () => lecturas };
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('App', () => {
  it('App_TrasUnAltaDeGasto_ElListadoMuestraElMovimientoNuevo', async () => {
    const usuario = userEvent.setup();
    const { lecturasDelListado } = prepararFetch(GASTO_CREADO);

    render(<App />);

    // La ausencia previa es la mitad que importa: sin ella, un listado que ya traía la fila daría
    // verde sin que la recarga hubiera pasado.
    await screen.findByText('01/08/2026');
    expect(screen.queryByText('17/08/2026')).toBeNull();
    expect(lecturasDelListado()).toBe(1);

    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.type(screen.getByLabelText('Monto'), '1500.50');
    await usuario.click(screen.getByRole('button', { name: /guardar/i }));

    // Esta espera es la que mata al mutante que borra `onCreado` de App.tsx (AC-05): sin la costura
    // formulario → version → listado, el POST responde 201 pero la tabla nunca se recarga.
    expect(await screen.findByText('17/08/2026')).not.toBeNull();
    expect(screen.getByText('ARS 1.500,50')).not.toBeNull();
    expect(screen.getByText('Supermercado')).not.toBeNull();

    // Cota superior, y lo único que la vigila: `cargar` es un `useCallback([])`, así que unas deps
    // mal puestas darían un fetch en bucle que la aserción del DOM no vería —la fila aparecería
    // igual—. Sincrónica a propósito: `waitFor` resuelve en el primer poll que no lanza, y a esta
    // altura ya vale 2, así que nunca observaría una tercera lectura posterior.
    expect(lecturasDelListado()).toBe(2);
  });

  it('App_TrasUnAltaDeIngreso_ElListadoMuestraElMovimientoNuevo', async () => {
    const usuario = userEvent.setup();
    const { lecturasDelListado } = prepararFetch(INGRESO_CREADO);

    render(<App />);

    await screen.findByText('01/08/2026');
    expect(screen.queryByText('16/08/2026')).toBeNull();

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
  });
});
