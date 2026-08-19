// Archivo temporal para comprobar que CI detecta errores. Se borra después.
export function sumar(a: number, b: number): number {
  const noSeUsa = 'esto deberia romper el lint';
  return a + b;
}
