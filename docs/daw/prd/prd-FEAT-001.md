# Parent PRD: Registro y listado de movimientos con resumen de totales

| Metric | Value |
|--------|-------|
| Ticket | FEAT-001 |
| Tracker | ninguno |
| Date | 2026-08-16 |
| Status | Dividido |

## Sub-tickets

| Sub-ticket | Título | PRD | Dependencias | Estado | Integración |
|---|---|---|---|---|---|
| FEAT-001a | Alta de movimientos y listado simple | prd-FEAT-001a.md | ninguna | **done** | Mergeado a `main` con `--no-ff` (`0b57669`), PR [#1](https://github.com/mauriciogonzales98/GestionGastos-daw/pull/1) mergeado — 2026-08-17 |
| FEAT-001b | Filtros del listado, edición y eliminación de movimientos | prd-FEAT-001b.md | depende de a | **done** | Mergeado a `main` con `--no-ff` (`52c8142`), PR [#2](https://github.com/mauriciogonzales98/GestionGastos-daw/pull/2) mergeado — 2026-08-19 |
| FEAT-001c | Resumen del mes con desglose por categoría | prd-FEAT-001c.md | depende de a y de b | **activo** | — |

> **FEAT-001c arranca desde `main`**, que ya tiene el modelo de datos, la API y el frontend de `a`,
> más los filtros, la edición y la eliminación de `b`.
>
> **Deuda que hereda de FEAT-001b**, con ubicación exacta en `docs/daw/reports/verify-FEAT-001b.md` y
> en la sección "Errata" de `docs/daw/reports/tdd-FEAT-001b.md`:
> - **Dos correcciones a `spec-FEAT-001b.md`** para aplicar en el PLAN de `c`, por el mismo motivo
>   estructural de siempre —desde CODE y VERIFY la fase PLAN es inalcanzable—: la firma escrita de
>   `obtenerMovimientos` incluye un `senal?: AbortSignal` que no existe en el código, y la aritmética
>   de "Final verification" dice 186 tests cuando la suite real tiene 217. Lo segundo se investigó y
>   **no esconde ningún gap**: cada test extra está justificado bloque por bloque en el reporte TDD.
> - **Un refactor transversal, candidato para el arranque:** `FiltrosMovimientos` y
>   `FormularioMovimiento` piden cada uno `GET /api/categorias` al montar —dos peticiones al mismo
>   endpoint por arranque, y una tercera al abrir la edición—. Subir la carga del catálogo a `App` y
>   bajarla como prop lo elimina, pero cambia la interfaz pública de dos componentes y obliga a
>   reescribir sus fixtures.
> - **Dos componentes por debajo del 80% de ramas mirados de a uno:** `FiltrosMovimientos.tsx`
>   (73.33%) y `ConfirmarEliminacion.tsx` (75%). El agregado sobre el código nuevo pasa, que es lo
>   que mide F-VER-03; queda decidir si se sube o se acepta.
> - **Los tests de rendimiento miden tiempo real** (p95 < 1s) y son sensibles a un entorno cargado.
>   Patrón heredado de `a` y conservado en `b`.
>
> **Deuda que FEAT-001b heredó de FEAT-001a** —ya saldada en `b`, se conserva como registro—, con
> ubicación exacta en la sección "Errata" y en los WARNINGs de
> `docs/daw/reports/verify-FEAT-001a.md`:
> - **Diez correcciones a `spec-FEAT-001a.md`** que hay que aplicar en el PLAN de `b`, porque desde
>   CODE y VERIFY la fase PLAN es inalcanzable (el grafo de transiciones no tiene esas aristas). Las
>   tres que importan: la spec contradice a ADR-002 sobre la cadena de tests, su "API contract" del
>   POST no documenta `tipoEsperado` —y `b` **consume ese contrato**—, y describe un test como "de
>   punta a punta" cuando no lo es, que fue el defecto que costó una ronda de verificación.
> - **Tres arreglos chicos de código**, candidatos para el arranque: un mutante sobreviviente en
>   `leerProblema` (`cliente.ts:113-117`), el parámetro `AbortSignal` que ningún llamador pasa
>   (`cliente.ts:49,53`), y una cuarta copia del helper `json` en `cliente.test.ts:19`.
> - **Cualquier test de ordenamiento necesita doble capa.** El índice
>   `(usuario_id, fecha DESC, id DESC)` hace que MySQL devuelva el orden correcto aunque la consulta
>   no lo pida, así que un test conductual da verde con el `ORDER BY` borrado. Los dos tests de orden
>   de `a` asertan además sobre el SQL que EF emite, vía `Tests/Infra/ObservadorDeSql.cs`.
> - **El filtro global de EF protege las lecturas, no las escrituras.** No aplica a INSERT: cada
>   bloque que escriba movimientos tiene que asignar el propietario desde `IUsuarioActual` a mano.

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
