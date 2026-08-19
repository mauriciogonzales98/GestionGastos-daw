# Verificación — FEAT-001c

- **Ticket:** FEAT-001c — Resumen del mes con desglose por categoría
- **Tier:** FEATURE · **Fase:** VERIFY · **Gate bloqueante**
- **Fecha:** 2026-08-19 · **Ronda:** 1 (sin bucles correctivos)
- **PRD:** `docs/daw/prd/prd-FEAT-001c.md` (12 AC)
- **Spec:** `docs/daw/specs/spec-FEAT-001c.md` (4 bloques, 30 tests listados)
- **Threat model:** `docs/daw/security/threat-FEAT-001c.md` (R-24 … R-30)
- **Reglas aplicadas:** `.daw/rules/validation/verify.md` con `.daw/rules/validation/common.md`
  (F-VER-01 … F-VER-06, W-VER-01 … W-VER-03)
- **Alcance:** `git diff main...HEAD`, 23 archivos, 5 commits (`52bfecb`, `ecdb6f5`, `6b0e07e`,
  `6ec248a`, `3bd4631`)
- **Cross-verificación:** `daw-module-verifier`, sobre código que no escribió.

---

## F-VER-01 — Cada AC del PRD con su test verde

Los 12 AC tienen implementación localizada y al menos un test que verifica **el comportamiento** —
valores concretos—, no solo el status code.

| AC | Implementación | Test |
|---|---|---|
| AC-01 | `ResumenEndpoints.cs:ObtenerAsync` (agregación por tipo) | `Resumen_ConIngresosYGastos_DevuelveLosTresTotales` + `Resumen_MuestraLosTresTotalesYElMes` |
| AC-02 | `ObtenerAsync` (grupo ausente → 0) + `ResumenDelMes.tsx:desglose()` | `Resumen_SinMovimientosEnElMes_DevuelveCerosYDesgloseVacio` + `Resumen_SinGastosEnElMes_DiceQueNoHayNadaQueDesglosar` |
| AC-03 | `ObtenerAsync` (balance = ingresado − gastado) + signo explícito en el texto y clase `.balance-negativo` | `Resumen_ConGastosMayoresQueIngresos_DevuelveBalanceNegativo` + `Resumen_ConBalanceNegativo_LoMuestraConSigno`, que asserta el **texto** `'ARS -150,50'` y no la clase |
| AC-04 | `GroupBy(CategoriaId, Nombre)` restringido a `Tipo == Gasto` | `Resumen_ConVariasCategorias_DevuelveUnTotalPorCategoriaConGastos`, `Resumen_ConCategoriaSinGastosEnElMes_NoLaIncluyeEnElDesglose`, `Resumen_MuestraElDesglosePorCategoria` |
| AC-05 | Las dos agregaciones parten del **mismo** `IQueryable delMes` | `Resumen_LaSumaDelDesglose_EsIgualAlTotalGastado` |
| AC-06 | El efecto se suscribe **solo a `version`**; `App.tsx:73` no le pasa `filtros` | `Resumen_AlFiltrarElListado_NoCambia` — **cuenta peticiones** con `lecturasDelResumen()`, porque comparar importes no distinguiría el bug (ver mutación 1 del Block 4) |
| AC-07 | `Where(m => m.Fecha >= rango.PrimerDia && m.Fecha <= rango.UltimoDia)` | `Resumen_ConMovimientosDeOtrosMeses_LosExcluye` |
| AC-08 | `RangoDelMes.De` (`primerDia.AddMonths(1).AddDays(-1)`) | `Resumen_ConMovimientosElPrimeroYElUltimoDiaDelMes_LosIncluye` + los 3 unitarios de `RangoDelMes` |
| AC-09 | `rango.PrimerDia.Month/.Year` en el DTO; `rotularPeriodo()` en el frontend, alimentado por la respuesta y no por `mesActual.ts` | `Resumen_DevuelveElMesYElAnioDelPeriodo` + `Resumen_MuestraLosTresTotalesYElMes` |
| AC-10 | Sin `IgnoreQueryFilters()`; filtro global de `AppDbContext` | `Resumen_DeOtroPropietario_NoEntraEnLosTotales`, **con mutación registrada** (`IgnoreQueryFilters()` → `10 / 1009.00`) |
| AC-11 | Sin código de producción propio: es medición. Índice `(usuario_id, fecha)` reutilizado | `Pantalla_ConMilMovimientos_ListadoYResumenRespondenBajoDosSegundos` — mide las 2 peticiones HTTP reales, p95 ~15 ms contra un presupuesto de 2000 ms |
| AC-12 | El DTO trae 3 totales + `desglose[]`, nunca la lista de movimientos | `Resumen_NoDevuelveLaListaDeMovimientos` + `Resumen_EmiteLaAgregacionEnSql` (estructural) + `Resumen_ConMilMovimientos_NoMaterializaFilasDeMovimientos` |

