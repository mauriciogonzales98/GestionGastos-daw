import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

/** Puerto en el que Kestrel expone la API (backend/GestionGastos.Api/appsettings.json). */
const API = 'http://127.0.0.1:5175';

// La fecha del movimiento no lleva zona horaria, pero el navegador sí: fijar un huso negativo hace
// que un `toISOString()` colado en el código rompa los tests en vez de pasar inadvertido. Va acá y
// no en `test.env` porque los workers son hilos y un hilo no puede cambiar su propio huso: hay que
// dejarlo puesto en el proceso padre antes de que arranquen.
if (process.env.VITEST) {
  process.env.TZ = 'America/Argentina/Buenos_Aires';
}

export default defineConfig({
  plugins: [react()],
  server: {
    // El frontend habla siempre contra rutas relativas /api: el proxy evita CORS en desarrollo y
    // deja la URL de la API en un solo lugar.
    proxy: {
      '/api': { target: API, changeOrigin: false },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    // Hilos en vez de procesos: arrancar un fork por archivo tarda demasiado sobre un volumen
    // montado (WSL) y el pool termina descartando workers que todavía estaban levantando.
    pool: 'threads',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/main.tsx', 'src/**/*.test.{ts,tsx}'],
    },
  },
});
