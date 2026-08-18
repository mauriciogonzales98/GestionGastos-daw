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
