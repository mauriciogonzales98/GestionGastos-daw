import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { hoyComoIso } from './fecha';

describe('fecha', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('fecha_HoyComoIso_NoUsaToISOString_NoDesplazaElDia', () => {
    // 2026-03-01T01:00:00Z son las 22:00 del 28/02 en Buenos Aires (UTC-3), el huso que fija
    // vite.config.ts. `toISOString()` diría 2026-03-01 y desplazaría el movimiento un día.
    const instante = new Date('2026-03-01T01:00:00.000Z');
    vi.setSystemTime(instante);

    expect(hoyComoIso()).toBe('2026-02-28');
    expect(hoyComoIso()).not.toBe(instante.toISOString().slice(0, 10));
  });

  it('fecha_HoyComoIso_RellenaMesYDiaConCero', () => {
    vi.setSystemTime(new Date('2026-01-05T15:00:00.000Z'));

    expect(hoyComoIso()).toBe('2026-01-05');
  });
});
