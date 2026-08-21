# PRD FEAT-003: Alineación verificada del contrato frontend-backend

| Field | Value |
|-------|-------|
| Ticket | FEAT-003 |
| Tracker | ninguno |
| Date | 2026-08-21 |
| PRD loops | 0 |

> D-2 del mapa de DISC-001 (`docs/daw/discovery/concept-DISC-001.md`), reencuadrado. El mapa lo
> anotaba como *"Vitest sin `typecheck`"* y afirmaba que `tsc --noEmit` ya detecta el
> desalineamiento. La medición del 2026-08-21, abajo, muestra que no.

## Context and Problem

El contrato HTTP entre el frontend y el backend está escrito **dos veces, a mano, en dos lenguajes**,
y nada compara las dos copias.

- El backend lo define en `record`s de C#: `ResumenMensualDto`, `MovimientoDto`, `CategoriaDto`.
- El frontend lo define en interfaces de TypeScript escritas a mano en `frontend/src/api/tipos.ts`.
- No hay OpenAPI, ni generación de tipos, ni esquema compartido. `cliente.ts:135` hace
  `(await respuesta.json()) as T`: un cast, sin validación en runtime.
- Los fixtures de los tests del frontend están tipados con la interfaz **del propio frontend**, así
  que confirman que el frontend es coherente consigo mismo, no que coincida con lo que llega.

El propio `tipos.ts` ya nombra el riesgo, y es la mejor descripción del problema que hay en el
repositorio: *"un nombre que no coincida no rompe la compilación, llega como `undefined` en
runtime"*.

### Medición (2026-08-21)

Se renombró `TotalIngresado` → `Ingresos` en `ResumenMensualDto` y se observó qué gate se pone en
rojo, en tres escenarios de profundidad creciente:

| Escenario | Se pone en rojo |
|---|---|
| Sólo el DTO | 10 tests del backend |
| DTO + el helper de test que lee la clave del JSON | 1 test: `Resumen_NoDevuelveLaListaDeMovimientos`, que fija el conjunto exacto de claves |
| DTO + helper + la lista de claves esperada (**rename coherente del backend**) | **nada** |

En el tercer escenario quedan en verde: `dotnet build -warnaserror`, los 142 tests del backend,
`tsc --noEmit`, los 105 tests de Vitest, ESLint y la barrera del linter. El contrato está roto y el
semáforo entero está en verde.

**El problema no es que falten tests.** El backend tiene guardas razonables sobre la forma del JSON
que emite; son mejores de lo que el mapa sugería. Lo que no existe es **el punto donde lo que el
backend emite y lo que el frontend espera se comparan entre sí**. Son dos verdades paralelas: un
refactor coherente mueve una y deja la otra intacta, y nadie se entera hasta que alguien mira la
pantalla.

Esto no es hipotético en este proyecto: el orden de trabajo acordado pone la autenticación
(`DISC-001-01a/b/c`) inmediatamente después de esta deuda, y esos tickets agregan campos y endpoints
nuevos al contrato. Cada uno es una oportunidad de desalineamiento silencioso.

## Goals

- Que un cambio de contrato del lado del backend rompa algo **antes** de llegar a producción, y no
  en la pantalla del usuario.
- Que el mensaje de esa rotura diga qué campo y en qué dirección, no `undefined`.
- Que la verificación corra en el pipeline y no dependa de que alguien se acuerde de mirar.
- Que el costo de mantenerla sea menor que el de mantener las dos copias sincronizadas a mano.

## Functional Requirements

- FR-01: El proyecto debe verificar que cada tipo del contrato declarado en el frontend se corresponde con el DTO que el backend emite para ese mismo endpoint. Origen: la medición muestra que hoy nada compara las dos definiciones.
- FR-02: El proyecto debe cubrir con esa verificación los cuerpos de respuesta de los cuatro endpoints existentes: `GET /api/categorias`, `GET /api/movimientos`, `GET /api/movimientos/{id}` y `GET /api/resumen`. Origen: son los que el frontend consume hoy.
- FR-03: El proyecto debe cubrir con esa verificación los cuerpos de petición que el frontend envía: `CrearMovimientoRequest` y `ModificarMovimientoRequest`. Origen: un campo de más o de menos en el cuerpo se rechaza como 400 sin decir por qué.
- FR-04: El proyecto debe fallar la verificación cuando un campo del contrato exista en un lado y no en el otro, y también cuando exista en ambos con tipos incompatibles. Origen: el rename es el caso medido, pero agregar u omitir un campo rompe igual.
- FR-05: El proyecto debe emitir, al fallar, el nombre del campo, el endpoint y la diferencia detectada. Origen: el defecto original se manifiesta como `undefined`, que no dice dónde mirar.
- FR-06: El proyecto debe ejecutar la verificación en el pipeline de integración continua, en el workflow donde ya corren los jobs de frontend y backend. Origen: FEAT-002; una barrera que sólo corre localmente no bloquea nada.
- FR-07: El proyecto debe declarar el comando de la verificación en la sección Stack de `AGENTS.md`. Origen: `AGENTS.md` es la única fuente del stack, y un comando no declarado es un comando que los gates de DAW no corren.
- FR-08: El proyecto debe documentar en un ADR el mecanismo elegido y las alternativas descartadas. Origen: es una decisión de arquitectura con consecuencias de mantenimiento a largo plazo.

