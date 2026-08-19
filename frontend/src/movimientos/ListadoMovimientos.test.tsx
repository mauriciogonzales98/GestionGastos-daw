import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ListadoMovimientos } from './ListadoMovimientos';
import type { FiltrosDeMovimientos, ListadoMovimientosResponse, MovimientoDto } from '../api/tipos';
import { json } from '../test/infra';

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

/** El rango que el padre baja: acá es fijo, para que la URL esperada no dependa del calendario. */
const FILTROS: FiltrosDeMovimientos = { desde: '2026-08-01', hasta: '2026-08-31' };

function respuestaListado(items: MovimientoDto[], recortado = false): ListadoMovimientosResponse {
  return { items, recortado, total: items.length };
}

/**
 * Registra **la URL de cada lectura**, no solo su orden: sin eso, un listado que se olvidara de
 * pasar los filtros al cliente daría verde en todos los tests de este archivo.
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
 * Las acciones por fila son obligatorias: una fila con botones que no llaman a nadie sería peor que
 * no tenerlos. Los tests que no las ejercen igual las pasan, con espías que nadie mira.
 */
function acciones() {
  return { onEditar: vi.fn(), onEliminar: vi.fn() };
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

describe('ListadoMovimientos', () => {
  it('Listado_MuestraGastosEIngresos_OrdenadosPorFecha', async () => {
    const { urls } = prepararFetch([
      async () => json(respuestaListado([MOVIMIENTO_1, MOVIMIENTO_2])),
    ]);
    render(<ListadoMovimientos version={0} filtros={FILTROS} {...acciones()} />);

    const filas = await screen.findAllByRole('row');
    // La primera fila es el encabezado; las siguientes respetan el orden que envía el backend.
    expect(within(obtener(filas, 1)).getByText('17/08/2026')).not.toBeNull();
    expect(within(obtener(filas, 2)).getByText('01/08/2026')).not.toBeNull();
    // Los filtros que el padre bajó viajaron en la petición, y no se quedaron en el estado.
    expect(urls()).toEqual(['/api/movimientos?desde=2026-08-01&hasta=2026-08-31']);
  });

  it('Listado_CadaFilaMuestraLosCincoDatos', async () => {
    prepararFetch([async () => json(respuestaListado([MOVIMIENTO_1]))]);
    render(<ListadoMovimientos version={0} filtros={FILTROS} {...acciones()} />);

    const filas = await screen.findAllByRole('row');
    const celdas = within(obtener(filas, 1)).getAllByRole('cell');
    // Cinco datos y una sexta celda con las acciones de la fila, que no es un dato del movimiento.
    expect(celdas).toHaveLength(6);
    expect(obtener(celdas, 0).textContent).toBe('17/08/2026');
    expect(obtener(celdas, 1).textContent).toBe('gasto');
    expect(obtener(celdas, 2).textContent).toBe('Comida');
    expect(obtener(celdas, 3).textContent).toBe('ARS 1.500,50');
    expect(obtener(celdas, 4).textContent).toBe('Supermercado');
  });

  it('Listado_MuestraLaMonedaJuntoAlMonto', async () => {
    prepararFetch([async () => json(respuestaListado([MOVIMIENTO_1]))]);
    render(<ListadoMovimientos version={0} filtros={FILTROS} {...acciones()} />);

    expect(await screen.findByText('ARS 1.500,50')).not.toBeNull();
  });

  it('Listado_MuestraLaNota', async () => {
    prepararFetch([async () => json(respuestaListado([MOVIMIENTO_1]))]);
    render(<ListadoMovimientos version={0} filtros={FILTROS} {...acciones()} />);

    expect(await screen.findByText('Supermercado')).not.toBeNull();
  });

  it('Listado_MuestraElRangoVigente', async () => {
    prepararFetch([async () => json(respuestaListado([MOVIMIENTO_1]))]);
    render(<ListadoMovimientos version={0} filtros={FILTROS} {...acciones()} />);

    // Mitigación del PRD: el rango se ve siempre, para que nadie crea que perdió los movimientos
    // de los meses anteriores.
    expect(
      await screen.findByText('Mostrando movimientos del 01/08/2026 al 31/08/2026.'),
    ).not.toBeNull();
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
    const { urls } = prepararFetch([
      async () => json(respuestaListado([MOVIMIENTO_1])),
      async () => json(respuestaListado([NUEVO, MOVIMIENTO_1])),
    ]);

    const { rerender } = render(
      <ListadoMovimientos version={0} filtros={FILTROS} {...acciones()} />,
    );
    await screen.findByText('17/08/2026');
    expect(screen.queryByText('18/08/2026')).toBeNull();

    // Simula el refresco tras un alta exitosa: el padre incrementa `version`.
    rerender(<ListadoMovimientos version={1} filtros={FILTROS} {...acciones()} />);

    expect(await screen.findByText('18/08/2026')).not.toBeNull();
    // La recarga tampoco pierde los filtros por el camino.
    expect(urls()).toEqual([
      '/api/movimientos?desde=2026-08-01&hasta=2026-08-31',
      '/api/movimientos?desde=2026-08-01&hasta=2026-08-31',
    ]);
  });

  it('Listado_AlCambiarLosFiltros_VuelveAPedirConElRangoNuevo', async () => {
    const { urls } = prepararFetch([
      async () => json(respuestaListado([MOVIMIENTO_1])),
      async () => json(respuestaListado([])),
    ]);

    const { rerender } = render(
      <ListadoMovimientos version={0} filtros={FILTROS} {...acciones()} />,
    );
    await screen.findByText('17/08/2026');

    rerender(
      <ListadoMovimientos
        version={0}
        filtros={{ categoriaId: 8, desde: '2026-07-01' }}
        {...acciones()}
      />,
    );

    expect(await screen.findByText(/todavía no hay movimientos/i)).not.toBeNull();
    expect(obtener(urls(), 1)).toBe('/api/movimientos?categoriaId=8&desde=2026-07-01');
    expect(screen.getByText('Mostrando movimientos desde el 01/07/2026.')).not.toBeNull();
  });

  it('Listado_NotaNula_CeldaVaciaSinRelleno', async () => {
    prepararFetch([async () => json(respuestaListado([MOVIMIENTO_2]))]);
    render(<ListadoMovimientos version={0} filtros={FILTROS} {...acciones()} />);

    const filas = await screen.findAllByRole('row');
    const celdaDeNota = obtener(within(obtener(filas, 1)).getAllByRole('cell'), 4);
    expect(celdaDeNota.textContent).toBe('');
  });

  it('Listado_SinMovimientos_MuestraEstadoVacio', async () => {
    prepararFetch([async () => json(respuestaListado([]))]);
    render(<ListadoMovimientos version={0} filtros={FILTROS} {...acciones()} />);

    expect(await screen.findByText(/todavía no hay movimientos/i)).not.toBeNull();
    expect(screen.queryByRole('table')).toBeNull();
    // Sin resultados es justo cuando el usuario podría creer que se le perdieron los datos: el
    // rango tiene que seguir a la vista.
    expect(screen.getByText('Mostrando movimientos del 01/08/2026 al 31/08/2026.')).not.toBeNull();
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

    render(<ListadoMovimientos version={0} filtros={FILTROS} {...acciones()} />);

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
    render(<ListadoMovimientos version={0} filtros={FILTROS} {...acciones()} />);

    expect(await screen.findByText(/500 movimientos más recientes/i)).not.toBeNull();
  });

  it('Listado_NotaConHtml_SeMuestraComoTextoPlano', async () => {
    const conHtml: MovimientoDto = {
      ...MOVIMIENTO_1,
      nota: '<img src=x onerror="alert(1)">',
    };
    prepararFetch([async () => json(respuestaListado([conHtml]))]);
    render(<ListadoMovimientos version={0} filtros={FILTROS} {...acciones()} />);

    const texto = await screen.findByText('<img src=x onerror="alert(1)">');
    expect(texto.tagName.toLowerCase()).not.toBe('img');
    expect(document.querySelector('img')).toBeNull();
  });

  it('Listado_CadaFilaOfreceEditarYEliminar', async () => {
    const usuario = userEvent.setup();
    prepararFetch([async () => json(respuestaListado([MOVIMIENTO_1, MOVIMIENTO_2]))]);
    const onEditar = vi.fn();
    const onEliminar = vi.fn();
    render(
      <ListadoMovimientos
        version={0}
        filtros={FILTROS}
        onEditar={onEditar}
        onEliminar={onEliminar}
      />,
    );

    const filas = await screen.findAllByRole('row');
    // El nombre accesible nombra el movimiento: con varias filas, un "Editar" a secas deja al
    // lector de pantalla —y al test— sin saber cuál de todas.
    const editar = within(obtener(filas, 1)).getByRole('button', {
      name: 'Editar el movimiento del 17/08/2026 de Comida',
    });
    await usuario.click(editar);
    expect(onEditar).toHaveBeenCalledWith(MOVIMIENTO_1);

    const eliminar = within(obtener(filas, 2)).getByRole('button', {
      name: 'Eliminar el movimiento del 01/08/2026 de Sueldo',
    });
    await usuario.click(eliminar);
    expect(onEliminar).toHaveBeenCalledWith(MOVIMIENTO_2);

    // Ninguna acción se dispara sola por el hecho de renderizar la fila.
    expect(onEditar).toHaveBeenCalledTimes(1);
    expect(onEliminar).toHaveBeenCalledTimes(1);
  });
});
