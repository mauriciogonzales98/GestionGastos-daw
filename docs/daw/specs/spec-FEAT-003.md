# Spec FEAT-003: Alineación verificada del contrato frontend-backend

| Field | Value |
|-------|-------|
| Ticket | FEAT-003 |
| PRD | docs/daw/prd/prd-FEAT-003.md |
| Tier | FEATURE |
| Date | 2026-08-21 |
| Spec loops | 0 |

## Summary

Se agrega una verificación que toma como **fuente de verdad la declaración del frontend**
(`frontend/src/api/tipos.ts`) y la compara contra lo que el backend realmente hace. Vive en el
proyecto de tests del backend, que es el único lugar del repositorio donde ya conviven la API
levantada (`ApiFactory`), la base real (`BaseDeDatosFixture`) y los tipos de C# — las tres cosas que
hacen falta. **Cero dependencias nuevas**, en ningún lado.

Dos direcciones, porque los cuerpos de respuesta y los de petición no se verifican igual:

- **Respuestas** → contra el **JSON real** que emite la API levantada. Es lo único que verifica
  también la serialización, que hoy es el comportamiento por defecto de ASP.NET Core y no está
  escrita en ninguna parte del repositorio (el impact scan confirmó que no hay `JsonNamingPolicy` ni
  `ConfigureHttpJsonOptions`). Un esquema derivado del `record` no lo cubriría.
- **Peticiones** → por **reflexión** sobre los `record` de C#, más una prueba de aceptación de ida y
  vuelta. El cuerpo de la petición lo emite el cliente, así que no hay JSON del servidor que mirar.

**La decisión de diseño que define el ticket, y la trampa que evita.** El impact scan recomendó
apoyarse en el precedente de `ResumenTests.cs:248`, que fija a mano el conjunto de claves esperadas.
Eso **no cierra el hueco**: una lista escrita a mano en C# es una *tercera* copia del contrato, y la
medición del PRD lo demostró — al renombrar el DTO se actualizó esa lista y todo quedó en verde. Por
eso la lista esperada **no se escribe**: se **lee de `tipos.ts`**. Ese archivo pasa de ser
documentación a ser la especificación ejecutable del contrato.

**Corolario que gobierna el Block 1.** Si el parser de TypeScript no entiende una construcción y la
saltea, el tipo salteado *parece verificado*: es el mismo modo de falla que este ticket combate, un
nivel más arriba. Por eso el parser es **estricto**: lo que no reconoce lanza, no omite. Y las
exclusiones deliberadas se declaran en un solo lugar, con su motivo (NFR-05).

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 1, Block 2, Block 3 |
| FR-02 | Block 2 |
| FR-03 | Block 3 |
| FR-04 | Block 2, Block 3 |
| FR-05 | Block 2, Block 3 |
| FR-06 | Block 5 |
| FR-07 | Block 5 |
| FR-08 | Block 5 |
| NFR-01 | Estrategia: la verificación son tests de xUnit dentro del proyecto que **ya corre** en el job `backend` del CI, reutilizando `ApiFactory` y `BaseDeDatosFixture`. El costo mayor —levantar MySQL y la API— ya está pagado por los 142 tests existentes; lo que se agrega son ~10 casos que hacen una petición HTTP en memoria cada uno. Se mide en Block 5 contra el techo de 90 s |
| NFR-02 | Estrategia: no se modifica ningún test existente. Los archivos de Block 1 a 4 son todos nuevos, en una carpeta `Contrato/` propia. El único archivo existente que se toca en todo el ticket es `.github/workflows/ci.yml` y `AGENTS.md`, en Block 5 |
| NFR-03 | Estrategia: 0 dependencias nuevas, ni en `frontend/package.json` ni en los `.csproj`. El parseo de TypeScript se hace con `System.Text.RegularExpressions` de la BCL sobre un archivo cuya forma controla este mismo repositorio; la comparación, con `System.Text.Json`, ya en uso en los tests |
| NFR-04 | Estrategia: Block 4 entrega `backend/verificar-contrato.sh`, que introduce un desalineamiento deliberado, comprueba que la verificación falla, lo revierte y comprueba que vuelve a pasar. Mismo patrón que `verificar-linter.sh` de FIX-001, cuya ausencia hizo que la verificación ronda 1 de aquel ticket saliera BLOCKED |
| NFR-05 | Estrategia: Block 1 define una lista única de tipos excluidos, cada uno con su motivo en el código, y un test que falla si un tipo de `tipos.ts` no está ni verificado ni excluido |

## Dependencies between blocks

