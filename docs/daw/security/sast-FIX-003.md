# SAST FIX-003 — Comparación por subcadena en validate_prd.py

| Field | Value |
|-------|-------|
| Ticket | FIX-003 |
| Tier | QUICK-FIX |
| Fecha | 2026-08-21 |
| Resultado | **PASSED** — 0 vulnerabilidades, 0 supresiones |

## Superficie analizada

Un archivo, `.daw/scripts/validate_prd.py`: 14 líneas agregadas y 2 modificadas. De las 14, **10 son
el docstring** del helper nuevo; la lógica real son 3 líneas (el `return` de `_mentions` y los dos
puntos de llamada). No hay código de producción, ni frontend, ni backend, ni dependencias nuevas —
`re` ya estaba importado.

## Resultado por regla

```
Secretos
  ✅ F-SAST-01: sin coincidencias en el diff

Inyección
  ✅ F-SAST-02 / F-SAST-03 / F-SAST-05: sin SQL, sin exec/spawn, sin rutas de archivo
     construidas con entrada

XSS y funciones inseguras
  ✅ F-SAST-04 / F-SAST-06 / F-SAST-08 / F-SAST-17: sin eval, sin deserialización,
     sin criptografía, sin render de HTML

Resto de categorías
  ✅ F-SAST-07 / 09 / 10 / 11 / 12 / 14 / 15: sin superficie en este diff

Dependencias
  ✅ F-SAST-13 / F-SAST-16: ninguna agregada, quitada ni actualizada
```

## Lo único que merece análisis: una regex construida en tiempo de ejecución

El fix arma un patrón interpolando un valor — `rf"\b{re.escape(req_id)}\b"` — y eso es
exactamente la forma de dos problemas reales, así que van los dos revisados:

- **Inyección de patrón.** Mitigada por `re.escape`, que neutraliza cualquier metacarácter. Y
  además el valor interpolado no es libre: `req_id` sale de `REQ_ID = re.compile(r"\b(FR|NFR|AC)-(\d+)\b")`,
  así que solo puede ser un prefijo conocido seguido de dígitos. Dos capas, no una.
- **ReDoS.** El patrón resultante es `\b` + literal + `\b`: sin cuantificadores, sin alternancia,
  sin anidamiento, así que no hay backtracking que explotar. Medido sobre 1,4 M de caracteres del
  peor caso (`"NFR-01 "` repetido, que fuerza fallo de frontera en cada posición): **0,0146 s**.

El cambio **endurece** `F-PRD-01`, que es un control de calidad y no de seguridad, pero la dirección
importa: pasa de aceptar de más a aceptar lo justo. Un gate que se vuelve más estricto no abre
superficie.

## Supresiones

Ninguna.

## Veredicto

**PASSED.** 0 Critical, 0 High, 0 Medium, 0 Low. Gate `sast` cumplido.
