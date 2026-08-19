# Evidencia TDD — FEAT-001c

| Campo | Valor |
|---|---|
| Ticket | FEAT-001c |
| Spec | `docs/daw/specs/spec-FEAT-001c.md` |
| Fase | CODE |

**Por qué existe este archivo.** Mismo motivo que en FEAT-001b: la regla de testing exige que cada
test falle **antes** de la implementación, pero esa evidencia la produce el agente implementador en
su mensaje de vuelta, que no queda en ningún lado. El verificador solo ve el repositorio, así que sin
este archivo no puede confirmar test-first por buena que haya sido la práctica.

**Y volvió a pasar.** En el Block 1 de este ticket la revisión se despachó **antes** de escribir esta
evidencia, y `daw-module-verifier` devolvió BLOCKED por su ausencia, con el código impecable —
exactamente el mismo desenlace que el Block 2 de FEAT-001b, que este archivo documentaba como
lección aprendida. El error es del orquestador, no del implementador: la consigna del bloque no pidió
escribir el reporte. Corregido para los bloques 2, 3 y 4, donde la consigna lo exige explícitamente.

Se actualiza al cerrar cada bloque, **antes de la revisión**.

---

## Block 1 — Backend: endpoint de resumen con agregación en SQL

**16 tests escritos. Rojo ANTES: 16/16.**

### Ronda 1 — rojo de compilación

Con los tests escritos y nada de producción:

```
RangoDelMesTests.cs(1,25): error CS0234: The type or namespace name 'Resumen'
does not exist in the namespace 'GestionGastos.Api'
```

### Ronda 2 — rojo de aserción

Con el esqueleto mínimo —ruta registrada devolviendo la forma del contrato en cero, `RangoDelMes.De`
devolviendo `default`—, para que el rojo fuera de aserción y no de compilación.
`Failed: 16, Passed: 0, Total: 16`:

| Test | Aserción que rompió |
|---|---|
| `RangoDelMes_DevuelveElPrimeroYElUltimoDiaDelMes` | `Expected: 03/01/2026 / Actual: 01/01/0001` |
| `RangoDelMes_EnDiciembre_NoSeCorreAlAnioSiguiente` | `Expected: 12/01/2026 / Actual: 01/01/0001` |
| `RangoDelMes_EnFebreroDeAnioBisiesto_TerminaEl29` | `Expected: 02/01/2024 / Actual: 01/01/0001` |
| `Resumen_ConIngresosYGastos_DevuelveLosTresTotales` | `Expected: 1500.50 / Actual: 0` |
| `Resumen_SinMovimientosEnElMes_DevuelveCerosYDesgloseVacio` | `mes` → `Expected: 8 / Actual: 0` |
| `Resumen_ConGastosMayoresQueIngresos_DevuelveBalanceNegativo` | `Expected: -150.50 / Actual: 0` |
| `Resumen_ConVariasCategorias_DevuelveUnTotalPorCategoriaConGastos` | `Expected: 2 / Actual: 0` (filas del desglose) |
| `Resumen_ConCategoriaSinGastosEnElMes_NoLaIncluyeEnElDesglose` | `Assert.Single() Failure: The collection was empty` |
| `Resumen_LaSumaDelDesglose_EsIgualAlTotalGastado` | `Expected: 1334.57 / Actual: 0` |
| `Resumen_ConMovimientosDeOtrosMeses_LosExcluye` | `Expected: 25 / Actual: 0` |
| `Resumen_ConMovimientosElPrimeroYElUltimoDiaDelMes_LosIncluye` | `Expected: 33 / Actual: 0` |
| `Resumen_DeOtroPropietario_NoEntraEnLosTotales` | `Expected: 10 / Actual: 0` |
| `Resumen_NoDevuelveLaListaDeMovimientos` | `Assert.Single() Failure: The collection was empty` |
| `Resumen_EmiteLaAgregacionEnSql` | `Expected: 30 / Actual: 0` |
| `Resumen_DevuelveElMesYElAnioDelPeriodo` | `Expected: 8 / Actual: 0` |
| `Resumen_ConLaBaseCaida_Devuelve500SinDetalleInterno` | precondición con base sana: `Expected: 10 / Actual: 0` |

**Verde DESPUÉS: 16/16.** Suite backend completa: **140/140** (124 de `a`+`b` más los 16 nuevos), sin
regresiones y sin warnings nuevos con `TreatWarningsAsErrors` activo.

### Un test que podía pasar de entrada y hubo que endurecer

`Resumen_SinMovimientosEnElMes_DevuelveCerosYDesgloseVacio` era el candidato a dar verde contra un
stub de ceros: si el endpoint devolvía todo en cero por no estar implementado, el test que espera
ceros pasaba. Se endureció **antes** de correrlo: siembra movimientos del propietario en el mes
anterior y en el siguiente —para que no dé verde con la tabla vacía— y exige además el rótulo
`mes`/`anio`. Eso es lo que efectivamente lo puso en rojo (`mes` → `Expected: 8 / Actual: 0`).

