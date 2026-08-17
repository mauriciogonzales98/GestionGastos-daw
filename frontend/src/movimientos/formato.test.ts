import { describe, expect, it } from 'vitest';
import { formatearFecha, formatearMonto } from './formato';

describe('formato', () => {
  it('formatearMonto_MuestraLaMonedaJuntoAlNumero', () => {
    expect(formatearMonto(1500.5, 'ARS')).toBe('ARS 1.500,50');
  });

  it('formatearMonto_SiempreDosDecimales', () => {
    expect(formatearMonto(10, 'ARS')).toBe('ARS 10,00');
  });

  it('formatearFecha_ConvierteIsoADdMmYyyy', () => {
    expect(formatearFecha('2026-08-17')).toBe('17/08/2026');
  });
});
