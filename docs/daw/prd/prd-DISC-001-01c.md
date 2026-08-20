# PRD DISC-001-01c: Aislamiento entre cuentas verificado

| Field | Value |
|-------|-------|
| Ticket | DISC-001-01c |
| Tracker | ninguno |
| Date | 2026-08-20 |
| PRD loops | 0 |

> Tercero de los ocho PRDs de **DISC-001** (mapa en `docs/daw/discovery/concept-DISC-001.md`),
> recorte de PRD-001 (`docs/daw/prd/PRD.md`, versión 5). La columna "Origen" de cada requerimiento
> traza al RF del PRD del producto.
>
> Último de los tres cortes de la autenticación: `01a` construye identidad y sesión, `01b` agrega el
> límite de intentos, **`01c` (este)** verifica y cierra el aislamiento entre cuentas. Depende de
> `01a`; no depende de `01b` para funcionar, pero va después por orden acordado.

## Context and Problem

Este es el ticket que hace verificable lo que PRD-001 pide en RF-04 y en AC-06, AC-07 y AC-08: que
cada usuario acceda únicamente a sus propios datos. Hasta `01a` esos criterios no eran observables
—con un solo usuario semilla, un test de aislamiento no puede fallar— y por eso FEAT-001 los dejó
fuera de forma explícita.

**Es más chico de lo que su nombre sugiere, y conviene decir por qué.** Al reemplazar
`IUsuarioActual` en `01a`, el filtro global de consulta que ya existe en `AppDbContext`
(`HasQueryFilter(m => m.UsuarioId == PropietarioRequerido)`) pasa a acotar automáticamente todas las
lecturas al usuario de la sesión. Es decir: buena parte del aislamiento llega **heredada**, sin que
nadie escriba una línea nueva.

El problema es exactamente ese. Llega heredada y **nadie la comprobó nunca con dos cuentas reales**.
Y hay tres huecos conocidos que el filtro no tapa:

1. **El filtro global protege las lecturas, no las escrituras.** No se aplica a INSERT. Cada
   endpoint que escribe tiene que asignar el propietario desde `IUsuarioActual` a mano, y hoy lo
   hace por convención —está documentado en el código como mitigación de R-04—, no por una barrera
   que impida olvidarlo.
2. **El acceso por identificador directo** (`GET`, `PUT` y `DELETE /api/movimientos/{id}`) depende
   de que el filtro esté activo en esa consulta concreta. Un `IgnoreQueryFilters()` puesto por
   cualquier motivo lo desarma sin romper ningún test existente.
3. **Una modificación podría cambiar el propietario** si el cuerpo de la petición llegara a
   influir sobre ese campo.

La superficie a cubrir son los **seis endpoints** que hoy tocan movimientos:
`POST /api/movimientos`, `GET /api/movimientos`, `GET /api/movimientos/{id}`,
`PUT /api/movimientos/{id}`, `DELETE /api/movimientos/{id}` y `GET /api/resumen`.
`GET /api/categorias` queda fuera: hoy el catálogo es global y no tiene propietario. Su aislamiento
aparece recién con las categorías propias, que es el PRD 03.

## Goals

- Que ninguna cuenta pueda leer, modificar ni eliminar datos de otra, ni siquiera indicando su
  identificador directamente.
- Que el aislamiento deje de ser una propiedad heredada y sin comprobar, y pase a estar verificado
  endpoint por endpoint con dos cuentas reales.
- Que al intentar acceder a un dato ajeno el sistema no confirme siquiera que ese dato existe.

## Functional Requirements

- FR-01: El sistema debe acotar toda lectura de movimientos —el listado, el resumen y la consulta por identificador— a los movimientos cuyo propietario es el usuario de la sesión. Origen: RF-04.
- FR-02: El sistema debe denegar la consulta, la modificación y la eliminación de un movimiento cuyo propietario no es el usuario de la sesión, dejando ese movimiento sin cambios. Origen: RF-04.
- FR-03: El sistema debe asignar como propietario de todo movimiento creado al usuario de la sesión, descartando cualquier propietario que venga indicado en el cuerpo de la petición. Origen: RF-04.
- FR-04: El sistema debe conservar el propietario original de un movimiento al modificarlo, descartando cualquier propietario que venga indicado en el cuerpo de la petición. Origen: RF-04.

## Non-Functional Requirements

- NFR-01: El sistema debe responder a la consulta, modificación o eliminación de un movimiento de otro propietario con el mismo código de estado y el mismo cuerpo que emplea para un identificador que no existe, de modo que la respuesta no permita determinar si el movimiento existe. Origen: RF-04.
- NFR-02: La suite de pruebas debe cubrir con al menos un caso de acceso cruzado entre dos cuentas cada uno de los 6 endpoints de movimientos y resumen, es decir el 100 % de esa superficie. Origen: AC-06, AC-07 y AC-08 de PRD-001.

## Acceptance Criteria

