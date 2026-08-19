# Evidencia TDD — FEAT-001b

| Campo | Valor |
|---|---|
| Ticket | FEAT-001b |
| Spec | `docs/daw/specs/spec-FEAT-001b.md` |
| Fase | CODE |

**Por qué existe este archivo.** La regla de testing exige que cada test falle **antes** de la
implementación, y el verificador de módulo lo comprueba. Pero la evidencia la produce el agente
implementador en su reporte, que no queda en ningún lado: el verificador solo ve el repo, así que
estructuralmente no puede confirmar test-first por buena que haya sido la práctica. En el Block 1 la
evidencia sobrevivió por casualidad, narrada en el mensaje del commit; en el Block 2 la verificación
corrió antes de commitear y el verificador devolvió BLOCKED por ausencia de evidencia, con el código
impecable.

Este archivo le da domicilio a esa evidencia. Se actualiza al cerrar cada bloque, antes de la
revisión.

---

## Block 1 — Backend: filtros del listado

**13 tests planificados. Primera corrida: 10 en rojo, 3 en verde.**

Los tres verdes **no mordían**: afirmaban algo que ya era cierto sin filtros. Se endurecieron hasta
volverlos rojos *antes* de tocar producción, conservando los nombres exigidos por la spec.

| Test | Aserción que rompía |
|---|---|
| `Filtrar_PorCategoria_DevuelveSoloEsaCategoria` | `Expected: 2 / Actual: 3` |
| `Filtrar_PorRango_ExcluyeLoDeAfuera` | `Assert.Single()` — la colección tenía 3 |
| `Filtrar_PorCategoriaYRango_AplicaLasDosCondiciones` | `Assert.Single()` — la colección tenía 4 |
| `Filtrar_ConDesdePosteriorAHasta_Devuelve400` | `Expected: BadRequest / Actual: OK` |
| `Filtrar_ConFechaMalFormada_Devuelve400ConElCampo` | `Expected: BadRequest / Actual: OK` |
| `Filtrar_ConCategoriaInexistente_DevuelveListadoVacio` | `Assert.Empty()` — no estaba vacía |
| `Filtrar_ElTotalYElRecorte_SeCuentanSobreLoFiltrado` | `Expected: 5 / Actual: 500` |
| `Filtrar_EmiteElWhereEnSql` | `Assert.Single()` — 2 sentencias |
| `Listar_ConParametrosDesconocidos_LosIgnora` | `Expected: BadRequest / Actual: OK` |
| `Listado_ConMilMovimientos_RespondeBajoUnSegundo` | total del rango vs. 1000 sin filtrar |

Los tres endurecidos, ya en rojo y todavía sin implementación:

| Test | Aserción que rompía tras endurecerlo |
|---|---|
| `Filtrar_SinCategoria_DevuelveTodasLasCategorias` | `Expected: 3 / Actual: 4` (se le agregó un rango, para que omitir un filtro no arrastre a los otros) |
| `Filtrar_PorRango_IncluyeAmbosExtremos` | colecciones distintas: aparecían `dia-siguiente` y `vispera` (se sembraron los vecinos inmediatos de cada extremo) |
| `Listar_ConFalloDeBase_DevuelveProblemDetailsCon500` | `Assert.Single()` — 2 sentencias (se le agregó la precondición de que la query sana sí filtró) |

**Después: 13/13 en verde.** Suite backend 99/99.

### Corrección posterior — `Filtrar_ConFechaVacia_LaTrataComoAusente`

Cubre una decisión de contrato que había quedado sin test: `?desde=` vacío se trata como **ausente**,
no como formato inválido. **Pasó de entrada**, porque `FiltrosDeListado` ya contemplaba el caso. Como
un test que no puede fallar no prueba nada, se validó con tres mutaciones de `ValidarFecha`, cada una
rompiendo una aserción distinta:

