# Verificación — FEAT-001a: Alta de movimientos y listado simple

- **Ticket:** FEAT-001a · **Tier:** FEATURE
- **PRD:** `docs/daw/prd/prd-FEAT-001a.md`
- **Spec:** `docs/daw/specs/spec-FEAT-001a.md`
- **Threat model:** `docs/daw/security/threat-FEAT-001a.md` · **SAST:** `docs/daw/security/sast-FEAT-001a.md`
- **Rama:** `feat/FEAT-001a-alta-listado-movimientos`
- **Reglas aplicadas:** `.daw/rules/validation-rules.instructions.md` §5 (F-VER-01 … F-VER-06,
  W-VER-01 … W-VER-03)

---

## Ronda 1 — 2026-08-17 — **BLOCKED**

Verificación cruzada ejecutada por `daw-module-verifier`, un agente que no escribió el código.

### Estado medido

| Medición | Resultado |
|----------|-----------|
| Suite backend (xUnit) | 87/87 verde |
| Suite frontend (Vitest) | 38/38 verde, 5 archivos |
| Cobertura backend (coverlet, `Migrations/` excluido) | líneas **97,42%** (415/426) · ramas **93,59%** (73/78) · métodos **94,74%** (36/38) |
| Cobertura frontend (`@vitest/coverage-v8`) | líneas **90,65%** · ramas **84,81%** · funciones **87,23%** (41/47) |
| `dotnet build` | 0 warnings |
| `dotnet format --verify-no-changes` | limpio |
| `pnpm build` (`tsc --noEmit` + `vite build`), `pnpm lint`, `pnpm format` | limpios |

### Resultado por regla

| Regla | Resultado |
|-------|-----------|
| **F-VER-01** — cada AC del PRD con código y test que verifica comportamiento | ❌ **FAIL** en AC-05 y AC-06 (una sola causa raíz). 16 ✅, 1 ⚠️ (AC-18) |
| **F-VER-02** — cada tarea de la spec implementada | ✅ los 5 bloques completos: 18/18, 11/11, 3/3, 15/15, 5/5 archivos |
| **F-VER-03** — cobertura ≥ 80% en líneas, ramas **y funciones** | ✅ las tres métricas por encima, en backend y frontend |
| **F-VER-04** — camino triste por endpoint/función con entrada | ✅ POST `/api/movimientos` (12+ casos), `GET /{id}` (404 ×2), `GET /api/movimientos` (vacío, recorte, base caída, query desconocida), formulario (8), cliente HTTP (red, 500, cuerpo no-JSON) |
| **F-VER-05** — lint / type checker sin errores | ✅ |
| **F-VER-06** — cada test que la spec nombra existe y pasa | ❌ **FAIL**: los 79 tests nombrados existen y pasan, pero `Listado_TrasUnAlta_MuestraElMovimientoNuevo` no entrega la garantía que la spec le atribuye |
| **W-VER-01** — código muerto, imports sin usar | ⚠️ 2 hallazgos |
| **W-VER-02** — lógica de negocio 80–90% | ✅ **por encima del rango** |
| **W-VER-03** — tests frágiles | ⚠️ menores, ninguno produce flakiness hoy |

```
FAILs: 2 (una causa raíz) · WARNs: 8 · PASSes: 34
Veredicto: BLOCKED
```

---

### ❌ FAIL — F-VER-01 (AC-05, AC-06) y F-VER-06: la costura alta → refresco no tiene test

`frontend/src/App.tsx` está al **0% de statements y 0% de funciones**. Ningún test lo renderiza, y
está incluido en la medición (`vite.config.ts` solo excluye `src/main.tsx` y los `*.test.*`).

Lo que vive sin cubrir es la única pieza de código que cumple la mitad *"…y **mostrarlo en el
listado**"* de AC-05 y AC-06:

```tsx
// frontend/src/App.tsx:13
<FormularioMovimiento onCreado={() => setVersion((actual) => actual + 1)} />
```

