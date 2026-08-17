# Parent PRD: Registro y listado de movimientos con resumen de totales

| Metric | Value |
|--------|-------|
| Ticket | FEAT-001 |
| Tracker | ninguno |
| Date | 2026-08-16 |
| Status | Dividido |

## Sub-tickets

| Sub-ticket | Título | PRD | Dependencias | Estado |
|---|---|---|---|---|
| FEAT-001a | Alta de movimientos y listado simple | prd-FEAT-001a.md | ninguna | activo |
| FEAT-001b | Filtros del listado, edición y eliminación de movimientos | prd-FEAT-001b.md | depende de a | pendiente |
| FEAT-001c | Resumen del mes con desglose por categoría | prd-FEAT-001c.md | depende de a y de b | pendiente |

## Orden de implementación sugerido

a → b → c

`c` depende de `b` y no solo de `a`: su FR-03 exige que el resumen se mantenga fijo al mes actual
cuando el listado se filtra por otro rango, y eso no es observable hasta que los filtros existen.

## Contexto original

El repositorio contenía únicamente el scaffolding del método y el PRD del producto (PRD-001,
`docs/daw/prd/PRD.md`, versión 5), sin una línea de código. FEAT-001 se definió como el núcleo mínimo
utilizable de la aplicación, recortando de PRD-001 el registro de movimientos, el listado con filtros
y el resumen de totales, y dejando fuera la autenticación (RF-01..RF-05), el ABM de categorías
propias (RF-07..RF-09), el dashboard con gráficos (RF-19..RF-21) y el soporte multi-moneda
(RF-24..RF-32).

Ese recorte quedó igualmente demasiado grande para un ticket: 18 requerimientos funcionales, 36
criterios de aceptación y tres módulos afectados —esquema y migración MySQL, API .NET y frontend
React—, frente a un umbral de 5 a 7 criterios. Se dividió en tres sub-tickets porque `a` carga con
todo el arranque técnico del proyecto y fija el modelo de datos del que dependen los otros dos: un
error de esquema ahí se descubre con el triple de código escrito encima si va en el mismo ticket que
los filtros y el resumen.

Dos decisiones de producto que atraviesan los tres sub-tickets y que se tomaron de forma explícita:

- **Sin autenticación.** El modelo lleva la pertenencia al usuario desde el día uno, pero el usuario
  es una fila semilla fija provista por una única abstracción. El aislamiento real entre usuarios
  (AC-06..AC-08 de PRD-001) no es verificable en este ticket y queda para el de autenticación, que
  solo reemplaza esa abstracción.
- **Una sola moneda.** Todo movimiento se registra en ARS, pero la moneda se persiste como dato del
  movimiento y no como constante del código, para que multi-moneda no requiera migrar datos.
