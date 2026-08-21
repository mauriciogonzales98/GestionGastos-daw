# Threat model FEAT-003 — Alineación verificada del contrato frontend-backend

| Field | Value |
|-------|-------|
| Ticket | FEAT-003 |
| Spec | docs/daw/specs/spec-FEAT-003.md |
| PRD | docs/daw/prd/prd-FEAT-003.md |
| Fecha | 2026-08-21 |
| Resultado | **PASSED** — 0 CRITICAL, 2 HIGH mitigados y plegados a la spec, 0 riesgos aceptados |

## Qué se está analizando, en concreto

No se agrega superficie de producción. **Ni un archivo de `backend/GestionGastos.Api/` ni de
`frontend/src/` cambia**: lo que se agrega son cuatro archivos de test en
`backend/GestionGastos.Api.Tests/Contrato/`, un script `backend/verificar-contrato.sh`, un paso de CI
y dos documentos. Esa es la razón por la que el análisis se concentra en la **cadena de
verificación** y no en la aplicación: el activo que este ticket introduce es una barrera, y una
barrera comprometida es peor que ninguna, porque genera confianza sin respaldarla.

Los componentes nuevos:

1. `LectorDeTiposDelFrontend` — lee y parsea `frontend/src/api/tipos.ts`.
2. `ComparadorDeFormas` — compara la forma declarada contra un `JsonElement`.
3. `ContratoDeRespuestasTests` / `ContratoDePeticionesTests` — levantan la API con `ApiFactory` y la
   base con `BaseDeDatosFixture`.
4. `backend/verificar-contrato.sh` — **modifica archivos del backend a propósito** y los revierte.
5. El paso nuevo del job `backend` en `.github/workflows/ci.yml`.

## Trust boundaries

| # | Frontera | Qué la cruza | Nivel de confianza |
|---|---|---|---|
| TB-1 | Archivo del repositorio → parser | El contenido de `frontend/src/api/tipos.ts` | **Confiable**: versionado, revisado en PR. No es entrada de usuario |
| TB-2 | API levantada en memoria → comparador | El JSON de respuesta | Confiable: lo emite el código de este mismo repositorio |
| TB-3 | Script → árbol de trabajo | Escrituras y reversión sobre archivos `.cs` del backend | **La frontera peligrosa**: el script escribe en el repositorio |
| TB-4 | Runner de CI → repositorio | El checkout y el token del workflow | Confiable con permisos de sólo lectura, ya fijados en `ci.yml` (`contents: read`) |
| TB-5 | Test → base `gestiongastos_test` | Siembra y lectura | Confiable: base efímera, aislada de producción (ADR-002) |

**No hay frontera con ningún servicio externo**: no se agrega ninguna dependencia, así que no hay
superficie de cadena de suministro nueva. Eso no es una casualidad del diseño, es NFR-03 haciendo su
trabajo.

## STRIDE por componente

### 1. `LectorDeTiposDelFrontend` (TB-1)

| Categoría | Análisis |
|---|---|
| **S** Spoofing | No hay identidades. El archivo se localiza por ruta relativa a la raíz del repositorio; no se acepta una ruta por parámetro ni por variable de entorno, así que no hay forma de sustituir el archivo leído sin commitear el cambio. |
| **T** Tampering | Alterar `tipos.ts` altera la verificación — **pero esa es la propiedad buscada**: el archivo es la especificación. Un cambio malicioso ahí es un cambio en un archivo revisado por PR, igual que cualquier otro. |
| **R** Repudiation | Cubierto por git: quién cambió el contrato queda en el historial. |
| **I** Information Disclosure | Lee un archivo público del repositorio. No hay secretos en `tipos.ts` y no debe haberlos nunca — es una definición de forma, no de valores. |
| **D** Denial of Service | Un archivo enorme o una regex catastrófica podrían colgar la corrida. → **R-01**. |
| **E** Elevation of Privilege | No hay privilegios que escalar; corre como el proceso de test. |

### 2. `ComparadorDeFormas` (TB-2)

| Categoría | Análisis |
|---|---|
| **S** / **R** / **E** | No aplican: función pura sobre dos estructuras en memoria. |
| **T** Tampering | El riesgo real es que el comparador sea **demasiado permisivo** y dé por verificado lo que no comparó. → **R-02**, el riesgo principal de este ticket. |
| **I** Information Disclosure | El mensaje de error incluye nombres de campo y el endpoint (AC-06), no valores. Un cuerpo sembrado no lleva datos personales. → **R-03**. |
| **D** Denial of Service | Recursión sobre interfaces anidadas: una referencia circular entre tipos colgaría el comparador. → **R-04**. |