El test que la spec comprometió para eso —Bloque 5, `Listado_TrasUnAlta_MuestraElMovimientoNuevo`,
descrito en la spec como *"valida AC-05 y AC-06 de punta a punta"*— **no va de punta a punta**:
simula la costura a mano en `ListadoMovimientos.test.tsx:120`.

```tsx
// Simula el refresco tras un alta exitosa: el padre incrementa `version`.
rerender(<ListadoMovimientos version={1} />);
```

**Consecuencia demostrable:** borrando `onCreado={…}` de `App.tsx:13`, las 125 pruebas siguen verdes
y la aplicación deja de cumplir AC-05/AC-06 de forma visible para el usuario. Es exactamente el modo
de fallo que este mismo ticket persiguió y mató en el Bloque 3 con `ObservadorDeSql` — un mutante que
sobrevive porque el test no toca el contrato. Aplicar ese estándar en el backend y no acá es
incoherente, y es el motivo por el que esto es FAIL y no WARN.

**Qué falta exactamente:** un test que renderice `<App />` con `fetch` mockeado (catálogo + listado
inicial + 201 del alta + listado con el item nuevo) y afirme que, tras completar y enviar el
formulario, la fila nueva aparece en la tabla. Aproximadamente 25 líneas en
`frontend/src/App.test.tsx`. Corresponde a la fase CODE: VERIFY no escribe código.

---

### Las seis contradicciones de la spec — veredicto

**Ninguna es un incumplimiento de la implementación. Las seis son defectos de la spec.** La
distinción importa porque decide qué fase tiene que corregirlas.

| # | Ubicación | Veredicto |
|---|-----------|-----------|
| 1 | `spec:188` | Error de la spec. El criterio dice 12 tests; la lista tiene 14 y hay 14 en disco, verdes. Aritmética del criterio. |
| 2 | `spec:299` | Error de la spec. Dice 25; la lista tiene 26 y en disco hay 35 métodos. La implementación es un **superset**, nunca un déficit. |
| 3 | `spec:365` | Error de la spec. Dice 8; la lista tiene 10 y hay 10 en disco. |
| 4 | `spec:300` | Error de la spec. Los ocho casos canónicos están implementados literalmente en `CrearMovimientoTests.cs:547` y los ocho devuelven 400 con `errors` poblado dejando la tabla en 0. El "ocho" **subcuenta** la superficie real de rechazo (hay ≥12). Subcuenta, no incumplimiento. |
| 5 | `spec:99-103` vs ADR-002 | **La spec está mal; el ADR tiene razón.** La spec promete un valor por defecto apuntando a `gestiongastos_test`; ADR-002 exige que sin la variable el gate falle. `BaseDeDatosFixture.ResolverCadena` implementa el ADR y `CadenaDeTests_SinVariableDeEntorno_FallaNombrandola` (Theory ×3) lo fija. Corregir la spec: el default silencioso convierte un error de configuración en un error de autenticación contra una base ajena, que es el diagnóstico peor. |
| 6 | `spec:231-236` vs `spec:261` | Error de la spec. El "API contract" del POST omite `tipoEsperado`; el "Error handling" lo exige y la implementación lo sigue (`CrearMovimientoRequest.cs:26`, opcional, comparado en `MovimientosEndpoints.cs:59`, **no persistido**). Hueco de documentación con consecuencia real: quien consuma este contrato en FEAT-001b no encontrará el campo. |

**Corrección de proceso.** `PENDIENTES-FEAT-001a.md:98` afirmaba que estas seis "van todas juntas a
VERIFY, que es la fase donde se corrigen". Es incorrecto: `.daw/orchestrator.md` prohíbe modificar la
spec en CODE, VERIFY y RELEASE. Corregirlas exige un **bucle correctivo a PLAN**. Como las seis son
documentales y la implementación es correcta o superset, la alternativa proporcionada es dejarlas
registradas acá como deuda documental aceptada. **No bloquean por sí mismas.**

