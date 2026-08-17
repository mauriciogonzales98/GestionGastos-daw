/**
 * Utilidades compartidas por los tests. Vive acá y no dentro de una carpeta de feature porque nada
 * de esto es de un componente en particular.
 */

/**
 * Construye una `Response` con cuerpo JSON. Los valores por omisión son el caso frecuente —200 con
 * `application/json`— y `tipo` se pasa explícito solo para los rechazos, que llegan como
 * `application/problem+json` (RFC 9457).
 */
export function json(cuerpo: unknown, estado = 200, tipo = 'application/json'): Response {
  return new Response(JSON.stringify(cuerpo), {
    status: estado,
    headers: { 'Content-Type': tipo },
  });
}