| Mutación | Aserción que falló |
|---|---|
| `IsNullOrWhiteSpace(fecha)` → `fecha is null` | `?desde=&hasta=` → `Expected: OK / Actual: BadRequest` |
| `IsNullOrWhiteSpace` → `IsNullOrEmpty` | `?desde=%20` → `Expected: OK / Actual: BadRequest` |
| blanco → `return FechaMaxima` | listado completo → `Expected: 2 / Actual: 0` |

`FiltrosDeListado.cs` quedó restaurado byte a byte, verificado con `diff` contra la copia previa.

---

## Block 2 — Backend: modificación (`PUT /api/movimientos/{id}`)

**13 tests planificados (19 casos con los `[Theory]`). Primera corrida: 19/19 en rojo.**

Todos fallaron con `Actual: MethodNotAllowed` — la ruta no existía todavía —, con el `Expected` propio
de cada uno:

| Test | Expected |
|---|---|
| `Modificar_ElMonto_LoRefleja` | `OK` |
| `Modificar_CategoriaYFecha_PersisteAmbas` | `OK` |
| `Modificar_LaNota_LaRefleja` | `OK` |
| `Modificar_BorrandoLaNota_GuardaNull` (×3: `null`, `""`, `"   "`) | `OK` |
| `Modificar_ConMontoInvalido_Devuelve400YNoAltera` (×2: `0`, `-10.5`) | `BadRequest` |
| `Modificar_ConMontoDeTresDecimales_Devuelve400YNoAltera` | `BadRequest` |
| `Modificar_SinCategoria_Devuelve400YNoAltera` (×3: omitida, nula, cero) | `BadRequest` |
| `Modificar_ConCategoriaDeOtroTipo_Devuelve400YNoAltera` | `BadRequest` |
| `Modificar_ConCategoriaInexistente_Devuelve400YNoAltera` | `BadRequest` |
| `Modificar_ConNotaDemasiadoLarga_Devuelve400YNoAltera` | `BadRequest` |
| `Modificar_Inexistente_Devuelve404` | `NotFound` |
| `Modificar_DeOtroPropietario_Devuelve404` | `NotFound` |
| `Modificar_ConCuerpoQueTraeUsuarioId_LoIgnora` | `OK` |
| `Modificar_ConMontoNoNumerico_Devuelve400ConElCampo` | `BadRequest` |

**Después: 19/19 en verde.** Suite backend 119/119, 0 warnings.

`Modificar_ConMontoDeTresDecimales_Devuelve400YNoAltera` es un test **extra**, no un renombre: los 13
nombres que la spec exige están todos presentes, verificado por el verificador de módulo.

### Mutaciones que prueban que los tests muerden

| Mutación | Test que la atrapa | Salida |
|---|---|---|
| Quitar `[property: JsonConverter(typeof(MontoJsonConverter))]` de `ModificarMovimientoRequest.Monto` | `Modificar_ConMontoNoNumerico_Devuelve400ConElCampo` | *"El ProblemDetails no trae la extensión 'errors'"* — el 400 se degrada a genérico sin campo |
| Asignar los campos y `SaveChangesAsync` **antes** del chequeo de tipo cruzado | `Modificar_ConCategoriaDeOtroTipo_Devuelve400YNoAltera` | `AssertIntactoAsync`: `Expected 1234.56 / Actual 99.00` |

La primera es exactamente el fallo silencioso que la auditoría de arquitectura anticipó en PLAN, dos
fases antes de que existiera el código. La segunda demuestra que la mitad de AC-04 que suele quedar
decorativa —"deja el movimiento con todos sus valores anteriores"— acá sí verifica.

Ambas mutaciones fueron revertidas.

---

## Block 3 — Backend: eliminación (`DELETE /api/movimientos/{id}`)

**5 tests planificados. Primera corrida: 5/5 en rojo.**

Todos fallaron con `Actual: MethodNotAllowed` — la ruta `/api/movimientos/{id:int}` ya existía para
`GET` y `PUT`, pero no para `DELETE` —, con el `Expected` propio de cada uno:

