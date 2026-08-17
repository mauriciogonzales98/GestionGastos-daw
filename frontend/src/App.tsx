import { useState } from 'react';
import { FormularioMovimiento } from './movimientos/FormularioMovimiento';
import { ListadoMovimientos } from './movimientos/ListadoMovimientos';

export default function App() {
  // Incrementar `version` es la señal para que el listado se recargue tras un alta exitosa,
  // sin acoplar ambos componentes a un estado de movimientos compartido.
  const [version, setVersion] = useState(0);

  return (
    <main>
      <h1>Gestión de Gastos</h1>
      <FormularioMovimiento onCreado={() => setVersion((actual) => actual + 1)} />
      <ListadoMovimientos version={version} />
    </main>
  );
}