✅ **PASS** — ningún AC queda cubierto solo por un status code.

## F-VER-02 / F-VER-06 — Tareas de la spec y tests comprometidos

Los 30 tests que la spec listó como compromiso fueron **recontados uno por uno con `grep` contra el
código**, no tomados de la narrativa del reporte TDD.

| Bloque | Comprometidos | En disco | Verdes | Completion criterion |
|---|---|---|---|---|
| Block 1 — endpoint con agregación en SQL | 16 | ✅ 16/16 con el nombre exacto | ✅ | ✅ mutación de `IgnoreQueryFilters()` registrada |
| Block 2 — rendimiento de la pantalla | 2 | ✅ 2/2 | ✅ | ✅ p95 registrado: 15/16/15 ms en tres corridas |
| Block 3 — cliente HTTP y tipos | 4 | ✅ 4/4 | ✅ | ✅ sin segunda implementación del manejo de errores |
| Block 4 — el resumen en pantalla | 8 | ✅ 5 + 3 que montan `App` | ✅ | ✅ `App.test.tsx` verde en su totalidad |

**30/30.** La sección "Final verification" de la spec también se verificó punto por punto: los FR y
NFR con cobertura, el SQL observado con `ObservadorDeSql`, el rango fijado en servidor sin
parámetros, la suscripción a `version`, el balance negativo distinguido por texto, el doble de
servidor atendiendo `/api/resumen`, y lint/format/build limpios.

## F-VER-03 — Cobertura

**Backend** (`dotnet test`, 142/142; XML de cobertura inspeccionado directamente):

| Archivo | Líneas | Ramas |
|---|---|---|
| `Resumen/RangoDelMes.cs` | 100% | 100% |
| `Resumen/ResumenEndpoints.cs` | 100% | 100% |

**Frontend** (`pnpm run coverage`, 105/105):

| Ámbito | Stmts | Ramas | Funcs | Líneas |
|---|---|---|---|---|
| `src/resumen/ResumenDelMes.tsx` | 100% | 87.5% | 100% | 100% |
| `src/api/cliente.ts` (delta) | 100% | 90.9% | 100% | 100% |

✅ **PASS** — los tres ejes por encima del 80% que exige F-VER-03. Ver **W-VER-02** más abajo por el
87.5% de ramas.

## F-VER-04 — Sad paths

`GET /api/resumen` **no acepta parámetros por diseño** (mitigación R-27, escrita en el PRD, la spec y
el threat model), así que no hay entrada que invalidar: exigir tests de entrada inválida para un
endpoint sin entrada sería exigir un test sin sujeto. Los sad paths que sí corresponden están:

- base caída → `Resumen_ConLaBaseCaida_Devuelve500SinDetalleInterno`
- mes sin movimientos → `Resumen_SinMovimientosEnElMes_DevuelveCerosYDesgloseVacio`
- red caída → `ObtenerResumen_ConRedCaida_LanzaErrorDeRed` + `Resumen_ConRedCaida_MuestraElErrorConReintento`
- 500 sin tumbar el listado → `Resumen_ConErrorDelServidor_NoTumbaElListado`

✅ **PASS** — la ausencia de tests de entrada inválida es correcta acá, no una laguna.

## F-VER-05 — Lint, tipos y build

| Comando | Resultado |
|---|---|
| `pnpm exec tsc --noEmit` | ✅ 0 errores |
| `pnpm exec eslint .` | ✅ 0 errores, 0 warnings |
| `dotnet build` | ✅ 0 warnings, 0 errores (con `TreatWarningsAsErrors`) |

## Evidencia TDD

Los cuatro bloques documentan el rojo **antes** de implementar, con la aserción textual que rompió:
16/16, 2/2, 4/4 y 8/8. Dos casos con matiz, ambos legítimos y declarados como tales:

- **Block 2**, `Resumen_ConMilMovimientos_NoMaterializaFilasDeMovimientos`: daba verde de entrada
  porque mide el endpoint que el Block 1 ya había implementado. Se endureció y se lo puso en rojo por
  mutación. No se reportó "0 failing" y se siguió de largo: se declaró la imposibilidad de un rojo
  natural y se compensó.
- **Block 3**, mutación 3: el reporte declaró 10 errores de `tsc` y eran 17. El error se detectó en
  revisión, se corrigió, y **la corrección quedó anotada en el propio reporte** con la causa (una
  salida truncada con `head -8`) en vez de reescribirse en silencio.

## Trazabilidad del threat model