| Test | Aserción que rompía |
|---|---|
| `Eliminar_UnMovimientoPropio_Devuelve204` | `Expected: NoContent / Actual: MethodNotAllowed` |
| `Eliminar_UnMovimientoPropio_DejaDeAparecerEnElListado` | `Expected: NoContent / Actual: MethodNotAllowed` |
| `Eliminar_DosVeces_DevuelveNotFoundLaSegunda` | `Expected: NoContent / Actual: MethodNotAllowed` (la primera eliminación) |
| `Eliminar_Inexistente_Devuelve404` | `Expected: NotFound / Actual: MethodNotAllowed` |
| `Eliminar_DeOtroPropietario_Devuelve404YNoLoBorra` | `Expected: NotFound / Actual: MethodNotAllowed` |

Que el 405 sea el desenlace inicial no vuelve a los dos tests de 404 complacientes: ninguno se
conforma con el estado. `Eliminar_Inexistente_Devuelve404` exige además `application/problem+json` y
el `title` igual a `TituloNoEncontrado`, que es lo que distingue el 404 del endpoint del que produce
el ruteo cuando no hay ruta.

**Después: 5/5 en verde.** Suite backend 124/124, 0 warnings.

### Mutaciones que prueban que los tests muerden

| Mutación | Test que la atrapa | Salida |
|---|---|---|
| `.IgnoreQueryFilters()` en la lectura previa (lo que ADR-003 prohíbe y R-15 mitiga) | `Eliminar_DeOtroPropietario_Devuelve404YNoLoBorra` | `Expected: NotFound / Actual: NoContent` — la fila ajena se borraba |
| Quitar `datos.Movimientos.Remove(movimiento)` dejando el 204 y el `SaveChangesAsync` | `Eliminar_UnMovimientoPropio_DejaDeAparecerEnElListado` (y otros 2) | `Assert.DoesNotContain() Failure: Item found in collection` — el id borrado seguía en el listado |

La primera es el riesgo crítico del threat model: un borrado que responde igual de bien pero pisa
filas de otro propietario. La segunda cubre la mitad de AC-05 que se suele dar por sentada —"dejar de
devolverlo en consultas posteriores"—: un 204 mentiroso, sin baja real, no pasa.

Ambas mutaciones fueron revertidas; `MovimientosEndpoints.cs` quedó restaurado desde la copia previa
a mutar y verificado con `git diff --stat` (34 inserciones, ninguna eliminación).

### Revisión de comentarios del archivo tocado

`MovimientosEndpoints.cs` es el único archivo de producción modificado. Se revisaron sus 20
comentarios uno por uno contra el estado posterior al `DELETE`:

- `TituloNoEncontrado` — sigue exacto: describe *por qué* el título es el mismo para el inexistente y
  el ajeno, y no enumera los handlers que lo usan, así que sumar el tercero no lo desactualiza.
- `TechoDeItems`, y los de `CrearAsync`, `ListarAsync` (incluido el `remarks` sobre los desenlaces
  del contrato del listado), `ModificarAsync`, `ObtenerPorIdAsync` y `ADto` — hablan de sus propios
  handlers; el `DELETE` no cambia nada de lo que afirman.

No se encontró ningún comentario que quedara mintiendo. Los cuatro comentarios nuevos son los del
handler `EliminarAsync`.

---

## Block 4 — Frontend: cliente HTTP y tipos

**8 tests exigidos por la spec + 3 extra (deuda heredada y decisiones propias). Primera corrida: 11
en rojo, 5 en verde** — los 5 verdes son los que `cliente.test.ts` ya traía de FEAT-001a, que este
bloque conserva sin tocar su intención.