### Mutaciones que prueban que los tests muerden

Las dos se corrieron **dos veces**: por el implementador, y de forma independiente por
`daw-module-verifier`, que no escribió el código. Los dos resultados coinciden.

| Mutación | Resultado |
|---|---|
| Agregar `.IgnoreQueryFilters()` a la consulta (**R-24**, el riesgo HIGH) | Cae **exactamente 1** test: `Resumen_DeOtroPropietario_NoEntraEnLosTotales`, con `Assert.Equal() Failure — Expected: 10 / Actual: 1009.00` |
| Reemplazar la agregación en SQL por `.Include(m => m.Categoria).ToListAsync()` + `GroupBy` en memoria, **preservando el resultado correcto** (**NFR-02 / R-25**) | Cae **exactamente 1** test: `Resumen_EmiteLaAgregacionEnSql`, con `Assert.Contains() Failure: Sub-string not found` sobre `SUM(`. Los otros **15 siguen verdes** |

**La segunda mutación es el hallazgo del bloque.** Es la falla silenciosa que el threat model
anticipaba, comprobada en vivo: una agregación evaluada en memoria devuelve los mismos números, pasa
los quince tests conductuales, y deja NFR-02 incumplido sin que nada se ponga rojo. El único test que
la distingue es el estructural. Por eso es requisito de cierre y no un extra.

La firma de la primera mutación también es la que el threat model describía para R-24: **no aparece
una fila de más, aparece un número más grande** (10 → 1009.00). Una fuga por agregación no se ve.

**Nota metodológica del verificador:** su primer intento de la segunda mutación —sin `.Include`—
rompía 12 de 16 tests por `NullReferenceException` sobre la navegación `Categoria` no cargada. Eso no
es la mutación que se quería probar: una mutación válida tiene que preservar la semántica y cambiar
solo el mecanismo. Con la versión semánticamente equivalente el resultado es limpio. Queda anotado
porque es fácil creer que una mutación "mató" un test cuando en realidad rompió otra cosa.

### El WARN de la auditoría de arquitectura, resuelto

`daw-arch-auditor` señaló que `TotalDe` (`ResumenEndpoints.cs:80-83`) usa
`SingleOrDefault(...).Total`, y preguntó si el valor 0 del enum `TipoMovimiento` podría colisionar con
un tipo real y producir un total incorrecto en silencio. **Verificado: no.**

```csharp
public enum TipoMovimiento : byte
{
    Gasto = 1,
    Ingreso = 2,
}
```

No hay valor 0, así que la tupla por defecto nunca coincide con `Gasto` ni con `Ingreso`. Y hay una
segunda razón por la que el riesgo no existe ni en principio: el predicado es `t.Tipo == tipo` con
`tipo` siempre `Gasto` o `Ingreso`, de modo que la tupla por defecto solo aparece **cuando nada
coincidió**, y ahí `.Total` es `0m` — exactamente lo que AC-02 pide para un mes sin movimientos de
ese tipo. La pregunta era pertinente y la respuesta no se deducía sin abrir el enum; queda escrita
para no volver a investigarla.

### Decisiones que la spec dejó abiertas

1. **Forma de `RangoDelMes`.** La spec pide "dado un `DateOnly` devuelve el primer y el último día".
   Se eligió `readonly record struct RangoDelMes(DateOnly PrimerDia, DateOnly UltimoDia)` con factory
   `De(DateOnly)`. El último día se deriva de `primerDia.AddMonths(1).AddDays(-1)`, que cubre
   diciembre y el año bisiesto **sin casos especiales** — un `switch` por mes o un `28/29` a mano
   serían dos lugares más donde equivocarse.
2. **Orden del desglose:** total descendente, y nombre ascendente como desempate. La spec no lo
   especificaba; sin un orden explícito, dos categorías con el mismo total podrían alternarse entre
   llamadas.
3. **Fuente de "hoy": `DateOnly.FromDateTime(DateTime.Today)`, hora local del servidor.** La spec fija
   que el rango lo pone el servidor pero no dice local ni UTC. Con la base y la API en la misma
   máquina no hay diferencia observable hoy. **Queda anotado como decisión revisable**: si esto se
   despliega con el servidor en UTC y el usuario en UTC-3, el corte del mes se desplaza y un
   movimiento del día 1 a la madrugada podría quedar fuera del resumen. Es R-28 en su versión de
   despliegue.
4. **Nombres JSON del contrato** (camelCase por defecto de Minimal API, sin configuración extra):
   `mes`, `anio`, `totalIngresado`, `totalGastado`, `balance`,
   `desglose[].categoriaId|categoriaNombre|total`. **El Block 3 tiene que espejarlos exactamente.**

### Por qué los tests de `RangoDelMes` no son tautológicos

