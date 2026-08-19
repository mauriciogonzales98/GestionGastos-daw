# Spec FEAT-001c: Resumen del mes con desglose por categoría

| Field | Value |
|-------|-------|
| Ticket | FEAT-001c |
| PRD | docs/daw/prd/prd-FEAT-001c.md |
| Tier | FEATURE |
| Date | 2026-08-19 |
| Spec loops | 0 |

## Summary

Se agrega un endpoint nuevo, `GET /api/resumen`, que devuelve los tres totales del mes calendario en
curso —ingresado, gastado y balance— más el desglose de los gastos de ese mes por categoría. Todo
sale **agregado desde la base**: la respuesta trae los tres totales y a lo sumo una fila por
categoría, nunca la lista de movimientos. En el frontend se agrega un componente de resumen a la
pantalla principal, que se refresca cuando el usuario da de alta, modifica o elimina un movimiento, y
que **no** reacciona a los filtros del listado.

**Decisión de diseño registrada, y es la inversa de la de FEAT-001b:** acá el rango del mes lo fija
**el servidor**, no el cliente. En `b` el default del mes lo pone el frontend, y está comentado como
tal en `MovimientosEndpoints.cs:16-21` — repetir ese patrón acá por inercia sería el error natural, y
sería un error. Tres razones. FR-03 exige que el resumen se calcule "siempre" sobre el mes calendario
actual **con independencia** de lo que el usuario filtre; si el rango llegara por query string, el
endpoint pasaría a ser un resumen de rango arbitrario y FR-03 dejaría de estar garantizado por el
contrato para pasar a depender de que el cliente se porte bien. NFR-02 exige que el cálculo viva en
la base, y el corte del mes es parte de ese cálculo. Y AC-06 se vuelve verificable de un lado solo:
con el rango fijado en el servidor, no existe forma de que un filtro del listado lo altere.

`mesActual.ts` **se sigue usando en el frontend**, pero solo para rotular (AC-09). El número que se
muestra y el número que se calcula salen de la misma fuente —la respuesta del endpoint, que incluye
mes y año—, así que no hay dos criterios de "mes en curso" que puedan discrepar en el cambio de mes.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 1, Block 4 |
| FR-02 | Block 1, Block 4 |
| FR-03 | Block 1, Block 4 |
| FR-04 | Block 1, Block 4 |
| FR-05 | Block 1 |
| NFR-01 | Estrategia: el resumen se apoya en el mismo índice `ix_movimientos_usuario_fecha_id` sobre `(usuario_id, fecha DESC, id DESC)` que FEAT-001a creó, cuyo prefijo `(usuario_id, fecha)` sirve al corte por mes. Se mide en Block 2 con la infraestructura que ya existe —`MedicionDeRendimiento`, 100 ejecuciones sobre 1000 movimientos— pero con **presupuesto propio de 2 s**, porque AC-11 mide listado **y** resumen juntos y la constante `PresupuestoP95` existente vale 1 s y es la del listado solo |
| NFR-02 | Estrategia: la agregación se expresa como `GroupBy`/`Sum` sobre el `IQueryable`, sin materializar movimientos. Verificado en dos capas en Block 1: conductual (la respuesta trae los 3 totales y a lo sumo 1 fila por categoría, AC-12) y **estructural** (el SQL emitido contiene la agregación y no un `SELECT` de filas, vía `Tests/Infra/ObservadorDeSql.cs`) |

**Sobre por qué la capa estructural no es opcional acá.** El impact scan confirmó que **no existe
ningún precedente de `GroupBy`/`Sum` sobre `IQueryable` en el backend**: los únicos `GroupBy` del
repositorio son en memoria, sobre listas ya materializadas (`ResultadoValidacion.cs:32`). Una
agregación que EF no logra traducir no falla: la evalúa del lado del cliente, trae todas las filas y
suma en memoria. El resultado HTTP es idéntico, todos los tests conductuales pasan, y NFR-02 queda
incumplido en silencio. Es exactamente el mismo modo de falla que el ordenamiento de FEAT-001a, donde
un test conductual daba verde con el `ORDER BY` borrado porque el índice devolvía el orden igual. Por
eso el test que inspecciona el SQL es un requisito del bloque y no un extra.

**Sobre el índice.** No se agrega migración. El prefijo `(usuario_id, fecha)` del índice existente
cubre el corte por mes, que es el único predicado del resumen. El `GROUP BY categoria_id` opera sobre
las filas ya acotadas a un mes de un usuario: a la escala de NFR-01 (1000 movimientos en total, de los
cuales un mes es una fracción) el costo es despreciable. Si AC-11 fallara en Block 2, esa medición
sería la que justificaría un índice nuevo — no una suposición previa.

