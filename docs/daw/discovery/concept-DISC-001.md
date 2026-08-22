# Concept: Mapa de tickets del resto de PRD-001

| Metric | Value |
|--------|-------|
| Ticket | DISC-001 |
| Date | 2026-08-20 |
| Status | Closed |

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

- **D-1 · Linter del backend. ✅ HECHO** — FIX-001 (PR #6) y FIX-002 (PR #7), mergeados el
  2026-08-21. `backend/Directory.Build.props` + `backend/.editorconfig`, con
  `backend/verificar-linter.sh` corriendo en el CI para que la barrera no se pueda desarmar en
  silencio.
  > **La medición que este ítem citaba estaba mal.** Decía 258 hallazgos con 188 CA1707 y 18
  > CA1725; se había hecho sobre `a`+`b` sin contar `Resumen/`. Rehecha en
  > `docs/daw/specs/rca-FIX-001.md` (2026-08-20): **158 hallazgos únicos**, 143 en tests y 15 en
  > producción, con 117 CA1707 y 9 CA1725. Y el dato que cambió la forma del ticket: en producción
  > no había **ninguna** corrección que hacer a mano, solo tres decisiones de configuración.
- **D-2 · Contrato frontend↔backend sin verificar. ✅ HECHO** — FEAT-003 (PR #9). La verificación
  lee `frontend/src/api/tipos.ts` como fuente de verdad y lo compara contra el JSON que la API emite
  de verdad, en las dos direcciones y en los cuatro `GET` más los dos cuerpos de petición.
  > **Este ítem estaba mal descrito, y en el punto que más importaba.** Decía *"hoy sólo lo detecta
  > `tsc --noEmit`, que corre aparte"*, lo que implicaba que el arreglo era meter el typecheck en el
  > comando de test. La medición del 2026-08-21 lo refutó: los tipos del frontend están escritos a
  > mano y no derivan de nada del backend, así que un rename **coherente** del backend deja en verde
  > el build, los 142 tests, `tsc`, los 105 de Vitest, ESLint y la barrera del linter — y llega
  > `undefined` a la pantalla. `tsc` verifica que el frontend sea coherente **consigo mismo**, no que
  > coincida con el backend. Por eso el ticket salió FEATURE y no la corrección de configuración que
  > este ítem sugería.
- **D-3 · El fixture de rendimiento vence el 2027-01-01. ✅ HECHO** — FIX-004 (PR #10).
  `GenerarFechasSembradas(DateOnly)` es ahora una función pura parametrizada por fecha, anclada al
  año en curso, siguiendo el patrón que producción ya usaba para el calendario (`RangoDelMes.De`).
  El piso quedó calculado: 60 filas en el peor mes contra las 2 que el criterio exige.
  > **Este ítem se equivocaba en las dos direcciones, y conviene que quede escrito.**
  > **Exageraba** al decir que arreglarlo obligaba a revisar *los otros dos* tests de rendimiento:
  > `RendimientoListadoTests` sí hubo que tocarlo —su rango estaba fijo en marzo de 2026 y dejaba de
  > coincidir en cuanto el sembrado se volvía relativo—, pero `RendimientoAltaTests` no depende de
  > fechas y no se tocó, y `RendimientoResumenTests` tampoco: el plan predijo que no haría falta y
  > las tres rondas de verificación lo confirmaron. Uno de dos, no dos de dos.
  > **Y omitía lo que más acotaba el daño:** `ConfirmarQueElMesTieneFilas()` ya existía y convertía
  > el fallo silencioso en uno explícito. Sin ese guardarraíl el test habría pasado en verde
  > midiendo una consulta vacía, y el ítem lo habría descrito como una rotura y no como un arnés que
  > avisa.
  > **Lo que el ítem no podía ver:** el test hermano, `RendimientoListadoTests`, no tenía guardarraíl
  > propio. FIX-004 le agregó el suyo tras un FAIL en la ronda 2 de verificación.

- **D-4 · Comparación por subcadena en el validador de PRDs. ✅ HECHO** — FIX-003 (PR #8).
  `.daw/scripts/validate_prd.py` comparaba los identificadores con `if i in t`, y `"FR-01"` es
  subcadena de `"NFR-01"`. Detectado el 2026-08-20 al validar `prd-DISC-001-01c.md`: reportó 7 AC
  sobre FR-01, que tiene 4. Resuelto con un límite de palabra en los dos puntos de llamada.
  > **Este ítem describía una sola de las dos víctimas.** Además del falso positivo de `W-PRD-02`,
  > la misma línea rota le costaba a **`F-PRD-01` —una regla FAIL— un falso negativo**: un FR sin
  > ningún AC que lo validara pasaba el gate en cuanto algún AC mencionara `NFR-01`. Eso es
  > exactamente lo que el catálogo dice que nunca puede quedar sin detectar. Revalidados los 14
  > PRDs del repositorio tras el arreglo: ninguno cambió de veredicto.

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
- **2026-08-20: los datos del usuario semilla se descartan en la migración de `1a`.** Decisión del
  usuario. Son datos de desarrollo, no de un usuario real: la aplicación arranca vacía y la primera
  cuenta empieza de cero. Se pierde lo cargado probando, a cambio de no dejar en el modelo una
  lógica de adopción que corre una sola vez en la vida del producto.
- **2026-08-20: la autenticación se parte en tres desde acá, no en DEFINE.** Decisión del usuario.
  El límite de intentos fallidos sale a `1b` en vez de esperar a que el PLAN descubra que no entra.
- **2026-08-20: multi-moneda se parte en dos, y el corte no es por capa sino por seguridad del
  dato.** `4a` (catálogo + totales por moneda) no cambia nada visible; `4b` (selector, columna,
  filtro) hace alcanzable la segunda moneda. El orden inverso dejaría un ticket entero mostrando
  totales que mezclan monedas.
- **2026-08-20: el dashboard va después de multi-moneda, no antes.** Los dos reescriben la
  agregación de totales; hacerlo al revés es escribirla dos veces.

## PRDs identificados

| # | Título | Archivo | Estado |
|---|--------|---------|--------|
| 1a | Identidad y sesión | prd-DISC-001-01a.md | validated |
| 1b | Límite de intentos fallidos | prd-DISC-001-01b.md | validated |
| 1c | Aislamiento entre cuentas verificado | prd-DISC-001-01c.md | validated |
| 2 | Nota descriptiva del movimiento | prd-DISC-001-02.md | validated |
| 3 | Categorías propias del usuario | prd-DISC-001-03.md | validated |
| 4a | Catálogo de monedas y totales por moneda | prd-DISC-001-04a.md | validated |
| 4b | Registrar y filtrar en varias monedas | prd-DISC-001-04b.md | validated |
| 5 | Dashboard con gráficos | prd-DISC-001-05.md | validated |
| 6 | Maquetación y accesibilidad | prd-DISC-001-06.md | validated |

## Mapa de dependencias

```
D-1 linter backend  ─┐  ✅ FIX-001 + FIX-002
D-2 contrato sin     ├─→ (infraestructura, sin PRD, primero por decisión del usuario)
    verificar        │   ✅ FEAT-003
D-3 fixture 2027     │   ✅ FIX-004  ← deuda de infraestructura cerrada
D-4 subcadena en    ─┘  ✅ FIX-003
    validate_prd.py
                     │
                     ▼
   [1a] Identidad ─→ [1b] Límite ─→ [1c] Aislamiento
     y sesión         de intentos      verificado
                                          │
                                          ├──→ [3] Categorías propias
                                          │
                     [2] Nota ────────────┤    (independiente: entra en cualquier hueco)
                                          │
                                          └──→ [4a] Catálogo y ──→ [4b] Registrar y ──→ [5] Dashboard
                                               totales por      filtrar en
                                               moneda           varias monedas
                                                                      │
                                                                      ▼
                                                            [6] Maquetación y AC-55
```

**Por qué cada arista:**

- **[1a..1c] antes que [3]:** AC-12 exige que una categoría propia de un usuario **no aparezca para
  ningún otro**. Sin autenticación hay un solo usuario y ese criterio no es observable — se
  implementaría a ciegas y se verificaría con un test que no puede fallar.
- **[1a..1c] antes que [4] y [5]:** no es una dependencia lógica, es de superficie. Multi-moneda toca el
  formulario, el listado, los filtros y el resumen; el dashboard agrega una pantalla entera. Todo lo
  que exista cuando llegue la autenticación hay que revisarlo para el aislamiento. Adelantarlas
  agranda ese barrido sin comprar nada.
- **[4a] antes que [4b], y [4b] antes que [5]:** RF-29 prohíbe sumar montos de monedas distintas en cualquier total. El
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

**La autenticación ya está partida en tres.** Tenía 5 RF, 3 RNF y 12 AC sobre esquema, API y
frontend, por encima del umbral que obligó a partir FEAT-001. Los tres cortes son secuenciales y
**los tres tienen que estar en `main` antes de exponer la aplicación a usuarios reales**:

- **[1a] Identidad y sesión** — alta, login, logout, sesión obligatoria, hash y expiración a 24 h.
  Es el que reemplaza `IUsuarioActual` y hace observable todo lo demás.
- **[1b] Límite de intentos fallidos** — el conteo de 5 fallos y la ventana de 15 minutos de
  RNF-05. Sale aparte porque es la parte con más estado propio del ticket (dónde se guarda el
  contador, cómo se limpia) y porque `1a` sin él ya es entregable e independientemente
  verificable. La contra, anotada en el PRD de `1a`: entre los dos tickets no hay ninguna
  protección de fuerza bruta.
- **[1c] Aislamiento verificado** — AC-06..AC-08 de PRD-001 con dos cuentas reales. Es más chico
  de lo que parece: al reemplazar `IUsuarioActual` en `1a`, el filtro global de EF ya acota las
  lecturas. Lo que queda es verificarlo con dos cuentas, cerrar las escrituras —el filtro no
  aplica a INSERT— y el acceso por id directo.
