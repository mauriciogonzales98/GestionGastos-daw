import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ErrorDeRed,
  ErrorDeValidacion,
  ErrorDelServidor,
  crearMovimiento,
  obtenerCategorias,
  type ErroresPorCampo,
} from '../api/cliente';
import type { CategoriaDto, MovimientoDto, TipoMovimiento } from '../api/tipos';
import { hoyComoIso } from './fecha';

export const LARGO_MAXIMO_DE_NOTA = 120;

/** Espeja la regla del servidor: hasta 13 enteros y como mucho 2 decimales, con punto o coma. */
const MONTO_VALIDO = /^\d{1,13}([.,]\d{1,2})?$/;

export interface PropsFormularioMovimiento {
  /** Se invoca con el movimiento creado. Block 5 lo usa para refrescar el listado. */
  onCreado?: (movimiento: MovimientoDto) => void;
}

export function FormularioMovimiento({ onCreado }: PropsFormularioMovimiento) {
  const [categorias, setCategorias] = useState<CategoriaDto[]>([]);
  const [errorDeCategorias, setErrorDeCategorias] = useState<string | null>(null);
  const [cargandoCategorias, setCargandoCategorias] = useState(true);

  const [tipo, setTipo] = useState<TipoMovimiento>('gasto');
  const [categoriaId, setCategoriaId] = useState('');
  const [monto, setMonto] = useState('');
  const [fecha, setFecha] = useState(() => hoyComoIso());
  const [nota, setNota] = useState('');

  const [errores, setErrores] = useState<ErroresPorCampo>({});
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  // El `disabled` del botón tarda un render en aplicarse; el ref cierra la ventana del doble envío
  // por doble clic o por Enter repetido.
  const enVuelo = useRef(false);

  // Sin resets sincrónicos al inicio: los llama el efecto de montaje, y el estado inicial de
  // `cargando`/`error` ya vale lo mismo (true/null) — resetearlo ahí dispararía un setState
  // sincrónico dentro de un efecto. `reintentarCategorias` es quien resetea, desde un click.
  const cargarCategorias = useCallback(async () => {
    try {
      setCategorias(await obtenerCategorias());
      setErrorDeCategorias(null);
    } catch (error) {
      // Sin catálogo el formulario no puede funcionar: se deshabilita con el motivo a la vista, en
      // vez de ofrecer un selector vacío sin explicación.
      setCategorias([]);
      setErrorDeCategorias(
        error instanceof ErrorDeRed
          ? 'No pudimos cargar las categorías: no hay conexión con el servidor.'
          : 'No pudimos cargar las categorías.',
      );
      registrarEnConsola(error);
    } finally {
      setCargandoCategorias(false);
    }
  }, []);

  useEffect(() => {
    // Cargar el catálogo al montar es sincronizar con un sistema externo (la API), el caso que
    // react.dev documenta como uso válido de un efecto — la regla no distingue esto de un
    // setState evitable. https://react.dev/learn/you-might-not-need-an-effect#fetching-data
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void cargarCategorias();
  }, [cargarCategorias]);

  function reintentarCategorias() {
    setCargandoCategorias(true);
    setErrorDeCategorias(null);
    void cargarCategorias();
  }

  const categoriasDelTipo = useMemo(
    () => categorias.filter((categoria) => categoria.tipo === tipo),
    [categorias, tipo],
  );

  const deshabilitado = cargandoCategorias || errorDeCategorias !== null;

  function cambiarTipo(nuevo: TipoMovimiento) {
    setTipo(nuevo);
    // La categoría elegida pertenece al tipo anterior: conservarla enviaría el cruce de AC-10.
    setCategoriaId('');
    limpiarError('categoriaId');
  }

  function limpiarError(campo: string) {
    setErrores((previos) => {
      if (!(campo in previos)) {
        return previos;
      }
      const siguientes = { ...previos };
      delete siguientes[campo];
      return siguientes;
    });
  }

  function validar(): ErroresPorCampo {
    const encontrados: ErroresPorCampo = {};

    if (categoriaId === '') {
      encontrados.categoriaId = 'La categoría es obligatoria';
    }

    const montoNormalizado = monto.trim();
    if (montoNormalizado === '') {
      encontrados.monto = 'El monto es obligatorio y debe ser un número';
    } else if (!MONTO_VALIDO.test(montoNormalizado)) {
      encontrados.monto = /^\d+[.,]\d{3,}$/.test(montoNormalizado)
        ? 'El monto admite como máximo 2 decimales'
        : 'El monto debe ser un número con hasta 2 decimales';
    } else if (Number(montoNormalizado.replace(',', '.')) <= 0) {
      encontrados.monto = 'El monto debe ser mayor a cero';
    }

    if (fecha.trim() === '') {
      encontrados.fecha = 'La fecha es obligatoria';
    }

    if (nota.length > LARGO_MAXIMO_DE_NOTA) {
      encontrados.nota = `La nota no puede superar los ${LARGO_MAXIMO_DE_NOTA} caracteres`;
    }

    return encontrados;
  }

  async function enviar() {
    if (enVuelo.current || deshabilitado) {
      return;
    }

    const encontrados = validar();
    setErrores(encontrados);
    if (Object.keys(encontrados).length > 0) {
      setErrorGeneral(null);
      return;
    }

    enVuelo.current = true;
    setEnviando(true);
    setErrorGeneral(null);
    try {
      const creado = await crearMovimiento({
        categoriaId: Number(categoriaId),
        monto: Number(monto.trim().replace(',', '.')),
        fecha,
        nota: nota.trim() === '' ? null : nota,
        tipoEsperado: tipo,
      });
      setErrores({});
      setCategoriaId('');
      setMonto('');
      setNota('');
      onCreado?.(creado);
    } catch (error) {
      manejarFalloDelAlta(error);
    } finally {
      enVuelo.current = false;
      setEnviando(false);
    }
  }

  function manejarFalloDelAlta(error: unknown) {
    if (error instanceof ErrorDeValidacion) {
      setErrores(mapearErroresDelServidor(error.errores));
      return;
    }

    if (error instanceof ErrorDeRed) {
      setErrorGeneral('No pudimos conectar con el servidor. Revisá la conexión y probá de nuevo.');
      registrarEnConsola(error);
      return;
    }

    // Ni un 500 ni un error inesperado se tragan: mensaje genérico a la vista y detalle a consola.
    setErrorGeneral('No pudimos guardar el movimiento. Probá de nuevo en unos segundos.');
    registrarEnConsola(error);
  }

  const idErrorCategoria = 'error-categoria';
  const idErrorMonto = 'error-monto';
  const idErrorFecha = 'error-fecha';
  const idErrorNota = 'error-nota';
  const idContadorNota = 'contador-nota';

  return (
    <form
      className="formulario"
      noValidate
      onSubmit={(evento) => {
        evento.preventDefault();
        void enviar();
      }}
    >
      <h2>Nuevo movimiento</h2>

      {errorDeCategorias !== null && (
        <p className="aviso-error" role="alert">
          {errorDeCategorias}{' '}
          <button type="button" onClick={reintentarCategorias}>
            Reintentar
          </button>
        </p>
      )}

      <fieldset className="grupo-tipo" disabled={deshabilitado}>
        <legend>Tipo</legend>
        <input
          id="tipo-gasto"
          type="radio"
          name="tipo"
          value="gasto"
          checked={tipo === 'gasto'}
          onChange={() => cambiarTipo('gasto')}
        />
        <label htmlFor="tipo-gasto">Gasto</label>
        <input
          id="tipo-ingreso"
          type="radio"
          name="tipo"
          value="ingreso"
          checked={tipo === 'ingreso'}
          onChange={() => cambiarTipo('ingreso')}
        />
        <label htmlFor="tipo-ingreso">Ingreso</label>
      </fieldset>

      <div className="campo">
        <label htmlFor="categoria">Categoría</label>
        <select
          id="categoria"
          value={categoriaId}
          disabled={deshabilitado}
          aria-invalid={errores.categoriaId !== undefined}
          aria-describedby={idErrorCategoria}
          onChange={(evento) => {
            setCategoriaId(evento.target.value);
            limpiarError('categoriaId');
          }}
        >
          <option value="">Elegí una categoría</option>
          {categoriasDelTipo.map((categoria) => (
            <option key={categoria.id} value={categoria.id}>
              {categoria.nombre}
            </option>
          ))}
        </select>
        <p className="error-de-campo" id={idErrorCategoria}>
          {errores.categoriaId ?? ''}
        </p>
      </div>

      <div className="campo">
        <label htmlFor="monto">Monto</label>
        <input
          id="monto"
          type="text"
          inputMode="decimal"
          autoComplete="off"
          value={monto}
          disabled={deshabilitado}
          aria-invalid={errores.monto !== undefined}
          aria-describedby={idErrorMonto}
          onChange={(evento) => {
            setMonto(evento.target.value);
            limpiarError('monto');
          }}
        />
        <p className="error-de-campo" id={idErrorMonto}>
          {errores.monto ?? ''}
        </p>
      </div>

      <div className="campo">
        <label htmlFor="fecha">Fecha</label>
        <input
          id="fecha"
          type="date"
          value={fecha}
          disabled={deshabilitado}
          aria-invalid={errores.fecha !== undefined}
          aria-describedby={idErrorFecha}
          onChange={(evento) => {
            setFecha(evento.target.value);
            limpiarError('fecha');
          }}
        />
        <p className="error-de-campo" id={idErrorFecha}>
          {errores.fecha ?? ''}
        </p>
      </div>

      <div className="campo">
        <label htmlFor="nota">Nota (opcional)</label>
        <textarea
          id="nota"
          rows={2}
          maxLength={LARGO_MAXIMO_DE_NOTA}
          value={nota}
          disabled={deshabilitado}
          aria-invalid={errores.nota !== undefined}
          aria-describedby={`${idContadorNota} ${idErrorNota}`}
          onChange={(evento) => {
            setNota(evento.target.value);
            limpiarError('nota');
          }}
        />
        <p className="contador" id={idContadorNota}>
          {nota.length} de {LARGO_MAXIMO_DE_NOTA}
        </p>
        <p className="error-de-campo" id={idErrorNota}>
          {errores.nota ?? ''}
        </p>
      </div>

      {errorGeneral !== null && (
        <p className="aviso-error" role="alert">
          {errorGeneral}{' '}
          <button type="button" disabled={enviando} onClick={() => void enviar()}>
            Reintentar
          </button>
        </p>
      )}

      {/* El nombre accesible no cambia mientras la petición vuela: el estado lo comunica
          `aria-busy`, y renombrar el botón haría que un lector de pantalla anuncie otro control. */}
      <button type="submit" disabled={deshabilitado || enviando} aria-busy={enviando}>
        Guardar movimiento
      </button>
    </form>
  );
}

/**
 * El servidor reporta el cruce de tipo (AC-10) bajo `errors.categoriaId` y el formato inválido de
 * `tipoEsperado` bajo `errors.tipoEsperado`. `tipoEsperado` no tiene un control propio —el tipo se
 * elige con radios que solo pueden emitir "gasto" o "ingreso"—, así que ambos mensajes se muestran
 * en el selector de categoría, que es el control que el usuario puede corregir.
 */
function mapearErroresDelServidor(errores: ErroresPorCampo): ErroresPorCampo {
  const { tipoEsperado, ...resto } = errores;
  if (tipoEsperado === undefined) {
    return resto;
  }
  return {
    ...resto,
    categoriaId: [resto.categoriaId, tipoEsperado].filter((mensaje) => mensaje).join(' '),
  };
}

/** Deja rastro del fallo para diagnóstico. El `traceId` es lo único que correlaciona con la API. */
function registrarEnConsola(error: unknown) {
  if (error instanceof ErrorDelServidor) {
    console.error('Fallo del servidor', error.message, 'traceId:', error.traceId);
    return;
  }
  console.error('Fallo al hablar con la API', error);
}