Orden de ejecución: **1 → 2 → 3 → 4 → 5**.

| Block | Depende de | Motivo |
|---|---|---|
| 1 — Parser de `tipos.ts` | — | Es la fuente de verdad de todo lo demás |
| 2 — Respuestas contra JSON real | 1 | Compara lo que el parser extrajo |
| 3 — Peticiones por reflexión | 1 | Usa la misma extracción para el otro sentido |
| 4 — Comprobación repetible de la barrera | 2, 3 | No se puede comprobar que falla algo que todavía no existe |
| 5 — CI, `AGENTS.md` y ADR | 4 | Poner el paso de CI antes dejaría el pipeline en rojo |

---

## Block 1 — Parser estricto de `tipos.ts`

**Files**
- `backend/GestionGastos.Api.Tests/Contrato/TipoDelFrontend.cs` (nuevo) — el modelo de un tipo
  declarado en el frontend: nombre, y sus campos con nombre, tipo TypeScript y si admite `null`.
- `backend/GestionGastos.Api.Tests/Contrato/LectorDeTiposDelFrontend.cs` (nuevo) — lee y parsea
  `frontend/src/api/tipos.ts`.
- `backend/GestionGastos.Api.Tests/Contrato/TiposExcluidos.cs` (nuevo) — la lista única de tipos
  fuera de alcance, cada uno con su motivo.
- `backend/GestionGastos.Api.Tests/Contrato/LectorDeTiposDelFrontendTests.cs` (nuevo) — sus tests.

**Logic**

Localiza `frontend/src/api/tipos.ts` subiendo desde el directorio del ensamblado hasta la raíz del
repositorio, igual que `verificar-linter.sh` resuelve `$raiz`. Si no lo encuentra, **lanza**: un
contrato que no se pudo leer no es un contrato verificado.

Extrae cada bloque `export interface X { ... }` y, dentro, cada campo `nombre: tipo;` con su
opcionalidad (`nombre?:`) y su admisión de `null` (`tipo | null`). Reconoce estos tipos y **ningún
otro**: `number`, `string`, `boolean`, un alias de unión de literales de cadena declarado en el mismo
archivo (`TipoMovimiento`), el nombre de otra interfaz del archivo, y el sufijo `[]` sobre cualquiera
de los anteriores.

**Estricto por diseño.** Cualquier construcción que no encaje —un tipo genérico, una unión que no sea
`| null`, un campo sin `;`, una interfaz con `extends`— produce una excepción que nombra la línea. La
alternativa, saltear en silencio, convierte un tipo no entendido en un tipo aparentemente verificado,
que es exactamente el defecto que este ticket arregla.

**Mitigación de R-01 del threat model, plegada acá:** el parseo se hace **línea por línea**, no con
una expresión regular sobre el archivo entero, y ningún patrón lleva cuantificadores anidados. Es la
lección de FIX-003 aplicada antes y no después: una regex escrita sin cuidado es un problema real, y
acá el archivo de entrada sólo va a crecer.

**Input validation**
- El archivo debe existir y ser legible; si no, excepción con la ruta buscada.
- Cada interfaz debe tener al menos un campo; una interfaz vacía es un error de parseo, no un tipo
  sin campos.
- Los nombres de campo deben ser identificadores simples; cualquier otra cosa lanza.

**Error handling**
- Archivo ausente o ilegible → `InvalidOperationException` con la ruta.
- Construcción no reconocida → `InvalidOperationException` con el nombre de la interfaz, el número de
  línea y el texto que no se pudo interpretar.
- Ningún caso devuelve una lista parcial.

**Required tests**
- [ ] `Lector_SobreElArchivoReal_ExtraeLasNueveInterfaces` — el archivo del repositorio se parsea
  entero, sin excepciones (FR-01).
- [ ] `Lector_ExtraeNombreTipoYNulabilidadDeCadaCampo` — sobre `MovimientoDto`: 7 campos, `nota`
  admite `null`, `categoria` referencia otra interfaz (FR-01).
- [ ] `Lector_ConUnTipoNoReconocido_Lanza` — sad path: una interfaz con `Record<string, unknown>`
  produce excepción, no una lista parcial.
- [ ] `Lector_ConUnaInterfazSinCerrar_Lanza` — sad path: sintaxis rota.
- [ ] `Lector_SinElArchivo_LanzaNombrandoLaRuta` — sad path: ruta inexistente.
- [ ] `TiposExcluidos_CadaUno_TieneMotivoNoVacio` — NFR-05: ninguna exclusión sin motivo escrito.

