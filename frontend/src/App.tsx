import { useCallback, useState } from 'react';
import { FormularioMovimiento } from './movimientos/FormularioMovimiento';
import { ConfirmarEliminacion } from './movimientos/ConfirmarEliminacion';
import { FiltrosMovimientos } from './movimientos/FiltrosMovimientos';
import { ListadoMovimientos } from './movimientos/ListadoMovimientos';
import { ResumenDelMes } from './resumen/ResumenDelMes';
import { mesActual } from './movimientos/mesActual';
import type { ErroresPorCampo } from './api/cliente';
import type { FiltrosDeMovimientos, MovimientoDto } from './api/tipos';

/** El 404 de un `PUT` o un `DELETE` no es un fallo: alguien lo borró antes, y hay que refrescar. */
const MENSAJE_DESAPARECIDO = 'El movimiento ya no existe. Actualizamos el listado.';

export default function App() {
  // Incrementar `version` es la señal para que el listado se recargue sin acoplar los componentes a
  // un estado de movimientos compartido. La disparan las cuatro cosas que cambian el contenido del
  // listado sin cambiar el filtro vigente: un alta, una modificación, una eliminación y el refresco
  // tras descubrir que el movimiento ya no existía. Sigue haciendo falta con los filtros puestos,
  // porque ninguno de esos cuatro sucesos toca el filtro.
  const [version, setVersion] = useState(0);

  // El default del mes en curso (AC-09) lo pone el frontend: `GET /api/movimientos` sin parámetros
  // devuelve todo, y así sigue siendo para cualquier otro consumidor. La identidad de este objeto
  // solo cambia al aplicar, que es lo que el listado usa como disparador de recarga.
  const [filtros, setFiltros] = useState<FiltrosDeMovimientos>(() => mesActual());
  const [erroresDeFiltro, setErroresDeFiltro] = useState<ErroresPorCampo>({});

  // Las dos acciones de fila son excluyentes entre sí: editar cierra una confirmación abierta y
  // viceversa, para que no queden dos diálogos pidiendo cosas distintas sobre el mismo movimiento.
  const [enEdicion, setEnEdicion] = useState<MovimientoDto | null>(null);
  const [aEliminar, setAEliminar] = useState<MovimientoDto | null>(null);
  const [avisoDeDesaparecido, setAvisoDeDesaparecido] = useState<string | null>(null);

  const recargar = useCallback(() => {
    setVersion((actual) => actual + 1);
  }, []);

  const aplicarFiltros = useCallback((nuevos: FiltrosDeMovimientos) => {
    // Los rechazos del servidor son del filtro anterior: se limpian al pedir uno nuevo, o quedarían
    // señalando un control que el usuario ya corrigió.
    setErroresDeFiltro({});
    setAvisoDeDesaparecido(null);
    setFiltros(nuevos);
  }, []);

  function editar(movimiento: MovimientoDto) {
    setAvisoDeDesaparecido(null);
    setAEliminar(null);
    setEnEdicion(movimiento);
  }

  function eliminar(movimiento: MovimientoDto) {
    setAvisoDeDesaparecido(null);
    setEnEdicion(null);
    setAEliminar(movimiento);
  }

  function cerrarYRecargar() {
    setEnEdicion(null);
    setAEliminar(null);
    recargar();
  }

  function avisarQueYaNoExiste() {
    setAvisoDeDesaparecido(MENSAJE_DESAPARECIDO);
    cerrarYRecargar();
  }

  return (
    <main>
      <h1>Gestión de Gastos</h1>

      <ResumenDelMes version={version} />

      {/* Un solo formulario a la vez: el de edición reemplaza al de alta mientras dura. Dos
          instancias simultáneas repetirían los `id` de los controles y sus etiquetas. La `key`
          fuerza el remontaje al pasar a editar otro movimiento, que es lo que vuelve a sembrar el
          estado inicial con los valores nuevos. */}
      {enEdicion === null ? (
        <FormularioMovimiento onCreado={recargar} />
      ) : (
        <FormularioMovimiento
          key={enEdicion.id}
          movimiento={enEdicion}
          onGuardado={cerrarYRecargar}
          onCancelar={() => {
            setEnEdicion(null);
          }}
          onNoEncontrado={avisarQueYaNoExiste}
        />
      )}

      <FiltrosMovimientos
        filtros={filtros}
        erroresDelServidor={erroresDeFiltro}
        onAplicar={aplicarFiltros}
      />

      {avisoDeDesaparecido !== null && (
        <p className="aviso-desaparecido" role="status">
          {avisoDeDesaparecido}
        </p>
      )}

      <ListadoMovimientos
        version={version}
        filtros={filtros}
        onErroresDeFiltro={setErroresDeFiltro}
        onEditar={editar}
        onEliminar={eliminar}
      />

      {aEliminar !== null && (
        <ConfirmarEliminacion
          movimiento={aEliminar}
          onEliminado={cerrarYRecargar}
          onNoEncontrado={avisarQueYaNoExiste}
          onCancelar={() => {
            setAEliminar(null);
          }}
        />
      )}
    </main>
  );
}
