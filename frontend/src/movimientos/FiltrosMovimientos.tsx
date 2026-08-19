import { useCallback, useEffect, useState } from 'react';
import { ErrorDeRed, obtenerCategorias, type ErroresPorCampo } from '../api/cliente';
import type { CategoriaDto, FiltrosDeMovimientos } from '../api/tipos';

/** Mensaje de FR-06 del lado del cliente. El servidor lo repite: acá es comodidad, no control. */
const RANGO_INVERTIDO = 'La fecha de inicio no puede ser posterior a la de fin';

export interface PropsFiltrosMovimientos {
  /** Los filtros vigentes, los que el listado está mostrando ahora mismo. */
  filtros: FiltrosDeMovimientos;
  /** Rechazos del servidor por campo (`categoriaId`, `desde`, `hasta`), si los hubo. */
  erroresDelServidor?: ErroresPorCampo;
  /** Se invoca solo con un filtro válido: el rango invertido no llega a salir de acá. */
  onAplicar: (filtros: FiltrosDeMovimientos) => void;
}

export function FiltrosMovimientos({
  filtros,
  erroresDelServidor,
  onAplicar,
}: PropsFiltrosMovimientos) {
  const [categorias, setCategorias] = useState<CategoriaDto[]>([]);
  const [errorDeCategorias, setErrorDeCategorias] = useState<string | null>(null);
  const [cargandoCategorias, setCargandoCategorias] = useState(true);

  // Borrador: lo que hay tecleado en los controles, que no es todavía lo que el listado pide. Se
  // inicializa una sola vez desde los filtros vigentes; a partir de ahí manda el usuario.
  const [categoriaId, setCategoriaId] = useState(() =>
    filtros.categoriaId === undefined ? '' : String(filtros.categoriaId),
  );
  const [desde, setDesde] = useState(() => filtros.desde ?? '');
  const [hasta, setHasta] = useState(() => filtros.hasta ?? '');
  const [errorDeRango, setErrorDeRango] = useState<string | null>(null);

  // Mismo patrón que `FormularioMovimiento`: el reset síncrono lo hace el reintento, no el efecto
  // de montaje, para no disparar `react-hooks/set-state-in-effect` con un `setState` evitable.
  const cargarCategorias = useCallback(async () => {
    try {
      setCategorias(await obtenerCategorias());
      setErrorDeCategorias(null);
    } catch (error) {
      // Sin catálogo el filtro de categoría no se puede ofrecer, pero el de fechas sí: se
      // deshabilita solo el selector, con el motivo a la vista.
      setCategorias([]);
      setErrorDeCategorias(
        error instanceof ErrorDeRed
          ? 'No pudimos cargar las categorías: no hay conexión con el servidor.'
          : 'No pudimos cargar las categorías.',
      );
      console.error('Fallo al cargar las categorías del filtro', error);
    } finally {
      setCargandoCategorias(false);
    }
  }, []);

  useEffect(() => {
    // Cargar el catálogo al montar es sincronizar con un sistema externo (la API), el caso que
    // react.dev documenta como uso válido de un efecto.
    // https://react.dev/learn/you-might-not-need-an-effect#fetching-data
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void cargarCategorias();
  }, [cargarCategorias]);

  function reintentarCategorias() {
    setCargandoCategorias(true);
    setErrorDeCategorias(null);
    void cargarCategorias();
  }

  function aplicar() {
    // `yyyy-MM-dd` ordena igual como texto que como fecha, así que la comparación no necesita
    // construir dos `Date` —que además arrastrarían la hora y el huso a una decisión que no los
    // tiene—.
    if (desde !== '' && hasta !== '' && desde > hasta) {
      setErrorDeRango(RANGO_INVERTIDO);
      return;
    }

    setErrorDeRango(null);
    onAplicar({
      categoriaId: categoriaId === '' ? undefined : Number(categoriaId),
      desde: desde === '' ? undefined : desde,
      hasta: hasta === '' ? undefined : hasta,
    });
  }

  const idErrorCategoria = 'error-filtro-categoria';
  const idErrorDesde = 'error-filtro-desde';
  const idErrorHasta = 'error-filtro-hasta';

  // El rechazo del cliente gana sobre el del servidor: es el más reciente, y el que el usuario
  // acaba de provocar.
  const mensajeDesde = errorDeRango ?? erroresDelServidor?.desde ?? '';
  const mensajeHasta = erroresDelServidor?.hasta ?? '';
  const mensajeCategoria = erroresDelServidor?.categoriaId ?? '';

  return (
    <form
      className="filtros"
      aria-label="Filtros del listado"
      noValidate
      onSubmit={(evento) => {
        evento.preventDefault();
        aplicar();
      }}
    >
      <h2>Filtros</h2>

      {errorDeCategorias !== null && (
        <p className="aviso-error" role="alert">
          {errorDeCategorias}{' '}
          <button type="button" onClick={reintentarCategorias}>
            Reintentar categorías
          </button>
        </p>
      )}

      <div className="campo">
        <label htmlFor="filtro-categoria">Filtrar por categoría</label>
        <select
          id="filtro-categoria"
          value={categoriaId}
          disabled={cargandoCategorias || errorDeCategorias !== null}
          aria-invalid={mensajeCategoria !== ''}
          aria-describedby={idErrorCategoria}
          onChange={(evento) => {
            setCategoriaId(evento.target.value);
          }}
        >
          {/* La opción vacía no es "ninguna categoría" sino "todas" (AC-08): el filtro ausente. */}
          <option value="">Todas las categorías</option>
          {categorias.map((categoria) => (
            <option key={categoria.id} value={categoria.id}>
              {categoria.nombre}
            </option>
          ))}
        </select>
        <p className="error-de-campo" id={idErrorCategoria}>
          {mensajeCategoria}
        </p>
      </div>

      <div className="campo">
        <label htmlFor="filtro-desde">Desde</label>
        <input
          id="filtro-desde"
          type="date"
          value={desde}
          aria-invalid={mensajeDesde !== ''}
          aria-describedby={idErrorDesde}
          onChange={(evento) => {
            setDesde(evento.target.value);
            setErrorDeRango(null);
          }}
        />
        <p className="error-de-campo" id={idErrorDesde}>
          {mensajeDesde}
        </p>
      </div>

      <div className="campo">
        <label htmlFor="filtro-hasta">Hasta</label>
        <input
          id="filtro-hasta"
          type="date"
          value={hasta}
          aria-invalid={mensajeHasta !== ''}
          aria-describedby={idErrorHasta}
          onChange={(evento) => {
            setHasta(evento.target.value);
            setErrorDeRango(null);
          }}
        />
        <p className="error-de-campo" id={idErrorHasta}>
          {mensajeHasta}
        </p>
      </div>

      {/* Los filtros se aplican con un botón y no a cada tecla: un `<input type="date">` a medio
          completar emite valores intermedios, y pedirle al servidor cada uno sería una petición por
          dígito contra un rango que el usuario todavía no terminó de escribir. */}
      <button type="submit">Aplicar filtros</button>
    </form>
  );
}