**Completion criterion**
El lector parsea `frontend/src/api/tipos.ts` completo y devuelve las 9 interfaces con sus campos; los
tres sad paths lanzan con mensaje que nombra el problema; ninguna exclusión carece de motivo.

---

## Block 2 — Verificación de los cuerpos de respuesta contra el JSON real

**Files**
- `backend/GestionGastos.Api.Tests/Contrato/ComparadorDeFormas.cs` (nuevo) — compara un
  `TipoDelFrontend` contra un `JsonElement` y devuelve las diferencias.
- `backend/GestionGastos.Api.Tests/Contrato/ContratoDeRespuestasTests.cs` (nuevo) — un caso por
  endpoint.

**Logic**

Para cada uno de los cuatro GET, levanta la API con `ApiFactory`, siembra el mínimo necesario con
`BaseDeDatosFixture`, hace la petición, lee el cuerpo con `JsonDeRespuesta` y lo compara contra el
tipo que el frontend declara para ese endpoint.

La comparación es **en las dos direcciones**: un campo presente en el JSON y ausente en el tipo del
frontend es una diferencia, y uno declarado en el frontend y ausente del JSON también. Recorre las
interfaces anidadas y los arreglos.

**Tabla de compatibilidad de tipos**, que es donde se resuelve el riesgo `decimal`/`number` que el
PRD señala:

| Tipo en `tipos.ts` | `JsonValueKind` aceptado |
|---|---|
| `number` | `Number` — incluye lo que C# serializa desde `decimal`; el criterio es la forma en el cable, no el tipo de origen |
| `string` | `String` |
| `boolean` | `True` o `False` |
| `T \| null` | lo que acepte `T`, o `Null` |
| `T[]` | `Array`, y cada elemento debe cumplir `T` |
| interfaz | `Object`, y se recorre |
| unión de literales (`TipoMovimiento`) | `String`, y el valor debe ser uno de los literales declarados |

**API contract**
- `GET /api/categorias` → arreglo de `CategoriaDto`
- `GET /api/movimientos` → `ListadoMovimientosResponse`
- `GET /api/movimientos/{id}` → `MovimientoDto`
- `GET /api/resumen` → `ResumenMensual`

**Error handling**
- Si el endpoint no devuelve 2xx, el test **falla diciendo que no pudo obtener el cuerpo**, y no
  reporta el contrato como verificado (AC-15).
- Si el arreglo devuelto viene vacío, el test falla: sin al menos un elemento no hay forma que
  comparar. La siembra del fixture es responsabilidad del test.
- **Si dos interfaces se referencian mutuamente**, el recorrido lanza nombrando los tipos del ciclo,
  en vez de colgarse. Mitigación de R-04 del threat model: hoy el grafo de `tipos.ts` es acíclico,
  pero nada lo garantiza mañana, y un test colgado no da señal. El recorrido lleva el conjunto de
  tipos ya visitados en la rama actual.

**Required tests**
- [ ] `Contrato_Categorias_CoincideConElTipoDelFrontend` — valida AC-01 sobre `CategoriaDto`.
- [ ] `Contrato_Movimientos_CoincideConElTipoDelFrontend` — valida AC-01 sobre
      `ListadoMovimientosResponse` y `MovimientoDto` anidado.
- [ ] `Contrato_MovimientoPorId_CoincideConElTipoDelFrontend` — valida AC-01.
- [ ] `Contrato_Resumen_CoincideConElTipoDelFrontend` — valida AC-01 sobre `ResumenMensual` y
      `CategoriaConTotal` anidado.
- [ ] `Comparador_CampoEnElJsonQueElFrontendNoDeclara_LoReporta` — valida AC-03.
- [ ] `Comparador_CampoDeclaradoQueElJsonNoTrae_LoReporta` — valida AC-04.
- [ ] `Comparador_MismoCampoConTipoIncompatible_LoReporta` — valida AC-05: `string` declarado contra
      un `Number` en el cable.
- [ ] `Comparador_AlReportar_NombraCampoEndpointYDiferencia` — valida AC-06: el mensaje incluye el
      nombre del campo, **el endpoint afectado** y en qué consiste la diferencia. Los tres, porque
      el AC pide los tres.
- [ ] `Contrato_SiElEndpointNoDevuelve2xx_FallaSinReportarVerificado` — sad path, valida AC-15.
- [ ] `Contrato_SiLaColeccionVieneVacia_FallaEnVezDeDarPorVerificado` — sad path del error
      documentado arriba: sin un elemento que comparar, un arreglo vacío no puede leerse como
      "contrato verificado".
