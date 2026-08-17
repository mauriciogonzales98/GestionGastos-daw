import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FormularioMovimiento } from './FormularioMovimiento';
import type { CategoriaDto, MovimientoDto } from '../api/tipos';
import { json } from '../test/infra';

const CATEGORIAS: CategoriaDto[] = [
  { id: 1, nombre: 'Comida', tipo: 'gasto' },
  { id: 2, nombre: 'Transporte', tipo: 'gasto' },
  { id: 3, nombre: 'Vivienda', tipo: 'gasto' },
  { id: 4, nombre: 'Servicios', tipo: 'gasto' },
  { id: 5, nombre: 'Salud', tipo: 'gasto' },
  { id: 6, nombre: 'Ocio', tipo: 'gasto' },
  { id: 7, nombre: 'Otros', tipo: 'gasto' },
  { id: 8, nombre: 'Sueldo', tipo: 'ingreso' },
  { id: 9, nombre: 'Ingreso extra', tipo: 'ingreso' },
  { id: 10, nombre: 'Otros', tipo: 'ingreso' },
];

const CREADO: MovimientoDto = {
  id: 42,
  tipo: 'gasto',
  categoria: { id: 1, nombre: 'Comida' },
  monto: 1500.5,
  moneda: 'ARS',
  fecha: '2026-08-17',
  nota: null,
};

type RespuestaDeAlta = () => Promise<Response>;

/** Enruta el `fetch` global por endpoint, para no acoplar los tests al orden de las llamadas. */
function prepararFetch(opciones?: {
  categorias?: () => Promise<Response>;
  alta?: RespuestaDeAlta;
}): ReturnType<typeof vi.fn> {
  const falso = vi.fn(async (ruta: string, init?: RequestInit) => {
    if (ruta.startsWith('/api/categorias')) {
      return opciones?.categorias ? await opciones.categorias() : json(CATEGORIAS, 200);
    }
    if (ruta.startsWith('/api/movimientos') && init?.method === 'POST') {
      return opciones?.alta ? await opciones.alta() : json(CREADO, 201);
    }
    throw new Error(`Ruta no esperada en el test: ${ruta}`);
  });
  vi.stubGlobal('fetch', falso);
  return falso as unknown as ReturnType<typeof vi.fn>;
}

/** Texto del mensaje que el control referencia con `aria-describedby`. */
function motivoDe(control: HTMLElement): string {
  const ids = control.getAttribute('aria-describedby')?.split(' ') ?? [];
  return ids
    .map((id) => document.getElementById(id)?.textContent ?? '')
    .join(' ')
    .trim();
}

async function renderizarConCategorias(alta?: RespuestaDeAlta) {
  const usuario = userEvent.setup();
  const fetchFalso = prepararFetch(alta ? { alta } : undefined);
  const onCreado = vi.fn();
  render(<FormularioMovimiento onCreado={onCreado} />);
  await screen.findByRole('option', { name: 'Comida' });
  return { usuario, fetchFalso, onCreado };
}

function cuerpoDelAlta(fetchFalso: ReturnType<typeof vi.fn>): Record<string, unknown> {
  const llamada = fetchFalso.mock.calls.find(
    (argumentos) => (argumentos[1] as RequestInit | undefined)?.method === 'POST',
  );
  if (!llamada) {
    throw new Error('No se hizo ningún POST /api/movimientos');
  }
  return JSON.parse(String((llamada[1] as RequestInit).body)) as Record<string, unknown>;
}

const seleccionar = {
  tipoGasto: () => screen.getByLabelText('Gasto') as HTMLInputElement,
  tipoIngreso: () => screen.getByLabelText('Ingreso') as HTMLInputElement,
  categoria: () => screen.getByLabelText('Categoría') as HTMLSelectElement,
  monto: () => screen.getByLabelText('Monto') as HTMLInputElement,
  fecha: () => screen.getByLabelText('Fecha') as HTMLInputElement,
  nota: () => screen.getByLabelText(/^Nota/) as HTMLTextAreaElement,
  guardar: () => screen.getByRole('button', { name: /guardar/i }) as HTMLButtonElement,
};

afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('FormularioMovimiento', () => {
  it('Formulario_ProponeLaFechaDeHoy', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-08-17T12:00:00.000Z'));
    prepararFetch();

    render(<FormularioMovimiento onCreado={vi.fn()} />);
    await screen.findByRole('option', { name: 'Comida' });

    expect(seleccionar.fecha().value).toBe('2026-08-17');
  });

  it('Formulario_TipoGasto_OfreceSoloCategoriasDeGasto', async () => {
    await renderizarConCategorias();

    const opciones = Array.from(seleccionar.categoria().options)
      .map((opcion) => opcion.textContent)
      .filter((texto) => texto !== 'Elegí una categoría');

    expect(opciones).toEqual([
      'Comida',
      'Transporte',
      'Vivienda',
      'Servicios',
      'Salud',
      'Ocio',
      'Otros',
    ]);
    expect(opciones).not.toContain('Sueldo');
  });

  it('Formulario_TipoIngreso_OfreceSoloCategoriasDeIngreso', async () => {
    const { usuario } = await renderizarConCategorias();

    await usuario.click(seleccionar.tipoIngreso());

    const opciones = Array.from(seleccionar.categoria().options)
      .map((opcion) => opcion.textContent)
      .filter((texto) => texto !== 'Elegí una categoría');

    // En el orden en que los manda el servidor, que ya ordena por tipo y nombre: el formulario
    // filtra por tipo pero no reordena.
    expect(opciones).toEqual(['Sueldo', 'Ingreso extra', 'Otros']);
    expect(opciones).not.toContain('Comida');
  });

  it('Formulario_GastoValido_EnviaYLimpia', async () => {
    const { usuario, fetchFalso, onCreado } = await renderizarConCategorias();
    const fechaPropuesta = seleccionar.fecha().value;

    await usuario.selectOptions(seleccionar.categoria(), '1');
    await usuario.type(seleccionar.monto(), '1500.50');
    await usuario.click(seleccionar.guardar());

    await waitFor(() => expect(onCreado).toHaveBeenCalledWith(CREADO));
    expect(cuerpoDelAlta(fetchFalso)).toEqual({
      categoriaId: 1,
      monto: 1500.5,
      fecha: fechaPropuesta,
      nota: null,
      tipoEsperado: 'gasto',
    });
    expect(seleccionar.monto().value).toBe('');
    expect(seleccionar.categoria().value).toBe('');
    expect(seleccionar.fecha().value).toBe(fechaPropuesta);
  });

  it('Formulario_IngresoValido_EnviaYLimpia', async () => {
    const { usuario, fetchFalso, onCreado } = await renderizarConCategorias();

    await usuario.click(seleccionar.tipoIngreso());
    await usuario.selectOptions(seleccionar.categoria(), '8');
    await usuario.type(seleccionar.monto(), '90000');
    await usuario.click(seleccionar.guardar());

    await waitFor(() => expect(onCreado).toHaveBeenCalled());
    const cuerpo = cuerpoDelAlta(fetchFalso);
    expect(cuerpo.categoriaId).toBe(8);
    expect(cuerpo.monto).toBe(90000);
    expect(cuerpo.tipoEsperado).toBe('ingreso');
    expect(seleccionar.monto().value).toBe('');
  });

  it('Formulario_ConNota_LaEnvia', async () => {
    const { usuario, fetchFalso, onCreado } = await renderizarConCategorias();

    await usuario.selectOptions(seleccionar.categoria(), '1');
    await usuario.type(seleccionar.monto(), '10');
    await usuario.type(seleccionar.nota(), 'Almuerzo con Ana');
    await usuario.click(seleccionar.guardar());

    await waitFor(() => expect(onCreado).toHaveBeenCalled());
    expect(cuerpoDelAlta(fetchFalso).nota).toBe('Almuerzo con Ana');
    expect(seleccionar.nota().value).toBe('');
  });

  it('Formulario_SinNota_EnviaNull', async () => {
    const { usuario, fetchFalso, onCreado } = await renderizarConCategorias();

    await usuario.selectOptions(seleccionar.categoria(), '1');
    await usuario.type(seleccionar.monto(), '10');
    await usuario.click(seleccionar.guardar());

    await waitFor(() => expect(onCreado).toHaveBeenCalled());
    expect(cuerpoDelAlta(fetchFalso).nota).toBeNull();
  });

  it('Formulario_RecorridoSoloConTeclado_PermiteGuardar', async () => {
    const { usuario, fetchFalso } = await renderizarConCategorias();

    await usuario.tab();
    expect(document.activeElement).toBe(seleccionar.tipoGasto());

    await usuario.tab();
    expect(document.activeElement).toBe(seleccionar.categoria());
    await usuario.selectOptions(seleccionar.categoria(), '1');

    await usuario.tab();
    expect(document.activeElement).toBe(seleccionar.monto());
    await usuario.keyboard('250');

    await usuario.tab();
    expect(document.activeElement).toBe(seleccionar.fecha());

    await usuario.tab();
    expect(document.activeElement).toBe(seleccionar.nota());

    await usuario.tab();
    expect(document.activeElement).toBe(seleccionar.guardar());
    await usuario.keyboard('{Enter}');

    await waitFor(() => expect(cuerpoDelAlta(fetchFalso).monto).toBe(250));
  });

  it('Formulario_CadaControlTieneEtiquetaAsociada', async () => {
    await renderizarConCategorias();

    expect(seleccionar.tipoGasto().type).toBe('radio');
    expect(seleccionar.tipoIngreso().type).toBe('radio');
    expect(seleccionar.categoria().tagName).toBe('SELECT');
    expect(seleccionar.monto().tagName).toBe('INPUT');
    expect(seleccionar.fecha().type).toBe('date');
    expect(seleccionar.nota().tagName).toBe('TEXTAREA');
  });

  it('Formulario_MontoCero_MuestraElMotivoJuntoAlCampo', async () => {
    const { usuario, fetchFalso } = await renderizarConCategorias();

    await usuario.selectOptions(seleccionar.categoria(), '1');
    await usuario.type(seleccionar.monto(), '0');
    await usuario.click(seleccionar.guardar());

    expect(motivoDe(seleccionar.monto())).toMatch(/mayor a cero/i);
    expect(
      fetchFalso.mock.calls.some(
        (argumentos) => (argumentos[1] as RequestInit | undefined)?.method === 'POST',
      ),
    ).toBe(false);
  });

  it('Formulario_MontoConTresDecimales_MuestraElMotivo', async () => {
    const { usuario, fetchFalso } = await renderizarConCategorias();

    await usuario.selectOptions(seleccionar.categoria(), '1');
    await usuario.type(seleccionar.monto(), '10.123');
    await usuario.click(seleccionar.guardar());

    expect(motivoDe(seleccionar.monto())).toMatch(/2 decimales/i);
    expect(
      fetchFalso.mock.calls.some(
        (argumentos) => (argumentos[1] as RequestInit | undefined)?.method === 'POST',
      ),
    ).toBe(false);
  });

  it('Formulario_SinCategoria_MuestraElMotivo', async () => {
    const { usuario, fetchFalso } = await renderizarConCategorias();

    await usuario.type(seleccionar.monto(), '10');
    await usuario.click(seleccionar.guardar());

    expect(motivoDe(seleccionar.categoria())).toMatch(/categoría es obligatoria/i);
    expect(
      fetchFalso.mock.calls.some(
        (argumentos) => (argumentos[1] as RequestInit | undefined)?.method === 'POST',
      ),
    ).toBe(false);
  });

  it('Formulario_NotaDeCientoVeintiuno_MuestraElMotivo', async () => {
    const { usuario, fetchFalso } = await renderizarConCategorias();

    await usuario.selectOptions(seleccionar.categoria(), '1');
    await usuario.type(seleccionar.monto(), '10');
    // `maxLength` frena el tecleo en 120: para probar el rechazo hay que forzar el valor, que es lo
    // que haría un pegado desde otra aplicación.
    fireEvent.change(seleccionar.nota(), { target: { value: 'x'.repeat(121) } });
    await usuario.click(seleccionar.guardar());

    expect(motivoDe(seleccionar.nota())).toMatch(/120 caracteres/i);
    expect(
      fetchFalso.mock.calls.some(
        (argumentos) => (argumentos[1] as RequestInit | undefined)?.method === 'POST',
      ),
    ).toBe(false);
  });

  it('Formulario_TipoCruzado_MuestraElMotivoJuntoAlSelector', async () => {
    // El servidor reporta el cruce bajo `errors.categoriaId` y el formato de `tipoEsperado` bajo
    // `errors.tipoEsperado`: ambas claves se muestran en el selector de categoría, que es el control
    // que el usuario puede corregir.
    const { usuario } = await renderizarConCategorias(async () =>
      json(
        {
          status: 400,
          errors: {
            tipoEsperado: ['El tipo debe ser gasto o ingreso'],
          },
        },
        400,
        'application/problem+json',
      ),
    );

    await usuario.selectOptions(seleccionar.categoria(), '1');
    await usuario.type(seleccionar.monto(), '10');
    await usuario.click(seleccionar.guardar());

    await waitFor(() => expect(motivoDe(seleccionar.categoria())).toMatch(/gasto o ingreso/i));
  });

  it('Formulario_CruceDeTipoDelServidor_MuestraElMotivoJuntoAlSelector', async () => {
    // El caso real de AC-10 tal como llega del servidor: una sola clave, `errors.categoriaId`, con
    // el literal que emite MovimientosEndpoints. Cubre la rama en que `tipoEsperado` no viene y el
    // mapeo devuelve el resto sin tocarlo.
    const { usuario } = await renderizarConCategorias(async () =>
      json(
        {
          status: 400,
          errors: {
            categoriaId: [
              "La categoría 'Sueldo' es de tipo ingreso y no puede usarse en un movimiento de tipo gasto",
            ],
          },
        },
        400,
        'application/problem+json',
      ),
    );

    await usuario.selectOptions(seleccionar.categoria(), '1');
    await usuario.type(seleccionar.monto(), '10');
    await usuario.click(seleccionar.guardar());

    await waitFor(() =>
      expect(motivoDe(seleccionar.categoria())).toBe(
        "La categoría 'Sueldo' es de tipo ingreso y no puede usarse en un movimiento de tipo gasto",
      ),
    );
  });

  it('Formulario_CruceYFormatoJuntos_UneLosDosMotivosEnElSelector', async () => {
    // Con las dos claves presentes ninguna puede pisar a la otra: el mapeo las une. Un mutante que
    // devolviera solo una de las dos sobrevive a los otros dos tests, no a este.
    const { usuario } = await renderizarConCategorias(async () =>
      json(
        {
          status: 400,
          errors: {
            categoriaId: [
              "La categoría 'Sueldo' es de tipo ingreso y no puede usarse en un movimiento de tipo gasto",
            ],
            tipoEsperado: ['El tipo debe ser gasto o ingreso'],
          },
        },
        400,
        'application/problem+json',
      ),
    );

    await usuario.selectOptions(seleccionar.categoria(), '1');
    await usuario.type(seleccionar.monto(), '10');
    await usuario.click(seleccionar.guardar());

    await waitFor(() =>
      expect(motivoDe(seleccionar.categoria())).toBe(
        "La categoría 'Sueldo' es de tipo ingreso y no puede usarse en un movimiento de tipo gasto " +
          'El tipo debe ser gasto o ingreso',
      ),
    );
  });

  it('Formulario_ErrorDeRed_MuestraMensajeYPermiteReintentar', async () => {
    let caida = true;
    const { usuario, onCreado } = await renderizarConCategorias(async () => {
      if (caida) {
        throw new TypeError('Failed to fetch');
      }
      return json(CREADO, 201);
    });

    await usuario.selectOptions(seleccionar.categoria(), '1');
    await usuario.type(seleccionar.monto(), '10');
    await usuario.click(seleccionar.guardar());

    const aviso = await screen.findByRole('alert');
    expect(aviso.textContent).toMatch(/conectar/i);

    caida = false;
    await usuario.click(screen.getByRole('button', { name: /reintentar/i }));

    await waitFor(() => expect(onCreado).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('Formulario_ErrorQuinientos_MuestraMensajeGenerico', async () => {
    const errorDeConsola = vi.spyOn(console, 'error').mockImplementation(() => {});
    const { usuario, onCreado } = await renderizarConCategorias(async () =>
      json({ status: 500, traceId: 'traza-999' }, 500, 'application/problem+json'),
    );

    await usuario.selectOptions(seleccionar.categoria(), '1');
    await usuario.type(seleccionar.monto(), '10');
    await usuario.click(seleccionar.guardar());

    const aviso = await screen.findByRole('alert');
    expect(aviso.textContent).toMatch(/no pudimos guardar/i);
    expect(aviso.textContent).not.toMatch(/traza-999/);
    expect(errorDeConsola.mock.calls.flat().join(' ')).toMatch(/traza-999/);
    expect(onCreado).not.toHaveBeenCalled();
  });

  it('Formulario_CategoriasNoCargan_SeDeshabilitaConMotivo', async () => {
    prepararFetch({
      categorias: async () => {
        throw new TypeError('Failed to fetch');
      },
    });

    render(<FormularioMovimiento onCreado={vi.fn()} />);

    const aviso = await screen.findByRole('alert');
    expect(aviso.textContent).toMatch(/categor/i);
    expect(seleccionar.categoria().disabled).toBe(true);
    expect(seleccionar.monto().disabled).toBe(true);
    expect(seleccionar.guardar().disabled).toBe(true);
  });

  it('Formulario_DobleClic_EnviaUnaSolaVez', async () => {
    let resolver: (respuesta: Response) => void = () => {};
    const enVuelo = new Promise<Response>((resolucion) => {
      resolver = resolucion;
    });
    const { usuario, fetchFalso } = await renderizarConCategorias(() => enVuelo);

    await usuario.selectOptions(seleccionar.categoria(), '1');
    await usuario.type(seleccionar.monto(), '10');
    await usuario.click(seleccionar.guardar());
    await usuario.click(seleccionar.guardar());

    expect(seleccionar.guardar().disabled).toBe(true);
    resolver(json(CREADO, 201));

    await waitFor(() => expect(seleccionar.guardar().disabled).toBe(false));
    const altas = fetchFalso.mock.calls.filter(
      (argumentos) => (argumentos[1] as RequestInit | undefined)?.method === 'POST',
    );
    expect(altas).toHaveLength(1);
  });
});
