# SAST FIX-004 — El sembrado de rendimiento vence el 2027-01-01

| Field | Value |
|-------|-------|
| Ticket | FIX-004 |
| Tier | FIX |
| Fecha | 2026-08-22 |
| Resultado | **PASSED** — 0 vulnerabilidades, 0 supresiones |

## Superficie analizada

Tres archivos, **todos bajo `backend/GestionGastos.Api.Tests/`**:

| Archivo | Cambio |
|---|---|
| `Infra/MedicionDeRendimiento.cs` | `GenerarFechasSembradas(DateOnly hoy)` extraída como función pura; el ancla pasa a derivarse del año en curso |
| `Movimientos/RendimientoListadoTests.cs` | los dos literales de marzo de 2026 pasan a derivarse del mismo reloj |
| `Infra/GeneradorDeFechasSembradasTests.cs` | nuevo, 8 casos con fechas simuladas |

> **0 archivos de producción**, verificado con `git diff --name-only main...HEAD` filtrado por
> `backend/GestionGastos.Api/` y `frontend/src/`: vacío.

## Resultado por regla

```
Secretos
  ✅ F-SAST-01: sin coincidencias en el diff. El cambio son fechas calculadas

Inyección
  ✅ F-SAST-02: sin SQL nuevo. El sembrado usa el mismo AddRange de EF que antes
  ✅ F-SAST-03: sin exec/spawn, sin shell
  ✅ F-SAST-05: sin rutas de archivo

XSS y funciones inseguras
  ✅ F-SAST-04 / 06 / 08 / 17: sin eval, sin deserialización, sin criptografía,
     sin render de HTML. El frontend no se toca

Resto de categorías
  ✅ F-SAST-07 / 09 / 10 / 11 / 12 / 14 / 15: sin superficie

Dependencias
  ✅ F-SAST-13 / F-SAST-16: 0 dependencias nuevas
```

## Lo único con algo que analizar: la entrada no determinista

El cambio convierte una constante en una lectura de `DateTime.Today`. Vale mirarlo aunque el
veredicto sea benigno:

| Pregunta | Respuesta |
|---|---|
| ¿Es entrada de usuario? | **No.** Es el reloj del proceso de test. No viaja por la red, no lo controla nadie externo |
| ¿Puede manipularse para provocar algo? | Sólo cambiando el reloj de la máquina que corre los tests — quien puede hacer eso ya ejecuta código en ella. No es un vector nuevo |
| ¿El generador valida su entrada? | No la necesita: acepta cualquier `DateOnly` y usa sólo el año. El test `Generador_ConDistintosDiasDelMismoAnio_ProduceExactamenteLoMismo` lo fija |
| ¿Puede desbordar o colgar? | No. `Enumerable.Range(0, 1000)` con `i % 365` y `AddDays`; costo constante, sin recursión ni backtracking |

**El ancla es el año, no la fecha**, así que la superficie no determinista se reduce a un entero
entre 1 y 9999. Es menos, no más, que antes: la versión anterior también dependía del reloj —a través
del mes en curso que el resumen calcula— sólo que sin declararlo.

## Verificación de que la barrera detecta

Se reintrodujo el año literal (`new DateOnly(2026, 1, 1)`) y **6 de los 8 tests nuevos se pusieron en
rojo**, incluidos los tres del defecto original y el del febrero bisiesto. Los 2 que siguieron
pasando son los que no dependen del año —volumen y estabilidad frente al día—, lo cual es correcto.

Mutación revertida; `git diff` limpio sobre ese archivo.

## Supresiones

Ninguna.

## Veredicto

**PASSED.** 0 Critical, 0 High, 0 Medium, 0 Low. Gate `sast` cumplido.

---

## Ronda 2 — reanálisis tras el bucle correctivo (2026-08-22)

El bucle a CODE corrigió W-1 y W-2 de la verificación ronda 1. **La superficie que este análisis
tiene que revisar es la que la ronda 2 agrega**, no la del ticket entero — eso ya está arriba.

### Qué cambió, medido

`git diff --stat`: 2 archivos, 29 inserciones, 12 borrados, **ambos bajo
`backend/GestionGastos.Api.Tests/`**.

| Archivo | Cambio de la ronda 2 | Superficie |
|---|---|---|
| `Infra/MedicionDeRendimiento.cs` | Sólo el comentario XML de `GenerarFechasSembradas`: se reemplaza el peor caso mal calculado (56 en febrero) por el real (60 en noviembre) y su razonamiento | **Ninguna** — no cambia una sola línea ejecutable |
| `Infra/GeneradorDeFechasSembradasTests.cs` | Una `[Theory]` de 2 casos se parte en 2 `[Fact]` que afirman cosas distintas: `Assert.Contains(29/02)` + 29 días en bisiesto, `Assert.DoesNotContain` + 28 en común | Aritmética de `DateOnly` en memoria |

### Categorías obligatorias

| Regla | Verificación | Resultado |
|---|---|---|
| F-SAST-01 secretos | `git diff` filtrado por `password\|token\|secret\|conn` sobre las líneas agregadas | ✅ 0 coincidencias |
| F-SAST-02 inyección SQL | La ronda 2 no toca ninguna consulta ni `DbContext` | ✅ N/A |
| F-SAST-03 comandos | Filtrado por `Process\|exec` sobre lo agregado | ✅ 0 coincidencias |
| F-SAST-04 / F-SAST-17 deserialización | Filtrado por `eval\|Deserial` | ✅ 0 coincidencias |
| F-SAST-05 path traversal | Filtrado por `File.\|Path.` | ✅ 0 coincidencias |
| F-SAST-06 XSS | 0 archivos de frontend en el diff | ✅ N/A |
| F-SAST-07 SSRF | Filtrado por `http` | ✅ 0 coincidencias |
| F-SAST-08 cripto débil | No hay cripto en el diff | ✅ N/A |
| F-SAST-09 debug en producción | 0 archivos de producción en el diff | ✅ N/A |
| F-SAST-10 logging sensible | Las aserciones nuevas no emiten datos; los mensajes son fechas sintéticas | ✅ |
| F-SAST-11 / F-SAST-12 upload, CSRF | Sin endpoints ni formularios en el diff | ✅ N/A |
| F-SAST-14 validación de entrada | `GenerarFechasSembradas` no cambia su cuerpo en esta ronda; su contrato ya se analizó arriba | ✅ sin cambios |
| F-SAST-15 fuga por errores | Los `Assert` fallan con mensajes de xUnit sobre fechas sintéticas | ✅ |
| F-SAST-13 / F-SAST-16 dependencias | `git diff` no toca `.csproj` ni `package.json` | ✅ 0 dependencias nuevas |

### Supresiones

**Ninguna.** No hubo hallazgos que suprimir.

### Resultado

**PASSED** — 0 Critical, 0 High, 0 Medium, 0 supresiones.

Vale decir por qué este reanálisis es corto y no por trámite: **una de las dos correcciones no toca
código ejecutable en absoluto** (es un comentario que decía un número equivocado) y la otra sustituye
aserciones de xUnit por otras aserciones de xUnit sobre `DateOnly`. Un análisis largo acá sería
teatro. Lo que sí ameritaba revisarse —que el bucle correctivo no hubiera arrastrado nada a
producción— está medido con `git diff --stat` y da 0 archivos fuera de `GestionGastos.Api.Tests/`.