> **Decisión del usuario (2026-08-17): deuda documental registrada.** No se reabre PLAN. Las seis
> quedan asentadas en la tabla de arriba, con veredicto y ubicación, y esta sección es el registro
> autoritativo de la discrepancia entre la spec y el código: **donde difieran, el código y el ADR
> ganan.** Se asumen dos consecuencias concretas: la spec conserva cuatro conteos de tests
> equivocados, y su "API contract" del POST sigue sin nombrar `tipoEsperado` — lo que puede morder en
> FEAT-001b, que consume ese contrato. Queda anotado acá para que el ticket siguiente lo encuentre.

---

### Verificaciones específicas solicitadas

**Archivos de `Tests/Infra/` fuera de la lista de la spec — extracción de helpers, sin lógica nueva.**
Los cuatro leídos y confirmados: `ObservadorDeSql.cs` (técnica de observación de SQL, gemela de
`RegistroDeExcepciones`, sin lógica de dominio), `Excepciones.cs` (un método, elimina 4 copias),
`JsonDeRespuesta.cs` (con el `Clone()` correcto), `MovimientosEnLaBase.cs` (SQL **constante**, sin
interpolación, respeta R-05). Cero lógica de producción se colonizó ahí.

**La trampa del índice — sigue tapada y el mutante muere.** Doble capa confirmada en
`Listar_OrdenaPorFechaDescendente` (`ListarMovimientosTests.cs:70`) y
`Listar_DesempataPorIdDescendente` (`:119-125`). El mecanismo falla **cerrado**:
`ObservadorDeSql.OrderByDe` (`:60-69`) afirma primero que alguna consulta leyó `movimientos` y
después que alguna emitió `ORDER BY`. Por lo tanto: borrar `.OrderByDescending` completo → falla con
"Ninguna consulta a 'movimientos' emitió ORDER BY"; borrar solo `.ThenByDescending(m => m.Id)` →
falta `` `id` DESC `` → muerto; invertir el orden de las claves → falla la aserción de índices.
Muerto en los tres casos. Único costo: la aserción ata al dialecto de Pomelo (comillas invertidas),
aceptable porque `AGENTS.md` fija el stack.

**Final verification de la spec (8 condiciones):** 1 ❌ (por AC-05/06) · 2 ✅ · 3 ✅ · 4 ✅ · 5 ✅
(`git ls-files` sin credenciales) · 6 ✅ (grep de `FromSqlRaw`/`ExecuteSqlRaw`/
`dangerouslySetInnerHTML` vacío) · 7 ⚠️ verificado a nivel de configuración, no de binding en
runtime · 8 ⚠️ pendiente de comprobación manual, no automatizable.

---

### WARNINGs registrados (no bloquean)

1. **W-VER-01 — `ResultadoValidacion.Errores` (`ResultadoValidacion.cs:20`) es código muerto.**
   Público y sin un solo llamador: `grep -rn "\.Errores\b" backend` vuelve vacío. Borrar.
2. **W-VER-01 — `MontoJsonConverter.Write` (`CrearMovimientoRequest.cs:60`) es inalcanzable en
   producción.** El converter solo decora el DTO de entrada y la respuesta serializa `MovimientoDto`.
   Lo obliga la clase base; no se puede borrar.
3. **AC-18 (⚠️, F-VER-01) — accesibilidad parcialmente verificada.** El recorrido por teclado y las
   etiquetas asociadas sí tienen test (`Formulario_RecorridoSoloConTeclado_PermiteGuardar`,
   `Formulario_CadaControlTieneEtiquetaAsociada`), pero "foco visible" y el contraste 4.5:1 no. No
   son automatizables con jsdom.
