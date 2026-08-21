# ADR-004: Cómo se verifica que el contrato HTTP no se desalinee

| Field | Value |
|-------|-------|
| Date | 2026-08-21 |
| Ticket | FEAT-003 |
| Status | Accepted |

## Context

El contrato HTTP está escrito **dos veces, a mano, en dos lenguajes**: en los `record` de C# del
backend y en las interfaces de TypeScript de `frontend/src/api/tipos.ts`. No hay OpenAPI, ni
generación de tipos, ni esquema compartido; `cliente.ts:135` hace `(await respuesta.json()) as T`,
un cast sin validación.

**La medición del 2026-08-21**, que es lo que convirtió esto en un ticket: se renombró
`TotalIngresado` → `Ingresos` en `ResumenMensualDto` de tres formas, cada una más completa que la
anterior.

| Escenario | Se puso en rojo |
|---|---|
| Sólo el DTO | 10 tests del backend |
| DTO + el helper de test que lee la clave | 1 test, el que fija las seis claves |
| **Rename coherente de todo el backend** | **nada** |

En el tercer caso quedaron verdes el build con `-warnaserror`, los 142 tests del backend,
`tsc --noEmit`, los 105 de Vitest, ESLint y la barrera del linter. El contrato roto, el semáforo
entero en verde, y `undefined` en pantalla.

El problema no era falta de tests: el backend tenía guardas razonables sobre el JSON que emite. Lo
que no existía era **el punto donde lo que el backend emite y lo que el frontend espera se comparan
entre sí**. Dos verdades paralelas que nunca se tocaban.

El PRD acotó el presupuesto: **1 dependencia nueva como máximo en el frontend y 0 en el backend**
(NFR-03), y 90 s de CI (NFR-01).

## Options considered

### Opción 1: generar los tipos de TypeScript desde un esquema OpenAPI del backend

- **Pros:** ataca la causa de raíz. Elimina una de las dos copias: el tipo del frontend pasa a
  *derivar* del backend, así que el desalineamiento deja de ser posible en vez de ser detectable. Es
  la solución conceptualmente más limpia y la que menos mantenimiento pide a futuro.
- **Cons:** **cuesta una dependencia en el backend, y el presupuesto era cero.** Verificado
  empíricamente, no supuesto: `Microsoft.AspNetCore.OpenApi` **no está en el shared framework** de
  .NET 10.0.11, y `AddOpenApi()` no compila sin `PackageReference` — un proyecto limpio con el SDK
  web da `CS1061` mientras que el mismo proyecto sin OpenAPI compila. Además arriesga los 6
  componentes y 52 call sites del frontend si la forma de los tipos generados difiere de la actual, y
  un generador estricto podría mapear `decimal` a algo distinto de `number`.

### Opción 2: validación en runtime del cuerpo de las respuestas (zod o similar)

- **Pros:** una sola dependencia, en el frontend, dentro del presupuesto. Falla fuerte y claro en vez
  de mostrar `undefined`.
- **Cons:** **no cierra el hueco: lo mueve.** Valida que la respuesta cumpla un esquema *escrito a
  mano en el frontend*, que es exactamente la segunda copia que hoy no se compara contra nada. Un
  rename coherente del backend seguiría sin detectarse hasta que alguien ejecute la aplicación. Y
  agrega peso al bundle que el navegador de cada usuario descarga.

### Opción 3: verificación que lee `tipos.ts` como fuente de verdad y lo compara contra la API real

- **Pros:** **cero dependencias**, en ningún lado — cumple NFR-03 con margen y no agrega superficie
  de cadena de suministro. Reutiliza `ApiFactory`, `BaseDeDatosFixture` y `JsonDeRespuesta`, que ya
  existen y ya corren en el job `backend` del CI. Y compara contra el **JSON real**, que es lo único
  que verifica también la serialización: nadie configura `JsonNamingPolicy` ni
  `ConfigureHttpJsonOptions` en este proyecto, así que el camelCase que el frontend asume es el
  comportamiento por defecto de ASP.NET Core y ningún esquema derivado del `record` lo cubriría.
