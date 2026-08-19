import { useCallback, useEffect, useState } from 'react';
import { ErrorDeRed, obtenerResumen } from '../api/cliente';
import type { ResumenMensual } from '../api/tipos';
import { formatearMonto } from '../movimientos/formato';

/**
 * El endpoint del resumen no devuelve moneda: mientras haya una sola no hay nada que separar
 * (RF-29 quedó fuera de alcance en el PRD). Se reusa el formato del listado, con la misma moneda
 * que el backend persiste hoy, para que el mismo importe se lea igual arriba y en la tabla.
 */
const MONEDA = 'ARS';

/**
 * Los doce nombres, escritos. `Intl.DateTimeFormat` los devolvería en minúscula y con variaciones
 * entre builds de ICU, y habría que construir un `Date` —con su huso— para rotular un período que
 * ya viene desarmado en dos números.
 */
const NOMBRES_DE_MES = [
  'Enero',
  'Febrero',
  'Marzo',
  'Abril',
  'Mayo',
  'Junio',
  'Julio',
  'Agosto',
  'Septiembre',
  'Octubre',
  'Noviembre',
  'Diciembre',
];

export interface PropsResumenDelMes {
  /**
   * El padre incrementa este número tras cada suceso que cambia los movimientos: un alta, una
   * modificación, una eliminación y el refresco tras un 404. Es **el único** disparador de recarga
   * que el resumen tiene, y es deliberado: los filtros del listado no lo tocan porque el período
   * lo fija el servidor sobre el mes en curso (FR-03, AC-06).
   */
  version: number;
}

export function ResumenDelMes({ version }: PropsResumenDelMes) {
  const [resumen, setResumen] = useState<ResumenMensual | null>(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Sin dependencias, y no por descuido: `obtenerResumen` no toma parámetros —el período lo fija el
  // servidor— y agregar acá los filtros del listado sería justo lo que AC-06 prohíbe.
  const cargar = useCallback(async () => {
    try {
      setResumen(await obtenerResumen());
      setError(null);
    } catch (motivo) {
      // Los números viejos no se conservan: un resumen que falló mostrando lo anterior no se
      // distingue de uno al día.
      setResumen(null);
      setError(describirFallo(motivo));
      registrarEnConsola(motivo);
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => {
    // Recargar cuando cambia `version` es sincronizar con la API, el caso documentado en
    // https://react.dev/learn/you-might-not-need-an-effect#fetching-data.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void cargar();
  }, [cargar, version]);

  function reintentar() {
    setCargando(true);
    setError(null);
    void cargar();
  }

  return (
    <section className="resumen-del-mes" aria-label="Resumen del mes">
      <h2>Resumen del mes</h2>
      {cuerpo()}
    </section>
  );

  function cuerpo() {
    if (cargando) {
      return <p>Cargando el resumen…</p>;
    }

    if (error !== null || resumen === null) {
      return (
        <p className="aviso-error" role="alert">
          {error ?? 'No pudimos cargar el resumen del mes.'}{' '}
          <button type="button" onClick={reintentar}>
            Reintentar
          </button>
        </p>
      );
    }

    return (
      <>
        {/* El rótulo sale de la respuesta y no del reloj del navegador (AC-09): así el mes que se
            muestra y el mes que se calculó no pueden discrepar en el cambio de mes. */}
        <p className="periodo">{rotularPeriodo(resumen.mes, resumen.anio)}</p>

        <dl className="totales">
          <div className="total">
            <dt>Total ingresado</dt>
            <dd>{formatearMonto(resumen.totalIngresado, MONEDA)}</dd>
          </div>
          <div className="total">
            <dt>Total gastado</dt>
            <dd>{formatearMonto(resumen.totalGastado, MONEDA)}</dd>
          </div>
          <div className="total">
            <dt>Balance</dt>
            {/* AC-03: el negativo se distingue por el signo, que está en el texto y lo anuncia un
                lector de pantalla. La clase es para el color, que por sí solo no alcanzaría. */}
            <dd className={resumen.balance < 0 ? 'balance balance-negativo' : 'balance'}>
              {formatearMonto(resumen.balance, MONEDA)}
            </dd>
          </div>
        </dl>

        <h3>Gastos por categoría</h3>
        {desglose(resumen.desglose)}
      </>
    );
  }
}

/** Un mes sin gastos no es un error: se dice que no hay nada que desglosar y listo (AC-02). */
function desglose(filas: ResumenMensual['desglose']) {
  if (filas.length === 0) {
    return <p>No hay gastos para desglosar en este mes.</p>;
  }

  return (
    <ul className="desglose">
      {filas.map((fila) => (
        <li key={fila.categoriaId}>
          {/* El nombre se interpola como texto JSX, que React escapa. */}
          <span className="categoria">{fila.categoriaNombre}</span>{' '}
          <span className="total">{formatearMonto(fila.total, MONEDA)}</span>
        </li>
      ))}
    </ul>
  );
}

function rotularPeriodo(mes: number, anio: number): string {
  // `noUncheckedIndexedAccess`: un mes fuera de 1..12 no puede venir del backend, pero si viniera
  // el rótulo muestra el número en vez de romper la pantalla entera.
  const nombre = NOMBRES_DE_MES[mes - 1] ?? String(mes);
  return `${nombre} ${String(anio)}`;
}

function describirFallo(motivo: unknown): string {
  if (motivo instanceof ErrorDeRed) {
    return 'No pudimos cargar el resumen del mes: no hay conexión con el servidor.';
  }
  return 'No pudimos cargar el resumen del mes.';
}

function registrarEnConsola(error: unknown): void {
  console.error('Fallo al cargar el resumen del mes', error);
}