4. **Claves de error cruzadas — el código mapea las dos, el test cubre una.** El mapeo existe y es
   correcto (`FormularioMovimiento.tsx:344-353`, une los dos mensajes cuando llegan juntos), pero
   `Formulario_TipoCruzado_MuestraElMotivoJuntoAlSelector` (`:307`) inyecta solo
   `errors.tipoEsperado`. Sin cubrir: el caso real de AC-10 desde el servidor (`errors.categoriaId`),
   el caso combinado, y la rama `if (tipoEsperado === undefined) return resto;` — que es exactamente
   la línea 347 que v8 marca sin cubrir. No es FAIL porque AC-10 tiene verificación autoritativa en
   backend en las dos direcciones con mensaje exacto, y el PRD declara que AC-08..AC-13 se verifican
   contra la API y no contra el formulario. Tres líneas de test lo cierran.
5. **`*_FalloDeBase_*` verifican el handler global, no la propagación desde el handler concreto.**
   El middleware de `Program.cs:29-35` resuelve el usuario actual en toda request y toca la base, así
   que con la base caída corta antes de llegar a `ListarAsync`/`CrearAsync`. Estos tests
   sobrevivirían a un mutante que envolviera el handler concreto en un `try/catch`. No es FAIL porque
   el contrato que la spec escribe es "se propaga al handler global → 500 ProblemDetails con
   traceId, sin stack trace", que es lo que se verifica; los tests correlacionan la causa
   (`OfType<MySqlException>()`), así que no dan verde con cualquier 500; y
   `ExcepcionNoControlada_DevuelveProblemDetailsSinStackTrace` cubre el handler global con un
   endpoint sintético. Ningún AC depende de esto.
6. **`total` e `items` se leen en dos consultas sin transacción** (`MovimientosEndpoints.cs:122` y
   `:141`). Cada consulta corre en autocommit con su propio snapshot de InnoDB, así que con escritura
   concurrente `recortado` puede quedar inconsistente con los items devueltos. WARN y no FAIL: ningún
   AC ni compromiso de la spec se incumple, la concurrencia es imposible con un único usuario
   semilla, y el arreglo pertenece al ticket de autenticación, donde el escenario deja de ser
   hipotético.
7. **W-VER-03 — ids de semilla hardcodeados** (`CategoriaComidaId = 1`, `CategoriaSueldoId = 8`). El
   ejemplo literal de la regla, aunque están fijados por `HasData` en la migración y por la spec.
   `prepararFetch` de `ListadoMovimientos.test.tsx` depende del orden de llamadas, por diseño del
   caso. Nada produce flakiness hoy. A favor: `[Collection]` serializa los tests de base, cada test
   arranca con `LimpiarAsync()` explícito, los tests de fecha usan `vi.useFakeTimers()`,
   `prepararFetch` del formulario enruta por endpoint, y `ConfiguracionTests` usa `UseSetting` en vez
   de mutar la variable de entorno del proceso.
8. **F-VER-04 (⚠️ menor) — `formatearFecha` / `formatearMonto` solo tienen camino feliz.** Entrada
   tipada por el DTO y sin ramas.

### Evidencia TDD — no verificable con los insumos entregados

No se le entregaron al verificador los reportes del `daw-implementer` de los cinco bloques, así que
no pudo confirmar cuántos tests estaban en rojo antes de cada implementación ni qué aserción rompía.
Lo que sí observó, y es evidencia más fuerte que un "rojo primero" autodeclarado, son las rondas de
mutación registradas por bloque (Bloque 2: 7/7 mutantes muertos; Bloque 3: 14 mutantes, 12 muertos en
la ronda 1, 5/5 en la ronda 2) y su huella en el código: comentarios que explican qué mutante mata
cada aserción (`ListarMovimientosTests.cs:78-81`, `:105-118`; `RendimientoAltaTests.cs:32-37`;
`CrearMovimientoTests.cs:617-621`). Sigue siendo autorreporte. No cambia el veredicto.

---

### Valoración general de la ronda 1

Fuera del bloqueo, la calidad es alta y conviene dejarlo asentado: los 79 tests que la spec nombra
existen y pasan, **ningún test es de status-code-solo** (todos afirman cuerpo más fila persistida),
las tres métricas de cobertura superan el mínimo en ambos lados, la lógica de negocio está por encima
del rango de W-VER-02 (`ValidadorMovimiento` y `MovimientosEndpoints` al 100% de líneas y ramas), y
hay evidencia explícita de resistencia a mutación justo en los puntos donde el motor podía dar verde
por casualidad.

