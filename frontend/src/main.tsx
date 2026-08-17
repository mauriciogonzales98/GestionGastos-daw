import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './estilos/tokens.css';

const raiz = document.getElementById('root');
if (!raiz) {
  // Sin el contenedor no hay nada que montar: falla explícita, no una pantalla en blanco.
  throw new Error('No se encontró el elemento #root en index.html');
}

createRoot(raiz).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