Los valores esperados se escriben a mano (`new DateOnly(2026, 12, 31)`, `new DateOnly(2024, 2, 29)`,
`new DateOnly(2026, 2, 28)`) y no se derivan de `DateTime.DaysInMonth` ni de la función bajo prueba.
El test del bisiesto contrasta 2024 (29) contra 2026 (28) **en el mismo test**, que es justo el par
que distingue un `28` fijo de un cálculo real. Y los tests de `ResumenTests` siembran usando
`DateTime.DaysInMonth` y **no** `RangoDelMes`, para no acoplar el oráculo del test a la pieza que
prueba — con el comentario que lo explica en el propio archivo.

### Hallazgos fuera de alcance

1. **El endpoint hace dos round trips a la base** —totales por tipo, y desglose por categoría—, tal
   como prescribe la spec. Ambos agregan en SQL, así que NFR-02 está cumplido. Pero **el Block 2 va a
   medir dos consultas y no una** contra el presupuesto de 2 s: dato necesario para interpretar su
   p95. No se tocó.
2. `ObservadorDeSql.OrderByDe` es específico del listado (busca `ORDER BY`) y no hacía falta acá.
   `GetObservandoElSqlAsync` y `RegistroDeSentencias` se reutilizaron tal cual, sin tocar `Infra/`.

---

## Block 2 — Backend: rendimiento de la pantalla principal

**2 tests escritos. Rojo ANTES: 2/2** (el segundo, por mutación — ver más abajo por qué no podía ser
de otra forma).

### Ronda 1 — rojo de compilación

Con los dos tests escritos y sin tocar `MedicionDeRendimiento`:

```
RendimientoResumenTests.cs(141,53): error CS0117: 'MedicionDeRendimiento' does not contain
a definition for 'PresupuestoP95Pantalla'
RendimientoResumenTests.cs(145,38): error CS0117: 'MedicionDeRendimiento' does not contain
a definition for 'PresupuestoP95Pantalla'
```

### Ronda 2 — rojo de aserción

Con el esqueleto mínimo —`PresupuestoP95Pantalla = TimeSpan.Zero`, para que el rojo fuera de
aserción y no de compilación—, `Failed: 1, Passed: 1, Total: 2`:

| Test | Aserción que rompió |
|---|---|
| `Pantalla_ConMilMovimientos_ListadoYResumenRespondenBajoDosSegundos` | `El p95 de la pantalla fue 13 ms sobre 100 ejecuciones (listado 7 ms + resumen 7 ms, este último con sus dos consultas); el presupuesto de AC-11 es 0 ms.` |

### El test que dio verde de entrada, y qué se hizo

`Resumen_ConMilMovimientos_NoMaterializaFilasDeMovimientos` **pasó antes de escribir nada**, y no es
un defecto del test: el endpoint que mide ya existe desde el Block 1, y este bloque no agrega
producción salvo una constante. No hay forma de que ese test vea un rojo "natural" sin desimplementar
el Block 1. Se hicieron las dos cosas que sí correspondían:

1. **Se endureció antes de seguir.** A la versión original —`Assert.Single` sobre el desglose, el
   total exacto derivado del sembrado y la comparación de tamaño contra el listado del mismo mes— se
   le agregaron tres aserciones que expresan el criterio en su forma general y no en la del sembrado:
   el desglose no puede traer más filas que categorías tiene el catálogo (leído de
   `GET /api/categorias`, no escrito a mano), ningún `categoriaId` puede repetirse, y las filas del
   desglose tienen que ser un orden de magnitud menos que los movimientos del mes
   (`IndicesSembradosDelMes.Count > desglose.Count * 10`). Sin esa última, un desglose de 90 filas
   sobre 93 movimientos seguiría cumpliendo "una fila por categoría" por accidente si el catálogo
   fuera grande.
2. **Se probó por mutación que muerde.** Cambiando la agregación del desglose de
   `GroupBy(m => new { m.CategoriaId, m.Categoria!.Nombre })` a
   `GroupBy(m => new { m.CategoriaId, m.Categoria!.Nombre, m.Id })` —una fila por movimiento en vez
   de una por categoría, con los mismos totales— el test cae:

   ```
   Resumen_ConMilMovimientos_NoMaterializaFilasDeMovimientos
   Assert.Single() Failure: The collection contained 93 items
   ```

   La mutación se revirtió con `git checkout` inmediatamente: **ningún archivo de producción quedó
   modificado por este bloque**.

**Verde DESPUÉS: 2/2.** Suite backend completa: **142/142** (140 del Block 1 más los 2 nuevos), sin
regresiones y sin warnings nuevos con `TreatWarningsAsErrors` activo.

### El p95 medido — los números, no la impresión

Tres corridas independientes de 100 ejecuciones cada una, con 1000 movimientos sembrados en la
cuenta, de los cuales **93 caen en el mes que se mide** (agosto de 2026):

| Corrida | p95 listado del mes | p95 resumen | p95 pantalla completa |
|---|---|---|---|
| 1 | 8 ms | 7 ms | 15 ms |
| 2 | 8 ms | 7 ms | 16 ms |
| 3 | 8 ms | 7 ms | 15 ms |

