# Verificación — FEAT-001b

- **Ticket:** FEAT-001b — Filtros del listado, edición y eliminación de movimientos
- **Tier:** FEATURE · **Fase:** VERIFY · **Gate bloqueante**
- **Fecha:** 2026-08-19 · **Ronda:** 1 (sin bucles correctivos)
- **PRD:** `docs/daw/prd/prd-FEAT-001b.md` (14 AC, 6 FR, 2 NFR)
- **Spec:** `docs/daw/specs/spec-FEAT-001b.md` (6 bloques, 57 tests listados)
- **Threat model:** `docs/daw/security/threat-FEAT-001b.md` (10 mitigaciones en §7)
- **Reglas aplicadas:** `.daw/rules/validation/verify.md` con `.daw/rules/validation/common.md`
  (F-VER-01 … F-VER-06, W-VER-01 … W-VER-03)
- **Cross-verificación:** `daw-module-verifier`, sobre código que no escribió.

---

## F-VER-01 — Cada AC del PRD con su test verde

Los 14 AC tienen implementación localizada y un test que verifica **el comportamiento**, no solo el
status code.

| AC | Implementación | Test |
|---|---|---|
| AC-01 | `MovimientosEndpoints.cs:ModificarAsync` | `Modificar_ElMonto_LoRefleja` — comprueba el monto persistido **y** su reflejo en el listado |
| AC-02 | `ModificarAsync` | `Modificar_CategoriaYFecha_PersisteAmbas` + `Editar_CambiandoLaFechaFueraDelRango_QuitaLaFilaDelListado` |
| AC-03 | `ModificarAsync` | `Modificar_LaNota_LaRefleja` + `Modificar_BorrandoLaNota_GuardaNull` |
| AC-04 | `ValidadorMovimiento.Validar` (compartido POST/PUT) | 5 tests sad-path con `AssertIntactoAsync`, que verifican que el registro **no** cambió |
| AC-05 | `EliminarAsync` | `Eliminar_UnMovimientoPropio_Devuelve204` + `..._DejaDeAparecerEnElListado` — las dos mitades |
| AC-06 | `ModificarAsync` / `EliminarAsync` (404 con `TituloNoEncontrado`) | `Modificar_Inexistente/DeOtroPropietario_Devuelve404`, ídem `Eliminar_*`; los de "otro propietario" comprueban además que la fila ajena sobrevive intacta |
| AC-07 | `FiltrosDeListado` + `ListarAsync` | `Filtrar_PorCategoria_DevuelveSoloEsaCategoria` + `Filtros_AlElegirCategoria_LaPasaAlCliente` |
| AC-08 | `ListarAsync` | `Filtrar_SinCategoria_DevuelveTodasLasCategorias` |
| AC-09 | `mesActual.ts` + `App.tsx` | `Filtros_AlAbrir_PideElMesActual`, con temporizadores falsos |
| AC-10 | `FiltrosDeListado.ValidarFecha` + `ListarAsync` | `Filtrar_PorRango_IncluyeAmbosExtremos`, que siembra vecinos exactos de cada extremo |
| AC-11 | `ListarAsync` | `Filtrar_PorCategoriaYRango_AplicaLasDosCondiciones` |
| AC-12 | `FiltrosDeListado.Parsear` | `Filtrar_ConDesdePosteriorAHasta_Devuelve400` + `Filtros_ConRangoInvalido_MantieneElListadoAnterior` |
| AC-13 | `ListarAsync` + techo de items | `Listado_ConMilMovimientos_RespondeBajoUnSegundo` — p95 real sobre 100 ejecuciones y 1000 movimientos |
| AC-14 | `ListarAsync` | `Filtrar_PorRango_ExcluyeLoDeAfuera` + `Filtrar_EmiteElWhereEnSql`, capa estructural con `ObservadorDeSql` que evita el falso positivo del filtrado en memoria |

**Resultado: ✅ PASS** — 14/14, ninguno verificado solo por status code.

## F-VER-02 y F-VER-06 — Los bloques y los tests que la spec comprometió

| Bloque | Tests requeridos | En disco y verdes |
|---|---|---|
| Block 1 — Backend: filtros | 13 | ✅ 13/13 |
| Block 2 — Backend: `PUT` | 13 | ✅ 13/13 |
| Block 3 — Backend: `DELETE` | 5 | ✅ 5/5 |
| Block 4 — Cliente HTTP | 8 | ✅ 8/8 |
| Block 5 — Controles de filtro | 10 | ✅ 10/10 |
| Block 6 — Editar y eliminar | 8 | ✅ 8/8 |

Los 57 tests listados por nombre en la spec se verificaron **uno por uno** contra el código en
disco. **Resultado: ✅ PASS** — 57/57 presentes, ninguno renombrado en silencio, ninguno faltante.

