import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from './App';
import type { CategoriaDto, MovimientoDto, ResumenMensual } from './api/tipos';
import { json } from './test/infra';

/** Instante fijo: el rango por defecto es el mes en curso y no puede depender del día de la corrida. */
const AHORA = new Date(2026, 7, 18, 12, 0, 0);

/** El rango que `App` tiene que pedir sola al abrirse, derivado de `AHORA`. */
const URL_DEL_MES_ACTUAL = '/api/movimientos?desde=2026-08-01&hasta=2026-08-31';

const CATEGORIAS: CategoriaDto[] = [
  { id: 1, nombre: 'Comida', tipo: 'gasto' },
  { id: 2, nombre: 'Transporte', tipo: 'gasto' },
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

/** El movimiento sobre el que se ejercen la edición y la eliminación. */
const EDITABLE: MovimientoDto = {
  id: 5,
  tipo: 'gasto',
  categoria: { id: 1, nombre: 'Comida' },
  monto: 1500.5,
  moneda: 'ARS',
  fecha: '2026-08-17',
  nota: 'Supermercado',
};

/**
 * Lo que el doble devuelve en `/api/resumen` mientras el test no pida otra cosa. Va en cero a
 * propósito: los tests de movimientos no miran el resumen, y devolver ahí los mismos importes que
 * el listado volvería ambiguas sus búsquedas por texto —el mismo `ARS 1.500,50` estaría en la fila
 * y en el resumen—. Los tres tests del resumen piden el cálculo real con `resumen: 'calculado'`.
 */
const RESUMEN_EN_CERO: ResumenMensual = {
  mes: AHORA.getMonth() + 1,
  anio: AHORA.getFullYear(),
  totalIngresado: 0,
  totalGastado: 0,
  balance: 0,
  desglose: [],
};

interface Peticion {
  metodo: string;
  ruta: string;
  cuerpo: Record<string, unknown> | null;
}

interface ServidorFalso {
  lecturasDelListado: () => number;
  /** Cuántas veces se pidió `/api/resumen`: es lo que distingue "no cambió" de "no se pidió". */
  lecturasDelResumen: () => number;
  urlsDelListado: () => string[];
  peticionesCon: (metodo: string) => Peticion[];
}

/**
 * Servidor falso con **estado**: el alta agrega, el `PUT` modifica y el `DELETE` quita, de modo que
 * la lectura siguiente del listado devuelva otra cosa que la anterior. Si todas las lecturas
 * devolvieran lo mismo, estos tests darían verde aunque la recarga nunca ocurriera, que es
 * exactamente el agujero que cierran.
 *
 * **Honra la query string**, además: filtra por categoría y por rango con los dos extremos
 * incluidos, y guarda cada URL pedida. Ignorarla dejaría pasar un default del mes actual que nunca
 * se aplicara — y, con la edición, una fila que sale del rango y sigue mostrándose igual.
 */
function prepararServidor(opciones?: {
  iniciales?: MovimientoDto[];
  alCrear?: MovimientoDto;
  /** Simula que ya lo borraron desde otra pestaña: `PUT` y `DELETE` responden 404 y la fila no está. */
  yaBorrado?: boolean;
  /**
   * Qué contesta `/api/resumen`: el resumen en cero por omisión, el derivado de los movimientos
   * guardados (`'calculado'`, el único que cambia tras un alta) o un 500 (`'error'`).
   */
  resumen?: 'calculado' | 'error';
}): ServidorFalso {
  const movimientos = [...(opciones?.iniciales ?? [YA_CARGADO])];
  const urls: string[] = [];
  const peticiones: Peticion[] = [];
  const yaBorrado = opciones?.yaBorrado === true;
  let lecturasDelResumen = 0;

  const falso = vi.fn(async (ruta: string, init?: RequestInit) => {
    if (ruta.startsWith('/api/categorias')) {
      return json(CATEGORIAS);
    }
    if (ruta.startsWith('/api/resumen')) {
      lecturasDelResumen += 1;
      if (opciones?.resumen === 'error') {
        return json({ status: 500, title: 'Algo se rompió.' }, 500, 'application/problem+json');
      }
      return json(
        opciones?.resumen === 'calculado' ? calcularResumen(movimientos) : RESUMEN_EN_CERO,
      );
    }
    if (!ruta.startsWith('/api/movimientos')) {
      throw new Error(`Ruta no esperada en el test: ${ruta}`);
    }

    const metodo = init?.method ?? 'GET';
    const cuerpo =
      init?.body === undefined || init.body === null
        ? null
        : (JSON.parse(String(init.body)) as Record<string, unknown>);
    peticiones.push({ metodo, ruta, cuerpo });

    if (metodo === 'POST') {
      const creado = opciones?.alCrear;
      if (creado === undefined) {
        throw new Error('El test no configuró qué devuelve el alta.');
      }
      movimientos.unshift(creado);
      return json(creado, 201);
    }

    if (metodo === 'PUT' || metodo === 'DELETE') {
      const indice = movimientos.findIndex((movimiento) => movimiento.id === idDeLaRuta(ruta));
      if (yaBorrado || indice === -1) {
        if (indice !== -1) {
          movimientos.splice(indice, 1);
        }
        return json(
          { status: 404, title: 'El movimiento no existe.' },
          404,
          'application/problem+json',
        );
      }
      if (metodo === 'DELETE') {
        movimientos.splice(indice, 1);
        return new Response(null, { status: 204 });
      }
      const actualizado = aplicar(obtener(movimientos, indice), cuerpo);
      movimientos[indice] = actualizado;
      return json(actualizado);
    }

    urls.push(ruta);
    const url = new URL(ruta, 'http://localhost');
    const categoriaId = url.searchParams.get('categoriaId');
    const desde = url.searchParams.get('desde');
    const hasta = url.searchParams.get('hasta');
    const items = movimientos.filter(
      (movimiento) =>
        (categoriaId === null || movimiento.categoria.id === Number(categoriaId)) &&
        (desde === null || movimiento.fecha >= desde) &&
        (hasta === null || movimiento.fecha <= hasta),
    );
    return json({ items, recortado: false, total: items.length });
  });

  vi.stubGlobal('fetch', falso);
  return {
    lecturasDelListado: () => urls.length,
    lecturasDelResumen: () => lecturasDelResumen,
    urlsDelListado: () => urls,
    peticionesCon: (metodo) => peticiones.filter((peticion) => peticion.metodo === metodo),
  };
}

/**
 * Agrega lo que el doble tiene guardado, como haría el endpoint real. No recorta por mes: todas las
 * fechas de estos tests caen dentro del mes de `AHORA`, y el recorte es cosa del backend —está
 * cubierto por `ResumenTests` en el Block 1—.
 */
function calcularResumen(movimientos: MovimientoDto[]): ResumenMensual {
  const totalIngresado = sumar(movimientos.filter((movimiento) => movimiento.tipo === 'ingreso'));
  const gastos = movimientos.filter((movimiento) => movimiento.tipo === 'gasto');
  const porCategoria = new Map<number, { categoriaNombre: string; total: number }>();
  for (const gasto of gastos) {
    const acumulado = porCategoria.get(gasto.categoria.id);
    if (acumulado === undefined) {
      porCategoria.set(gasto.categoria.id, {
        categoriaNombre: gasto.categoria.nombre,
        total: gasto.monto,
      });
    } else {
      acumulado.total += gasto.monto;
    }
  }
  const totalGastado = sumar(gastos);
  return {
    mes: AHORA.getMonth() + 1,
    anio: AHORA.getFullYear(),
    totalIngresado,
    totalGastado,
    balance: totalIngresado - totalGastado,
    desglose: [...porCategoria].map(([categoriaId, fila]) => ({ categoriaId, ...fila })),
  };
}

function sumar(movimientos: MovimientoDto[]): number {
  return movimientos.reduce((total, movimiento) => total + movimiento.monto, 0);
}

/** Aplica el cuerpo del `PUT` como lo haría el backend: los cuatro campos, y nada más. */
function aplicar(actual: MovimientoDto, cuerpo: Record<string, unknown> | null): MovimientoDto {
  const categoria = CATEGORIAS.find((candidata) => candidata.id === Number(cuerpo?.categoriaId));
  if (categoria === undefined) {
    throw new Error(`Categoría inexistente en el test: ${String(cuerpo?.categoriaId)}`);
  }
  return {
    ...actual,
    categoria: { id: categoria.id, nombre: categoria.nombre },
    monto: Number(cuerpo?.monto),
    fecha: String(cuerpo?.fecha),
    nota: cuerpo?.nota === null ? null : String(cuerpo?.nota),
  };
}

function idDeLaRuta(ruta: string): number {
  return Number(ruta.slice(ruta.lastIndexOf('/') + 1));
}

/** `noUncheckedIndexedAccess` exige descartar `undefined`; el índice siempre existe acá. */
function obtener<T>(lista: T[], indice: number): T {
  const elemento = lista[indice];
  if (elemento === undefined) {
    throw new Error(`Índice fuera de rango: ${String(indice)}`);
  }
  return elemento;
}

/**
 * `<input type="date">` no se completa con `type()`: el control acepta el valor entero, no una
 * secuencia de teclas. `fireEvent.change` es la forma determinista de fijarlo.
 */
function ponerFecha(valor: string): void {
  fireEvent.change(screen.getByLabelText('Fecha'), { target: { value: valor } });
}

/** Texto del mensaje que el control referencia con `aria-describedby`. */
function motivoDe(control: HTMLElement): string {
  const ids = control.getAttribute('aria-describedby')?.split(' ') ?? [];
  return ids
    .map((id) => document.getElementById(id)?.textContent ?? '')
    .join(' ')
    .trim();
}

/** Abre el formulario en modo edición desde la fila y espera a que el catálogo esté cargado. */
async function abrirEdicion(usuario: ReturnType<typeof userEvent.setup>): Promise<void> {
  await usuario.click(
    await screen.findByRole('button', { name: 'Editar el movimiento del 17/08/2026 de Comida' }),
  );
  await screen.findByRole('heading', { name: /editar movimiento/i });
  await waitFor(() =>
    expect(
      (screen.getByLabelText('Categoría') as HTMLSelectElement).options.length,
    ).toBeGreaterThan(1),
  );
}

/** El valor que acompaña a un rótulo dentro del resumen, sin confundirlo con los de la tabla. */
function totalDelResumen(resumen: HTMLElement, rotulo: string): string {
  const termino = within(resumen).getByText(rotulo);
  const valor = termino.nextElementSibling;
  if (valor === null) {
    throw new Error(`El rótulo "${rotulo}" no tiene ningún valor al lado.`);
  }
  return valor.textContent ?? '';
}

/** La sección del resumen, ya cargada: todo lo que se le pregunta se le pregunta adentro. */
async function esperarElResumen(): Promise<HTMLElement> {
  const resumen = await screen.findByRole('region', { name: 'Resumen del mes' });
  await within(resumen).findByText('Agosto 2026');
  return resumen;
}

const seleccionar = {
  monto: () => screen.getByLabelText('Monto') as HTMLInputElement,
  nota: () => screen.getByLabelText(/^Nota/) as HTMLTextAreaElement,
  guardar: () => screen.getByRole('button', { name: /guardar/i }),
};

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
    const servidor = prepararServidor({ alCrear: GASTO_CREADO });

    render(<App />);

    // La ausencia previa es la mitad que importa: sin ella, un listado que ya traía la fila daría
    // verde sin que la recarga hubiera pasado.
    await screen.findByText('01/08/2026');
    expect(screen.queryByText('17/08/2026')).toBeNull();
    expect(servidor.lecturasDelListado()).toBe(1);
    // AC-09: la primera lectura ya pide el mes en curso, no todo el historial.
    expect(servidor.urlsDelListado()).toEqual([URL_DEL_MES_ACTUAL]);

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
    expect(servidor.lecturasDelListado()).toBe(2);
    // Y la recarga tampoco se olvida del rango: las dos lecturas lo llevan.
    expect(servidor.urlsDelListado()).toEqual([URL_DEL_MES_ACTUAL, URL_DEL_MES_ACTUAL]);
  });

  it('App_TrasUnAltaDeIngreso_ElListadoMuestraElMovimientoNuevo', async () => {
    const usuario = userEvent.setup();
    const servidor = prepararServidor({ alCrear: INGRESO_CREADO });

    render(<App />);

    await screen.findByText('01/08/2026');
    expect(screen.queryByText('16/08/2026')).toBeNull();
    expect(servidor.urlsDelListado()).toEqual([URL_DEL_MES_ACTUAL]);

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
    expect(servidor.lecturasDelListado()).toBe(2);
    expect(servidor.urlsDelListado()).toEqual([URL_DEL_MES_ACTUAL, URL_DEL_MES_ACTUAL]);
  });

  it('Editar_GuardaYActualizaLaFila', async () => {
    const usuario = userEvent.setup();
    const servidor = prepararServidor({ iniciales: [EDITABLE] });

    render(<App />);
    await screen.findByText('ARS 1.500,50');

    await abrirEdicion(usuario);

    // El formulario abre con los valores del movimiento, no en blanco.
    expect(seleccionar.monto().value).toBe('1500.5');
    expect(seleccionar.nota().value).toBe('Supermercado');

    await usuario.clear(seleccionar.monto());
    await usuario.type(seleccionar.monto(), '2000');
    await usuario.clear(seleccionar.nota());
    await usuario.type(seleccionar.nota(), 'Cena');
    await usuario.click(seleccionar.guardar());

    // La costura real —formulario → PUT → recarga del listado— montando `App`: sin ella el PUT
    // respondería 200 y la tabla seguiría mostrando el monto viejo (AC-01 y AC-03).
    expect(await screen.findByText('ARS 2.000,00')).not.toBeNull();
    expect(screen.queryByText('ARS 1.500,50')).toBeNull();
    expect(screen.getByText('Cena')).not.toBeNull();
    expect(screen.queryByText('Supermercado')).toBeNull();

    const modificaciones = servidor.peticionesCon('PUT');
    expect(modificaciones).toHaveLength(1);
    expect(obtener(modificaciones, 0).ruta).toBe('/api/movimientos/5');
    expect(obtener(modificaciones, 0).cuerpo).toEqual({
      categoriaId: 1,
      monto: 2000,
      fecha: '2026-08-17',
      nota: 'Cena',
    });
    // La recarga tampoco se olvida del rango vigente.
    expect(servidor.urlsDelListado()).toEqual([URL_DEL_MES_ACTUAL, URL_DEL_MES_ACTUAL]);
  });

  it('Editar_ConDatosInvalidos_MuestraElErrorYNoCambiaLaFila', async () => {
    const usuario = userEvent.setup();
    const servidor = prepararServidor({ iniciales: [EDITABLE] });

    render(<App />);
    await screen.findByText('ARS 1.500,50');
    await abrirEdicion(usuario);

    await usuario.clear(seleccionar.monto());
    await usuario.type(seleccionar.monto(), '0');
    await usuario.click(seleccionar.guardar());

    expect(motivoDe(seleccionar.monto())).toMatch(/mayor a cero/i);
    // AC-04: el rechazo no llega siquiera a salir, y la fila conserva todos sus valores.
    expect(servidor.peticionesCon('PUT')).toHaveLength(0);
    expect(screen.getByText('ARS 1.500,50')).not.toBeNull();
    expect(servidor.lecturasDelListado()).toBe(1);
  });

  it('Editar_CambiandoLaFechaFueraDelRango_QuitaLaFilaDelListado', async () => {
    const usuario = userEvent.setup();
    const servidor = prepararServidor({ iniciales: [EDITABLE] });

    render(<App />);
    await screen.findByText('ARS 1.500,50');
    await abrirEdicion(usuario);

    ponerFecha('2026-09-05');
    await usuario.click(seleccionar.guardar());

    // AC-02: la fecha nueva cae fuera del mes filtrado, así que el movimiento deja de listarse.
    expect(await screen.findByText(/todavía no hay movimientos/i)).not.toBeNull();
    expect(screen.queryByText('ARS 1.500,50')).toBeNull();
    expect(obtener(servidor.peticionesCon('PUT'), 0).cuerpo).toMatchObject({
      fecha: '2026-09-05',
    });
    // Y el rango sigue a la vista: es la mitigación contra creer que el movimiento se perdió.
    expect(screen.getByText('Mostrando movimientos del 01/08/2026 al 31/08/2026.')).not.toBeNull();
  });

  it('Eliminar_AlConfirmar_QuitaLaFila', async () => {
    const usuario = userEvent.setup();
    const servidor = prepararServidor({ iniciales: [EDITABLE] });

    render(<App />);
    await screen.findByText('ARS 1.500,50');

    await usuario.click(
      screen.getByRole('button', { name: 'Eliminar el movimiento del 17/08/2026 de Comida' }),
    );
    // La confirmación es la única red que hay: el borrado es definitivo (R-19).
    expect(screen.getByRole('dialog')).not.toBeNull();
    expect(servidor.peticionesCon('DELETE')).toHaveLength(0);

    await usuario.click(screen.getByRole('button', { name: /sí, eliminar/i }));

    expect(await screen.findByText(/todavía no hay movimientos/i)).not.toBeNull();
    expect(screen.queryByText('ARS 1.500,50')).toBeNull();
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(obtener(servidor.peticionesCon('DELETE'), 0).ruta).toBe('/api/movimientos/5');
    expect(servidor.urlsDelListado()).toEqual([URL_DEL_MES_ACTUAL, URL_DEL_MES_ACTUAL]);
  });

  it('Eliminar_ConMovimientoYaBorrado_MuestraElMensajeYRefresca', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const usuario = userEvent.setup();
    const servidor = prepararServidor({ iniciales: [EDITABLE], yaBorrado: true });

    render(<App />);
    await screen.findByText('ARS 1.500,50');

    await usuario.click(
      screen.getByRole('button', { name: 'Eliminar el movimiento del 17/08/2026 de Comida' }),
    );
    await usuario.click(screen.getByRole('button', { name: /sí, eliminar/i }));

    // El 404 no es un fallo del servidor: es "alguien lo borró antes", y la salida es refrescar.
    expect(await screen.findByText(/el movimiento ya no existe/i)).not.toBeNull();
    expect(screen.queryByRole('dialog')).toBeNull();
    await waitFor(() => expect(servidor.lecturasDelListado()).toBe(2));
    expect(screen.queryByText('ARS 1.500,50')).toBeNull();
  });

  it('Editar_ConMovimientoYaBorrado_MuestraElMensajeYRefresca', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const usuario = userEvent.setup();
    const servidor = prepararServidor({ iniciales: [EDITABLE], yaBorrado: true });

    render(<App />);
    await screen.findByText('ARS 1.500,50');
    await abrirEdicion(usuario);

    await usuario.clear(seleccionar.monto());
    await usuario.type(seleccionar.monto(), '2000');
    await usuario.click(seleccionar.guardar());

    expect(await screen.findByText(/el movimiento ya no existe/i)).not.toBeNull();
    expect(screen.queryByRole('heading', { name: /editar movimiento/i })).toBeNull();
    await waitFor(() => expect(servidor.lecturasDelListado()).toBe(2));
    expect(screen.queryByText('ARS 1.500,50')).toBeNull();
  });
});