**Presupuesto de AC-11: 2000 ms. Margen: ~130×.** El presupuesto no está ni cerca de tensionarse, y
por eso el índice nuevo que la spec dejaba condicionado a esta medición **no hace falta**: el prefijo
`(usuario_id, fecha)` del índice de FEAT-001a alcanza.

**Cómo leer el número del resumen.** Los 7 ms del resumen incluyen **sus dos round trips a la base**
—totales por tipo y desglose por categoría—, que es como está escrito el endpoint del Block 1. No es
el costo de una consulta: es el de dos, más el request HTTP. Si alguien fusionara las dos consultas
en una, este número es la referencia contra la cual comparar. El hallazgo #1 del Block 1 pedía
justamente que esto quedara dicho.

**Qué se cronometra exactamente.** Las dos peticiones que la pantalla principal hace al cargar:
`GET /api/movimientos?desde=<primer día>&hasta=<último día>` —el default del mes en curso que arma
`mesActual.ts`, sin categoría— y `GET /api/resumen`. Se suman por iteración, porque el usuario espera
a que estén las dos y no a la más rápida. Se descarta un calentamiento previo (JIT, primer plan de
consulta, pool de conexiones), igual que en `RendimientoListadoTests` y `RendimientoAltaTests`.

**Premisas que fallan ruidosamente antes de medir**, para que el verde no pueda salir de una tabla
vacía: `ConfirmarSembradoAsync` (las 1000 filas están), `ConfirmarQueElMesTieneFilas` (el mes medido
tiene movimientos), y el calentamiento afirma sobre el contenido —el listado devuelve las 93 filas
del mes y el resumen devuelve el total gastado esperado—. Cronometrar un listado vacío y un resumen
en cero daría verde midiendo otra cosa.

### Decisiones que la spec dejó abiertas

1. **Qué listado se cronometra.** La spec dice "el listado", sin fijar los parámetros. Se eligió el
   del **mes en curso sin categoría**, que es el que la pantalla realmente pide (`App.tsx` inicializa
   `filtros` con `mesActual()`). El test del listado de FEAT-001b mide otro —marzo con categoría—,
   así que los dos números no son comparables entre sí y no debería sorprender que difieran.
2. **Oráculo del total esperado.** `TotalGastadoEsperado` se deriva del contrato del sembrado
   (`SembrarMovimientosAsync` arranca en 100 y sube de a uno siguiendo `FechasSembradas`, así que la
   fila *i* vale 100 + *i*), en vez de un número escrito a mano. Acopla el test al sembrado, y es
   deliberado: es lo único que distingue "sumó el mes entero" de "sumó una parte", y si alguien
   cambia el sembrado el test lo dice con un número concreto en vez de callarse.
3. **Los tres p95 se emiten por `ITestOutputHelper`.** Ningún test del proyecto lo usaba. Se
   incorporó porque el criterio de cierre del bloque exige registrar el p95 medido, y un assert verde
   no dice cuánto margen quedaba: sin la salida, el número solo existiría cuando el test falla.

### Hallazgos fuera de alcance

1. **El sembrado compartido está anclado a 2026 y este test mide "el mes en curso".**
   `MedicionDeRendimiento.FechasSembradas` genera fechas de 2026 (`new DateOnly(2026, 1, 1).AddDays(i % 365)`),
   mientras que el período del resumen lo fija el servidor con la fecha real. **A partir de enero de
   2027 el mes en curso no va a tener ninguna fila sembrada** y los dos tests de este bloque van a
   fallar en `ConfirmarQueElMesTieneFilas` con el mensaje que lo explica. La corrección natural es
   anclar el sembrado al año en curso, pero eso es tocar `SembrarMovimientosAsync`, que la spec de
   este bloque marca explícitamente como "no cambia" y que comparten los otros dos tests de
   rendimiento. **No se tocó.** Queda anotado como bomba de tiempo con fecha conocida.
2. `ResumenTests` tiene el mismo anclaje al reloj real, pero siembra contra el mes en curso calculado
   en el momento, así que no lo afecta.

---

## Block 3 — Frontend: cliente HTTP y tipo del contrato

**4 tests escritos. Rojo ANTES: 4/4.**

### Ronda 1 — rojo de import

Con los 4 tests escritos, `ResumenMensual` inexistente y `obtenerResumen` sin escribir:

```
src/api/cliente.test.ts(13,3): error TS2305: Module '"./cliente"' has no exported member 'obtenerResumen'.
src/api/cliente.test.ts(18,3): error TS2305: Module '"./tipos"' has no exported member 'ResumenMensual'.
```

`pnpm test` → `Tests  4 failed | 93 passed (97)`, los cuatro con:

```
TypeError: obtenerResumen is not a function
```

### Ronda 2 — rojo de aserción