## F-VER-03 — Cobertura sobre el código nuevo/modificado

**Backend** (coverlet, `coverage.cobertura.xml`):

| Clase tocada | Líneas | Ramas |
|---|---|---|
| `MovimientosEndpoints` (los 5 handlers) | 100% | 100% |
| `FiltrosDeListado` | 100% | 100% |
| `ValidadorMovimiento` | 100% | 100% |
| Global del proyecto | 98.13% | 96.09% |

**Frontend** (v8), agregado sobre los 8 archivos que el ticket tocó:

| Métrica | Valor | Mínimo |
|---|---|---|
| Líneas | **92.19%** (295/320) | 80% |
| Ramas | **86.71%** (150/173) | 80% |
| Funciones | **94.12%** (80/85) | 80% |

**Resultado: ✅ PASS** — las tres métricas por encima del mínimo en los dos lados.

## F-VER-04 — Camino triste por endpoint y por función de entrada

| Superficie | Sad paths |
|---|---|
| `GET /api/movimientos` (filtros) | 3 — rango invertido, fecha mal formada, categoría inexistente |
| `PUT /api/movimientos/{id}` | 7 — monto, categoría, nota, inexistente, ajeno, overposting, monto no numérico |
| `DELETE /api/movimientos/{id}` | 3 — doble delete, inexistente, ajeno |
| `cliente.ts` | 404, 500, red caída, 400 |
| `FiltrosMovimientos.tsx` | rango inválido, red caída, rechazo del servidor |
| `FormularioMovimiento.tsx` (edición) | validación, 404, red caída |

**Resultado: ✅ PASS** — sin excepciones. FR-01 a FR-06 tienen camino triste probado.

## F-VER-05 — Lint y type checker

| Herramienta | Resultado |
|---|---|
| `tsc --noEmit` | ✅ sin errores |
| `eslint .` | ✅ sin errores ni warnings |
| `prettier --check .` | ✅ todo con el estilo del repo |
| `dotnet build` | ✅ 0 Warning(s), 0 Error(s) — también con `-warnaserror` |

**Resultado: ✅ PASS**

## Evidencia TDD

Los 6 bloques documentan corridas en rojo **antes** de implementar, con la aserción que rompía en
cada caso (`tdd-FEAT-001b.md`). Block 1: 10 en rojo / 3 en verde inicial, con los 3 verdes
endurecidos explícitamente porque no mordían. Blocks 2 y 3: 100% rojo inicial (405
`MethodNotAllowed`). Blocks 4–6: rojo por import inexistente más varios tests que pasaban de
entrada, identificados y endurecidos antes de implementar. Cada bloque agrega además una tabla de
mutaciones que matan los tests nuevos — evidencia más fuerte que el mínimo exigido.

**Resultado: ✅ PASS** — los nombres citados existen en disco y la suite corre verde.

## Threat model — las 10 mitigaciones de §7

| # | Mitigación | Verificación |
|---|---|---|
| 1-2 | R-13 / R-14 — filtro en el `IQueryable`, conteo sobre el universo filtrado | ✅ código + `Filtrar_EmiteElWhereEnSql` |
| 3-4 | R-15 — sin `id`/`usuarioId`/`tipo`/`moneda`/`creadoEn` en el request; lectura previa sobre `DbSet` | ✅ `ModificarMovimientoRequest.cs` + `Modificar_ConCuerpoQueTraeUsuarioId_LoIgnora`; la mutación `IgnoreQueryFilters()` la atrapa `Eliminar_DeOtroPropietario_Devuelve404YNoLoBorra` |
| 5 | R-16 — `ValidadorMovimiento` con una sola implementación | ✅ confirmado leyendo el código |
| 6 | R-17 — 404 indistinguible entre inexistente y ajeno | ✅ AC-06 |
| 7 | R-19 — confirmación explícita antes de eliminar | ✅ `ConfirmarEliminacion.tsx` + `Eliminar_PideConfirmacionAntesDeLlamar` / `..._AlCancelar_NoLlamaAlServidor` |
| 8 | R-20 — `TryParseExact` + `InvariantCulture` + rango | ✅ código |
| 9 | R-21 — sin `exception.Message` en la respuesta | ✅ código |
| 10 | R-23 — prohibición de `dangerouslySetInnerHTML` extendida a la edición | ✅ regla ESLint `no-restricted-syntax` activa para todo el proyecto |

**Resultado: ✅ PASS** — 10/10 incorporadas y fijadas por test o por regla de lint.

## "Final verification" de la spec, punto por punto