**Acción:** bucle correctivo a CODE. Los gates `tests` y `sast` se limpian y hay que reganarlos.

Alcance aprobado por el usuario para la vuelta a CODE:

1. **El test que desbloquea** — `frontend/src/App.test.tsx`: renderizar `<App />` con `fetch`
   mockeado y afirmar que tras enviar el formulario la fila nueva aparece en la tabla (cierra el FAIL
   de AC-05/AC-06 y F-VER-06).
2. **Borrar `ResultadoValidacion.Errores`** — WARNING 1, código muerto.
3. **Los dos tests de claves cruzadas** — el caso real de AC-10 desde el servidor
   (`errors.categoriaId`) y el combinado con las dos claves, que cierran la rama sin cubrir de
   `FormularioMovimiento.tsx:347` — WARNING 4.

Los WARNINGs 2, 3, 5, 6, 7 y 8 quedan registrados como deuda aceptada, sin acción en este ticket.

---

## Ronda 2 — 2026-08-17 — **PASSED**

Verificación cruzada tras el bucle correctivo (commit `7583be5`). Ejecutada por `daw-module-verifier`
sobre código que no escribió.

### Estado medido

| Medición | Resultado |
|----------|-----------|
| Suite backend (xUnit) | 87/87 verde |
| Suite frontend (Vitest) | 42/42 verde, 6 archivos |
| Cobertura backend (`Migrations/` excluido) | líneas **97,64%** (415/425) · ramas **93,58%** (73/78) · métodos **97,30%** (36/37) |
| Cobertura frontend | líneas **92,89%** (170/183) · ramas **86,41%** (70/81) · funciones **93,75%** (45/48) |
| `frontend/src/App.tsx` | **100%** — 3/3 líneas, 3/3 funciones (era 0%) |
| `pnpm lint` · `pnpm format` · `pnpm build` | limpios |
| `dotnet build -warnaserror` · `dotnet format --verify-no-changes` | 0 warnings · limpio |

### Resultado por regla

| Regla | Ronda 1 | Ronda 2 |
|-------|---------|---------|
| **F-VER-01** | ❌ AC-05, AC-06 | ✅ **cerrados**; 18 ✅, 1 ⚠️ (AC-18) |
| **F-VER-02** | ✅ | ✅ 5/5 bloques, más 18 archivos fuera de lista auditados uno por uno |
| **F-VER-03** | ✅ | ✅ las tres métricas sobre 80% en ambos lados |
| **F-VER-04** | ✅ | ✅ con una corrección a la ronda 1 (ver N2) |
| **F-VER-05** | ✅ | ✅ |
| **F-VER-06** | ❌ | ✅ los **79** nombres de test de la spec verificados uno por uno: 79/79 en disco y verdes |
| **W-VER-01** | ⚠️ 2 | ⚠️ 4 (2 heredados, 2 nuevos) |
| **W-VER-02** | ✅ | ✅ por encima del rango |
| **W-VER-03** | ⚠️ | ⚠️ sin flakiness activa |

```
FAILs: 0 · WARNs: 11 (6 heredados + 5 nuevos) · PASSes: 41
Veredicto: PASSED
```

### Los dos FAIL cerrados, con evidencia de mutación reproducida

El mutante que sobrevivía en la ronda 1 —`<FormularioMovimiento onCreado={…} />` →
`<FormularioMovimiento />` en `App.tsx:13`— ahora **muere dos veces**:

```
Tests  2 failed | 40 passed (42)
FAIL App_TrasUnAltaDeGasto_…   → Unable to find an element with the text: 17/08/2026  (App.test.tsx:95)
FAIL App_TrasUnAltaDeIngreso_… → Unable to find an element with the text: 16/08/2026  (App.test.tsx:123)
```