Vitest no hace typecheck, así que un rojo de import no prueba que las aserciones muerdan. Con los
tipos ya escritos y un **stub deliberado** de `obtenerResumen` que devolvía un resumen en ceros
**sin llamar a `fetch`**, `Tests  4 failed | 93 passed (97)`:

| Test | Aserción que rompió |
|---|---|
| `ObtenerResumen_DevuelveLosTotalesYElDesglose` | `TypeError: llamada is not a function or its return value is not iterable` — `fetch` nunca se llamó, así que no había ruta que inspeccionar: exactamente el fallo que corresponde a un cliente que no pide `/api/resumen` |
| `ObtenerResumen_ConMesVacio_DevuelveCerosYDesgloseVacio` | `AssertionError: expected +0 to be 8 // Object.is equality` (el `mes`: los ceros del stub pasaban, el período no) |
| `ObtenerResumen_ConErrorDelServidor_LanzaErrorDelServidorConTraceId` | `AssertionError: expected { mes: +0, anio: +0, …(4) } to be an instance of ErrorDelServidor` |
| `ObtenerResumen_ConRedCaida_LanzaErrorDeRed` | `AssertionError: expected { mes: +0, anio: +0, …(4) } to be an instance of ErrorDeRed` |

El stub se reemplazó por la implementación real —`pedir<ResumenMensual>('/resumen', {})`— en cuanto
se capturó el rojo; **no quedó ningún resto en producción** (`git diff` de `cliente.ts`: 6 líneas, la
función y su import).

### Verde DESPUÉS: 4/4

Suite frontend **completa**: `Test Files  9 passed (9)` / `Tests  97 passed (97)` (93 previos + 4
nuevos), sin regresiones. `pnpm exec tsc --noEmit` limpio, `pnpm lint` limpio, `prettier --check`
limpio.

### Mutaciones que prueban que los tests muerden

| # | Mutación | Qué cae |
|---|---|---|
| 1 | `anio` → `year` en `ResumenMensual` | **`tsc --noEmit`: 4 errores**, en las dos fixtures tipadas y en las dos aserciones — `error TS2353: Object literal may only specify known properties, and 'anio' does not exist in type 'ResumenMensual'` (líneas 55 y 68) y `error TS2339: Property 'anio' does not exist on type 'ResumenMensual'` (líneas 353 y 376). **`pnpm test` sigue en 97/97.** |
| 2 | `obtenerResumen` arma query string (`/resumen?mes=8&anio=2026`) | `ObtenerResumen_DevuelveLosTotalesYElDesglose` → `AssertionError: expected '/api/resumen?mes=8&anio=2026' to be '/api/resumen'`. `Tests  1 failed \| 96 passed` |
| 3 | `obtenerResumen` devuelve el JSON sin tipar (`Promise<unknown>` + `pedir<unknown>`) | **`tsc --noEmit`: 17 errores en total** — **16 `TS18046: 'resumen' is of type 'unknown'`**, uno por cada acceso a campo: **10 en el happy path** (`cliente.test.ts:352-361`, de `resumen.mes` a `resumen.desglose[1]?.categoriaNombre`) y **6 en el mes vacío** (`cliente.test.ts:371-376`, los tres ceros, el desglose vacío y el período); más **1 `TS6196: 'ResumenMensual' is declared but never used`** en `cliente.ts:8`, porque al destipar la función el import del contrato queda huérfano y `noUnusedLocals` lo delata |

Las tres se revirtieron con `cp` de la copia previa; el `git diff --stat` final es el de la
implementación y nada más.

**Corrección posterior a la revisión.** La primera versión de esta tabla declaraba **10** errores
para la mutación 3. Era falso: el conteo se tomó de una salida truncada con `head -8` en vez de
contar `tsc` entero, y se perdieron los 6 del mes vacío y el `TS6196`. Lo detectó
`daw-module-verifier`, que replicó la mutación y obtuvo 17. Los números de arriba son los de la
corrida completa (`pnpm exec tsc --noEmit 2>&1 | grep -c "error TS"` → `17`; por código: 16
`TS18046` + 1 `TS6196`). Queda anotado y no reescrito en silencio porque el argumento central de este
bloque es "`pnpm test` no alcanza, hay que mirar `tsc`": si el número que se le pide a un humano que
mire está mal contado en el mismo párrafo, el argumento se come a sí mismo.

**Lo que la mutación 1 deja al descubierto, y es importante:** el tipo **sí** está ejercido por los
tests —las fixtures se declaran `const RESUMEN_DE_AGOSTO: ResumenMensual = {...}` justamente para
eso—, pero quien lo hace cumplir es `tsc`, **no Vitest**: este proyecto no tiene `typecheck` activado
en la config de Vitest, así que `pnpm test` en verde no garantiza que los tipos cierren. La red de
seguridad existe igual —`pnpm build` corre `tsc --noEmit && vite build`—, pero **correr solo
`pnpm test` no alcanza para este bloque**: hay que correr también `tsc --noEmit`, que es lo que
detecta un contrato desalineado con el backend.