## Dependencies between blocks

Orden de ejecución: **1 → 2 → 3 → 4**.

| Block | Depende de | Motivo |
|---|---|---|
| 1 — Backend: endpoint de resumen | — | Arranca sobre lo que FEAT-001b dejó en `main` |
| 2 — Backend: rendimiento de la pantalla | 1 | Mide el endpoint que el Block 1 crea, junto al listado que ya existe |
| 3 — Frontend: cliente HTTP y tipo | 1 | Espeja un contrato que debe existir |
| 4 — Frontend: componente de resumen | 3 | Consume `obtenerResumen` |

---

## Block 1 — Backend: endpoint de resumen con agregación en SQL

**Files**

- `backend/GestionGastos.Api/Resumen/ResumenEndpoints.cs` (new) — el endpoint y su registro.
- `backend/GestionGastos.Api/Resumen/ResumenMensualDto.cs` (new) — el contrato de respuesta.
- `backend/GestionGastos.Api/Resumen/RangoDelMes.cs` (new) — el corte del mes calendario, aislado para poder probarlo sin base.
- `backend/GestionGastos.Api/Program.cs` (modified) — `app.MapResumenEndpoints()` junto al de movimientos.
- `backend/GestionGastos.Api.Tests/Resumen/ResumenTests.cs` (new)
- `backend/GestionGastos.Api.Tests/Resumen/RangoDelMesTests.cs` (new)

**Logic**

`GET /api/resumen` no acepta ningún parámetro: el período es fijo por FR-03. El rango sale de
`RangoDelMes`, que dado un `DateOnly` devuelve el primer y el último día de su mes calendario.

La consulta parte de `datos.Movimientos` **sin cláusula de propietario**: la aplica el filtro global
de EF (`AppDbContext.cs:32`), igual que `ListarAsync`. Reimplementar el filtro a mano acá sería una
segunda copia que puede divergir de la del listado, que es justo lo que FR-05 no quiere.

Los totales de ingresos y gastos se obtienen agrupando por `Tipo` y sumando `Monto`; el desglose,
agrupando por categoría y sumando, **restringido a gastos**. El balance es ingresado menos gastado,
calculado en el servidor y devuelto ya resuelto, para que el cliente no pueda equivocarse en la
resta.

**La agregación no debe materializar movimientos.** El criterio de aceptación de este bloque no es
que los números den bien —eso lo daría también una versión que trae todo y suma en memoria— sino que
el SQL emitido contenga la agregación.

**API contract**

| Campo | Valor |
|---|---|
| Método y ruta | `GET /api/resumen` |
| Parámetros | **ninguno** — ni de ruta, ni de query, ni de cuerpo |
| Autorización | sin autenticación en este ticket, como el resto de la API. La pertenencia la aplica el filtro global de EF sobre `IUsuarioActual`, igual que el listado (FR-05/AC-10). El ticket de autenticación reemplazará esa abstracción sin tocar este endpoint |
| 200 | `ResumenMensualDto` — también cuando el mes está vacío |
| 500 | `ProblemDetails` RFC 9457 con `traceId` y sin detalle interno, emitido por el manejador global |

No hay 400 porque no hay entrada que validar, ni 404 porque el recurso siempre existe: un mes sin
movimientos es un resumen en cero, no un recurso ausente.

**Structure of the response**

`ResumenMensualDto` lleva: `mes` (1–12), `anio`, `totalIngresado`, `totalGastado`, `balance` y
`desglose`, una lista de `{ categoriaId, categoriaNombre, total }`. Los montos son `decimal`, nunca
`double`: los montos se persisten en decimal exacto por NFR-03 de FEAT-001a y la agregación conserva
ese tipo.

**Input validation**

No hay entrada del usuario: el endpoint no toma parámetros de ruta, de query ni de cuerpo. Esto es
deliberado y es lo que hace que FR-03 esté garantizado por el contrato y no por la buena conducta del
cliente. Un parámetro de período convertiría este endpoint en otra cosa y está fuera de alcance por
PRD.

**Error handling**

- Un mes sin movimientos **no es un error**: es un 200 con los tres totales en cero y el desglose
  vacío (AC-02). Devolver 404 sería confundir "no hay datos" con "no existe el recurso".
- El fallo de base se propaga al manejador global, que lo convierte en `ProblemDetails` 500 con
  `traceId` y sin detalle interno, como el resto de la API.

**Required tests**

