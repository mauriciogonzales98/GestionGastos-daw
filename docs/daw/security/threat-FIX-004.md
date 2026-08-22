# Threat model FIX-004 — El sembrado de rendimiento vence el 2027-01-01

| Field | Value |
|-------|-------|
| Ticket | FIX-004 |
| Fix-plan | docs/daw/specs/fix-FIX-004.md |
| PRD | docs/daw/prd/prd-FIX-004.md |
| Fecha | 2026-08-22 |
| Resultado | **PASSED** — 0 CRITICAL, 0 HIGH, 1 MEDIUM mitigado, 0 riesgos aceptados |

## Qué se está analizando

Tres archivos, todos dentro de `backend/GestionGastos.Api.Tests/`:

| Archivo | Cambio |
|---|---|
| `Infra/MedicionDeRendimiento.cs` | `GenerarFechasSembradas(DateOnly ancla)` como función pura; `FechasSembradas` pasa a envoltorio con ancla relativa |
| `Movimientos/RendimientoListadoTests.cs` | `Desde`/`Hasta` dejan de ser literales |
| `Infra/GeneradorDeFechasSembradasTests.cs` | nuevo, prueba el generador con fechas simuladas |

**Cero archivos de producción.** NFR-02 lo exige y AC-10 lo verifica con `git diff`, no por afirmación.

Este análisis es corto **porque la superficie es corta**, no por trámite. Un threat model genérico
sobre un cambio de fixture sería teatro: lo que sigue son las preguntas que este cambio concreto
merece, y sus respuestas.

## Trust boundaries

| # | Frontera | Qué la cruza | Nivel |
|---|---|---|---|
| TB-1 | Reloj del sistema → generador de fechas | `DateTime.Today` en el envoltorio | **Confiable con matiz**: es el reloj del runner, no entrada de usuario, pero es la única entrada no determinista del cambio → R-01 |
| TB-2 | Generador → base de datos de tests | 1000 filas sembradas en `gestiongastos_test` | Confiable: base efímera, aislada de producción (ADR-002) |

**No hay frontera nueva.** El cambio no agrega entradas externas, ni red, ni archivos, ni
dependencias. La única entrada que existía —el reloj— sigue siendo la misma, sólo que ahora está
explícita en una firma en vez de implícita en un literal.

## STRIDE

Aplicado a los dos componentes que cambian, con las seis categorías.

### `GenerarFechasSembradas(DateOnly ancla)` — función pura

| Categoría | Análisis |
|---|---|
| **S** Spoofing | No hay identidades. La función no consulta nada: recibe una fecha y devuelve una lista. |
| **T** Tampering | Alterar la función altera qué mide el arnés — pero eso es el cambio, y el test del paso 3 es lo que detecta que alguien la rompa. Es la propiedad buscada, no un riesgo. |
| **R** Repudiation | Cubierto por git. |
| **I** Information Disclosure | Devuelve fechas calculadas. No lee ni emite datos de usuario. |
| **D** Denial of Service | Genera 1000 elementos con aritmética de calendario. Costo constante y acotado. → ver R-01 para el caso patológico. |
| **E** Elevation of Privilege | Sin privilegios en juego; corre en el proceso de test. |

### El sembrado en la base de tests

| Categoría | Análisis |
|---|---|
| **T** Tampering | Escribe 1000 filas en `gestiongastos_test`, exactamente como antes. Lo único que cambia son los valores del campo `fecha`. `BaseDeDatosFixture.LimpiarAsync()` sigue gobernando el aislamiento entre tests, sin cambios. |
| **I** Information Disclosure | Los montos y fechas son sintéticos —100, 101, 102…— y no representan datos de nadie. |
| **D** Denial of Service | Mismo volumen que antes: 1000 filas. NFR-01 lo congela. |
| **S** / **R** / **E** | Sin cambios respecto del comportamiento actual. |

## Clasificación de datos (F-TM-05)

