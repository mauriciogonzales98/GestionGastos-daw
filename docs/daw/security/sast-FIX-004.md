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
