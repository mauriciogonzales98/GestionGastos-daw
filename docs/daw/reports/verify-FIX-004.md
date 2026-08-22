# Verificación FIX-004

## Ronda 1: PASSED, con un error propio confirmado

| Field | Value |
|-------|-------|
| Ticket | FIX-004 |
| Tier | FIX |
| Ronda | 1 |
| Fecha | 2026-08-22 |
| Resultado | **PASSED** — 0 FAIL, 3 WARN, 11 PASS |
| Verificador | `daw-module-verifier`, sobre código que no escribió |

```
FAILs: 0 | WARNs: 3 | PASSes: 11  →  PASSED
```

El verificador reejecutó la contra-prueba, la suite y los `git diff` en vez de aceptar los números
del cierre de CODE. Los once AC quedan verificados y el gate cumplido.

## Los once AC

| AC | Verificación |
|---|---|
| AC-01 | Piso de 2 filas en el mes, recalculado de forma independiente: mínimo real por día = 2. **Ver W-1**: el peor mes documentado estaba mal |
| AC-02, AC-06, AC-07 | Contra-prueba **reproducida por el verificador**: con el año literal reintroducido, 6 de 8 tests en rojo y 2 en verde —volumen y estabilidad frente al día—, exactamente como el cierre afirmaba. Reversión confirmada |
| AC-03 | Sin literales de año funcionales. El «2026» que queda está dentro del XML-doc, citando el defecto histórico como texto explicativo |
| AC-04 | `ConfirmarQueElMesTieneFilas()` presente y sin tocar. **Ver W-3** |
| AC-05 | `Desde`/`Hasta` derivan de `DateTime.Today.Year`; `esperadas` sigue saliendo de `FechasSembradas` |
| AC-09 | 184/184 backend reejecutado por el verificador; 1000 movimientos y 100 ejecuciones intactos |
| AC-10 | `git diff` sobre producción: vacío, reejecutado |
| AC-11 | 0 dependencias; presupuestos en 1 s y 2 s |

Los 4 pasos del fix-plan implementados, y el **paso 4 confirmado como predicción cumplida**:
`RendimientoResumenTests` no se tocó, y el verificador razonó que es correcto — el oráculo y el
guardarraíl de ese archivo ya eran relativos al reloj, así que el defecto vivía enteramente en el
sembrado.

## W-1 — El «peor mes» estaba mal calculado, en tres documentos

**Es un error propio, y conviene que quede escrito con su corrección.**

El RCA, el fix-plan y el XML-doc de `GenerarFechasSembradas` afirman: *«el peor mes posible —un
febrero común de 28 días— deja 56 filas»*. **Es falso.**

El razonamiento fue "mínimo por día × mes más corto", sin mirar **dónde** caen los días de mínimo.
Con `i % 365` sobre 1000 elementos, los offsets 0–269 llevan 3 filas y los 270–364 llevan 2: los días
de mínimo están en **los últimos 95 del año**, no en febrero. Recalculado:

```
ene 93 · feb 84 · mar 93 · abr 90 · may 93 · jun 90
jul 93 · ago 93 · sep 87 · oct 62 · nov 60 ← mínimo · dic 62
```

**El peor mes es noviembre con 60 filas**, y febrero —lejos de ser el mínimo— tiene 84.

No cambia ningún veredicto: 60 sigue siendo 30 veces el piso de 2 que AC-01 exige. Pero es una
afirmación falsa presentada como el peor caso real, en un comentario de código que alguien podría
citar más adelante.

**Corregido en la ronda 2** en el XML-doc, que es el lugar que se lee. El RCA y el fix-plan no se
pueden modificar desde CODE ni VERIFY, así que su errata queda registrada acá — misma tensión del
método que FIX-001 dejó anotada.

## W-2 — El test «bisiesto vs común» no distingue nada

`Generador_EnFebrero_CubreElMesEnBisiestoYEnComun` aplica **la misma aserción** a 2028 y a 2029:
`NotEmpty` más `Month == 2`. No compara conteos ni verifica el 29 de febrero.

No es un test vacío —puede fallar ante una regresión real del generador— pero **su nombre promete una
distinción que el test no hace**. En un ticket sobre arneses que no miden lo que dicen medir, eso es
particularmente incómodo.

**Corregido en la ronda 2.**

## W-3 — El rol del guardarraíl cambió, y no quedó dicho

Con el sembrado anclado al año completo, el mes en curso tiene siempre ≥60 filas. Si el generador se
rompiera, `GeneradorDeFechasSembradasTests` lo detectaría **antes** de que
`ConfirmarQueElMesTieneFilas()` llegara a activarse.

El guardarraíl pasó de **«última línea de defensa»** —como lo llaman el PRD y el RCA— a **defensa
redundante**. Sigue valiendo conservarlo: FR-02 lo pide, su costo es cero, y puede activarse ante una
rotura que el test unitario no cubra. Pero la descripción era optimista.

**Se registra y no se corrige**: tocar `RendimientoResumenTests` contradiría la predicción del paso 4
que el propio ticket verificó, y el cambio sería sólo de redacción.

## Acción

**Gate `verify` cumplido.** El usuario pidió corregir W-1 y W-2 antes de RELEASE en vez de
arrastrarlos, así que va un bucle a CODE por esos dos. W-3 queda como deuda declarada.