| Dato | Clasificación | En tránsito | En reposo |
|---|---|---|---|
| Fechas generadas | **Público** — aritmética de calendario, sin relación con datos reales | N/A | N/A |
| Movimientos sembrados | **Sintético**, no PII: montos correlativos desde 100, sin usuario real, en base efímera | N/A | Fuera de alcance: base que se limpia entre tests |

**No hay PII ni credenciales en juego**, así que F-TM-07 no impone cifrado nuevo. Nada cambia en ese
plano respecto de lo que ya hacía el arnés.

## Riesgos

### 🟡 R-01 (MEDIUM) — El arreglo introduce su propia dependencia del reloj

**Categoría:** Denial of Service (de la señal, no del servicio) · **Probabilidad:** Media ·
**Impacto:** Medio

Es el único riesgo real del ticket, y es una ironía que conviene nombrar: **el arreglo de un defecto
causado por el tiempo puede introducir otro defecto causado por el tiempo.** Un sembrado derivado de
"hoy" puede comportarse distinto según el día del mes, el mes del año, o un año bisiesto — y el modo
de falla sería el mismo que hoy: una medición que no mide lo que dice.

No es hipotético. El propio repositorio ya tropezó con esto una vez, y por eso
`RangoDelMesTests.cs:48` prueba explícitamente un febrero bisiesto con un comentario que explica que
un último día fijo acierta un caso y falla el otro.

**Mitigación, plegada al fix-plan:**
- **El ancla es el año completo, no una ventana.** El peor caso está **calculado, no estimado**:
  `i % 365` sobre 1000 elementos deja 270 días con 3 filas y 95 con 2, así que el mínimo por día es
  2 y el peor mes posible —febrero común, 28 días— deja **56 filas** contra el piso de 2 que AC-01
  exige. No hay borde de calendario que demostrar caso por caso.
- **El generador es una función pura**, así que los bordes se prueban con fechas simuladas en vez de
  esperar a que el calendario llegue (paso 3, AC-02).
- **Se prueba con un febrero bisiesto**, siguiendo el precedente exacto de `RangoDelMesTests`.
- **`ConfirmarQueElMesTieneFilas()` se conserva** (FR-02) como última línea: si el ancla fallara en
  un borde que nadie previó, el arnés falla ruidosamente en vez de medir vacío.

### 🟢 R-02 (LOW) — El guardarraíl se debilita por parecer redundante

Una vez que el sembrado cubre el mes siempre, `ConfirmarQueElMesTieneFilas()` va a *parecer*
innecesario, y alguien podría borrarlo en una limpieza futura.

Mitigado por diseño: FR-02 y AC-04 lo declaran **requisito y no residuo**, y el fix-plan explica que
su valor no depende de que el sembrado esté bien hoy. Es LOW porque el daño requiere una acción
deliberada de alguien que además ignore el requisito escrito.

### 🟢 R-03 (LOW) — Alguien "arregla" los otros literales de 2026 por inercia

El impact scan encontró decenas de literales de fecha en 2026 en tests funcionales. **Ninguno
comparte este defecto** —comparan un dato contra sí mismo, no contra el reloj—, y volverlos
relativos los haría más frágiles, no menos.

Mitigado con documentación: el fix-plan cierra con una sección que lo dice explícitamente, para que
quien lea este ticket no propague el cambio donde no corresponde.

## Riesgos aceptados

**Ninguno.** El único MEDIUM tiene mitigación concreta plegada al plan, con su peor caso calculado.

## Superficie que este ticket NO agrega

- **0 dependencias** → 0 superficie de cadena de suministro.
- **0 archivos de producción**, verificado con `git diff` y no afirmado.
- **0 endpoints**, 0 cambios de esquema, 0 migraciones.
- **0 abstracciones de reloj en producción**: el PRD lo pone explícitamente fuera de alcance, y la
  solución no lo necesita — la parametrización vive en una función auxiliar de test.