| Punto | Resultado |
|---|---|
| Los 6 FR y 2 NFR tienen cobertura; los 14 AC tienen al menos un test que los nombra | ✅ |
| La suite completa pasa | ✅ 217/217 — pero la aritmética escrita es falsa, ver W-VER-04 |
| Ningún test existente quedó desactualizado en silencio | ✅ `Listar_ConParametrosDesconocidos_LosIgnora` reescrito según lo previsto; los tests de frontend contemplan la query string |
| El SQL del listado filtrado contiene el `WHERE`, verificado con `ObservadorDeSql` | ✅ `Filtrar_EmiteElWhereEnSql` |
| `ValidadorMovimiento` con una sola implementación de cada regla | ✅ |
| Las diez mitigaciones de §7 incorporadas | ✅ |
| Lint y format limpios; `dotnet build` sin warnings nuevos | ✅ |

---

## WARNINGs registrados (no bloquean)

### W-VER-03 — Tests sensibles al entorno de ejecución

`RendimientoListadoTests` y `RendimientoAltaTests` miden tiempo real (p95 < 1s). En una CI cargada
podrían dar rojo por motivos ajenos al código. **Es un patrón heredado de FEAT-001a**, no algo que
este ticket introduzca. Se registra para que, cuando aparezca el primer falso rojo, ya esté escrito
de dónde viene.

Verificado en cambio que los tests de fecha **no** son frágiles: `mesActual()` recibe la `Date` como
parámetro inyectable, y el default que lee el reloj lo cubre un test con `vi.useFakeTimers`. Ninguno
depende del día real de ejecución.

### W-VER-04 — La aritmética de la spec es falsa (errata, no gap)

La sección "Final verification" dice "los 129 tests que FEAT-001a dejó verdes, más los 57 de este
ticket" = **186**. La suite real es **217** (124 backend + 93 frontend). El delta real del ticket es
88 tests, no 57.

Se investigó si eso escondía un conteo doble o un test faltante: **no**. `tdd-FEAT-001b.md` documenta
bloque por bloque cada test extra —deuda heredada saldada, casos endurecidos, `Theory` con varios
`InlineData` que la spec contó como una sola entrada, archivos de test reescritos— y cada uno tiene
su fila con su motivo. **Es cobertura de más, no un gap.** No es FAIL porque no hay ningún AC,
requisito ni test comprometido sin cobertura; es una imprecisión de redacción de la spec, que solo se
puede corregir en PLAN. Va a la tabla de erratas pendientes para el PLAN de FEAT-001c, junto con la
firma de `obtenerMovimientos`.

### W-VER-02 (parcial) — Dos archivos por debajo del 80% en ramas, mirados de a uno

El agregado sobre el código nuevo/modificado pasa (86.71% de ramas), que es lo que F-VER-03 mide.
Pero archivo por archivo, dos quedan cortos en esa métrica:

| Archivo | Ramas |
|---|---|
| `frontend/src/movimientos/FiltrosMovimientos.tsx` | 73.33% |
| `frontend/src/movimientos/ConfirmarEliminacion.tsx` | 75% |

No bloquea, y queda anotado acá para que en FEAT-001c se decida si se sube o se acepta.

### W-VER-01 — Código muerto

Sin hallazgos. `eslint` y `tsc` con las reglas que detectan variables e imports sin usar pasan
limpio.

---

```
┌─────────────────────────────────────────────────────────┐
│  /daw-verify-module [FEAT-001b] — PASSED                 │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Criterios de aceptación:                                │
│    ✅ F-VER-01: 14/14 AC con test de comportamiento      │
│                                                          │
│  Tareas de la spec:                                      │
│    ✅ F-VER-02: 6/6 bloques implementados                │
│    ✅ F-VER-06: 57/57 tests comprometidos, en verde      │
│                                                          │
│  Cobertura:                                              │
│    ✅ F-VER-03 backend: 100% líneas y ramas en lo tocado │
│    ✅ F-VER-03 frontend: 92.19% L · 86.71% B · 94.12% F  │
│    ✅ F-VER-04: camino triste en toda entrada            │
│                                                          │
│  Calidad:                                                │
│    ✅ F-VER-05: lint, tsc, prettier y build limpios      │
│    ✅ W-VER-01: sin código muerto                        │
│    ⚠️ W-VER-02: 2 archivos < 80% de ramas de a uno       │
│    ⚠️ W-VER-03: 2 tests de rendimiento sensibles al      │
│       entorno (heredados de FEAT-001a)                   │
│    ⚠️ W-VER-04: errata de aritmética en la spec (186     │
│       escritos vs 217 reales) — cobertura de más         │
│                                                          │
│  ─────────────────────────────────────────────────────   │
│  Total: 24 passed, 0 failed, 3 warnings                  │
│  Result: PASSED                                          │
│  Next: gates.verify = true, avanzar a RELEASE            │
└─────────────────────────────────────────────────────────┘
```