| Test | Aserción que rompía |
|---|---|
| `obtenerMovimientos_ConFiltros_ArmaLaQueryString` | `Expected: "/api/movimientos?categoriaId=3&desde=2026-08-01&hasta=2026-08-31" / Received: "/api/movimientos"` |
| `obtenerMovimientos_SinFiltros_NoMandaParametrosVacios` | `Expected: "/api/movimientos?desde=2026-08-01" / Received: "/api/movimientos"` |
| `obtenerMovimientos_ConCadenasVacias_LasOmite` (extra) | `Expected: "/api/movimientos?categoriaId=5" / Received: "/api/movimientos"` |
| `obtenerMovimientos_ConRangoInvertido_LanzaErrorDeValidacionConElCampo` (extra) | `Expected: "/api/movimientos?desde=2026-08-31&hasta=2026-08-01" / Received: "/api/movimientos"` |
| `modificarMovimiento_MandaPutConElCuerpo` | `TypeError: modificarMovimiento is not a function` |
| `eliminarMovimiento_MandaDeleteYNoLeeCuerpo` | `TypeError: eliminarMovimiento is not a function` |
| `cliente_Ante404_LanzaErrorNoEncontrado` | `TypeError: modificarMovimiento is not a function` |
| `cliente_Ante500_SigueLanzandoErrorDelServidor` | `TypeError: modificarMovimiento is not a function` |
| `cliente_AnteRedCaida_LanzaErrorDeRed` | `TypeError: modificarMovimiento is not a function` |
| `cliente_Ante400ConErrors_LanzaErrorDeValidacion` | `TypeError: modificarMovimiento is not a function` |
| `cliente_ConCuerpoDeErrorQueNoEsJson_LanzaErrorDelServidorGenerico` (extra, deuda) | `TypeError: eliminarMovimiento is not a function` |

### Dos tests que pasaron de entrada y hubo que endurecer

La primera corrida real dio **9 en rojo y 7 en verde**: dos de los tests nuevos pasaban contra el
código viejo, y pasaban por el peor motivo — el cliente ignoraba el argumento de filtros por
completo, así que "no mandó parámetros vacíos" era cierto sin que nada estuviera implementado.

- `obtenerMovimientos_ConCadenasVacias_LasOmite` pedía `{ desde: '', hasta: '' }` y esperaba
  `/api/movimientos`. Se endureció agregando `categoriaId: 5` al mismo filtro: ahora exige
  `/api/movimientos?categoriaId=5`, que un cliente que ignore los filtros no puede producir.
- `obtenerMovimientos_ConRangoInvertido_LanzaErrorDeValidacionConElCampo` solo miraba el error
  devuelto, que ya existía desde FEAT-001a. Se endureció asertando además la URL pedida, para que
  quede atado a que el rango **llegó a viajar**.

Con las dos endurecidas, la corrida previa a implementar quedó en **11 en rojo / 5 en verde**.

**Después: 16/16 en verde.** Suite frontend completa 53/53 (6 archivos), `tsc --noEmit` sin errores,
`eslint` y `prettier --check` limpios.

### Mutaciones que prueban que los tests muerden

| Mutación | Test que la atrapa | Salida |
|---|---|---|
| `leerProblema` sin `try/catch`: parsear el cuerpo del error directo | `cliente_ConCuerpoDeErrorQueNoEsJson_LanzaErrorDelServidorGenerico` | `AssertionError: expected SyntaxError: Unexpected token '<', "<html… to be an instance of ErrorDelServidor` |
| `eliminarMovimiento` pasando por `pedir` (que lee el cuerpo) en vez de `enviar` | `eliminarMovimiento_MandaDeleteYNoLeeCuerpo` | `promise rejected "SyntaxError: Unexpected end of JSON input" instead of resolving` |
| `agregarSiTieneValor` sin la guarda: la clave ausente viaja vacía | `obtenerMovimientos_SinFiltros_NoMandaParametrosVacios` (+2) | `Expected: "/api/movimientos" / Received: "/api/movimientos?categoriaId=&desde=&hasta="` |
| Quitar la rama del 404 de `enviar` | `cliente_Ante404_LanzaErrorNoEncontrado` | `expected ErrorDelServidor: Movimiento no encontrado { traceId: null } to be an instance of ErrorNoEncontrado` |