## Non-Functional Requirements

- NFR-01: La verificación debe agregar como máximo 90 s al tiempo del pipeline de integración continua. Origen: FEAT-002 fijó 60 s para el lint del backend; esta verificación puede necesitar levantar la API, y por eso el techo es mayor.
- NFR-02: El proyecto debe conservar los 247 tests de la suite en verde, sin que ninguno haya sido modificado para acomodar el mecanismo nuevo. Origen: es una barrera nueva, no un cambio de comportamiento.
- NFR-03: El proyecto debe agregar como máximo 1 dependencia nueva de producción o desarrollo al frontend, y 0 al backend, y justificarla en la spec. Origen: `AGENTS.md` exige justificar toda dependencia nueva en la spec.
- NFR-04: La verificación debe fallar con código de salida distinto de 0 en al menos 1 caso de desalineamiento deliberado comprobado de forma repetible. Origen: FIX-001 entregó una barrera que nadie comprobaba; su verificación ronda 1 salió BLOCKED por eso.
- NFR-05: El proyecto debe dejar 0 tipos del contrato en `frontend/src/api/tipos.ts` fuera del alcance de la verificación sin un comentario adyacente que explique el motivo. Origen: una exclusión sin motivo es indistinguible de un olvido.

## Acceptance Criteria

- AC-01 (FR-01, FR-02): WHEN se ejecuta la verificación del contrato sobre el repositorio sin modificar, THE sistema SHALL terminar con código de salida 0.
- AC-02 (FR-01, FR-04, NFR-04): IF se renombra un campo de un DTO de respuesta en el backend de forma coherente en todo el backend, THEN THE verificación SHALL terminar con código de salida distinto de 0.
- AC-03 (FR-04): IF se agrega un campo a un DTO del backend que el tipo del frontend no declara, THEN THE verificación SHALL terminar con código de salida distinto de 0.
- AC-04 (FR-04): IF se declara en el frontend un campo que el DTO del backend no emite, THEN THE verificación SHALL terminar con código de salida distinto de 0.
- AC-05 (FR-04): IF un campo existe en los dos lados con tipos incompatibles, THEN THE verificación SHALL terminar con código de salida distinto de 0.
- AC-06 (FR-05): WHEN la verificación falla, THE sistema SHALL exhibir el nombre del campo, el endpoint afectado y la diferencia detectada.
- AC-07 (FR-03): IF se renombra un campo de `CrearMovimientoRequest` o de `ModificarMovimientoRequest` en el backend, THEN THE verificación SHALL terminar con código de salida distinto de 0.
- AC-08 (FR-06): IF un pull request introduce un desalineamiento del contrato, THEN THE pipeline de integración continua SHALL fallar identificando el campo.
- AC-09 (FR-06, NFR-01): WHEN se mide la duración del pipeline con la verificación y sin ella, THE sistema SHALL exhibir una diferencia de a lo sumo 90 s.
- AC-10 (FR-07): WHEN se lee la sección Stack de `AGENTS.md`, THE sistema SHALL declarar el comando de la verificación del contrato.
- AC-11 (FR-08): WHEN se lee `docs/adr/`, THE sistema SHALL exhibir un ADR con el mecanismo elegido, las alternativas descartadas y el motivo del descarte.
- AC-12 (NFR-02): WHEN se ejecuta la suite completa después de introducir el mecanismo, THE sistema SHALL pasar los 247 casos, y ningún archivo de test SHALL haber cambiado su comportamiento esperado.
- AC-13 (NFR-03): WHEN se inspecciona `frontend/package.json` y los `.csproj` del backend, THE sistema SHALL exhibir a lo sumo 1 dependencia nueva en el frontend y 0 en el backend.
- AC-14 (NFR-05): WHEN se inspeccionan los tipos de `frontend/src/api/tipos.ts`, THE sistema SHALL exhibir junto a cada tipo excluido de la verificación un comentario con el motivo.
- AC-15 (FR-01): IF el mecanismo elegido necesita la API levantada y la API no responde, THEN THE verificación SHALL fallar indicando que no pudo conectarse, y SHALL no reportar el contrato como verificado.

## Out of Scope

- **Elegir el mecanismo.** Este PRD fija *qué* tiene que pasar, no *cómo*. La decisión entre generar
  los tipos desde OpenAPI, validar en runtime o comparar contra la API real se toma en PLAN, con el
  impact scan y el threat model delante, y se registra en el ADR que pide FR-08.
