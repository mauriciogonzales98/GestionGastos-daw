import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ErrorDeRed,
  ErrorDeValidacion,
  ErrorDelServidor,
  ErrorNoEncontrado,
  crearMovimiento,
  modificarMovimiento,
  obtenerCategorias,
  type ErroresPorCampo,
} from '../api/cliente';
import type {
  CategoriaDto,
  ModificarMovimientoRequest,
  MovimientoDto,
  TipoMovimiento,
} from '../api/tipos';
import { hoyComoIso } from './fecha';

export const LARGO_MAXIMO_DE_NOTA = 120;

/** Espeja la regla del servidor: hasta 13 enteros y como mucho 2 decimales, con punto o coma. */
const MONTO_VALIDO = /^\d{1,13}([.,]\d{1,2})?$/;

export interface PropsFormularioMovimiento {
  /** Se invoca con el movimiento creado. El padre lo usa para refrescar el listado. */
  onCreado?: (movimiento: MovimientoDto) => void;
  /**
   * El movimiento a editar. Ausente = modo alta. Es **el mismo componente** a propósito: las
   * reglas de validación son una sola implementación, no una copia que pueda divergir, que es la
   * mitigación R-16 llevada al cliente.
   */
  movimiento?: MovimientoDto;
  /** Modo edición: se invoca con el movimiento ya modificado. */
  onGuardado?: (movimiento: MovimientoDto) => void;
  /** Modo edición: el usuario descarta los cambios. */
  onCancelar?: () => void;
  /** Modo edición: el servidor respondió 404 —el movimiento ya no está—. */
  onNoEncontrado?: () => void;
}

export function FormularioMovimiento({
  onCreado,
  movimiento,
  onGuardado,
  onCancelar,
  onNoEncontrado,
}: PropsFormularioMovimiento) {
  const enEdicion = movimiento !== undefined;
  const [categorias, setCategorias] = useState<CategoriaDto[]>([]);
  const [errorDeCategorias, setErrorDeCategorias] = useState<string | null>(null);
  const [cargandoCategorias, setCargandoCategorias] = useState(true);

  // En edición el estado arranca en los valores del movimiento. El padre monta este componente con
  // `key={movimiento.id}`, así que pasar a editar otro lo remonta y el estado se vuelve a sembrar.
  const [tipo, setTipo] = useState<TipoMovimiento>(movimiento?.tipo ?? 'gasto');
  const [categoriaId, setCategoriaId] = useState(
    movimiento === undefined ? '' : String(movimiento.categoria.id),
  );
  const [monto, setMonto] = useState(movimiento === undefined ? '' : String(movimiento.monto));
  const [fecha, setFecha] = useState(() => movimiento?.fecha ?? hoyComoIso());
  const [nota, setNota] = useState(movimiento?.nota ?? '');

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
      const datos: ModificarMovimientoRequest = {
        categoriaId: Number(categoriaId),
        monto: Number(monto.trim().replace(',', '.')),
        fecha,
        nota: nota.trim() === '' ? null : nota,
      };

      if (movimiento !== undefined) {
        // Sin `tipoEsperado`: el tipo del movimiento ya está persistido y el servidor lo lee de
        // ahí, así que no hay nada que el cliente tenga que declarar (mitigación R-15).
        const actualizado = await modificarMovimiento(movimiento.id, datos);
        setErrores({});
        // Los campos no se limpian: los cierra el padre. Vaciarlos acá haría parpadear el
        // formulario con datos en blanco antes de desmontarse.
        onGuardado?.(actualizado);
      } else {
        const creado = await crearMovimiento({ ...datos, tipoEsperado: tipo });
        setErrores({});
        setCategoriaId('');
        setMonto('');
        setNota('');
        onCreado?.(creado);
      }
    } catch (error) {
      manejarFallo(error);
    } finally {
      enVuelo.current = false;
      setEnviando(false);
    }
  }

  function manejarFallo(error: unknown) {
    if (error instanceof ErrorDeValidacion) {
      setErrores(mapearErroresDelServidor(error.errores));
      return;
    }

    if (error instanceof ErrorNoEncontrado) {
      // No es un fallo del servidor sino un desenlace de dominio: alguien lo borró antes. Por eso
      // el mensaje no ofrece reintentar —guardar de nuevo daría otro 404— y el padre refresca.
      setErrorGeneral('El movimiento ya no existe. Actualizamos el listado.');
      registrarEnConsola(error);
      onNoEncontrado?.();
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
      <h2>{enEdicion ? 'Editar movimiento' : 'Nuevo movimiento'}</h2>

      {errorDeCategorias !== null && (
        <p className="aviso-error" role="alert">
          {errorDeCategorias}{' '}
          <button type="button" onClick={reintentarCategorias}>
            Reintentar
          </button>
        </p>
      )}

      {/* En edición el tipo no se ofrece: convertir un gasto en ingreso está fuera de alcance por
          PRD, y el selector de categoría queda acotado al tipo del movimiento por el mismo filtro
          que usa el alta. */}
      {!enEdicion && (
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
      )}

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
        {enEdicion ? 'Guardar cambios' : 'Guardar movimiento'}
      </button>
      {enEdicion && (
        <button type="button" disabled={enviando} onClick={onCancelar}>
          Cancelar
        </button>
      )}
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