- [ ] `Resumen_ConIngresosYGastos_DevuelveLosTresTotales` — valida AC-01
- [ ] `Resumen_SinMovimientosEnElMes_DevuelveCerosYDesgloseVacio` — valida AC-02, sad path
- [ ] `Resumen_ConGastosMayoresQueIngresos_DevuelveBalanceNegativo` — valida AC-03 del lado del contrato
- [ ] `Resumen_ConVariasCategorias_DevuelveUnTotalPorCategoriaConGastos` — valida AC-04
- [ ] `Resumen_ConCategoriaSinGastosEnElMes_NoLaIncluyeEnElDesglose` — valida AC-04, la mitad que se olvida
- [ ] `Resumen_LaSumaDelDesglose_EsIgualAlTotalGastado` — valida AC-05, la identidad que evita dos consultas desincronizadas
- [ ] `Resumen_ConMovimientosDeOtrosMeses_LosExcluye` — valida AC-07
- [ ] `Resumen_ConMovimientosElPrimeroYElUltimoDiaDelMes_LosIncluye` — valida AC-08, los dos extremos
- [ ] `Resumen_DeOtroPropietario_NoEntraEnLosTotales` — valida AC-10
- [ ] `Resumen_NoDevuelveLaListaDeMovimientos` — valida AC-12 sobre la forma de la respuesta
- [ ] `Resumen_EmiteLaAgregacionEnSql` — **NFR-02, capa estructural**: con `ObservadorDeSql`, el SQL contiene `SUM(` y `GROUP BY` y no un `SELECT` de filas de movimientos
- [ ] `Resumen_DevuelveElMesYElAnioDelPeriodo` — valida AC-09 del lado del contrato
- [ ] `Resumen_ConLaBaseCaida_Devuelve500SinDetalleInterno` — **sad path del error que este bloque
      documenta**: el 500 sale como `ProblemDetails` con `traceId` y sin `stackTrace`, sin `   at `
      ni el mensaje del proveedor. Se apoya en `ApiFactory.CadenaHaciaUnPuertoCerrado`, el mismo
      recurso que usa `Tests/Infra/ManejoDeErroresTests.cs`
- [ ] `RangoDelMes_DevuelveElPrimeroYElUltimoDiaDelMes` — unitario, sin base
- [ ] `RangoDelMes_EnDiciembre_NoSeCorreAlAnioSiguiente` — unitario, el caso de borde del cambio de año
- [ ] `RangoDelMes_EnFebreroDeAnioBisiesto_TerminaEl29` — unitario, el otro borde

**Mutaciones a registrar en el reporte TDD (mitigación R-24)**

No alcanza con que los tests estén verdes: el reporte TDD del bloque tiene que registrar que
**agregar `IgnoreQueryFilters()` a la consulta pone en rojo**
`Resumen_DeOtroPropietario_NoEntraEnLosTotales`. El motivo es el que da el threat model: una fuga por
agregación no se ve —no aparece una fila de más, aparece un número más grande—, así que el test verde
por sí solo no prueba que esté mordiendo.

**Completion criterion**

Los 16 tests pasan; `GET /api/resumen` devuelve los tres totales y el desglose del mes en curso del
propietario; `Resumen_EmiteLaAgregacionEnSql` demuestra que la suma ocurre en la base; y la mutación
de `IgnoreQueryFilters()` queda registrada como muerta. Sin esas dos últimas evidencias el bloque no
está terminado por más que los números den bien.

---

## Block 2 — Backend: rendimiento de la pantalla principal

**Files**

- `backend/GestionGastos.Api.Tests/Infra/MedicionDeRendimiento.cs` (modified) — una constante de presupuesto propia para la pantalla completa.
- `backend/GestionGastos.Api.Tests/Resumen/RendimientoResumenTests.cs` (new)

**Logic**

AC-11 mide **la pantalla principal**, es decir el listado y el resumen juntos, sobre 100 ejecuciones
en una cuenta con 1000 movimientos, con p95 por debajo de 2 s. La constante `PresupuestoP95` que ya
existe vale 1 s y es la del listado solo: reusarla mediría otra cosa. Se agrega
`PresupuestoP95Pantalla = TimeSpan.FromSeconds(2)` junto a ella, con el comentario que dice por qué
son dos y no una.

`MedicionDeRendimiento` se reutiliza tal cual para el resto: `MovimientosSembrados`, `Ejecuciones`,
`Percentil` y `SembrarMovimientosAsync` no cambian.

**Error handling**

No aplica: es una medición, no un camino de usuario.

**Required tests**