- AC-01 (FR-01): WHEN dos cuentas tienen movimientos propios y una de ellas abre el listado, THE sistema SHALL devolver únicamente los movimientos de esa cuenta y ninguno de la otra.
- AC-02 (FR-01): WHEN dos cuentas tienen movimientos propios en el mes en curso y una de ellas abre el resumen, THE sistema SHALL calcular los totales y el desglose únicamente sobre los movimientos de esa cuenta.
- AC-03 (FR-02, NFR-01): IF una cuenta solicita por su identificador un movimiento que pertenece a otra cuenta, THEN THE sistema SHALL denegar la operación y SHALL responder con el mismo código y el mismo cuerpo que ante un identificador inexistente.
- AC-04 (FR-02, NFR-01): IF una cuenta intenta modificar por su identificador un movimiento que pertenece a otra cuenta, THEN THE sistema SHALL denegar la operación, SHALL dejar ese movimiento sin cambios, y SHALL responder con el mismo código y el mismo cuerpo que ante un identificador inexistente.
- AC-05 (FR-02, NFR-01): IF una cuenta intenta eliminar por su identificador un movimiento que pertenece a otra cuenta, THEN THE sistema SHALL denegar la operación, SHALL dejar ese movimiento en la base, y SHALL responder con el mismo código y el mismo cuerpo que ante un identificador inexistente.
- AC-06 (FR-03): IF una cuenta registra un movimiento indicando en el cuerpo de la petición a otra cuenta como propietario, THEN THE sistema SHALL asignar el movimiento a la cuenta de la sesión y SHALL dejar sin cambios el listado de la otra cuenta.
- AC-07 (FR-04): IF una cuenta modifica un movimiento propio indicando en el cuerpo de la petición a otra cuenta como propietario, THEN THE sistema SHALL conservar el propietario original y SHALL dejar sin cambios el listado de la otra cuenta.
- AC-08 (FR-01, FR-02): WHEN una cuenta elimina o modifica un movimiento propio, THE sistema SHALL dejar los movimientos de la otra cuenta con los mismos valores que antes de la operación.
- AC-09 (NFR-02): WHEN se recorre la suite de pruebas, THE sistema SHALL exhibir al menos un caso de acceso cruzado entre dos cuentas para cada uno de los 6 endpoints de movimientos y resumen.
- AC-10 (FR-01): IF una consulta de movimientos se ejecuta con los filtros de consulta desactivados, THEN THE suite SHALL fallar, de modo que desarmar el filtro global no pueda pasar inadvertido.

## Out of Scope

- **Aislamiento de las categorías propias por usuario**: es el PRD 03, que depende de este. Hoy el catálogo de categorías es global y no tiene propietario, así que no hay nada que aislar en `GET /api/categorias`.
- **Compartir movimientos entre cuentas**, cuentas conjuntas o visibilidad parcial: PRD-001 lo deja fuera de alcance de forma explícita.
- **Roles y permisos**: todas las cuentas son iguales y ninguna ve datos de otra.
- **Registro de auditoría de los intentos de acceso cruzado**.
- **Cifrado de los datos en reposo** o cualquier separación física por cuenta (una base o un esquema por usuario).
- Alta, login, sesión y límite de intentos: son `01a` y `01b`, de los que este depende.

## Risks and Mitigations

- **Riesgo: el aislamiento llega heredado, y lo heredado no se revisa.** El filtro global hace que la mayoría de estos criterios pasen sin escribir código nuevo, lo que invita a dar el ticket por hecho sin haberlo comprobado. → Mitigación: NFR-02 y AC-09 convierten la cobertura en un criterio contable —6 endpoints, al menos un caso cruzado cada uno— en vez de en una impresión.
- **Riesgo: un test de aislamiento puede dar verde sin probar nada.** Si las dos cuentas del fixture terminan siendo la misma, o si la segunda no tiene movimientos, el test pasa igual. Es el mismo patrón que en FEAT-001a hizo falta atacar con doble capa en los tests de ordenamiento. → Mitigación: AC-08 exige comprobar el estado de la **otra** cuenta después de cada operación, no solo el resultado de la propia.
- **Riesgo: `IgnoreQueryFilters()` desarma el aislamiento sin romper nada.** Un uso legítimo en una consulta y una copia distraída en otra bastan. → Mitigación: AC-10 pide un test que falle si los filtros se desactivan, de modo que el desarme sea ruidoso.
- **Riesgo: el filtro no aplica a las escrituras y eso es fácil de olvidar en el próximo endpoint.** Este ticket cierra los seis que existen hoy; el séptimo lo escribirá otra feature. → Mitigación: fuera del alcance de un PRD, pero se anota para que el PLAN evalúe una barrera estructural en lugar de la convención actual.
- **Riesgo: responder "no existe" ante un dato ajeno complica el diagnóstico.** Un usuario con un problema real recibe la misma respuesta que un atacante. → Mitigación: aceptado. NFR-01 lo pide de forma deliberada; distinguir los dos casos es precisamente lo que revelaría la existencia del dato.

## Dependencies

- `DISC-001-01a` (identidad y sesión) mergeado en `main`: sin sesiones reales no hay dos cuentas entre las cuales aislar, y los criterios de este PRD vuelven a no ser observables.
- El filtro global de consulta de `AppDbContext` (`HasQueryFilter` sobre `Movimiento`), que FEAT-001a introdujo y del que dependen FR-01 y AC-10.
- La abstracción `IUsuarioActual`, de la que FR-03 y FR-04 toman el propietario a asignar.
- Los seis endpoints de movimientos y resumen entregados por FEAT-001a, `b` y `c`, que son la superficie que NFR-02 obliga a cubrir por completo.
