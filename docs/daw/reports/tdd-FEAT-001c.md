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
