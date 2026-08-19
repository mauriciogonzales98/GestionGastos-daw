import { useCallback, useState } from 'react';
import { FormularioMovimiento } from './movimientos/FormularioMovimiento';
import { FiltrosMovimientos } from './movimientos/FiltrosMovimientos';
import { ListadoMovimientos } from './movimientos/ListadoMovimientos';
import { mesActual } from './movimientos/mesActual';
import type { ErroresPorCampo } from './api/cliente';
import type { FiltrosDeMovimientos } from './api/tipos';

export default function App() {
  // Incrementar `version` es la señal para que el listado se recargue tras un alta exitosa, sin
  // acoplar ambos componentes a un estado de movimientos compartido. Sigue siendo necesaria con los
  // filtros: un alta no cambia el filtro vigente, así que sin ella la recarga no tendría disparador.
  const [version, setVersion] = useState(0);

  // El default del mes en curso (AC-09) lo pone el frontend: `GET /api/movimientos` sin parámetros
  // devuelve todo, y así sigue siendo para cualquier otro consumidor. La identidad de este objeto
  // solo cambia al aplicar, que es lo que el listado usa como disparador de recarga.
  const [filtros, setFiltros] = useState<FiltrosDeMovimientos>(() => mesActual());
  const [erroresDeFiltro, setErroresDeFiltro] = useState<ErroresPorCampo>({});

  const aplicarFiltros = useCallback((nuevos: FiltrosDeMovimientos) => {
    // Los rechazos del servidor son del filtro anterior: se limpian al pedir uno nuevo, o quedarían
    // señalando un control que el usuario ya corrigió.
    setErroresDeFiltro({});
    setFiltros(nuevos);
  }, []);

  return (
    <main>
      <h1>Gestión de Gastos</h1>
      <FormularioMovimiento onCreado={() => setVersion((actual) => actual + 1)} />
      <FiltrosMovimientos
        filtros={filtros}
        erroresDelServidor={erroresDeFiltro}
        onAplicar={aplicarFiltros}
      />
      <ListadoMovimientos
        version={version}
        filtros={filtros}
        onErroresDeFiltro={setErroresDeFiltro}
      />
    </main>
  );
}
