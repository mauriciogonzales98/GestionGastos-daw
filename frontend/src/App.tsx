import { FormularioMovimiento } from './movimientos/FormularioMovimiento';

/**
 * En este bloque la aplicación es solo el formulario de alta. El listado —y el refresco tras un
 * alta, que es para lo que existe `onCreado`— llega con Block 5.
 */
export default function App() {
  return (
    <main>
      <h1>Gestión de Gastos</h1>
      <FormularioMovimiento />
    </main>
  );
}
