import { useCallback, useEffect, useState } from 'react';
import {
  ErrorDeRed,
  ErrorDeValidacion,
  obtenerMovimientos,
  type ErroresPorCampo,
} from '../api/cliente';
import type { FiltrosDeMovimientos, MovimientoDto } from '../api/tipos';
import { formatearFecha, formatearMonto } from './formato';

export interface PropsListadoMovimientos {
  /**
   * El padre incrementa este número para forzar el recargo del listado. Lo hace tras cada suceso
   * que cambia su contenido sin cambiar el filtro vigente: un alta, una modificación y una
   * eliminación.
   */
  version: number;
  /** Los filtros vigentes. Cambiarlos también recarga: el listado los pasa tal cual al cliente. */
  filtros: FiltrosDeMovimientos;
  /**
   * Los rechazos por campo que devuelva el servidor. El listado no los muestra: el control que el
   * usuario tiene que corregir vive en `FiltrosMovimientos`, así que suben al padre.
   */
  onErroresDeFiltro?: (errores: ErroresPorCampo) => void;
  /**
   * Las acciones de cada fila. Son obligatorias y no opcionales: una fila con botones que no
   * llaman a nadie sería peor que una fila sin botones.
   */
  onEditar: (movimiento: MovimientoDto) => void;
  onEliminar: (movimiento: MovimientoDto) => void;
}

/** Un fallo de carga y si tiene sentido reintentarlo con los mismos datos. */
interface FalloDeCarga {
  mensaje: string;
  reintentable: boolean;
}

export function ListadoMovimientos({
  version,
  filtros,
  onErroresDeFiltro,
  onEditar,
  onEliminar,
}: PropsListadoMovimientos) {
  const [items, setItems] = useState<MovimientoDto[]>([]);
  const [recortado, setRecortado] = useState(false);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<FalloDeCarga | null>(null);

  // Mismo patrón que `FormularioMovimiento`: el reset síncrono lo hace `reintentar`, no el efecto
  // de montaje, para no disparar `react-hooks/set-state-in-effect` con un `setState` evitable.
  const cargar = useCallback(async () => {
    try {
      const respuesta = await obtenerMovimientos(filtros);
      setItems(respuesta.items);
      setRecortado(respuesta.recortado);
      setError(null);
    } catch (motivo) {
      setItems([]);
      setRecortado(false);
      setError(describirFallo(motivo));
      if (motivo instanceof ErrorDeValidacion) {
        onErroresDeFiltro?.(motivo.errores);
      }
      registrarEnConsola(motivo);
    } finally {
      setCargando(false);
    }
  }, [filtros, onErroresDeFiltro]);

  useEffect(() => {
    // Recargar cuando cambia `version` (tras un alta, una modificación o una eliminación) o cuando
    // cambia `filtros` (tras aplicar un filtro nuevo) es sincronizar con la API, el caso documentado
    // en
    // https://react.dev/learn/you-might-not-need-an-effect#fetching-data. `filtros` entra por
    // `cargar`, que depende de él.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void cargar();
  }, [cargar, version]);

  function reintentar() {
    setCargando(true);
    setError(null);
    void cargar();
  }

  return (
    <section>
      <h2>Movimientos</h2>
      {/* El rango vigente se ve siempre, incluso sin resultados y mientras carga: es la mitigación
          del PRD contra creer que se perdieron los movimientos de los meses anteriores. */}
      <p className="rango-vigente">{describirRango(filtros)}</p>
      {cuerpo()}
    </section>
  );

  function cuerpo() {
    if (cargando) {
      return <p>Cargando movimientos…</p>;
    }

    if (error !== null) {
      return (
        <p className="aviso-error" role="alert">
          {error.mensaje}{' '}
          {error.reintentable && (
            <button type="button" onClick={reintentar}>
              Reintentar
            </button>
          )}
        </p>
      );
    }

    if (items.length === 0) {
      return <p>Todavía no hay movimientos.</p>;
    }

    return (
      <>
        {recortado && (
          <p className="aviso-recorte">Se muestran los 500 movimientos más recientes.</p>
        )}
        <table>
          <thead>
            <tr>
              <th>Fecha</th>
              <th>Tipo</th>
              <th>Categoría</th>
              <th>Monto</th>
              <th>Nota</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {items.map((movimiento) => (
              <tr key={movimiento.id}>
                <td>{formatearFecha(movimiento.fecha)}</td>
                <td>{movimiento.tipo}</td>
                <td>{movimiento.categoria.nombre}</td>
                <td>{formatearMonto(movimiento.monto, movimiento.moneda)}</td>
                <td>{movimiento.nota ?? ''}</td>
                <td className="acciones">
                  {/* El nombre accesible nombra el movimiento: con varias filas, un "Editar" a
                      secas deja al lector de pantalla sin saber cuál de todas. */}
                  <button
                    type="button"
                    aria-label={`Editar ${describirMovimiento(movimiento)}`}
                    onClick={() => onEditar(movimiento)}
                  >
                    Editar
                  </button>
                  <button
                    type="button"
                    aria-label={`Eliminar ${describirMovimiento(movimiento)}`}
                    onClick={() => onEliminar(movimiento)}
                  >
                    Eliminar
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </>
    );
  }
}

/** Fecha y categoría alcanzan para distinguir una fila de otra en el nombre accesible. */
function describirMovimiento(movimiento: MovimientoDto): string {
  return `el movimiento del ${formatearFecha(movimiento.fecha)} de ${movimiento.categoria.nombre}`;
}

/** Los dos extremos son opcionales e independientes, así que hay cuatro frases posibles. */
function describirRango(filtros: FiltrosDeMovimientos): string {
  const desde = conValor(filtros.desde);
  const hasta = conValor(filtros.hasta);

  if (desde !== null && hasta !== null) {
    return `Mostrando movimientos del ${formatearFecha(desde)} al ${formatearFecha(hasta)}.`;
  }
  if (desde !== null) {
    return `Mostrando movimientos desde el ${formatearFecha(desde)}.`;
  }
  if (hasta !== null) {
    return `Mostrando movimientos hasta el ${formatearFecha(hasta)}.`;
  }
  return 'Mostrando movimientos de todas las fechas.';
}

/** La cadena vacía cuenta como ausente: es lo que entrega un `<input type="date">` sin completar. */
function conValor(fecha: string | undefined): string | null {
  return fecha === undefined || fecha === '' ? null : fecha;
}

function describirFallo(motivo: unknown): FalloDeCarga {
  if (motivo instanceof ErrorDeValidacion) {
    // Reintentar el mismo filtro daría el mismo rechazo: la salida es corregirlo en el control, que
    // es donde el mensaje por campo aparece.
    return {
      mensaje: 'No pudimos aplicar el filtro. Revisá los datos marcados.',
      reintentable: false,
    };
  }
  if (motivo instanceof ErrorDeRed) {
    return { mensaje: 'No pudimos conectar con el servidor.', reintentable: true };
  }
  return { mensaje: 'No pudimos cargar los movimientos.', reintentable: true };
}

function registrarEnConsola(error: unknown): void {
  console.error('Fallo al cargar los movimientos', error);
}