- [ ] `Pantalla_ConMilMovimientos_ListadoYResumenRespondenBajoDosSegundos` — valida AC-11 y NFR-01, midiendo las dos peticiones que la pantalla hace
- [ ] `Resumen_ConMilMovimientos_NoMaterializaFilasDeMovimientos` — la otra mitad de NFR-02: con 1000 movimientos sembrados, la respuesta sigue trayendo a lo sumo una fila por categoría

**Completion criterion**

Los 2 tests pasan y el p95 medido queda registrado en el reporte TDD, para que la próxima vez que
alguien discuta el presupuesto tenga el número y no una impresión.

---

## Block 3 — Frontend: cliente HTTP y tipo del contrato

**Files**

- `frontend/src/api/tipos.ts` (modified) — `ResumenMensual` y `CategoriaConTotal`, espejando el DTO del backend.
- `frontend/src/api/cliente.ts` (modified) — `obtenerResumen()`.
- `frontend/src/api/cliente.test.ts` (modified)

**Logic**

`obtenerResumen()` no toma parámetros —el período es fijo— y pasa por el helper `pedir<T>`, como
`obtenerCategorias` y `obtenerMovimientos`, para heredar el mismo manejo de errores tipados sin una
segunda implementación. Los tipos van en `tipos.ts` con el comentario de cabecera que ese archivo ya
exige: documentan que espejan el DTO real del backend.

**Input validation**

No hay entrada. La función no construye query string.

**Error handling**

Los mismos tres errores tipados que el resto del cliente: `ErrorDeValidacion` no aplica acá (no hay
entrada que validar), `ErrorDelServidor` para el 500 y `ErrorDeRed` para la caída de red. No se
agrega ningún tipo de error nuevo.

**Required tests**

- [ ] `ObtenerResumen_DevuelveLosTotalesYElDesglose` — happy path
- [ ] `ObtenerResumen_ConMesVacio_DevuelveCerosYDesgloseVacio` — el contrato del mes sin movimientos
- [ ] `ObtenerResumen_ConErrorDelServidor_LanzaErrorDelServidorConTraceId` — sad path
- [ ] `ObtenerResumen_ConRedCaida_LanzaErrorDeRed` — sad path

**Completion criterion**

Los 4 tests pasan y `obtenerResumen` sigue la misma convención que sus hermanas, sin una segunda
implementación del manejo de errores.

---

## Block 4 — Frontend: el resumen en la pantalla principal

**Files**

- `frontend/src/resumen/ResumenDelMes.tsx` (new) — el componente.
- `frontend/src/resumen/ResumenDelMes.test.tsx` (new)
- `frontend/src/App.tsx` (modified) — monta el resumen y le pasa la señal de refresco.
- `frontend/src/App.test.tsx` (modified) — **el doble de servidor tiene que atender `/api/resumen`**.

**Logic**

**El disparador es `version`, no `filtros`.** Este es el punto donde AC-06 se gana o se pierde. `App`
ya mantiene dos estados: `filtros`, que cambia cuando el usuario toca los controles de filtro, y
`version`, que se incrementa tras un alta, una modificación, una eliminación o un refresco por 404. El
resumen se suscribe **solo a `version`**. Cablearlo a `filtros` violaría AC-06; no cablearlo a nada lo
dejaría desactualizado tras un alta. La spec lo fija explícitamente porque las dos formas de
equivocarse son fáciles y ninguna se nota mirando la pantalla una sola vez.

**El rótulo del mes sale de la respuesta**, no de `mesActual.ts`. El componente recibe `mes` y `anio`
del endpoint y los formatea; `mesActual.ts` se conserva para los filtros, que es su uso actual. Así el
número que se muestra y el número que se calculó vienen de la misma fuente y no pueden discrepar en el
cambio de mes.

**Cómo se distingue el balance negativo (AC-03).** El PRD pide "distinguible del positivo" sin decir
con qué, y esa vaguedad quedó anotada en la validación del PRD. Se fija acá: el balance negativo lleva
**el signo menos explícito en el texto** —que es lo que hace la distinción verificable por test y
disponible para un lector de pantalla— **más** una clase CSS propia para el color. El color solo no
alcanzaría: sería inaccesible para quien no lo distingue, y no habría forma de asertarlo sin depender
de estilos. El test asserta sobre el texto, no sobre la clase.

**Input validation**

El componente no acepta entrada del usuario: no tiene controles. Solo muestra.