### Decisiones que la spec dejó abiertas

1. **Dónde va `obtenerResumen` en el archivo.** Se ubicó junto a las otras dos lecturas
   (`obtenerCategorias`, `obtenerMovimientos`) y antes de las escrituras, para que el archivo siga
   leyéndose por tipo de operación y no por orden de llegada.
2. **Cuán específicas son las aserciones del happy path.** Se comprueba **campo por campo** en vez de
   un `toEqual` contra la fixture entera. Un `toEqual` contra el mismo objeto que se sirvió pasaría
   aunque el tipo declarara nombres que el backend no emite: compararía el JSON consigo mismo. Los
   accesos individuales (`resumen.totalIngresado`, `resumen.desglose[0]?.categoriaNombre`) son lo que
   hace que la mutación 3 produzca **los 10 `TS18046` de este test** —de los 16 que da en total, ver
   la tabla de mutaciones— en vez de ninguno: un `toEqual` sería un único acceso y no delataría nada.
3. **El mes vacío también afirma el período.** La spec pide "ceros y desglose vacío"; el test agrega
   `mes` y `anio`, porque el rótulo del Block 4 sale de la respuesta y un mes sin movimientos que
   viniera sin período rompería la pantalla justo en el caso menos probado.
4. **`ObtenerResumen_ConRedCaida_LanzaErrorDeRed` afirma además el negativo**
   (`not.toBeInstanceOf(ErrorDelServidor)`): sin eso, un cliente que convirtiera todo en
   `ErrorDelServidor` pasaría igual si `ErrorDeRed` heredara de él en el futuro.

### Hallazgos fuera de alcance

1. **Vitest sin `typecheck`.** Como explica la mutación 1, la config de Vitest
   (`frontend/vitest.config.ts`) no habilita `test.typecheck`, así que ninguna violación de tipos
   rompe `pnpm test`. Para un cliente HTTP cuyo valor está justamente en espejar el contrato del
   backend, eso es un agujero real en la señal. **No se tocó** —es config compartida por toda la
   suite, no del Block 3—. Candidato a ticket propio, junto con el del linter del backend.
2. **Nada verifica que `ResumenMensual` y `ResumenMensualDto` sigan coincidiendo.** Hoy la
   correspondencia la sostiene la revisión humana: si alguien renombra un campo en el record de C#,
   el frontend compila, los 97 tests pasan y el número aparece `undefined` en pantalla. Un test de
   contrato (esquema compartido o snapshot del JSON real) resolvería la clase entera de problema.
   Fuera del alcance de este bloque, que solo puede espejar el DTO tal como está hoy.
3. **`ErrorNoEncontrado` no se ejercita en `/api/resumen`.** El endpoint no devuelve 404 —el resumen
   del mes en curso siempre existe, aunque esté en ceros—, así que no hay test para ese camino y no
   se agregó ninguno: sería un test de un desenlace que el backend no produce.

---

## Block 4 — Frontend: el resumen en la pantalla principal

**8 tests escritos. Rojo ANTES: 8/8.** Cinco viven en `frontend/src/resumen/ResumenDelMes.test.tsx`
(el componente aislado) y tres en `frontend/src/App.test.tsx`, que son los que montan `App` entera:
los dos del disparador (AC-06 y su inversa) y el de las dos peticiones independientes, que necesita
el listado en pantalla para poder afirmar que no se cayó.

### Ronda 1 — rojo de resolución

Con los 8 tests escritos y ningún componente:

```
FAIL  src/resumen/ResumenDelMes.test.tsx [ src/resumen/ResumenDelMes.test.tsx ]
Error: Failed to resolve import "./ResumenDelMes" from "src/resumen/ResumenDelMes.test.tsx". Does the file exist?

FAIL  src/App.test.tsx > Resumen en la pantalla principal > Resumen_AlFiltrarElListado_NoCambia
TestingLibraryElementError: Unable to find role="region" and name "Resumen del mes"
```

`Tests  3 failed | 97 passed (100)`: los 5 del archivo nuevo ni siquiera se recolectaron.

### Ronda 2 — rojo de aserción

Un rojo de import no prueba que las aserciones muerdan. Con un **stub deliberado** —la sección con
su `aria-label`, los tres rótulos en `ARS 0,00`, el período como `"Mes en curso"`, sin `fetch` y sin
desglose— los 8 fallan por lo que cada uno afirma. `Tests  8 failed | 97 passed (105)`:

