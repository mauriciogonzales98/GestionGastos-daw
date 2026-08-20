# Concept: Mapa de tickets del resto de PRD-001

| Metric | Value |
|--------|-------|
| Ticket | DISC-001 |
| Date | 2026-08-20 |
| Status | Exploring |

## Visión

Llevar la aplicación desde el núcleo utilizable que dejó FEAT-001 —registrar, listar, filtrar,
editar, eliminar y resumir movimientos de un único usuario semilla en pesos— hasta el producto que
describe PRD-001: multiusuario real, con categorías propias, varias monedas y un dashboard con
gráficos. Este ticket no construye nada de eso: **decide en qué orden se construye y por qué**.

## Problema / Oportunidad

De PRD-001 quedan sin ticket 33 requerimientos funcionales y no funcionales repartidos en cinco
bloques temáticos. Tomados de a uno, en cualquier orden, tres cosas salen mal:

1. **La autenticación toca todo lo ya escrito.** Hoy el propietario de cada movimiento lo provee
   `IUsuarioActual`, apuntando a una fila semilla fija. Cada feature que se escriba antes de la
   autenticación agrega consultas, endpoints y componentes que después hay que revisar uno por uno.
   Cuanto más tarde llegue, más superficie tiene que barrer.
2. **Multi-moneda y dashboard compiten por los mismos archivos.** Los dos reescriben los totales.
   Hacer el dashboard primero significa escribir la agregación por categoría en una moneda y
   volver a escribirla entera cuando aparezca la segunda.
3. **La deuda de infraestructura no la levanta ninguna feature.** El backend sin linter y Vitest sin
   `typecheck` son gates que no existen: no fallan, no molestan, y por eso nunca son el trabajo de
   nadie. Ya se perdió una ventana para el linter —el plan era hacerlo entre FEAT-001b y `c`, y `c`
   se escribió, verificó y mergeó sin él.

## Usuarios objetivo

Sin cambios respecto de PRD-001: **el usuario individual**, una persona que controla sus gastos e
ingresos personales, no comparte sus finanzas con nadie dentro de la aplicación y necesita
responder dos preguntas —*en qué se me va la plata* y *cómo vengo este mes*.

Lo que sí cambia con la autenticación es que **deja de haber un solo usuario**: pasan a convivir
varias cuentas aisladas entre sí. Eso no agrega una persona nueva al producto; agrega el requisito
de que ninguna vea los datos de otra (AC-06..AC-08).

## Features candidatas

### Deuda de infraestructura (sin PRD — decidido primero por el usuario)

- **D-1 · Linter del backend.** `.editorconfig` + `Directory.Build.props` con
  `EnforceCodeStyleInBuild` y `AnalysisMode=Recommended`; el comando de lint pasa a ser
  `dotnet format backend/GestionGastos.sln --verify-no-changes`. Medición ya hecha (2026-08-18):
  258 hallazgos, de los cuales 188 son un CA1707 que hay que **apagar** —renombrar 188 tests para
  sacarles los guiones bajos los empeora— y 18 un CA1725 que choca con nombrar parámetros en
  español. Trabajo real: ~12 correcciones mecánicas + 2 reglas silenciadas con su motivo. La cifra
  de producción está **subestimada**: se midió sobre `a`+`b`, falta contar `Resumen/`.
- **D-2 · Vitest sin `typecheck`.** Un contrato del frontend desalineado con el DTO del backend deja
  la suite verde y aparece como `undefined` en pantalla. Demostrado con una mutación en el Block 3
  de FEAT-001c. Hoy solo lo detecta `tsc --noEmit`, que corre aparte.
- **D-3 · El fixture de rendimiento vence el 2027-01-01.** `MedicionDeRendimiento.FechasSembradas`
  siembra fechas de 2026 mientras el resumen usa el reloj real. Registrado como W-VER-03. No es una
  regresión, es el arnés. Arreglarlo obliga a revisar los otros dos tests de rendimiento que
  comparten `SembrarMovimientosAsync`.

### Producto (lo que queda de PRD-001)

- **Autenticación y aislamiento por usuario** — RF-01..RF-05, RNF-03, RNF-04, RNF-05.
  Alta de cuenta, login, sesión obligatoria, logout, hash seguro, expiración a 24 h y límite de
  5 intentos fallidos por email.
- **Categorías propias** — RF-07, RF-08, RF-09. Crear, renombrar y dar de baja lógica categorías
  del usuario, conservando el nombre en los movimientos ya registrados.
- **Multi-moneda** — RF-24..RF-32 más RF-27, RF-28, RF-29, RF-30. Catálogo de monedas administrado
  como dato, moneda por movimiento, filtro por moneda y la regla dura: **ningún total mezcla
  monedas**.
- **Dashboard con gráficos** — RF-19, RF-20, RF-21. Total de gastos por categoría representado
  gráficamente, balance por moneda y filtro por rango de fechas.
- **Nota descriptiva** — RF-33. Texto libre opcional de hasta 120 caracteres por movimiento,
  visible en el listado. No se busca, no se filtra, no se agrupa.
- **Maquetación y accesibilidad** — RNF-06 / AC-55. Ninguna de las tres features de FEAT-001 definió
  su maquetación: el CSS resuelve lo semántico (color de error, foco visible, contraste AA) pero
  las clases de disposición no tienen regla.

## Restricciones y consideraciones

- **El modelo ya carga la pertenencia al usuario.** FEAT-001 lo decidió así a propósito: el usuario
  es una fila semilla detrás de `IUsuarioActual`, y la autenticación **reemplaza esa abstracción**
  en vez de migrar datos.