El verificador aplicó el mutante él mismo, no aceptó el autorreporte. En total produjo **4 mutantes
muertos y 1 sobreviviente localizado** (ver N2). Esa es la garantía que el rojo-primero pretende
demostrar, verificada directamente: para estos cuatro cambios el TDD era imposible por construcción
—VERIFY encontró el hueco, CODE lo tapó—, así que la mutación es la evidencia equivalente y es más
fuerte.

### Corrección: qué cubre y qué NO cubre el test de ingreso

Se sospechó que el test de ingreso podía dar verde sin distinguir el tipo. **La sospecha era
correcta.** El mutante `tipoEsperado: tipo` → `tipoEsperado: 'gasto'`
(`FormularioMovimiento.tsx:152`, el payload miente el tipo) deja `App_TrasUnAltaDeIngreso_…` en
**verde**: el `fetch` falso devuelve `INGRESO_CREADO` hardcodeado sin mirar el cuerpo del POST, así
que la fila de la tabla es el eco del doble, no una consecuencia de lo que la app mandó. Lo mata
únicamente `Formulario_IngresoValido_EnviaYLimpia:173`
(`expect(cuerpo.tipoEsperado).toBe('ingreso')`).

Lo que el test de ingreso **sí** aporta, y nadie más aporta: que la costura alta→refresco funciona
también desde el camino de ingreso (radio → lista filtrada por tipo → alta → recarga → fila). Lo
prueba el mutante de `cambiarTipo` sin `setTipo(nuevo)`, que sí lo mata — y de paso refuta la otra
sospecha, la del orden de los pasos: si el tipo no cambiara, la opción `8` no existiría en el
`<select>` y `selectOptions` **lanza**, no manda vacío (`Value "8" not found in options`).

**Por lo tanto AC-06 queda cerrado por la conjunción de tres tests, no por `App.test.tsx` solo.** No
es FAIL: la mitad que estaba huérfana en la ronda 1 era *"…y mostrarlo en el listado"*, y esa es
exactamente la que este test cubre. Queda escrito acá porque el nombre del test promete más de lo que
entrega, y ese fue precisamente el defecto que costó la ronda 1.

### La aserción sincrónica: verificada en las dos direcciones

Se cambió `await waitFor(() => expect(lecturasDelListado()).toBe(2))` por la forma sincrónica. Las
dos objeciones posibles quedaron resueltas:

- **El `waitFor` no aportaba espera, aportaba la ilusión de espera.** Resuelve en el primer poll que
  no lanza; a esa altura el contador ya vale 2, así que nunca observaría una tercera lectura.
- **La forma sincrónica no abre la carrera inversa.** `lecturas += 1` ocurre al recibir el request, y
  la línea anterior es un `await findByText` de la fila nueva, que solo puede estar en el DOM si la
  segunda respuesta ya se renderizó. Cuando la aserción corre, el contador es necesariamente ≥ 2.
- **Y atrapa lo que dice atrapar.** Mutante `useCallback(cargar, [])` → `[items]`, que dispara refetch
  en bucle: `AssertionError: expected 23 to be 2`. La cota superior es lo único que ve el bucle,
  porque la fila aparece igual.

### La extracción de `json`: sin deriva de defaults

Auditadas **las 22 llamadas** de los tres archivos. El riesgo era que alguna quedara apoyada en el
default nuevo habiendo tenido `estado` obligatorio antes: en `FormularioMovimiento.test.tsx`, la
variante con `estado` obligatorio, **las 8 llamadas lo pasan explícito**. Ninguna cambió de
comportamiento.

### Trazabilidad de archivos

En la spec y no en disco: **cero**. En disco y no en la spec: **18 archivos, y los 18 son test,
test-infra o configuración de tooling — cero código de producción se colonizó ahí.** Leídos uno por
uno. Son omisiones de la lista de archivos, no fugas de responsabilidad; van a la errata (ítems 7 a
9).

### WARNINGs nuevos de la ronda 2