- [ ] `Comparador_ConTiposMutuamenteReferenciados_LanzaNombrandoElCiclo` — sad path de R-04: el
      recorrido corta en vez de colgarse.
- [ ] `Contrato_TodoTipoDeRespuesta_EstaVerificadoOExcluidoConMotivo` — valida AC-14 y NFR-05: falla
      si `tipos.ts` gana un tipo que nadie verifica ni excluye.

**Completion criterion**
Los cuatro endpoints se verifican contra el JSON real y pasan sobre el repositorio sin modificar; las
cuatro direcciones de diferencia (campo de más, de menos, tipo incompatible, endpoint caído) se
reportan con el nombre del campo.

---

## Block 3 — Verificación de los cuerpos de petición

**Files**
- `backend/GestionGastos.Api.Tests/Contrato/ContratoDePeticionesTests.cs` (nuevo).

**Logic**

Para `CrearMovimientoRequest` y `ModificarMovimientoRequest`, obtiene por reflexión las propiedades
del `record` de C#, las convierte a camelCase con la misma política que usa la serialización por
defecto, y las compara contra los campos que el frontend declara para ese mismo tipo.

Agrega una prueba de **aceptación de ida y vuelta**: construye un cuerpo JSON usando exactamente los
nombres de campo que declara el frontend y comprueba que la API lo acepta. Un desalineamiento que la
reflexión no detectara igual se manifiesta acá como un rechazo.

**Input validation**
- El `record` de C# debe tener al menos una propiedad; si no, el test falla antes de comparar.

**Error handling**
- Si el POST de ida y vuelta devuelve 400, el test falla mostrando el cuerpo del `ProblemDetails`,
  que es lo que dice qué campo no se entendió.

**Required tests**
- [ ] `Contrato_CrearMovimiento_LosCamposDelFrontendCoincidenConElRecord` — valida AC-07.
- [ ] `Contrato_ModificarMovimiento_LosCamposDelFrontendCoincidenConElRecord` — valida AC-07.
- [ ] `Contrato_CuerpoConLosNombresDelFrontend_EsAceptadoPorElAlta` — ida y vuelta, valida AC-07.
- [ ] `Contrato_CuerpoConUnCampoRenombrado_EsRechazado` — sad path: confirma que la aceptación del
      caso anterior no es trivial.

**Completion criterion**
Los dos cuerpos de petición se verifican en ambos sentidos y la prueba de ida y vuelta pasa; el sad
path confirma que el mecanismo distingue.

---

## Block 4 — Comprobación repetible de que la barrera funciona

**Files**
- `backend/verificar-contrato.sh` (nuevo) — introduce un desalineamiento deliberado, comprueba que la
  verificación falla, lo revierte y comprueba que vuelve a pasar.

**Logic**

Mismo patrón y mismas lecciones que `backend/verificar-linter.sh`: la barrera es código, y un cambio
la puede desarmar en silencio. El script:

1. Corre la verificación de contrato sobre el árbol sin tocar y exige que pase.
2. Renombra un campo en un DTO del backend, corre la verificación y exige que **falle**.
3. Revierte, corre de nuevo y exige que pase.

Limpia con `trap … EXIT` en toda salida, para no dejar el árbol modificado si se interrumpe.

**Error handling**
- Si el paso 1 falla, sale de inmediato: si el árbol limpio ya no verifica, el resto no dice nada.
- Si el paso 3 no restaura el estado original, sale distinto de 0 avisando que el árbol quedó sucio.

**Required tests**
- [ ] El script sale 0 sobre el repositorio sin modificar — valida NFR-04.
- [ ] **El paso 2 del script falla la verificación tras renombrar un campo del DTO** — valida
      **AC-02**, que es el escenario del rename coherente del backend medido en el PRD. Es este
      script, y no un test del Block 2, el único que puede validarlo: requiere modificar el backend
      y volver a correr la verificación.
- [ ] El script sale distinto de 0 si se desarma la verificación — comprobado invirtiendo el paso 2 a
      mano una vez, y registrado en el reporte del cierre de CODE.
- [ ] `El_script_sobre_un_arbol_que_ya_no_verifica_sale_de_inmediato` — sad path del primer error
      documentado: si el paso 1 falla, el script no sigue, porque los pasos 2 y 3 no dirían nada.
- [ ] `git status` queda limpio después de cada corrida, incluida una interrumpida — sad path del
      segundo error documentado.

**Completion criterion**
`./backend/verificar-contrato.sh` sale 0 sobre el árbol limpio, muestra los tres pasos, y deja el
repositorio sin cambios.