La primera es **el mutante sobreviviente que FEAT-001a dejó documentado**: con el `try/catch`
borrado, los 5 tests que el archivo ya tenía seguían los 5 en verde —por eso sobrevivía— y solo cae
con un cuerpo de error que no es JSON, que es el 502 de un proxy que ninguno simulaba. Las cuatro
mutaciones fueron revertidas y `cliente.ts` se restauró desde la copia previa a mutar.

### Deuda heredada de FEAT-001a, saldada

1. **Mutante en `leerProblema`** — muerto por
   `cliente_ConCuerpoDeErrorQueNoEsJson_LanzaErrorDelServidorGenerico` (evidencia arriba).
2. **`AbortSignal` que ningún llamador pasaba** — se **eliminó** de `obtenerCategorias` y no se
   agregó a `obtenerMovimientos`. Ningún llamador lo pasaba en FEAT-001a y ninguno de los que este
   ticket agrega lo necesita: los dos únicos consumidores (`ListadoMovimientos` y
   `FormularioMovimiento`) cargan al montar y no compiten consigo mismos. Un parámetro que nadie usa
   no se puede verificar —no hay test que lo ejerza sin inventarle un llamador— y aparenta una
   cancelación que el cliente no hace. Cuando alguien la necesite de verdad, se agrega junto con el
   llamador que la pasa, en una línea. Es una **desviación declarada** de la firma literal que la
   spec escribe en el Block 4; los 8 tests exigidos y el criterio de cierre no la mencionan.
3. **Cuarta copia del helper `json`** — `cliente.test.ts` usa ahora el `json` de `src/test/infra.ts`,
   como los otros tres archivos de test. Quedan dos `new Response(...)` construidos en línea, que no
   son copias del helper sino los dos casos que el helper no puede dar: el 204 **sin cuerpo** y el
   502 con cuerpo HTML.

### Revisión de comentarios de los archivos tocados

- `tipos.ts`, cabecera del contrato HTTP: decía que los tipos son "los que el backend emite de
  verdad". Con `FiltrosDeMovimientos` eso pasaba a ser **falso** —esos tres valores no son un tipo
  del backend ni viajan en un cuerpo, sino en la query string—, así que se agregó la excepción
  explícita. Los comentarios de `CrearMovimientoRequest`, `MovimientoDto`, `CategoriaDto` y
  `ListadoMovimientosResponse` siguen exactos: el `PUT` no cambió ninguno de esos contratos.
- `cliente.ts`, comentario de `traducirErrores`: enumeraba las claves que emite el servidor
  (`monto`, `categoriaId`, `fecha`, `nota`, `tipoEsperado`). Los filtros agregan `desde` y `hasta`,
  con lo cual la enumeración quedaba **incompleta**; se completó separando las del alta de las del
  listado.
- `cliente.ts`, comentario de `ErrorDelServidor` ("fallo del lado del servidor"): era el que volvía
  ambiguo al 404. No hubo que corregirlo, porque el 404 dejó de caer ahí; el porqué quedó escrito en
  el comentario nuevo de `ErrorNoEncontrado`.
- `cliente.ts`, comentario de `leerProblema` y el del `catch` de red: siguen describiendo lo que
  hacen. El de red se movió con el `try/catch` de `pedir` a `enviar`, sin cambiar una palabra.

---

## Block 5 — Frontend: controles de filtro

**10 tests exigidos por la spec + 3 extra. Primera corrida: 14 en rojo, 50 en verde** — los 50
verdes son los que la suite ya traía de FEAT-001a y del Block 4, que este bloque conserva. El
archivo `mesActual.test.ts` ni siquiera llegó a ejecutarse: sus 3 tests fallaron en la resolución
del import, que es la forma que toma "el módulo todavía no existe".