- **N1 · W-VER-01 — `senal?: AbortSignal` es capacidad muerta.** `obtenerCategorias`
  (`cliente.ts:49`) y `obtenerMovimientos` (`:53`) aceptan un `AbortSignal` que **ningún llamador
  pasa jamás**, ni en producción ni en tests; `grep -rn "AbortController" src/` vuelve vacío. Es la
  causa directa de 4 de las 11 ramas frontend sin cubrir. Borrar el parámetro.
- **N2 · `leerProblema` tiene un mutante sobreviviente, demostrado.** `cliente.ts:113-117` maneja el
  caso del 502-de-proxy-con-HTML y es la única línea sin cubrir de `cliente.ts`. Borrando el
  `try/catch` completo, la suite queda **42/42 verde**: ningún test envía un cuerpo de error no-JSON.
  No es FAIL —ningún AC ni test comprometido por la spec lo cubre— pero **contradice lo que la ronda 1
  acreditó bajo F-VER-04**, donde se dio por existente un caso "cuerpo no-JSON" del cliente HTTP que
  no existe. Son 3 líneas de test.
- **N3 · La extracción de `json` quedó a mitad de camino.** `cliente.test.ts:19` conserva una
  **cuarta** copia del helper con otro nombre (`respuesta`), idéntica salvo el `estado` obligatorio.
- **N4 · `src/test/infra.ts` está dentro del `include` de cobertura.** `vite.config.ts` excluye
  `main.tsx` y `*.test.*`, no `src/test/`: infra de test contada en el denominador. Impacto medido:
  excluyéndola, 92,86% líneas · 86,08% ramas · 93,62% funciones. Las tres siguen sobre 80% y el
  veredicto no cambia.
- **N5 · `TipoMovimientoTexto` al 87,5% de ramas** (`MovimientoDto.cs:36`): el
  `throw new ArgumentOutOfRangeException` del `switch` sobre el enum, inalcanzable con un valor
  válido. Informativo, misma categoría que el WARNING 2.

### Estado de los WARNINGs de la ronda 1

**Corrección de aritmética:** la ronda 1 dejó 8 WARNINGs y se accionaron **dos** (1 y 4) — AC-06 no
era un WARNING, era la mitad de un FAIL. Quedan **6**, no 5.

| # ronda 1 | Ronda 2 |
|-----------|---------|
| 1 · `ResultadoValidacion.Errores` | **RESUELTO**, verificado por lectura y `grep` |
| 4 · claves de error cruzadas | **RESUELTO**; la rama `:347` se ejerce y el combinado es el único que mata la pérdida de la unión |
| 2 · `MontoJsonConverter.Write` | ⚠️ sigue — el **único** método backend sin cubrir |
| 3 · AC-18 accesibilidad parcial | ⚠️ sigue |
| 5 · `*_FalloDeBase_*` verifican el handler global | ⚠️ sigue |
| 6 · `total`/`items` sin transacción | ⚠️ sigue |
| 7 · ids de semilla hardcodeados | ⚠️ sigue; `App.test.tsx` suma 1/8 pero enruta por endpoint, no empeora |
| 8 · `formatearFecha`/`formatearMonto` solo camino feliz | ⚠️ sigue |

Ninguno se degradó a FAIL. Ninguno incumple un AC, un test comprometido por la spec ni un umbral de
cobertura.

### Estado del árbol

El verificador aplicó y revirtió 5 mutantes. `git diff HEAD` vacío; `git status --porcelain` devuelve
solo los dos archivos sin trackear que están fuera del ticket a propósito. No escribió una línea de
código de producción.

**Gate:** `gates.verify` = `true`. El ticket puede pasar a RELEASE.

---

## Errata de la spec — pendiente de aplicar en el PLAN de FEAT-001b

