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