- **Cons:** hay que escribir un parser de TypeScript, aunque acotado a la forma que este repositorio
  controla. Verifica en el momento de la corrida y no estáticamente. Y **estrena un cruce entre
  `backend/` y `frontend/`** — ver abajo.

### La variante de la opción 3 que se descartó, y por qué importa

El impact scan recomendó apoyarse en el precedente de `ResumenTests.cs:248`, que fija a mano el
conjunto de claves esperadas. **Eso no cierra nada.** Una lista escrita a mano en C# es una *tercera*
copia del contrato, con el mismo problema que las otras dos: la medición lo demostró, porque al
renombrar el DTO se actualizó esa lista y todo quedó en verde.

De ahí la propiedad que define la implementación: la lista esperada **no se escribe, se lee de
`tipos.ts`**.

## Decision

**Opción 3.** La verificación vive en `backend/GestionGastos.Api.Tests/Contrato/` y toma como fuente
de verdad la declaración del frontend.

- **Respuestas** → contra el JSON real de la API levantada, en las dos direcciones: un campo que el
  backend emite y el frontend no declara es funcionalidad invisible; uno declarado que el backend no
  emite es un `undefined`.
- **Peticiones** → por reflexión sobre los `record`, más una prueba de ida y vuelta que arma el
  cuerpo con los nombres que declara el frontend.
- **El parser es estricto**: lo que no reconoce lanza, no saltea. Un tipo salteado quedaría
  *pareciendo* verificado, que es el defecto de este ticket un nivel más arriba.
- **`backend/verificar-contrato.sh`** comprueba que la barrera se pone en rojo cuando tiene que
  ponerse. Es la lección de FIX-001, cuya verificación ronda 1 salió BLOCKED porque el test de
  regresión sólo existía como narración de una comprobación manual borrada.

### Excepción declarada a la separación entre `backend/` y `frontend/`

`AGENTS.md` declara las dos carpetas como separadas, y hasta este ticket **no había ni un solo cruce
entre ellas, en ninguna dirección**. `daw-validate-arch` lo marcó, y la excepción se acepta
deliberadamente: **comparar dos definiciones exige que algo mire a las dos**, y no hay forma de
cerrar este hueco sin eso.

El alcance de la excepción es exactamente uno: los tests de `Contrato/` leen
`frontend/src/api/tipos.ts` en modo lectura. No se importa código, no se comparte build, y la
dirección inversa —el frontend leyendo algo del backend— sigue sin existir y debe seguir así. La
convención en `AGENTS.md` queda anotada con esta excepción para que la próxima persona sepa que es
una decisión y no un descuido.

## Consequences

- Un rename coherente del backend ahora rompe la verificación, nombrando el campo, el endpoint y la
  dirección de la diferencia. Antes no rompía nada.
- `frontend/src/api/tipos.ts` deja de ser documentación y pasa a ser la especificación ejecutable del
  contrato. Editarlo tiene consecuencias, que es lo buscado.
- Un tipo nuevo en `tipos.ts` que nadie verifique ni excluya **rompe un test**, así que el contrato no
  puede crecer en silencio.
- El costo de mantenimiento es real: los `ValoresDeEjemplo` de los cuerpos de petición se escriben a
  mano, y un campo nuevo obliga a agregar el suyo. Es deliberado — falla pidiendo la actualización en
  vez de omitir el campo.
- **La opción 1 sigue siendo mejor a largo plazo.** Si algún día se justifica una dependencia en el
  backend, generar los tipos desde OpenAPI elimina el problema en vez de detectarlo, y esta
  verificación pasaría a ser redundante. Este ADR no cierra esa puerta: la deja documentada con el
  motivo por el que hoy no se tomó.
- `ResumenTests.cs:248`, que fija las seis claves del resumen a mano, queda **redundante** con la
  verificación nueva. No se toca en este ticket —NFR-02 exige no modificar tests existentes— y
  decidir si se borra es materia de otro.