- **Validación de datos en runtime en producción.** Si el mecanismo elegido resulta ser validación
  en runtime, este ticket la usa como verificación; convertirla en un control permanente sobre cada
  respuesta en producción es una decisión de rendimiento y de manejo de errores propia.
- **Versionado del contrato de la API.** Compatibilidad hacia atrás, `Accept-Version`, deprecación
  de campos: no hay consumidores externos y hoy no hace falta.
- **`FiltrosDeMovimientos`**, que no espeja ningún tipo del backend: viaja en la query string y no
  en un cuerpo. Queda fuera con su motivo, según NFR-05.
- **Los mensajes de error `ProblemDetails`**: su forma la fija RFC 9457, no este proyecto.
- **Reescribir los tipos del frontend** más allá de lo que el mecanismo exija.
- **D-3** (el fixture de rendimiento que vence el 2027-01-01) y el `.gitattributes` que FIX-002 dejó
  anotado: son tickets propios.

## Risks and Mitigations

- **Riesgo: el mecanismo se elige por gusto y no por evidencia.** Es el riesgo principal, porque
  las tres opciones son defendibles y dos agregan dependencias. → Mitigación: FR-08 y AC-11 obligan
  a un ADR con las alternativas descartadas y el motivo; NFR-03 y AC-13 acotan el costo en
  dependencias a 1 y 0.
- **Riesgo: la verificación queda molesta y alguien la apaga.** Es exactamente lo que pasó con el
  linter del backend antes de FIX-001, y la lección está registrada ahí. → Mitigación: NFR-01 acota
  el costo en tiempo, AC-01 exige que arranque en verde, y NFR-05 obliga a que toda exclusión lleve
  su motivo escrito.
- **Riesgo: se entrega una barrera que nadie comprueba.** Ya pasó: la verificación ronda 1 de
  FIX-001 salió BLOCKED porque el test de regresión sólo existía como narración de una comprobación
  manual. → Mitigación: NFR-04 exige una comprobación **repetible** del desalineamiento, y AC-02 a
  AC-05 y AC-07 la desglosan en cinco direcciones distintas.
- **Riesgo: el mecanismo necesita la API levantada y el CI se vuelve frágil.** Un test que depende
  de un servicio arriba falla por razones que no son el código. → Mitigación: AC-15 exige que la
  falta de conexión se distinga de un contrato desalineado, y NFR-01 acota el costo.
- **Riesgo: la generación de tipos deja el archivo generado desincronizado del esquema.** Si el
  mecanismo genera código, alguien puede editarlo a mano o no regenerarlo. → Mitigación: AC-01 se
  verifica sobre el repositorio sin modificar, así que un archivo generado que no coincida con su
  fuente falla la verificación.
- **Riesgo: `decimal` de C# contra `number` de TypeScript.** El backend usa `decimal` para montos
  (NFR-03 de FEAT-001a) y TypeScript sólo tiene `number`, que es punto flotante. Un mecanismo
  estricto podría marcar como incompatible algo que hoy funciona. → Mitigación: AC-05 habla de
  tipos **incompatibles**, no idénticos; qué se considera compatible lo fija el ADR de FR-08.

## Dependencies

- `frontend/src/api/tipos.ts`, donde vive la definición del contrato del lado del frontend, y
  `frontend/src/api/cliente.ts`, que hace el cast sin validar.
- Los DTOs del backend: `backend/GestionGastos.Api/Resumen/ResumenMensualDto.cs`,
  `Movimientos/MovimientoDto.cs` y `Categorias/CategoriaDto.cs`, más los tipos de petición
  `CrearMovimientoRequest` y `ModificarMovimientoRequest`.
- El workflow de integración continua `.github/workflows/ci.yml` de FEAT-002, donde FR-06 agrega la
  verificación.
- La sección Stack de `AGENTS.md`, única fuente del stack, que FR-07 actualiza.
- `docs/adr/`, donde FR-08 registra la decisión, junto a los ADR ya existentes del proyecto.
- La suite de 247 tests (142 backend + 105 frontend), que NFR-02 usa como referencia de que el
  comportamiento no cambió.
- El mapa de DISC-001, `docs/daw/discovery/concept-DISC-001.md`, de donde sale D-2 y donde este
  ticket se marca al cerrar.
- **Como precedente, no como dependencia funcional:** `docs/daw/reports/verify-FIX-001.md` y
  `docs/daw/prd/prd-FIX-001.md`, de donde salen las dos lecciones que este PRD aplica —una barrera
  que molesta se apaga, y una barrera que nadie comprueba no es una barrera—; y `NFR-03` de
  `docs/daw/prd/prd-FEAT-001a.md`, que fija `decimal` para los montos y por eso condiciona qué se
  considera compatible entre `decimal` de C# y `number` de TypeScript.
