import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ConfirmarEliminacion } from './ConfirmarEliminacion';
import type { MovimientoDto } from '../api/tipos';
import { json } from '../test/infra';

const MOVIMIENTO: MovimientoDto = {
  id: 7,
  tipo: 'gasto',
  categoria: { id: 1, nombre: 'Comida' },
  monto: 1500.5,
  moneda: 'ARS',
  fecha: '2026-08-17',
  nota: 'Supermercado',
};

/** Una petición registrada por el doble: método y ruta, que es lo único que este diálogo emite. */
interface Peticion {
  metodo: string;
  ruta: string;
}

function prepararFetch(respuesta?: () => Promise<Response>): { peticiones: () => Peticion[] } {
  const peticiones: Peticion[] = [];
  const falso = vi.fn(async (ruta: string, init?: RequestInit) => {
    peticiones.push({ metodo: init?.method ?? 'GET', ruta });
    return respuesta ? await respuesta() : new Response(null, { status: 204 });
  });
  vi.stubGlobal('fetch', falso);
  return { peticiones: () => peticiones };
}

/** `noUncheckedIndexedAccess` exige descartar `undefined`; el índice siempre existe acá. */
function obtener<T>(lista: T[], indice: number): T {
  const elemento = lista[indice];
  if (elemento === undefined) {
    throw new Error(`Índice fuera de rango: ${String(indice)}`);
  }
  return elemento;
}

function renderizar(movimiento: MovimientoDto = MOVIMIENTO) {
  const onEliminado = vi.fn();
  const onCancelar = vi.fn();
  const onNoEncontrado = vi.fn();
  render(
    <ConfirmarEliminacion
      movimiento={movimiento}
      onEliminado={onEliminado}
      onCancelar={onCancelar}
      onNoEncontrado={onNoEncontrado}
    />,
  );
  return { onEliminado, onCancelar, onNoEncontrado };
}

const seleccionar = {
  confirmar: () => screen.getByRole('button', { name: /sí, eliminar/i }),
  cancelar: () => screen.getByRole('button', { name: /^cancelar$/i }),
};

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('ConfirmarEliminacion', () => {
  it('Eliminar_PideConfirmacionAntesDeLlamar', async () => {
    const usuario = userEvent.setup();
    const { peticiones } = prepararFetch();
    const { onEliminado } = renderizar();

    // Abrir el diálogo no borra nada: la mitigación R-19 es exactamente esta ventana entre el
    // click en la fila y la petición.
    expect(screen.getByRole('dialog')).not.toBeNull();
    expect(peticiones()).toHaveLength(0);
    expect(onEliminado).not.toHaveBeenCalled();

    await usuario.click(seleccionar.confirmar());

    await waitFor(() => expect(onEliminado).toHaveBeenCalledTimes(1));
    expect(peticiones()).toHaveLength(1);
    expect(obtener(peticiones(), 0)).toEqual({ metodo: 'DELETE', ruta: '/api/movimientos/7' });
  });

  it('Eliminar_AlCancelar_NoLlamaAlServidor', async () => {
    const usuario = userEvent.setup();
    const { peticiones } = prepararFetch();
    const { onCancelar, onEliminado } = renderizar();

    await usuario.click(seleccionar.cancelar());

    // El borrado es definitivo —sin baja lógica, sin papelera—, así que cancelar no puede dejar
    // ninguna petición en el camino.
    expect(peticiones()).toHaveLength(0);
    expect(onCancelar).toHaveBeenCalledTimes(1);
    expect(onEliminado).not.toHaveBeenCalled();
  });

  it('Eliminar_NombraElMovimientoYAvisaQueEsDefinitivo', async () => {
    prepararFetch();
    renderizar();

    const dialogo = screen.getByRole('dialog');
    expect(dialogo.textContent).toContain('17/08/2026');
    expect(dialogo.textContent).toContain('Comida');
    expect(dialogo.textContent).toContain('ARS 1.500,50');
    expect(dialogo.textContent).toMatch(/no se puede deshacer/i);
  });

  it('Eliminar_LaNotaSeMuestraComoTextoPlano', async () => {
    prepararFetch();
    renderizar({ ...MOVIMIENTO, nota: '<img src=x onerror="alert(1)">' });

    const texto = screen.getByText(/<img src=x onerror="alert\(1\)">/);
    expect(texto.tagName.toLowerCase()).not.toBe('img');
    expect(document.querySelector('img')).toBeNull();
  });

  it('Eliminar_ConRedCaida_MuestraElErrorConReintento', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const usuario = userEvent.setup();
    let caida = true;
    const { peticiones } = prepararFetch(async () => {
      if (caida) {
        throw new TypeError('Failed to fetch');
      }
      return new Response(null, { status: 204 });
    });
    const { onEliminado } = renderizar();

    await usuario.click(seleccionar.confirmar());

    const aviso = await screen.findByRole('alert');
    expect(aviso.textContent).toMatch(/conectar/i);
    expect(onEliminado).not.toHaveBeenCalled();

    caida = false;
    await usuario.click(screen.getByRole('button', { name: /reintentar/i }));

    await waitFor(() => expect(onEliminado).toHaveBeenCalledTimes(1));
    expect(peticiones()).toHaveLength(2);
  });

  it('Eliminar_ConMovimientoYaBorrado_AvisaAlPadre', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const usuario = userEvent.setup();
    prepararFetch(async () =>
      json({ status: 404, title: 'El movimiento no existe.' }, 404, 'application/problem+json'),
    );
    const { onEliminado, onNoEncontrado } = renderizar();

    await usuario.click(seleccionar.confirmar());

    // Un 404 no es un fallo: el movimiento ya no está, y el padre tiene que refrescar en vez de
    // ofrecer reintentar un borrado que nunca va a poder ocurrir.
    await waitFor(() => expect(onNoEncontrado).toHaveBeenCalledTimes(1));
    expect(onEliminado).not.toHaveBeenCalled();
  });
});