| Riesgo | Control | Test que lo fija |
|---|---|---|
| R-24 — fuga por agregación (**Alto**) | Sin `IgnoreQueryFilters()`, filtro global de EF | `Resumen_DeOtroPropietario_NoEntraEnLosTotales` + mutación (`10 → 1009.00`) |
| R-25 — agregación evaluada en memoria | `GroupBy`/`Sum` sobre el `IQueryable` | `Resumen_EmiteLaAgregacionEnSql` + mutación estructural + el test a escala |
| R-26 — el 500 filtra internos | `AddProblemDetails` + `UseExceptionHandler` globales | `Resumen_ConLaBaseCaida_Devuelve500SinDetalleInterno` |
| R-27 — parámetro de período agregado en el futuro | Contrato con cero parámetros, motivo escrito | Por diseño; sin superficie que testear |
| R-28 — bordes del corte del mes | `RangoDelMes` aislado, sin casos especiales | 3 tests de borde + el de punta a punta |
| R-29 — el fallo del resumen tumba el listado | Peticiones independientes | `Resumen_ConErrorDelServidor_NoTumbaElListado` |
| R-30 — cableado a `filtros` en vez de `version` | Efecto suscripto solo a `version` | `Resumen_AlFiltrarElListado_NoCambia` + `Resumen_TrasUnAlta_SeActualiza` |

Los dos riesgos que el modelo marcaba como **invisibles** (R-24 y R-25, donde la falla devuelve
números plausibles) se verificaron por **mutación corrida dos veces, por dos agentes distintos**. Es
la única forma de comprobar un control cuya falla no se ve en la respuesta.

---

## WARNINGs registrados (no bloquean)

**W-VER-02 — `ResumenDelMes.tsx` con ramas al 87.5%**, dentro de la banda 80-90% que
`.daw/rules/testing.instructions.md` señala como mejorable para lógica de negocio. Las dos ramas sin
cubrir son el `||` de un estado combinado que los tests no alcanzan, y el fallback
`NOMBRES_DE_MES[mes - 1] ?? String(mes)` para un mes fuera de 1..12 que el backend no puede emitir —
defensa contra lo imposible, no comportamiento sin probar.

**W-VER-03 — fecha fija en un fixture de rendimiento compartido, vence el 2027-01-01.**
`MedicionDeRendimiento.FechasSembradas` siembra con `new DateOnly(2026, 1, 1).AddDays(i % 365)`
mientras `RendimientoResumenTests` mide "el mes en curso" contra el reloj real. **A partir de enero de
2027 los dos tests del Block 2 van a fallar** en `ConfirmarQueElMesTieneFilas`, no por una regresión
sino por el desfase de calendario. Es un artefacto del arnés de pruebas, no del código de producción,
el mensaje de falla es autoexplicativo, y el fixture lo comparten otros dos tests de rendimiento que
la spec de este ticket marca como "no cambia". **Aceptable para cerrar FEAT-001c; se recomienda un
ticket de mantenimiento antes de esa fecha.**

## Puntos evaluados que NO son hallazgos

- **La moneda `'ARS'` fija en el componente.** El PRD saca "totales separados por moneda" del
  alcance y ningún AC exige mostrarla. Sin impacto.
- **El desfase de fin de mes entre el rótulo (reloj del servidor) y el filtro por defecto (reloj del
  navegador).** Se fue al PRD a comprobarlo: el riesgo que el PRD sí registra es otro —el corte del
  mes desplazándose por zona horaria, cubierto por AC-08—. Éste no está en ningún AC, pero tampoco es
  observable en el despliegue actual, donde servidor y cliente corren sobre `127.0.0.1` (RA-02 del
  threat model). Decisión revisable para un despliegue distribuido, no laguna de este ticket.
- **W-VER-01, código muerto:** sin hallazgos. El diff de `App.tsx` es un import y una línea de JSX;
  no quedaron restos de los stubs usados para forzar el rojo.

---

```
┌─────────────────────────────────────────────────────────┐
│  /daw-verify-module FEAT-001c — PASSED                   │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Criterios de aceptación:                                │
│    ✅ F-VER-01: 12/12 AC con código y test de conducta   │
│                                                          │
│  Tareas de la spec:                                      │
│    ✅ F-VER-02 / F-VER-06: 30/30 tests comprometidos      │
│       existen con el nombre exacto y pasan               │
│                                                          │
│  Cobertura:                                              │
│    ✅ F-VER-03: backend 100/100 · frontend 100/87.5/100  │
│    ✅ F-VER-04: sad paths cubiertos; sin entrada que     │
│       invalidar, por diseño (R-27)                       │
│                                                          │
│  Calidad:                                                │
│    ✅ F-VER-05: tsc, eslint y dotnet build limpios       │
│    ✅ W-VER-01: sin código muerto                        │
│    ⚠️ W-VER-02: ramas al 87.5% en ResumenDelMes.tsx      │
│    ⚠️ W-VER-03: fixture de rendimiento vence 2027-01-01  │
│                                                          │
│  Suites: 247 verdes (142 backend + 105 frontend)         │
│                                                          │
│  ─────────────────────────────────────────────────────   │
│  Total: 24 passed, 0 failed, 2 warnings                  │
│  Resultado: PASSED (ronda 1, sin bucles correctivos)     │
│  Next: gates.verify = true, continuar a RELEASE          │
└─────────────────────────────────────────────────────────┘
```
