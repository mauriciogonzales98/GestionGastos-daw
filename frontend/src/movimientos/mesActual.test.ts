import { afterEach, describe, expect, it, vi } from 'vitest';
import { mesActual } from './mesActual';

afterEach(() => {
  vi.useRealTimers();
});

describe('mesActual', () => {
  it('mesActual_DevuelveElPrimeroYElUltimoDiaDelMes', () => {
    // Los tres largos posibles de mes, más el febrero bisiesto: calcular el último día sumando 30
    // o leyendo una tabla fija falla en al menos uno de estos cuatro.
    const casos = [
      { referencia: new Date(2026, 0, 15, 12, 0, 0), desde: '2026-01-01', hasta: '2026-01-31' },
      { referencia: new Date(2026, 3, 10, 12, 0, 0), desde: '2026-04-01', hasta: '2026-04-30' },
      { referencia: new Date(2026, 1, 20, 12, 0, 0), desde: '2026-02-01', hasta: '2026-02-28' },
      { referencia: new Date(2024, 1, 20, 12, 0, 0), desde: '2024-02-01', hasta: '2024-02-29' },
      { referencia: new Date(2026, 7, 18, 12, 0, 0), desde: '2026-08-01', hasta: '2026-08-31' },
    ];

    for (const caso of casos) {
      expect(mesActual(caso.referencia)).toEqual({ desde: caso.desde, hasta: caso.hasta });
    }
  });

  it('mesActual_ElUltimoDiaPorLaNoche_NoSeCorreAlMesSiguiente', () => {
    // El 31/08 a las 23:30 en UTC-3 ya es el 01/09 en UTC: un `toISOString()` colado en el cálculo
    // devolvería septiembre entero y el usuario vería el listado vacío el último día de cada mes.
    expect(mesActual(new Date(2026, 7, 31, 23, 30, 0))).toEqual({
      desde: '2026-08-01',
      hasta: '2026-08-31',
    });
  });

  it('mesActual_SinArgumento_UsaLaFechaDelSistema', () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 10, 5, 8, 0, 0));

    expect(mesActual()).toEqual({ desde: '2026-11-01', hasta: '2026-11-30' });
  });
});
