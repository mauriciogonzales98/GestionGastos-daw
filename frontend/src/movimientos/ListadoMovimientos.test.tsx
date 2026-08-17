import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ListadoMovimientos } from './ListadoMovimientos';
import type { ListadoMovimientosResponse, MovimientoDto } from '../api/tipos';

const MOVIMIENTO_1: MovimientoDto = {
  id: 1,
  tipo: 'gasto',
  categoria: { id: 1, nombre: 'Comida' },
  monto: 1500.5,
  moneda: 'ARS',
  fecha: '2026-08-17',
  nota: 'Supermercado',
};

const MOVIMIENTO_2: MovimientoDto = {
  id: 2,
  tipo: 'ingreso',
  categoria: { id: 8, nombre: 'Sueldo' },
  monto: 250000,
  moneda: 'ARS',
  fecha: '2026-08-01',
  nota: null,
};

function json(cuerpo: unknown, estado = 200): Response {
  return new Response(JSON.stringify(cuerpo), {
    status: estado,
    headers: { 'Content-Type': 'application/json' },
  });
}

function respuestaListado(items: MovimientoDto[], recortado = false): ListadoMovimientosResponse {
  return { items, recortado, total: items.length };
}

function prepararFetch(respuestas: (() => Promise<Response>)[]): ReturnType<typeof vi.fn> {
  let indice = 0;
  const falso = vi.fn(async () => {
    const siguiente = obtener(respuestas, Math.min(indice, respuestas.length - 1));
    indice += 1;
    return await siguiente();
  });
  vi.stubGlobal('fetch', falso);
  return falso as unknown as ReturnType<typeof vi.fn>;
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
  vi.restoreAllMocks();
});

describe('ListadoMovimientos', () => {
  it('Listado_MuestraGastosEIngresos_OrdenadosPorFecha', async () => {
    prepararFetch([async () => json(respuestaListado([MOVIMIENTO_1, MOVIMIENTO_2]))]);
    render(<ListadoMovimientos version={0} />);

    const filas = await screen.findAllByRole('row');
    // La primera fila es el encabezado; las siguientes respetan el orden que envía el backend.
    expect(within(obtener(filas, 1)).getByText('17/08/2026')).not.toBeNull();
    expect(within(obtener(filas, 2)).getByText('01/08/2026')).not.toBeNull();
  });

  it('Listado_CadaFilaMuestraLosCincoDatos', async () => {
    prepararFetch([async () => json(respuestaListado([MOVIMIENTO_1]))]);
    render(<ListadoMovimientos version={0} />);

    const filas = await screen.findAllByRole('row');
    const celdas = within(obtener(filas, 1)).getAllByRole('cell');
    expect(celdas).toHaveLength(5);
    expect(obtener(celdas, 0).textContent).toBe('17/08/2026');
    expect(obtener(celdas, 1).textContent).toBe('gasto');
    expect(obtener(celdas, 2).textContent).toBe('Comida');
    expect(obtener(celdas, 3).textContent).toBe('ARS 1.500,50');
    expect(obtener(celdas, 4).textContent).toBe('Supermercado');
  });

  it('Listado_MuestraLaMonedaJuntoAlMonto', async () => {
    prepararFetch([async () => json(respuestaListado([MOVIMIENTO_1]))]);
    render(<ListadoMovimientos version={0} />);

    expect(await screen.findByText('ARS 1.500,50')).not.toBeNull();
  });

  it('Listado_MuestraLaNota', async () => {
    prepararFetch([async () => json(respuestaListado([MOVIMIENTO_1]))]);
    render(<ListadoMovimientos version={0} />);

    expect(await screen.findByText('Supermercado')).not.toBeNull();
  });

  it('Listado_TrasUnAlta_MuestraElMovimientoNuevo', async () => {
    const NUEVO: MovimientoDto = {
      id: 3,
      tipo: 'gasto',
      categoria: { id: 2, nombre: 'Transporte' },
      monto: 300,
      moneda: 'ARS',
      fecha: '2026-08-18',
      nota: null,
    };
    prepararFetch([
      async () => json(respuestaListado([MOVIMIENTO_1])),
      async () => json(respuestaListado([NUEVO, MOVIMIENTO_1])),
    ]);

    const { rerender } = render(<ListadoMovimientos version={0} />);
    await screen.findByText('17/08/2026');
    expect(screen.queryByText('18/08/2026')).toBeNull();

    // Simula el refresco tras un alta exitosa: el padre incrementa `version`.
    rerender(<ListadoMovimientos version={1} />);

    expect(await screen.findByText('18/08/2026')).not.toBeNull();
  });

  it('Listado_NotaNula_CeldaVaciaSinRelleno', async () => {
    prepararFetch([async () => json(respuestaListado([MOVIMIENTO_2]))]);
    render(<ListadoMovimientos version={0} />);

    const filas = await screen.findAllByRole('row');
    const celdaDeNota = obtener(within(obtener(filas, 1)).getAllByRole('cell'), 4);
    expect(celdaDeNota.textContent).toBe('');
  });

  it('Listado_SinMovimientos_MuestraEstadoVacio', async () => {
    prepararFetch([async () => json(respuestaListado([]))]);
    render(<ListadoMovimientos version={0} />);

    expect(await screen.findByText(/todavía no hay movimientos/i)).not.toBeNull();
    expect(screen.queryByRole('table')).toBeNull();
  });

  it('Listado_ErrorDeRed_MuestraMensajeYPermiteReintentar', async () => {
    const usuario = userEvent.setup();
    let intentos = 0;
    const falso = vi.fn(async () => {
      intentos += 1;
      if (intentos === 1) {
        throw new TypeError('Failed to fetch');
      }
      return json(respuestaListado([MOVIMIENTO_1]));
    });
    vi.stubGlobal('fetch', falso);

    render(<ListadoMovimientos version={0} />);

    await screen.findByRole('alert');
    const botonReintentar = screen.getByRole('button', { name: /reintentar/i });
    await usuario.click(botonReintentar);

    await waitFor(() => {
      expect(screen.queryByRole('alert')).toBeNull();
    });
    expect(await screen.findByText('17/08/2026')).not.toBeNull();
  });

  it('Listado_Recortado_AvisaAlUsuario', async () => {
    prepararFetch([async () => json(respuestaListado([MOVIMIENTO_1], true))]);
    render(<ListadoMovimientos version={0} />);

    expect(await screen.findByText(/500 movimientos más recientes/i)).not.toBeNull();
  });

  it('Listado_NotaConHtml_SeMuestraComoTextoPlano', async () => {
    const conHtml: MovimientoDto = {
      ...MOVIMIENTO_1,
      nota: '<img src=x onerror="alert(1)">',
    };
    prepararFetch([async () => json(respuestaListado([conHtml]))]);
    render(<ListadoMovimientos version={0} />);

    const texto = await screen.findByText('<img src=x onerror="alert(1)">');
    expect(texto.tagName.toLowerCase()).not.toBe('img');
    expect(document.querySelector('img')).toBeNull();
  });
});
