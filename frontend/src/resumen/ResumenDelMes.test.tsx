import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ResumenDelMes } from './ResumenDelMes';
import type { ResumenMensual } from '../api/tipos';
import { json } from '../test/infra';

/**
 * El período viene en la respuesta y no del reloj: el rótulo tiene que salir del mismo lugar que
 * el cálculo, así que estas fixtures fijan `mes` y `anio` y ningún test toca la fecha del sistema.
 */
const RESUMEN_DE_AGOSTO: ResumenMensual = {
  mes: 8,
  anio: 2026,
  totalIngresado: 250000,
  totalGastado: 1500.5,
  balance: 248499.5,
  // AC-05: el desglose suma exactamente el total gastado (1200,50 + 300 = 1500,50).
  desglose: [
    { categoriaId: 1, categoriaNombre: 'Comida', total: 1200.5 },
    { categoriaId: 2, categoriaNombre: 'Transporte', total: 300 },
  ],
};

/** Mes con más gasto que ingreso: el balance queda en negativo (AC-03). */
const RESUMEN_NEGATIVO: ResumenMensual = {
  mes: 2,
  anio: 2026,
  totalIngresado: 1000,
  totalGastado: 1150.5,
  balance: -150.5,
  desglose: [{ categoriaId: 1, categoriaNombre: 'Comida', total: 1150.5 }],
};

/** Mes sin movimientos: ceros y desglose vacío, que no es un error (AC-02). */
const RESUMEN_VACIO: ResumenMensual = {
  mes: 8,
  anio: 2026,
  totalIngresado: 0,
  totalGastado: 0,
  balance: 0,
  desglose: [],
};

/**
 * Registra **la URL de cada lectura**, no solo su orden: sin eso, un resumen que pidiera otra ruta
 * —o que le colgara un período por query string, que el servidor no acepta— daría verde igual.
 */
function prepararFetch(respuestas: (() => Promise<Response>)[]): { urls: () => string[] } {
  let indice = 0;
  const urls: string[] = [];
  const falso = vi.fn(async (ruta: string) => {
    urls.push(ruta);
    const siguiente = obtener(respuestas, Math.min(indice, respuestas.length - 1));
    indice += 1;
    return await siguiente();
  });
  vi.stubGlobal('fetch', falso);
  return { urls: () => urls };
}

/**
 * Lee el valor que acompaña a un rótulo. Va por el DOM y no por un `getByText` del importe suelto
 * porque tres totales sin rótulo no se distinguen entre sí: un componente que mostrara el gastado
 * donde va el ingresado pasaría igual.
 */
function total(rotulo: string): string {
  const termino = screen.getByText(rotulo);
  const valor = termino.nextElementSibling;
  if (valor === null) {
    throw new Error(`El rótulo "${rotulo}" no tiene ningún valor al lado.`);
  }
  return valor.textContent ?? '';
}

/** `noUncheckedIndexedAccess` exige descartar `undefined`; el índice siempre existe en estos tests. */
function obtener<T>(lista: T[], indice: number): T {
  const elemento = lista[indice];
  if (elemento === undefined) {
    throw new Error(`Índice fuera de rango: ${String(indice)}`);
  }
  return elemento;
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('ResumenDelMes', () => {
  it('Resumen_MuestraLosTresTotalesYElMes', async () => {
    const { urls } = prepararFetch([async () => json(RESUMEN_DE_AGOSTO)]);

    render(<ResumenDelMes version={0} />);

    // AC-09: el rótulo del período sale de la respuesta, no del reloj del navegador.
    expect(await screen.findByText('Agosto 2026')).not.toBeNull();
    // AC-01: los tres totales, cada uno en su rótulo.
    expect(total('Total ingresado')).toBe('ARS 250.000,00');
    expect(total('Total gastado')).toBe('ARS 1.500,50');
    expect(total('Balance')).toBe('ARS 248.499,50');
    // El período lo fija el servidor (FR-03): el cliente pide la ruta pelada, sin parámetros.
    expect(urls()).toEqual(['/api/resumen']);
  });

  it('Resumen_ConBalanceNegativo_LoMuestraConSigno', async () => {
    prepararFetch([async () => json(RESUMEN_NEGATIVO)]);

    render(<ResumenDelMes version={0} />);

    expect(await screen.findByText('Febrero 2026')).not.toBeNull();
    // AC-03: la distinción va en el texto y no solo en el color, que ni se puede asertar sin
    // depender de estilos ni sirve a quien no lo distingue.
    expect(total('Balance')).toBe('ARS -150,50');
    expect(total('Balance')).toContain('-');
    // Y el negativo no se cuela en los otros dos totales, que siguen siendo positivos.
    expect(total('Total gastado')).toBe('ARS 1.150,50');
  });

  it('Resumen_MuestraElDesglosePorCategoria', async () => {
    prepararFetch([async () => json(RESUMEN_DE_AGOSTO)]);

    render(<ResumenDelMes version={0} />);

    const filas = await screen.findAllByRole('listitem');
    // AC-04: una fila por categoría con gastos, y ninguna más.
    expect(filas).toHaveLength(2);
    expect(within(obtener(filas, 0)).getByText('Comida')).not.toBeNull();
    expect(within(obtener(filas, 0)).getByText('ARS 1.200,50')).not.toBeNull();
    expect(within(obtener(filas, 1)).getByText('Transporte')).not.toBeNull();
    expect(within(obtener(filas, 1)).getByText('ARS 300,00')).not.toBeNull();
    // Lo que el desglose NO trae tampoco se inventa: el catálogo tiene más categorías que estas.
    expect(screen.queryByText('Sueldo')).toBeNull();
  });

  it('Resumen_SinGastosEnElMes_DiceQueNoHayNadaQueDesglosar', async () => {
    prepararFetch([async () => json(RESUMEN_VACIO)]);

    render(<ResumenDelMes version={0} />);

    // AC-02: un mes vacío no es un error, es un mes vacío.
    expect(await screen.findByText(/no hay gastos para desglosar/i)).not.toBeNull();
    expect(total('Total ingresado')).toBe('ARS 0,00');
    expect(total('Total gastado')).toBe('ARS 0,00');
    expect(total('Balance')).toBe('ARS 0,00');
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryAllByRole('listitem')).toHaveLength(0);
  });

  it('Resumen_ConRedCaida_MuestraElErrorConReintento', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const usuario = userEvent.setup();
    const { urls } = prepararFetch([
      () => Promise.reject(new TypeError('Failed to fetch')),
      async () => json(RESUMEN_DE_AGOSTO),
    ]);

    render(<ResumenDelMes version={0} />);

    const aviso = await screen.findByRole('alert');
    expect(aviso.textContent).toMatch(/no pudimos cargar el resumen/i);
    // Sin números a medias: un resumen que fallara mostrando ceros se leería como un mes sin gastos.
    expect(screen.queryByText('Agosto 2026')).toBeNull();

    await usuario.click(screen.getByRole('button', { name: /reintentar/i }));

    expect(await screen.findByText('Agosto 2026')).not.toBeNull();
    expect(total('Total ingresado')).toBe('ARS 250.000,00');
    expect(screen.queryByRole('alert')).toBeNull();
    expect(urls()).toEqual(['/api/resumen', '/api/resumen']);
  });
});