| Test | Aserción que rompía |
|---|---|
| `mesActual_DevuelveElPrimeroYElUltimoDiaDelMes` | `Error: Failed to resolve import "./mesActual" from "src/movimientos/mesActual.test.ts". Does the file exist?` |
| `mesActual_ElUltimoDiaPorLaNoche_NoSeCorreAlMesSiguiente` (extra) | ídem — el archivo entero contó como `(0 test)` |
| `mesActual_SinArgumento_UsaLaFechaDelSistema` (extra) | ídem |
| `Filtros_AlAbrir_PideElMesActual` | `expected '/api/movimientos' to be '/api/movimientos?desde=2026-03-01&hasta=2026-03-31'` |
| `Filtros_AlElegirCategoria_LaPasaAlCliente` | `TestingLibraryElementError: Unable to find a label with the text of: Filtrar por categoría` |
| `Filtros_AlAplicarUnRango_LoPasaAlCliente` | `Unable to find a label with the text of: Desde` |
| `Filtros_AlAplicarCategoriaYRango_PasaLosDos` | `Unable to find a label with the text of: Filtrar por categoría` |
| `Filtros_ConDesdePosteriorAHasta_MuestraElMotivoYNoPide` | `Unable to find a label with the text of: Desde` |
| `Filtros_ConRangoInvalido_MantieneElListadoAnterior` | `Unable to find a label with the text of: Desde` |
| `Filtros_ConRechazoDelServidor_MuestraElMensajePorCampo` | `Unable to find a label with the text of: Desde` |
| `Listado_MuestraElRangoVigente` | `Unable to find an element with the text: Mostrando movimientos del 01/08/2026 al 31/08/2026.` |
| `Listado_MuestraGastosEIngresos_OrdenadosPorFecha` (reescrito) | `expected [ '/api/movimientos' ] to deeply equal [ '/api/movimientos?desde=2026-08-01&hasta=2026-08-31' ]` |
| `Listado_TrasUnAlta_MuestraElMovimientoNuevo` (reescrito) | `expected [ '/api/movimientos', …(1) ] to deeply equal [ …(2) ]` |
| `Listado_AlCambiarLosFiltros_VuelveAPedirConElRangoNuevo` (extra) | `Unable to find an element with the text: /todavía no hay movimientos/i` — el listado no reaccionaba al cambio de filtros |
| `Listado_SinMovimientos_MuestraEstadoVacio` (reescrito) | `Unable to find an element with the text: Mostrando movimientos del 01/08/2026 al 31/08/2026.` |
| `App_TrasUnAltaDeGasto_ElListadoMuestraElMovimientoNuevo` (reescrito) | `expected [ '/api/movimientos' ] to deeply equal [ Array(1) ]` |
| `App_TrasUnAltaDeIngreso_ElListadoMuestraElMovimientoNuevo` (reescrito) | ídem |

### Dos tests que pasaron de entrada y hubo que endurecer

- `Filtros_SinTocarLaCategoria_NoMandaCategoriaId` (extra, AC-08) afirmaba
  `expect(url).not.toContain('categoriaId')`, que era cierto **porque el listado no mandaba ningún
  filtro**: el mismo agujero que el Block 4 encontró en su propio archivo. Se endureció comparando
  la URL entera contra `/api/movimientos?desde=2026-08-01&hasta=2026-08-31`, que un cliente sin
  filtros no puede producir.
- `Filtros_ConRedCaida_MuestraElErrorConReintento` daba verde contra el manejo de `ErrorDeRed` que
  el listado ya tenía desde FEAT-001a. Se endureció asertando que **las dos** lecturas —la que falla
  y la del reintento— llevan el rango, con lo cual queda atado a este bloque y no al anterior.

Las dos, ya endurecidas, mueren con la mutación 2 de la tabla de abajo.