| Test | Aserción que rompió |
|---|---|
| `Resumen_MuestraLosTresTotalesYElMes` | `TestingLibraryElementError: Unable to find an element with the text: Agosto 2026` (el rótulo sale de la respuesta, no del reloj) |
| `Resumen_ConBalanceNegativo_LoMuestraConSigno` | `Unable to find an element with the text: Febrero 2026` |
| `Resumen_MuestraElDesglosePorCategoria` | `Unable to find role="listitem"` |
| `Resumen_SinGastosEnElMes_DiceQueNoHayNadaQueDesglosar` | `Unable to find an element with the text: /no hay gastos para desglosar/i` |
| `Resumen_ConRedCaida_MuestraElErrorConReintento` | `Unable to find role="alert"` |
| `Resumen_AlFiltrarElListado_NoCambia` | `Unable to find an element with the text: Agosto 2026` |
| `Resumen_TrasUnAlta_SeActualiza` | `Unable to find an element with the text: Agosto 2026` |
| `Resumen_ConErrorDelServidor_NoTumbaElListado` | `Unable to find role="alert"` |

El stub se reemplazó por la implementación real en cuanto se capturó el rojo; **no quedó ningún
resto en producción** (`ResumenDelMes.tsx` no contiene ni el texto `"Mes en curso"` ni el
`void version`).

Los tres totales del stub ya venían en `ARS 0,00`, así que las aserciones de ceros de
`Resumen_SinGastosEnElMes_DiceQueNoHayNadaQueDesglosar` pasaban con el stub: lo que rompe ese test
es el texto del desglose vacío, que es lo que AC-02 pide de nuevo respecto del resto.

### Verde DESPUÉS: 8/8

Suite frontend **completa**: `Test Files  10 passed (10)` / `Tests  105 passed (105)` (97 previos +
8 nuevos), sin regresiones. `pnpm exec tsc --noEmit` limpio, `pnpm lint` limpio,
`prettier --check .` limpio.

### Mutaciones que prueban que los tests muerden

Las dos del disparador, que son las que este bloque se juega:

| # | Mutación | Qué cae |
|---|---|---|
| 1 | El resumen se suscribe a `filtros`: prop nueva en `PropsResumenDelMes`, cableada desde `App`, y `filtros` agregado a las deps del efecto | `Resumen_AlFiltrarElListado_NoCambia` → `AssertionError: expected 2 to be 1 // Object.is equality` en `expect(servidor.lecturasDelResumen()).toBe(1)` (`App.test.tsx:554`). `Tests  1 failed \| 104 passed (105)` |
| 2 | Se quita `version` de las deps del efecto (`}, [cargar]);`) | `Resumen_TrasUnAlta_SeActualiza` → `AssertionError: expected 'ARS 0,00' to be 'ARS 1.500,50'` en el `waitFor` sobre el total gastado. `Tests  1 failed \| 10 passed (11)` en `App.test.tsx` |

Las dos se revirtieron con `cp` de la copia previa; el árbol quedó confirmado con `git status`
(`M App.test.tsx`, `M App.tsx`, `M movimientos/FiltrosMovimientos.test.tsx`, `?? src/resumen/`) y la
suite volvió a `105 passed (105)`.

**Lo que la mutación 1 deja al descubierto:** los números del resumen **no** cambian al filtrar
aunque el resumen se resuscriba mal —el endpoint no acepta período, así que la segunda respuesta es
idéntica a la primera—. Es decir: mirar la pantalla no distingue las dos formas de equivocarse. Lo
único que las distingue es **contar las peticiones**, y por eso el doble de `App.test.tsx` lleva
`lecturasDelResumen()`. Un test que solo comparara los importes daría verde con AC-06 violado.

### Decisiones que la spec dejó abiertas

1. **Dónde se monta el resumen.** Justo debajo del `<h1>`, antes del formulario de alta: es la
   respuesta a "cómo vengo este mes" y lo primero que la pantalla tiene que contestar. La spec dice
   "la pantalla principal" sin fijar la posición.
2. **La moneda del resumen.** `ResumenMensual` no trae `moneda` —el PRD deja los totales por moneda
   fuera de alcance—, así que el componente formatea con la constante `MONEDA = 'ARS'` reusando
   `formatearMonto` del listado, para que el mismo importe se lea igual arriba y en la tabla. Si
   alguna vez hay una segunda moneda, esto es lo primero que hay que tocar y está en una sola línea.
3. **Los nombres de mes van escritos, no por `Intl`.** `Intl.DateTimeFormat` los devuelve en
   minúscula y con variaciones entre builds de ICU, y obligaría a construir un `Date` —con su huso—
   para rotular un período que ya viene desarmado en dos números.
4. **Los tres tests que montan `App` viven en `App.test.tsx`.** La spec no dice en qué archivo. Se
   eligió ese porque el doble con estado ya está ahí y es el que la spec manda extender: duplicarlo
   en `ResumenDelMes.test.tsx` habría dado dos servidores falsos que se desincronizan.
5. **El doble de `App.test.tsx` devuelve el resumen en cero por omisión.** Con `resumen: 'calculado'`
   —solo los tres tests del resumen— agrega los movimientos que tiene guardados. Devolver siempre
   los números derivados del listado volvería ambiguas las búsquedas por texto de los ocho tests
   preexistentes: `ARS 1.500,50` estaría en la fila, en el total gastado y en el desglose, y
   `getByText` fallaría con "Found multiple elements" en tests que no tienen nada que ver con esto.