---

## Block 5 — CI, `AGENTS.md` y el ADR

**Files**
- `.github/workflows/ci.yml` (modificado) — paso nuevo en el job `backend`.
- `AGENTS.md` (modificado) — fila nueva en la tabla Stack.
- `docs/adr/ADR-00X-verificacion-del-contrato.md` (nuevo) — la decisión y las alternativas.

**Logic**

El paso de CI va en el job `backend`, que ya tiene MySQL y el SDK, **después** de `Tests` y **antes**
de `Barrera del linter` — igual que aquel, compila y modifica el árbol temporalmente, así que no
puede quedar antes de un paso que use `--no-build`.

El ADR registra la decisión con las tres alternativas y por qué se descartaron dos, incluyendo el
dato medido: `Microsoft.AspNetCore.OpenApi` no está en el shared framework de .NET 10.0.11 y
`AddOpenApi()` no compila sin `PackageReference`, así que la opción de generar tipos desde OpenAPI
costaba una dependencia en el backend contra un presupuesto de cero.

**Error handling**
- **El paso de CI falla por el contrato** → es el comportamiento buscado (AC-08). El mensaje del
  paso tiene que ser el del comparador, con campo y endpoint, y no un volcado de xUnit sin contexto.
- **El paso de CI falla porque MySQL no levantó** → se distingue del caso anterior: el fixture ya
  lanza con su propio mensaje (ADR-002), y no debe leerse como un desalineamiento de contrato.
**Dos contingencias de proceso, que no son errores de ejecución y por eso no llevan test**
- Si la medición de AC-09 no se puede tomar porque no hay dos corridas comparables, **se registra el
  desvío en el reporte del cierre** en vez de dar el número por bueno. Es exactamente el W-3 que la
  verificación ronda 1 de FIX-001 dejó anotado, y repetirlo en silencio sería no haber aprendido.
- Si el número de ADR elegido ya está usado, se toma el siguiente libre. Los ADR existentes no se
  renumeran.

**Required tests**
- [ ] El job `backend` del CI ejecuta la verificación y pasa — valida AC-08.
- [ ] `La_suite_completa_pasa_sin_tests_modificados` — valida **AC-12** y NFR-02: los 247 casos
      previos más los nuevos, y `git diff` no muestra cambios en ningún archivo de test existente.
- [ ] El paso de CI, cuando falla por contrato, muestra campo y endpoint — sad path del primer error
      documentado.
- [ ] Una corrida sin MySQL falla con el mensaje del fixture y no con uno de contrato — sad path del
      segundo error documentado.
- [ ] La diferencia de duración del job con y sin el paso es de a lo sumo 90 s — valida AC-09 y
      NFR-01, medido sobre dos corridas reales.
- [ ] La tabla Stack de `AGENTS.md` declara el comando — valida AC-10.
- [ ] Existe un ADR con mecanismo elegido, alternativas descartadas y motivo — valida AC-11.
- [ ] `frontend/package.json` y los `.csproj` no ganan ninguna dependencia — valida AC-13 y NFR-03.

**Completion criterion**
El CI corre la verificación en verde, `AGENTS.md` declara el comando, el ADR está escrito y el
recuento de dependencias no cambió.

## Final verification

- La suite completa pasa: 247 casos previos más los nuevos, sin que ningún test existente haya sido
  modificado (AC-12, NFR-02).
- `./backend/verificar-contrato.sh` sale 0 y deja el árbol limpio (NFR-04).
- `dotnet build -warnaserror`, `dotnet format --verify-no-changes` y `./backend/verificar-linter.sh`
  siguen en verde: la carpeta `Contrato/` nueva pasa por los mismos analizadores que el resto.
- 0 dependencias nuevas en `frontend/package.json` y en los dos `.csproj` (AC-13).
- Todo tipo de `frontend/src/api/tipos.ts` está verificado o excluido con motivo (AC-14, NFR-05).

## Deuda heredada que NO se toma en este ticket

- **D-3**, el fixture de rendimiento que vence el 2027-01-01 (W-VER-03 de FEAT-001c).
- El **`.gitattributes`** con `* text=auto` y `*.sh text eol=lf` que FIX-002 dejó anotado. Este
  ticket agrega un `.sh` más, así que la deuda crece; sigue siendo un ticket propio.
- El precedente incompleto que el impact scan señaló: `ResumenTests.cs:248` fija las claves del
  resumen a mano y ahora queda **redundante** con la verificación nueva. No se toca: modificarlo
  violaría NFR-02, y decidir si se borra es materia de otro ticket.
