import { useRef, useState } from 'react';
import {
  ErrorDeRed,
  ErrorDelServidor,
  ErrorNoEncontrado,
  eliminarMovimiento,
} from '../api/cliente';
import type { MovimientoDto } from '../api/tipos';
import { formatearFecha, formatearMonto } from './formato';

export interface PropsConfirmarEliminacion {
  /** El movimiento que se va a borrar. El diálogo lo nombra: confirmar a ciegas no es confirmar. */
  movimiento: MovimientoDto;
  /** El borrado ocurrió: el padre cierra el diálogo y refresca el listado. */
  onEliminado: () => void;
  /** El movimiento ya no estaba (404). No es un fallo: el padre avisa y refresca. */
  onNoEncontrado: () => void;
  onCancelar: () => void;
}

/**
 * Confirmación explícita antes de borrar (mitigación R-19). Es la **única** red que existe: el PRD
 * descarta baja lógica, historial y papelera, así que un borrado no tiene vuelta atrás. Por eso el
 * diálogo no acepta ninguna otra entrada —solo confirmar o cancelar— y cancelar no dispara ninguna
 * petición.
 */
export function ConfirmarEliminacion({
  movimiento,
  onEliminado,
  onNoEncontrado,
  onCancelar,
}: PropsConfirmarEliminacion) {
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  // Mismo motivo que en el formulario: el `disabled` tarda un render en aplicarse, y un doble clic
  // sobre "Sí, eliminar" mandaría dos DELETE, el segundo de los cuales respondería 404.
  const enVuelo = useRef(false);

  async function confirmar() {
    if (enVuelo.current) {
      return;
    }

    enVuelo.current = true;
    setEnviando(true);
    setError(null);
    try {
      await eliminarMovimiento(movimiento.id);
      onEliminado();
    } catch (motivo) {
      manejarFallo(motivo);
    } finally {
      enVuelo.current = false;
      setEnviando(false);
    }
  }

  function manejarFallo(motivo: unknown) {
    registrarEnConsola(motivo);

    if (motivo instanceof ErrorNoEncontrado) {
      // Reintentar no tiene sentido: el movimiento ya no está, y el desenlace que el usuario
      // quería —que no esté— ya ocurrió. El padre avisa y refresca.
      onNoEncontrado();
      return;
    }

    setError(
      motivo instanceof ErrorDeRed
        ? 'No pudimos conectar con el servidor. Revisá la conexión y probá de nuevo.'
        : 'No pudimos eliminar el movimiento. Probá de nuevo en unos segundos.',
    );
  }

  const idTitulo = 'titulo-confirmar-eliminacion';
  const idDetalle = 'detalle-confirmar-eliminacion';

  return (
    <div
      className="dialogo"
      role="dialog"
      aria-modal="true"
      aria-labelledby={idTitulo}
      aria-describedby={idDetalle}
    >
      <h2 id={idTitulo}>Eliminar movimiento</h2>

      <p id={idDetalle}>
        Vas a eliminar el movimiento del {formatearFecha(movimiento.fecha)} de{' '}
        {movimiento.categoria.nombre} por {formatearMonto(movimiento.monto, movimiento.moneda)}.
      </p>

      {/* La nota se muestra como texto, igual que en el listado y en el formulario: React escapa
          por defecto y `dangerouslySetInnerHTML` está prohibido (mitigación R-23). */}
      {movimiento.nota !== null && <p className="nota-del-dialogo">Nota: {movimiento.nota}</p>}

      <p className="aviso-definitivo">
        Esta acción es definitiva: no se puede deshacer y el movimiento no queda en ninguna
        papelera.
      </p>

      {error !== null && (
        <p className="aviso-error" role="alert">
          {error}{' '}
          <button type="button" disabled={enviando} onClick={() => void confirmar()}>
            Reintentar
          </button>
        </p>
      )}

      <button
        type="button"
        disabled={enviando}
        aria-busy={enviando}
        onClick={() => void confirmar()}
      >
        Sí, eliminar
      </button>
      <button type="button" disabled={enviando} onClick={onCancelar}>
        Cancelar
      </button>
    </div>
  );
}

/** Deja rastro del fallo para diagnóstico. El `traceId` es lo único que correlaciona con la API. */
function registrarEnConsola(error: unknown) {
  if (error instanceof ErrorDelServidor) {
    console.error('Fallo del servidor al eliminar', error.message, 'traceId:', error.traceId);
    return;
  }
  console.error('Fallo al eliminar el movimiento', error);
}