### 3. Tests con `ApiFactory` y `BaseDeDatosFixture` (TB-5)

| Categoría | Análisis |
|---|---|
| **S** Spoofing | La API corre en entorno `Testing` con el usuario actual resuelto por el mismo mecanismo que el resto de los tests. Sin cambios. |
| **T** Tampering | Los tests siembran y borran en `gestiongastos_test`. Regla #0 de `testing.instructions.md`: crean lo suyo y limpian lo suyo. → **R-05**. |
| **R** Repudiation | No aplica. |
| **I** Information Disclosure | La cadena de conexión llega por variable de entorno, nunca en `appsettings` (convención del proyecto). El ticket no la toca. |
| **D** Denial of Service | ~10 peticiones HTTP en memoria; el costo dominante ya lo pagan los 142 tests existentes. Acotado por NFR-01. |
| **E** Elevation of Privilege | El filtro global de propietario sigue vigente; el ticket no lo toca ni usa `IgnoreQueryFilters()`. |

### 4. `backend/verificar-contrato.sh` (TB-3) — **el componente de mayor riesgo**

| Categoría | Análisis |
|---|---|
| **S** Spoofing | No aplica. |
| **T** Tampering | **Escribe sobre archivos `.cs` de producción y los revierte.** Si se interrumpe, deja el backend modificado; si alguien commitea en ese estado, entra un cambio que nadie escribió. → **R-06**. |
| **R** Repudiation | La modificación es transitoria y no se commitea. `git status` limpio al final es parte del criterio de cierre del bloque. |
| **I** Information Disclosure | No imprime contenido de archivos ni variables de entorno. |
| **D** Denial of Service | Compila tres veces; acotado por NFR-01. |
| **E** Elevation of Privilege | Corre con los permisos de quien lo invoca. No usa `sudo`, no escribe fuera del repositorio. |

### 5. Paso de CI (TB-4)

| Categoría | Análisis |
|---|---|
| **S** / **E** | El workflow ya declara `permissions: contents: read`. El paso nuevo no pide más. |
| **T** Tampering | El script modifica el árbol **del runner**, que es efímero y se descarta. No hay push. |
| **R** Repudiation | La corrida queda registrada en Actions. |
| **I** Information Disclosure | El log del paso muestra nombres de campo y endpoints, no valores ni secretos. |
| **D** Denial of Service | Acotado por NFR-01 (90 s) y AC-09. |

## Clasificación de datos (F-TM-05)

| Dato | Clasificación | Cifrado en tránsito | Cifrado en reposo |
|---|---|---|---|
| `frontend/src/api/tipos.ts` | **Público** — definición de forma, versionada | N/A (lectura local) | N/A |
| Cuerpos JSON de las respuestas verificadas | **Público en este contexto**: son datos sembrados por el fixture, no de usuarios reales | N/A (en memoria) | N/A |
| Cadena de conexión de la base de tests | **Credencial** | Variable de entorno del runner, nunca en el repositorio | Fuera de alcance: base efímera sin datos reales |

**No se manipula PII ni datos financieros reales.** Los montos que aparecen en los cuerpos
verificados son los que el fixture siembra. Por eso F-TM-07 no impone cifrado nuevo: no hay PII ni
credenciales nuevas en juego.

## Riesgos

### 🟠 R-02 (HIGH) — El comparador da por verificado lo que no comparó

**Categoría:** Tampering · **Probabilidad:** Alta · **Impacto:** Alto

Es el riesgo que define el ticket. Si el parser no entiende un tipo y lo saltea, o si el comparador
recorre sólo el primer nivel, o si un arreglo vacío se lee como "todo bien", el resultado es una
barrera que siempre está en verde. **Y una barrera que nunca falla es indistinguible de no tener
barrera, salvo por la confianza que genera** — que es peor, porque el hueco original al menos se
sabía abierto.

Es literalmente el mismo modo de falla que el ticket combate, un nivel más arriba.

**Mitigación, ya plegada a la spec:**
- El parser es **estricto**: lo que no reconoce lanza, no omite (Block 1, con tres sad paths).
- `Contrato_TodoTipoDeRespuesta_EstaVerificadoOExcluidoConMotivo` falla si `tipos.ts` gana un tipo
  que nadie verifica ni excluye (Block 2, AC-14).
