import { useCallback, useEffect, useState } from 'react';
import { ErrorDeRed, obtenerMovimientos } from '../api/cliente';
import type { MovimientoDto } from '../api/tipos';
import { formatearFecha, formatearMonto } from './formato';

export interface PropsListadoMovimientos {
  /** El padre incrementa este número tras un alta exitosa para forzar el recargo del listado. */
  version: number;
}

export function ListadoMovimientos({ version }: PropsListadoMovimientos) {
  const [items, setItems] = useState<MovimientoDto[]>([]);
  const [recortado, setRecortado] = useState(false);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Mismo patrón que `FormularioMovimiento`: el reset síncrono lo hace `reintentar`, no el efecto
  // de montaje, para no disparar `react-hooks/set-state-in-effect` con un `setState` evitable.
  const cargar = useCallback(async () => {
    try {
      const respuesta = await obtenerMovimientos();
      setItems(respuesta.items);
      setRecortado(respuesta.recortado);
      setError(null);
    } catch (motivo) {
      setItems([]);
      setRecortado(false);
      setError(
        motivo instanceof ErrorDeRed
          ? 'No pudimos conectar con el servidor.'
          : 'No pudimos cargar los movimientos.',
      );
      registrarEnConsola(motivo);
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => {
    // Recargar cuando `version` cambia (tras un alta) es sincronizar con la API, el mismo caso
    // documentado en https://react.dev/learn/you-might-not-need-an-effect#fetching-data.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void cargar();
  }, [cargar, version]);

  function reintentar() {
    setCargando(true);
    setError(null);
    void cargar();
  }

  if (cargando) {
    return <p>Cargando movimientos…</p>;
  }

  if (error !== null) {
    return (
      <p className="aviso-error" role="alert">
        {error}{' '}
        <button type="button" onClick={reintentar}>
          Reintentar
        </button>
      </p>
    );
  }

  if (items.length === 0) {
    return <p>Todavía no hay movimientos.</p>;
  }

  return (
    <section>
      <h2>Movimientos</h2>
      {recortado && (
        <p className="aviso-recorte">
          Se muestran los 500 movimientos más recientes.
        </p>
      )}
      <table>
        <thead>
          <tr>
            <th>Fecha</th>
            <th>Tipo</th>
            <th>Categoría</th>
            <th>Monto</th>
            <th>Nota</th>
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
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

function registrarEnConsola(error: unknown): void {
  console.error('Fallo al cargar los movimientos', error);
}