- **La moneda ya se persiste como dato del movimiento**, no como constante del código, por la misma
  razón: multi-moneda no requiere migrar datos.
- **El filtro global de EF protege las lecturas, no las escrituras.** No aplica a INSERT. Cada
  bloque que escriba movimientos asigna el propietario desde `IUsuarioActual` a mano. Con
  autenticación real esto pasa de ser una convención a ser un control de seguridad.
- **Cualquier test de ordenamiento necesita doble capa.** El índice `(usuario_id, fecha DESC,
  id DESC)` hace que MySQL devuelva el orden correcto aunque la consulta no lo pida.
- **Los tests de rendimiento miden tiempo de pared** y el CI los excluye con
  `--filter "FullyQualifiedName!~Rendimiento"`. En local corren todos.
- **Techo de ~300 líneas agregadas por commit**, acordado el 2026-08-20. Los bloques que pinten por
  encima se parten desde el plan, no al momento de commitear.
- Sin dependencias nuevas sin justificarlas en la spec (`AGENTS.md`). Esto pesa sobre todo en dos
  puntos: la librería de gráficos del dashboard y la de hashing de contraseñas.

## Decisiones tomadas

- **2026-08-20: la deuda de infraestructura va primero, antes que cualquier feature de producto.**
  Decisión del usuario. Motivo: son gates que no existen, y mientras no existan, todo lo que se
  escriba encima se escribe sin ellos —que es exactamente lo que ya pasó con FEAT-001c y el linter.
- **2026-08-20: la autenticación va inmediatamente después de la deuda.** Decisión del usuario.
  Coincide con el análisis: es la que más superficie ya escrita toca, y cada feature que se
  adelante agranda esa superficie.
- **2026-08-20: la deuda de infraestructura no lleva PRD.** Un `.editorconfig` y una línea de
  configuración de Vitest no tienen requerimientos funcionales ni criterios de aceptación de
  producto que valga la pena escribir. Se clasifican como FIX o QUICK-FIX cuando les toque, con su
  fix-brief.
- **2026-08-20: el dashboard va después de multi-moneda, no antes.** Los dos reescriben la
  agregación de totales; hacerlo al revés es escribirla dos veces.

## PRDs identificados

| # | Título | Archivo | Estado |
|---|--------|---------|--------|
| 1 | Autenticación y aislamiento por usuario | prd-DISC-001-01.md | identified |
| 2 | Nota descriptiva del movimiento | prd-DISC-001-02.md | identified |
| 3 | Categorías propias del usuario | prd-DISC-001-03.md | identified |
| 4 | Multi-moneda | prd-DISC-001-04.md | identified |
| 5 | Dashboard con gráficos | prd-DISC-001-05.md | identified |
| 6 | Maquetación y accesibilidad | prd-DISC-001-06.md | identified |

## Mapa de dependencias

```
D-1 linter backend ─┐
D-2 vitest typecheck├─→ (infraestructura, sin PRD, primero por decisión del usuario)
D-3 fixture 2027   ─┘
                     │
                     ▼
              [1] Autenticación ──────┬──→ [3] Categorías propias
                                      │
                     [2] Nota ────────┤    (independiente: puede entrar en cualquier hueco)
                                      │
                                      └──→ [4] Multi-moneda ──→ [5] Dashboard
                                                                      │
                                                                      ▼
                                                            [6] Maquetación y AC-55
```

**Por qué cada arista:**

- **[1] antes que [3]:** AC-12 exige que una categoría propia de un usuario **no aparezca para
  ningún otro**. Sin autenticación hay un solo usuario y ese criterio no es observable — se
  implementaría a ciegas y se verificaría con un test que no puede fallar.
- **[1] antes que [4] y [5]:** no es una dependencia lógica, es de superficie. Multi-moneda toca el
  formulario, el listado, los filtros y el resumen; el dashboard agrega una pantalla entera. Todo lo
  que exista cuando llegue la autenticación hay que revisarlo para el aislamiento. Adelantarlas
  agranda ese barrido sin comprar nada.
- **[4] antes que [5]:** RF-29 prohíbe sumar montos de monedas distintas en cualquier total. El
  dashboard es todo totales (RF-19, RF-20). Construirlo primero es escribir la agregación por
  categoría en una sola moneda y reescribirla entera después.
- **[6] al final:** una pasada de maquetación sobre pantallas que todavía no existen se rehace.
  AC-55 (completar y enviar el formulario solo con teclado) sí se puede verificar antes, pero el
  resto de RNF-06 se mide sobre la disposición final.
- **[2] sin aristas:** la nota es una columna, un `input`, una validación de 120 caracteres y una
  celda del listado. No la bloquea nada y no bloquea nada. Es el ticket que entra en cualquier hueco
  —por ejemplo, si la autenticación se parte en dos y hay que esperar algo.

**Lo que puede ir en paralelo:** [2] con cualquiera, en otro `git worktree`. [3] y [4] entre sí una
vez que [1] esté en `main`, pero se pisan en el formulario de registro, así que en la práctica
conviene serializarlas salvo que haya dos personas.

**Advertencia de tamaño:** [1] Autenticación tiene 5 RF, 3 RNF y 12 AC, y toca esquema, API y
frontend. Está por encima del umbral de 5 a 7 criterios que ya obligó a partir FEAT-001 en tres.
Es muy probable que su PRD se divida en sub-tickets al llegar a DEFINE —el corte natural es
*alta + login + sesión* primero, y *aislamiento retroactivo de lo ya escrito* después—. Se anota
acá para que no sorprenda.