**Por qué no se corrige en este ticket.** El grafo de transiciones
(`.daw/rules/transition-graph.json`, tier FEATURE) tiene exactamente dos aristas hacia atrás:
`PLAN->DEFINE` y `VERIFY->CODE`. No existe `CODE->PLAN` ni `VERIFY->PLAN`, y la spec solo se puede
modificar en PLAN: desde CODE o VERIFY es inalcanzable, y el hook `validate-state-transition.sh`
rechaza el intento. Pausar tampoco sirve — un ticket reanuda la fase desde la que se pausó. La única
forma de volver a PLAN con este ticket sería abandonarlo y reclasificar, tirando los gates de cinco
bloques, dos rondas de revisión y dos SAST para corregir unos números y un campo de documentación.

**Decisión del usuario (2026-08-17):** este reporte es el registro autoritativo —**donde la spec y el
código difieran, gana el código**— y las ediciones se aplican en el **PLAN de FEAT-001b**, que es
cuando se vuelve a estar legítimamente en esa fase. No es una postergación por comodidad: FEAT-001b
consume el contrato del POST, así que el ítem 6 es el que muerde primero.

| # | Ubicación | Edición |
|---|-----------|---------|
| 1 | `spec:187` | `los 12 tests de arriba pasan` → **14** |
| 2 | `spec:298` | `Los 25 tests pasan` → `Los **26** tests listados pasan` |
| 3 | `spec:363` | `Los 8 tests pasan` → **10** |
| 4 | `spec:299-300` | `los ocho casos de rechazo devuelven 400` → `los casos de rechazo listados devuelven 400`. Son 10 nombrados y 12 tras el cierre: un número fijo vuelve a envejecer mal |
| 5 | `spec:99-102` | Borrar `con un valor por defecto apuntando a gestiongastos_test en localhost` y escribir la regla de **ADR-002**: sin la variable `ConnectionStrings__Default`, el gate falla nombrándola. Un default silencioso convierte un error de configuración en un error de autenticación contra una base ajena. La implementación ya sigue al ADR |
| 6 | `spec:232` | Agregar al contrato del `POST /api/movimientos`: `tipoEsperado?: "gasto" \| "ingreso"` — **opcional**, se compara contra el tipo de la categoría y **no se persiste**. Hoy el "API contract" lo omite mientras el "Error handling" (`spec:261`) lo exige, y la implementación sigue al segundo |
| 7 | `spec:454-462` | La lista de archivos del Bloque 5 omite `frontend/src/App.test.tsx` y `frontend/src/test/infra.ts` — los dos archivos que cerraron el FAIL de ese mismo bloque |
| 8 | `spec:305-311` | La lista del Bloque 3 omite `backend/GestionGastos.Api.Tests/Infra/ObservadorDeSql.cs`, la pieza que sostiene la doble capa contra la trampa del índice |
| 9 | `spec:83-85` | La lista del Bloque 1 omite 5 archivos de `Tests/Infra/` y `Tests/Datos/` que contienen tests que la spec **sí nombra**, más `AssemblyInfo.cs` y `coverlet.runsettings`; la del Bloque 4 omite `frontend/.prettierignore` |
| 10 | `spec:495` | `Listado_TrasUnAlta_MuestraElMovimientoNuevo` está descrito como *"valida AC-05 y AC-06 de punta a punta"* y no lo hace: simula la costura con `rerender()`. **Es el defecto que costó la ronda 1** — la descripción de la spec registraba una garantía que el test no daba. Corregirla a lo que el test verifica y atribuir AC-05/AC-06 a `App.test.tsx` |

**Prioridad si hay que recortar:** los ítems 1 a 4 y 9 son aritmética y listas, y no afectan a nadie
salvo a quien lea la spec buscando un número. El **5** contradice un ADR, el **6** deja un campo del
contrato sin documentar en el artefacto que FEAT-001b va a leer, y el **10** es el que ya costó una
ronda de verificación. Esos tres primero.

## Candidatos para el arranque de FEAT-001b

Tres tests y un borrado de parámetro, todos localizados con `archivo:línea` arriba: **N2** (el mutante
sobreviviente de `leerProblema`), **N1** (borrar el `AbortSignal` muerto) y **N3** (la cuarta copia
del helper `json`).