**Después: 67/67 en verde** (8 archivos, 64 previos + los 3 nuevos de `mesActual` — 14 tests nuevos
en total y 3 archivos de test reescritos). `tsc --noEmit` sin errores, `eslint` y `prettier --check`
limpios.

### Mutaciones que prueban que los tests muerden

| Mutación | Test que la atrapa | Salida |
|---|---|---|
| `mesActual` derivando el mes de `ahora.toISOString()` en vez de los componentes locales | `mesActual_ElUltimoDiaPorLaNoche_NoSeCorreAlMesSiguiente` | `expected { desde: '2026-09-01', …(1) } to deeply equal { desde: '2026-08-01', …(1) }` |
| `ListadoMovimientos` llamando `obtenerMovimientos()` sin los filtros | **12 tests**, entre ellos `Filtros_AlAbrir_PideElMesActual` | `expected '/api/movimientos' to be '/api/movimientos?desde=2026-03-01&hasta=2026-03-31'` |
| `aplicar()` sin la guarda de rango invertido: la petición sale igual | `Filtros_ConDesdePosteriorAHasta_MuestraElMotivoYNoPide` y `Filtros_ConRangoInvalido_MantieneElListadoAnterior` | `expected '' to be 'La fecha de inicio no puede ser poste…'` |

**Un mutante que sobrevivió, y por qué no importa.** La primera mutación probada fue
`new Date(anio, mes, 1).toISOString().slice(0, 10)` —el `toISOString()` clásico— y quedó viva:
67/67 en verde. No es un agujero de los tests sino un **mutante equivalente en este huso**. El
`Date` construido es la medianoche local; en UTC-3 pasar a UTC suma 3 horas y el día no se mueve.
El desplazamiento aparece con husos positivos, o —y esto sí ocurre acá— cuando lo que se convierte
es el instante **actual** en vez de una medianoche construida, que es la mutación que la tabla
registra y que el test de las 23:30 mata. El caso peligroso en UTC-3 es el que está cubierto.

### Decisiones que la spec dejó abiertas

1. **Los filtros se aplican con un botón**, no a cada tecla. Un `<input type="date">` a medio
   completar emite valores intermedios, y pedirle al servidor cada uno sería una petición por dígito
   contra un rango que el usuario todavía no terminó de escribir. AC-12 —"mantener el listado con el
   rango anterior"— además presupone un momento explícito de aplicación: sin él, "el rango anterior"
   no está definido.
2. **`FiltrosMovimientos` carga su propio catálogo de categorías**, como hace `FormularioMovimiento`.
   Levantarlo a `App` para compartir una sola lectura obligaba a tocar `FormularioMovimiento.tsx`,
   que no es un archivo de este bloque. Queda como candidato para cuando el Block 6 vuelva sobre ese
   componente.
3. **El rechazo del servidor sube por `onErroresDeFiltro` hasta `App`**, que lo baja a
   `FiltrosMovimientos`. El error lo detecta quien hace la petición (el listado), pero el control que
   el usuario tiene que corregir vive en el filtro: el estado se sostiene en el padre común, que es
   el único punto desde el que las dos mitades se ven.
4. **Un `ErrorDeValidacion` del listado se muestra sin botón "Reintentar"**, a diferencia de un
   `ErrorDeRed`: reintentar el mismo filtro rechazado daría el mismo rechazo. El camino de salida es
   el mensaje por campo, junto al control.

### Revisión de comentarios de los archivos tocados

- `ListadoMovimientos.tsx`, comentario del efecto: decía *"Recargar cuando `version` cambia (tras un
  alta) es sincronizar con la API"*. Quedaba **incompleto**: ahora también recarga cuando cambian
  los filtros, por la dependencia `cargar`. Se reescribió nombrando los dos disparadores.
- `ListadoMovimientos.tsx`, doc de la prop `version`: seguía siendo exacta y se conservó palabra por
  palabra. La prop no cambió de sentido: un alta no altera el filtro vigente, así que sin ella la
  recarga tras el alta no tendría disparador.