6. **El rótulo del período va en su propio `<p className="periodo">`**, separado del `<h2>`, para que
   `getByText('Agosto 2026')` sea una coincidencia exacta y no un `toContain` sobre el encabezado.
7. **Un fallo de carga no conserva los números viejos.** Se limpia el resumen y se muestra solo el
   aviso: un resumen que falló mostrando lo anterior no se distingue de uno al día.
8. **AC-03 se afirma sobre el texto.** `expect(total('Balance')).toBe('ARS -150,50')`. La clase
   `balance-negativo` se emite —es lo que la spec pide para el color— pero ningún test la mira.

### Hallazgos fuera de alcance

1. **`FiltrosMovimientos.test.tsx` también monta `App`, y hubo que tocarlo.** La lista de archivos
   del bloque solo prevé `App.test.tsx` como doble a extender, pero
   `frontend/src/movimientos/FiltrosMovimientos.test.tsx` monta `App` entera en sus 9 tests
   (deliberadamente: su criterio de cierre es "qué URL se pide"). Con el resumen montado, su doble
   respondía `throw new Error('Ruta no esperada en el test: /api/resumen')` y
   `Filtros_ConRedCaida_MuestraElErrorConReintento` rompía en
   `AssertionError: expected 'No pudimos cargar el resumen del mes:…' to contain 'No pudimos conectar con el servidor.'`:
   su `findByRole('alert')` estaba encontrando el aviso del resumen. **Se agregó la rama
   `/api/resumen` devolviendo el resumen en cero**, en el helper compartido y en el doble inline de
   ese test; **ninguna aserción existente se tocó**. Es el mismo cambio que la spec manda hacer en
   `App.test.tsx`, en un archivo que la spec no listó porque no consta que monte `App`.
2. **~~`tokens.css` no tiene regla para `.balance-negativo`~~ — RESUELTO tras la revisión.** El
   hallazgo original decía que el color de AC-03 quedaba sin definir porque la hoja de estilos no
   estaba en la lista de archivos del bloque. El diagnóstico estaba incompleto: **el proyecto ya
   tenía el token exacto**, `--error: #b3261e`, con su contraste 5.9:1 documentado en la cabecera de
   `frontend/src/estilos/tokens.css` y ya en uso por `.aviso-error` y `.error-de-campo`. No faltaba
   una decisión de diseño, faltaba una línea con un color ya elegido y ya validado, y la spec la
   pedía explícitamente ("el signo menos explícito en el texto **más** una clase CSS propia para el
   color"). Se agregó al final de `tokens.css`, que es donde vive `.aviso-error` —el CSS del proyecto
   es una sola hoja, no hay archivo por componente—:

   ```css
   .balance-negativo {
     color: var(--error);
   }
   ```

   Se eligió `var(--error)` y no un color nuevo por dos razones: el contraste ya está verificado
   contra el fondo (5.9:1, por encima del 4.5:1 que pide texto normal) y reusa el mismo rojo con el
   que la aplicación ya dice "acá hay algo que mirar". **Ningún test cambió**: la spec fija que la
   distinción verificable es el signo en el texto, y asertar sobre la clase volvería a atar el test a
   los estilos. Los 8 siguen como estaban.
3. **Las demás clases del resumen siguen sin regla, y es lo normal en este proyecto.**
   `.resumen-del-mes`, `.periodo`, `.totales`, `.total`, `.balance`, `.desglose` y `.categoria` son
   ganchos de maquetación, no de color, y no se inventaron reglas para ellas porque implican
   decisiones de diseño que el proyecto no tomó todavía (disposición de los tres totales, separación
   del desglose, jerarquía tipográfica). No es una deuda de este bloque: `tokens.css` tampoco tiene
   reglas para `.filtros`, `.acciones`, `.dialogo`, `.rango-vigente`, `.aviso-recorte`,
   `.aviso-desaparecido`, `.aviso-definitivo` ni `.nota-del-dialogo`, todas de FEAT-001a y FEAT-001b.
   Lo que sí está resuelto en la hoja es todo lo semántico —el color del error, el foco visible, el
   contraste—. Una pasada de diseño sobre la pantalla entera es un ticket propio.
4. **Vitest sigue sin `typecheck`** (ya anotado en el Block 3): `pnpm test` en verde no dice nada
   sobre los tipos. Este bloque volvió a correr `tsc --noEmit` a mano por eso.
5. **Nadie verifica que el mes del resumen y el del filtro por defecto coincidan.** El resumen rotula
   con lo que devuelve el servidor y los filtros se inicializan con `mesActual()` del navegador: si
   el reloj del cliente y el del servidor cayeran en meses distintos —fin de mes, husos cruzados—, la
   pantalla mostraría el resumen de un mes y el listado de otro, cada uno rotulado correctamente. Es
   coherente con lo que el PRD decidió (el resumen no sigue al filtro) y no hay test que lo cubra
   porque no hay comportamiento definido para ese caso.