- `Contrato_SiLaColeccionVieneVacia_FallaEnVezDeDarPorVerificado` (Block 2).
- `Contrato_SiElEndpointNoDevuelve2xx_FallaSinReportarVerificado` (Block 2, AC-15).
- Y sobre todo `backend/verificar-contrato.sh` (Block 4, NFR-04): comprueba que la verificación
  **falla de verdad** ante un desalineamiento deliberado. Sin eso, todo lo anterior es una promesa.

### 🟠 R-06 (HIGH) — El script deja el backend modificado

**Categoría:** Tampering · **Probabilidad:** Media · **Impacto:** Alto

`verificar-contrato.sh` renombra un campo de un DTO de producción para comprobar que la verificación
falla. Si se interrumpe entre el paso 2 y el paso 3 —Ctrl-C, corte de energía, el runner cancelado
por `cancel-in-progress`— el árbol queda con un DTO alterado. Alguien que commitee sin mirar mete en
producción un cambio de contrato que nadie escribió, y este ticket habría **causado** exactamente el
defecto que vino a prevenir.

**Mitigación, ya plegada a la spec:**
- `trap … EXIT` que revierte en toda salida, incluida la interrumpida (Block 4).
- El script exige `git status` limpio al terminar y sale distinto de 0 si no lo está (Block 4, error
  documentado y su test).
- La reversión no reconstruye el archivo a mano: se restaura el original guardado antes de tocarlo.
- El precedente existe y funciona: `verificar-linter.sh` usa el mismo patrón desde FIX-001 y su
  corrida real en CI (run 32484099632) dejó el árbol limpio.

### 🟡 R-01 (MEDIUM) — ReDoS o archivo desmedido en el parser

**Categoría:** Denial of Service · **Probabilidad:** Baja · **Impacto:** Medio

El parser usa expresiones regulares sobre un archivo del repositorio. Un patrón con cuantificadores
anidados podría degradarse. El archivo es propio y hoy tiene ~100 líneas, así que la probabilidad es
baja, pero la lección de FIX-003 está fresca: una regex construida sin cuidado es un problema real.

**Mitigación:** los patrones se escriben sin cuantificadores anidados, y el parseo se hace línea por
línea en vez de con una regex sobre el archivo entero. Se verifica en el SAST del cierre de CODE,
igual que se hizo en FIX-003.

### 🟡 R-04 (MEDIUM) — Recursión infinita por tipos mutuamente referenciados

**Categoría:** Denial of Service · **Probabilidad:** Baja · **Impacto:** Medio

El comparador recorre interfaces anidadas. Hoy el grafo de `tipos.ts` es acíclico, pero nada lo
garantiza a futuro: `A { b: B }` y `B { a: A }` colgarían el test.

**Mitigación:** el recorrido lleva el conjunto de tipos ya visitados en la rama actual y lanza al
detectar un ciclo, nombrando los tipos involucrados. Va como parte del Block 2.

### 🟢 R-03 (LOW) — Los mensajes de error exponen la forma del contrato

Los mensajes incluyen nombres de campo y endpoints. Es información que ya está en el repositorio
público y en el JavaScript que el navegador descarga. No hay exposición nueva.

### 🟢 R-05 (LOW) — Contaminación de la base de tests entre casos

Los tests nuevos siembran datos. La regla #0 de `testing.instructions.md` ya gobierna esto y
`BaseDeDatosFixture` ya lo resuelve para los 142 tests existentes; los nuevos siguen el mismo
patrón, sin operaciones destructivas sobre datos que no crearon.

## Riesgos aceptados

**Ninguno.** Los dos HIGH tienen mitigación concreta plegada a la spec, y los dos MEDIUM también. No
hace falta que apruebes ningún riesgo residual.

## Superficie que este ticket NO agrega

Vale la pena dejarlo escrito, porque es la mitad del valor del diseño elegido:

- **0 dependencias nuevas** → 0 superficie de cadena de suministro. La opción de generar tipos desde
  OpenAPI habría agregado un paquete al backend que corre en cada build; la de validar en runtime,
  una librería que corre en el navegador de cada usuario.
- **0 endpoints nuevos** y ninguno modificado.
- **0 cambios en producción**: ni en `GestionGastos.Api/` ni en `frontend/src/`.
- **0 cambios de esquema** y ninguna migración.
- **0 permisos nuevos** en el workflow de CI.