- `ListadoMovimientos.tsx`, comentario *"Mismo patrón que `FormularioMovimiento`: el reset síncrono
  lo hace `reintentar`"*: sigue describiendo lo que el código hace. Sin cambios.
- `App.tsx`, comentario sobre la señal `version`: era el candidato a quedar obsoleto con este
  bloque, porque ahora hay un segundo motivo de recarga. **No lo quedó, pero sí incompleto**: se
  amplió para decir por qué la señal sigue haciendo falta habiendo filtros —un alta no cambia el
  filtro vigente— en vez de dejar al lector suponiendo que es un resto del bloque anterior.
- `ListadoMovimientos.test.tsx`, comentario *"La primera fila es el encabezado"*: sigue exacto. El
  del `rerender` que *"simula el refresco tras un alta exitosa"* también, y se mantiene: ese archivo
  prueba el componente aislado; la costura de verdad la ejercen `App.test.tsx` y
  `FiltrosMovimientos.test.tsx` montando `App`.
- `App.test.tsx`, comentario de `prepararFetch`: decía que enruta *"por endpoint —no por orden de
  llamadas—"*, lo cual seguía siendo cierto pero ya no completo: el doble ahora **honra la query
  string**. Se amplió con esa mitad y con el motivo, que es el defecto que el impact scan detectó.
- `App.test.tsx`, comentario de la cota superior de lecturas: nombraba a `cargar` como *"un
  `useCallback([])`"*, y eso pasó a ser **falso** —ahora depende de `filtros`—. Se corrigió.
- `formato.ts` y `fecha.ts`: no se tocaron, pero se leyeron porque `mesActual` reutiliza
  `hoyComoIso`. Sus dos comentarios sobre por qué no pasan por `toISOString()`/`Date` siguen
  exactos, y son la razón de que `mesActual` delegue en `hoyComoIso` en vez de formatear por su
  cuenta.

### Hallazgo fuera de alcance

`ListadoMovimientos.test.tsx` llamaba `vi.stubGlobal('fetch', …)` en cada test pero su `afterEach`
solo hacía `vi.restoreAllMocks()`, que **no desmonta los globales**: el `fetch` falso sobrevivía al
archivo. No dio problemas porque cada test lo vuelve a pisar, pero es una fuga. Se agregó
`vi.unstubAllGlobals()` a ese `afterEach` —el archivo es de este bloque— y se puso desde el
principio en los dos archivos nuevos. `FormularioMovimiento.test.tsx` tiene la misma omisión y **no
se tocó**: no es un archivo de este bloque.

---

## Errata de la spec — pendiente de aplicar en el PLAN de FEAT-001c

Misma situación que las diez erratas que FEAT-001a heredó a este ticket, y por el mismo motivo
estructural: la spec solo se puede editar en PLAN, y el grafo de transiciones no tiene arista desde
CODE ni VERIFY hacia esa fase. Lo que se descubre implementando espera al PLAN del ticket siguiente.

| # | Ubicación | Edición |
|---|-----------|---------|
| 1 | `spec-FEAT-001b.md:283` | La firma escrita es `obtenerMovimientos(filtros?: FiltrosDeMovimientos, senal?: AbortSignal)`. El `senal?` **no existe** en el código: se eliminó, junto con el de `obtenerCategorias`, como parte de la deuda heredada que el índice del PRD padre declaró candidata para este ticket y que `verify-FEAT-001a.md` (W-VER-01) ya había recomendado borrar. Ningún llamador lo pasaba. La firma real es `obtenerMovimientos(filtros?: FiltrosDeMovimientos)` |

**Verificado en la revisión del Block 4:** la desviación es legítima —mandato explícito, ningún
llamador, y los bloques 5 y 6 no necesitan cancelación porque sus disparadores son un `<select>` y
dos `<input type="date">` que confirman valores completos, no texto tecleado—. Lo que quedó mal es
el documento, no el código.