Los nombres de categoría se interpolan como **texto JSX**, que React escapa. A diferencia de la nota
del movimiento (R-06 en `a`, R-23 en `b`), acá no se renderiza ningún texto que el usuario haya
escrito: el catálogo de categorías es global y sembrado. Aun así `dangerouslySetInnerHTML` sigue
prohibido, y desde `b` esa prohibición está fijada por una regla de ESLint para todo el proyecto —no
por disciplina de quien escribe el componente— (mitigación 9 del threat model).

**Error handling**

- `ErrorDelServidor` o `ErrorDeRed` al cargar → el resumen muestra un mensaje con reintento, **sin
  tumbar el listado**: son dos peticiones independientes y que falle una no debe vaciar la otra.
- Un mes vacío no es un error: ceros y un texto que dice que no hay gastos para desglosar (AC-02).

**Required tests**

- [ ] `Resumen_MuestraLosTresTotalesYElMes` — valida AC-01 y AC-09
- [ ] `Resumen_ConBalanceNegativo_LoMuestraConSigno` — valida AC-03 con el criterio que esta spec fija
- [ ] `Resumen_MuestraElDesglosePorCategoria` — valida AC-04
- [ ] `Resumen_SinGastosEnElMes_DiceQueNoHayNadaQueDesglosar` — valida AC-02, sad path
- [ ] `Resumen_ConRedCaida_MuestraElErrorConReintento` — sad path
- [ ] `Resumen_AlFiltrarElListado_NoCambia` — **valida AC-06 montando `App`**, ejerciendo la costura real: se cambia el filtro y se comprueba que el resumen no se vuelve a pedir ni cambia sus números
- [ ] `Resumen_TrasUnAlta_SeActualiza` — la otra mitad del disparador, montando `App`
- [ ] `Resumen_ConErrorDelServidor_NoTumbaElListado` — las dos peticiones son independientes

**Completion criterion**

Los 8 tests pasan; el resumen aparece en la pantalla principal, se refresca tras un alta y **no** se
mueve al filtrar; y `App.test.tsx` sigue verde en su totalidad —lo que exige haber agregado el
handler de `/api/resumen` al doble de servidor, sin el cual todos sus tests rompen—.

---

## Final verification

- Los 5 FR y los 2 NFR del PRD tienen cobertura, y los 12 AC tienen al menos un test que los nombra.
- La suite completa pasa. **El conteo se reporta contando la suite real, no sumando estimaciones**:
  la aritmética escrita en la spec de FEAT-001b resultó falsa (decía 186, había 217) y quedó como
  errata. Acá se declara el criterio en lugar del número: la corrida de cierre de CODE informa el
  total observado.
- El SQL emitido por el resumen contiene la agregación, verificado con `ObservadorDeSql`. Sin esa
  evidencia NFR-02 no está cumplido, por más que los números den bien.
- El rango del mes se fija en el servidor, y no hay ningún parámetro de período en el endpoint.
- El resumen se suscribe a `version` y no a `filtros`, verificado montando `App`.
- El balance negativo se distingue por signo en el texto, no solo por color.
- `App.test.tsx` atiende `/api/resumen` en su doble de servidor.
- Lint y format limpios en frontend; `dotnet build` sin warnings nuevos.

## Erratas heredadas de FEAT-001b, aplicadas en este PLAN

La spec de `b` solo se puede editar en PLAN, y el grafo de transiciones no tiene arista desde CODE ni
VERIFY hacia esa fase. Las dos erratas que `b` dejó anotadas se resuelven acá:

| # | Ubicación | Estado |
|---|-----------|--------|
| 1 | `spec-FEAT-001b.md:283` — la firma escrita `obtenerMovimientos(filtros?, senal?: AbortSignal)` incluye un `senal?` que no existe en el código | Se corrige en este PLAN |
| 2 | `spec-FEAT-001b.md`, "Final verification" — "129 + 57 = 186 tests" cuando la suite real tiene 217 | Se corrige en este PLAN |

## Deuda heredada que NO se toma en este ticket

`FiltrosMovimientos` y `FormularioMovimiento` piden cada uno `GET /api/categorias` al montar. Subir la
carga del catálogo a `App` y bajarla como prop lo eliminaría, pero cambia la interfaz pública de dos
componentes y obliga a reescribir sus fixtures. **Este ticket agrega una petición más a la pantalla,
no la quita**, así que el refactor se vuelve más atractivo, no menos — pero sigue siendo transversal y
ajeno a "el resumen del mes". Queda anotado para un ticket propio.

Igual pasa con los dos componentes por debajo del 80% de ramas (`FiltrosMovimientos.tsx` 73.33%,
`ConfirmarEliminacion.tsx` 75%): no se tocan acá, y la decisión de subirlos o aceptarlos sigue
pendiente.