/**
 * Los tres tests que no se pueden escribir contra el componente solo: dos ejercen la costura del
 * disparador —el resumen se suscribe a `version` y no a `filtros` (AC-06)— y el tercero, que las
 * dos peticiones de la pantalla son independientes. Montan `App` entera a propósito.
 */
describe('Resumen en la pantalla principal', () => {
  it('Resumen_AlFiltrarElListado_NoCambia', async () => {
    const usuario = userEvent.setup();
    const servidor = prepararServidor({
      iniciales: [YA_CARGADO, EDITABLE],
      resumen: 'calculado',
    });

    render(<App />);
    const resumen = await esperarElResumen();
    expect(totalDelResumen(resumen, 'Total ingresado')).toBe('ARS 250.000,00');
    expect(totalDelResumen(resumen, 'Total gastado')).toBe('ARS 1.500,50');
    expect(totalDelResumen(resumen, 'Balance')).toBe('ARS 248.499,50');
    expect(servidor.lecturasDelResumen()).toBe(1);

    const categoria = screen.getByLabelText('Filtrar por categoría') as HTMLSelectElement;
    await waitFor(() => {
      expect(categoria.disabled).toBe(false);
    });
    await usuario.selectOptions(categoria, '2');
    await usuario.click(screen.getByRole('button', { name: /aplicar filtros/i }));

    // El listado sí se movió: sin esto, un resumen quieto no probaría nada.
    expect(await screen.findByText(/todavía no hay movimientos/i)).not.toBeNull();
    expect(servidor.lecturasDelListado()).toBe(2);

    // AC-06: el resumen no se volvió a pedir y sus tres números siguen siendo los del mes.
    expect(servidor.lecturasDelResumen()).toBe(1);
    expect(totalDelResumen(resumen, 'Total ingresado')).toBe('ARS 250.000,00');
    expect(totalDelResumen(resumen, 'Total gastado')).toBe('ARS 1.500,50');
    expect(totalDelResumen(resumen, 'Balance')).toBe('ARS 248.499,50');
    expect(within(resumen).getByText('Comida')).not.toBeNull();
  });

  it('Resumen_TrasUnAlta_SeActualiza', async () => {
    const usuario = userEvent.setup();
    const servidor = prepararServidor({ alCrear: GASTO_CREADO, resumen: 'calculado' });

    render(<App />);
    const resumen = await esperarElResumen();
    // La ausencia previa es la mitad que importa: sin ella, un resumen que ya trajera el gasto
    // daría verde sin que la recarga hubiera pasado.
    expect(totalDelResumen(resumen, 'Total gastado')).toBe('ARS 0,00');
    expect(within(resumen).queryByText('Comida')).toBeNull();
    expect(servidor.lecturasDelResumen()).toBe(1);

    await usuario.selectOptions(screen.getByLabelText('Categoría'), '1');
    await usuario.type(screen.getByLabelText('Monto'), '1500.50');
    await usuario.click(screen.getByRole('button', { name: /guardar/i }));

    // La otra mitad del disparador: el alta incrementa `version` y el resumen vuelve a pedirse.
    await waitFor(() => {
      expect(totalDelResumen(resumen, 'Total gastado')).toBe('ARS 1.500,50');
    });
    expect(totalDelResumen(resumen, 'Balance')).toBe('ARS 248.499,50');
    // El desglose también se movió, y se lee en su fila: el importe está además en el total
    // gastado, así que buscarlo suelto sería ambiguo.
    const fila = within(resumen).getByRole('listitem');
    expect(within(fila).getByText('Comida')).not.toBeNull();
    expect(within(fila).getByText('ARS 1.500,50')).not.toBeNull();
    expect(servidor.lecturasDelResumen()).toBe(2);
  });

  it('Resumen_ConErrorDelServidor_NoTumbaElListado', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const servidor = prepararServidor({ iniciales: [EDITABLE], resumen: 'error' });

    render(<App />);

    // Son dos peticiones independientes: que falle la del resumen no vacía la tabla.
    expect(await screen.findByText('17/08/2026')).not.toBeNull();
    expect(screen.getByText('ARS 1.500,50')).not.toBeNull();

    const resumen = screen.getByRole('region', { name: 'Resumen del mes' });
    const aviso = await within(resumen).findByRole('alert');
    expect(aviso.textContent).toMatch(/no pudimos cargar el resumen/i);
    expect(within(resumen).getByRole('button', { name: /reintentar/i })).not.toBeNull();
    expect(servidor.lecturasDelListado()).toBe(1);
    expect(servidor.lecturasDelResumen()).toBe(1);
  });
});
