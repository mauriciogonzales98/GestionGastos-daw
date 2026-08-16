# PRD-001: Gestion de Gastos — Aplicacion para el registro y gestion de gastos personales

> Versión 5 del PRD: agrega límite de intentos de autenticación (RNF-05) y mínimos de
> accesibilidad (RNF-06), surgidos de la revisión de checklists de la spec 001. Los RNF y AC
> nuevos se numeran al final (RNF-05+, AC-54+).
> Versión 4: agrega una nota descriptiva opcional por movimiento (RF-33), para
> registrar en qué se gastó más allá de la categoría, sin abrir una segunda taxonomía.
> Versión 3: soporte de varias monedas (inicialmente pesos y dólares) con
> totales separados, sin conversión. Los RF y AC nuevos se numeran al final (RF-23+, AC-37+) en
> lugar de insertarse en el medio, para no invalidar las referencias que ya existen en el
> código y los tests.
> Las versiones anteriores quedaron en el historial de Git (`c681a0c`, `093fd1a`).

## Contexto y Problema

Llevar el control de los gastos personales suele requerir anotar todo en una hoja de
cálculo o app genérica, categorizando manualmente cada movimiento. Eso genera fricción:
la gente deja de registrar gastos porque cargarlos "bien" (con categoría, fecha, etc.)
toma tiempo. El resultado es que no se tiene una visión clara de en qué se gasta la plata.

**Persona: el usuario individual.** Una persona que quiere controlar sus gastos e ingresos
personales y que hoy usa —o ya abandonó— una planilla de cálculo. Necesita cargar un
movimiento en pocos segundos desde el formulario, sin pensar demasiado en cómo
clasificarlo, y poder responder después dos preguntas concretas: *en qué se me va la plata*
y *cómo vengo este mes*. No comparte sus finanzas con nadie dentro de la aplicación: sus
datos son privados y solo él los ve.

## Objetivos

- Registrar gastos e ingresos de manera simple mediante un formulario, con la menor
  cantidad de campos y decisiones posibles.
- Visualizar y consultar esos movimientos en un dashboard, con filtros por fecha y por
  categoría.
- Garantizar que cada usuario acceda únicamente a sus propios datos.

## Requerimientos Funcionales

### Cuentas y autenticación

- RF-01: El sistema debe permitir crear una cuenta de usuario indicando email y contraseña.
- RF-02: El sistema debe autenticar usuarios mediante email y contraseña.
- RF-03: El sistema debe requerir una sesión autenticada para acceder a cualquier pantalla o función de la aplicación.
- RF-04: El sistema debe restringir el acceso de cada usuario exclusivamente a sus propios movimientos y categorías.
- RF-05: El sistema debe permitir al usuario cerrar su sesión.

### Categorías

- RF-06: El sistema debe ofrecer un catálogo de categorías predefinidas, no modificables por el usuario, diferenciadas por tipo (gasto o ingreso).
- RF-07: El sistema debe permitir al usuario crear categorías propias, indicando nombre y tipo (gasto o ingreso).
- RF-08: El sistema debe permitir al usuario modificar el nombre de una categoría propia.
- RF-09: El sistema debe permitir al usuario eliminar una categoría propia mediante baja lógica, conservando su nombre en los movimientos ya registrados.

### Registro de movimientos

- RF-10: El sistema debe permitir registrar un gasto indicando monto, moneda, categoría y fecha mediante un formulario.
- RF-11: El sistema debe permitir registrar un ingreso indicando monto, moneda, categoría y fecha mediante un formulario.
- RF-12: El sistema debe proponer la fecha actual como valor por defecto del campo fecha en el formulario de registro.
- RF-13: El sistema debe rechazar el registro de un movimiento cuyo monto no sea un número mayor a cero con hasta dos decimales.
- RF-14: El sistema debe permitir modificar el monto, la moneda, la categoría y la fecha de un movimiento propio ya registrado.
- RF-15: El sistema debe permitir eliminar un movimiento propio ya registrado.
- RF-23: El sistema debe rechazar el registro de un movimiento que no tenga categoría asignada.
- RF-24: El sistema debe permitir registrar cada movimiento en una de las monedas del catálogo.
- RF-25: El sistema debe proponer la moneda marcada como predeterminada en el catálogo como valor por defecto del campo moneda del formulario de registro.
- RF-26: El sistema debe rechazar el registro de un movimiento cuya moneda no esté en el catálogo.
- RF-31: El sistema debe ofrecer un catálogo de monedas, no modificable por el usuario, que contiene inicialmente pesos y dólares.
- RF-32: El sistema debe permitir sumar una moneda al catálogo sin modificar el código de la aplicación.
- RF-33: El sistema debe permitir asociar a cada movimiento una nota descriptiva opcional de texto libre, de hasta 120 caracteres, y mostrarla en el listado. La nota es descriptiva: no clasifica ni agrupa movimientos.

### Listado de movimientos

- RF-16: El sistema debe listar los movimientos individuales (gastos e ingresos) del usuario autenticado.
- RF-17: El sistema debe permitir filtrar el listado de movimientos por categoría, tomando "todas las categorías" como valor por defecto.
- RF-18: El sistema debe permitir filtrar el listado de movimientos por rango de fechas, tomando el mes actual como valor por defecto.
- RF-27: El sistema debe mostrar la moneda de cada movimiento en el listado.
- RF-28: El sistema debe permitir filtrar el listado de movimientos por moneda, tomando "todas las monedas" como valor por defecto.

### Dashboard y resumen

- RF-19: El sistema debe mostrar, en una sección de dashboard, el total de gastos agrupado por categoría y por moneda, representado gráficamente (el tipo de gráfico específico queda a criterio de diseño de UI).
- RF-20: El sistema debe mostrar en el dashboard un balance por cada moneda, calculado como total de ingresos menos total de gastos de esa moneda.
- RF-21: El sistema debe permitir filtrar los datos del dashboard por rango de fechas.
- RF-22: El sistema debe mostrar en la pantalla principal un resumen con el total ingresado y el total gastado en el mes actual, discriminado por moneda.
- RF-29: El sistema debe calcular todo total, subtotal y balance sumando únicamente montos de una misma moneda.
- RF-30: El sistema debe permitir filtrar los datos del dashboard por moneda, tomando "todas las monedas" como valor por defecto.

## Requerimientos No Funcionales

- RNF-01: El dashboard debe cargar en < 2 s p95 con hasta 1000 movimientos registrados, y en < 4 s p95 con hasta 10000 movimientos registrados.
- RNF-02: El registro de un gasto o ingreso debe confirmarse (guardado) en < 1 s p95.
- RNF-03: Las contraseñas deben almacenarse con hash seguro (bcrypt o argon2), nunca en texto plano ni con cifrado reversible.
- RNF-04: La sesión debe expirar tras 24 h de inactividad.
- RNF-05: El sistema debe limitar los intentos fallidos de autenticación: tras 5 intentos fallidos consecutivos sobre un mismo email, debe rechazar todo intento nuevo sobre ese email durante al menos 15 minutos, sin revelar si el email está registrado.
- RNF-06: La aplicación debe cumplir un piso de accesibilidad verificable: el formulario de registro debe poder completarse y enviarse íntegramente con el teclado; todo control interactivo debe tener foco visible y una etiqueta asociada; el texto y los controles deben cumplir contraste AA (4.5:1 en texto normal, 3:1 en texto grande y en componentes de interfaz).

## Criterios de Aceptación

### Cuentas y autenticación

- AC-01 (RF-01): Dado un email no registrado, cuando el usuario completa email y contraseña y confirma el alta, entonces la cuenta queda creada y puede iniciar sesión con esas credenciales.
- AC-02 (RF-01): Dado un email ya registrado, cuando otro intento de alta usa el mismo email, entonces el sistema rechaza el alta y la cantidad de cuentas con ese email sigue siendo una.
- AC-03 (RF-02): Dado un usuario registrado, cuando ingresa su email y su contraseña correctos, entonces el sistema inicia sesión y lo lleva a la pantalla principal.
- AC-04 (RF-02): Dado un usuario registrado, cuando ingresa credenciales incorrectas, entonces el sistema rechaza el acceso y no inicia sesión.
- AC-05 (RF-03): Dado un usuario no autenticado, cuando intenta acceder a cualquier pantalla o acción de la aplicación, entonces el sistema lo redirige a login/registro sin ejecutar la acción.
- AC-06 (RF-04): Dado dos usuarios con movimientos propios, cuando el usuario A inicia sesión, entonces el listado y el dashboard muestran únicamente movimientos de A y ninguno de B.
- AC-07 (RF-04): Dado un movimiento perteneciente al usuario B, cuando el usuario A intenta consultarlo, modificarlo o eliminarlo indicando su identificador directamente, entonces el sistema deniega la operación y el movimiento de B queda sin cambios.
- AC-08 (RF-04): Dado el usuario A con sesión iniciada, cuando intenta registrar un movimiento indicando al usuario B como propietario, entonces el movimiento queda asociado a A y el listado de B no cambia.
- AC-09 (RF-05): Dado un usuario con sesión iniciada, cuando cierra sesión, entonces el sistema lo lleva a la pantalla de login y un nuevo intento de acceder a una pantalla de la aplicación vuelve a exigir autenticación.

### Categorías

- AC-10 (RF-06): Dado un usuario recién registrado sin categorías propias, cuando abre el formulario de registro de un gasto, entonces el selector ofrece las categorías predefinidas de tipo gasto y ninguna de tipo ingreso.
- AC-11 (RF-06): Dada una categoría predefinida del sistema, cuando el usuario intenta modificarla o eliminarla, entonces el sistema rechaza la operación y la categoría queda sin cambios.
- AC-12 (RF-07): Dado un usuario autenticado, cuando crea una categoría propia de tipo gasto, entonces esa categoría aparece en el selector de gastos de ese usuario y no aparece para ningún otro usuario.
- AC-13 (RF-08): Dada una categoría propia con movimientos asociados, cuando el usuario cambia su nombre, entonces el listado y el dashboard muestran el nombre nuevo en esos movimientos.
- AC-14 (RF-09): Dada una categoría propia con movimientos asociados, cuando el usuario la elimina, entonces deja de ofrecerse en el formulario de registro, los movimientos existentes siguen mostrando su nombre y sus montos siguen sumando en el total por categoría del dashboard.

### Registro de movimientos

- AC-15 (RF-10): Dado que el usuario completa monto, moneda, categoría y fecha de un gasto, cuando lo guarda, entonces el gasto aparece en el listado y su monto suma al total de esa categoría en esa moneda en el dashboard.
- AC-16 (RF-11): Dado que el usuario completa monto, moneda, categoría y fecha de un ingreso, cuando lo guarda, entonces el monto se refleja en el resumen del mes actual (RF-22) y en el balance del dashboard (RF-20), en la moneda del movimiento.
- AC-17 (RF-12): Dado que el usuario completa monto y categoría sin tocar el campo fecha, cuando guarda, entonces el movimiento queda registrado con la fecha del día actual.
- AC-18 (RF-13): Dado un formulario con monto vacío, monto igual o menor a cero, o monto con más de dos decimales, cuando el usuario intenta guardar, entonces el sistema rechaza el guardado, muestra el motivo y no se crea ningún movimiento.
- AC-19 (RF-14): Dado un movimiento propio ya registrado, cuando el usuario modifica su monto y guarda, entonces el listado, el total por categoría y el balance del dashboard reflejan el monto nuevo y no el anterior.
- AC-20 (RF-14): Dado un movimiento propio ya registrado, cuando el usuario cambia su categoría y su fecha y guarda, entonces su monto deja de sumar en el total de la categoría anterior y suma en el de la nueva, y el movimiento aparece en el listado solo si su fecha nueva cae dentro del período filtrado.
- AC-21 (RF-15): Dado un movimiento propio ya registrado, cuando el usuario lo elimina, entonces deja de aparecer en el listado y su monto deja de sumar en el dashboard.
- AC-50 (RF-33): Dado que el usuario completa monto, categoría y una nota, cuando guarda, entonces el movimiento queda registrado con esa nota y el listado la muestra junto al movimiento.
- AC-51 (RF-33): Dado un formulario con la nota vacía, cuando el usuario guarda, entonces el movimiento se registra sin nota y el listado lo muestra sin error ni texto de relleno.
- AC-52 (RF-33): Dado un formulario con una nota de más de 120 caracteres, cuando el usuario intenta guardar, entonces el sistema rechaza el guardado, muestra el motivo y no se crea ningún movimiento.
- AC-53 (RF-33): Dado un movimiento propio con nota, cuando el usuario la edita o la borra y guarda, entonces el listado refleja el valor nuevo y los totales del dashboard no cambian.

### Listado de movimientos

- AC-22 (RF-16): Dado un usuario con gastos e ingresos cargados dentro del período vigente del filtro, cuando abre el listado, entonces ve todos esos movimientos individuales, tanto gastos como ingresos.
- AC-23 (RF-17): Dado que el usuario selecciona una categoría, cuando aplica el filtro, entonces el listado muestra únicamente movimientos de esa categoría.
- AC-24 (RF-17): Dado que el usuario no aplica ningún filtro de categoría, cuando abre el listado, entonces ve movimientos de todas sus categorías.
- AC-25 (RF-18): Dado que el usuario no aplica ningún filtro de fecha, cuando abre el listado, entonces ve únicamente los movimientos cuya fecha cae dentro del mes actual.
- AC-26 (RF-18): Dado que el usuario selecciona un rango de fechas, cuando aplica el filtro, entonces el listado muestra únicamente los movimientos cuya fecha cae dentro de ese rango, incluidos sus extremos.

### Dashboard y resumen

- AC-27 (RF-19): Dados gastos cargados en distintas categorías, cuando el usuario abre el dashboard, entonces para cada categoría el total mostrado es igual a la suma de los montos de los gastos de esa categoría dentro del período filtrado, y esos totales se representan gráficamente.
- AC-28 (RF-20): Dados gastos e ingresos cargados, cuando el usuario abre el dashboard, entonces el balance mostrado para cada moneda es igual a la suma de los montos de los ingresos de esa moneda menos la suma de los montos de los gastos de esa moneda, dentro del período filtrado.
- AC-29 (RF-21): Dado que el usuario selecciona un rango de fechas en el filtro del dashboard, cuando lo aplica, entonces los totales por categoría y el balance de cada moneda se calculan únicamente con los movimientos cuya fecha cae dentro de ese rango, incluidos sus extremos.
- AC-30 (RF-22): Dados gastos e ingresos cargados en el mes actual, cuando el usuario entra a la pantalla principal, entonces el total ingresado y el total gastado de cada moneda son iguales a los que muestra el dashboard de esa moneda filtrado por el mes actual.
- AC-31 (RF-19, RF-20, RF-22): Dado un usuario sin ningún movimiento en el período filtrado, cuando abre la pantalla principal y el dashboard, entonces el total ingresado, el total gastado y el balance se muestran en cero para cada moneda, el gráfico por categoría indica que no hay datos, y no se muestra ningún mensaje de error.

### Monedas

- AC-37 (RF-24): Dado que el usuario completa un gasto y elige dólares como moneda, cuando lo guarda, entonces el movimiento queda registrado en dólares y el listado lo muestra en dólares.
- AC-38 (RF-25): Dado que el usuario completa monto y categoría sin tocar el campo moneda, cuando guarda, entonces el movimiento queda registrado en la moneda predeterminada del catálogo, que inicialmente es pesos.
- AC-39 (RF-26): Dado un formulario con una moneda que no está en el catálogo, cuando el usuario intenta guardar, entonces el sistema rechaza el guardado, muestra el motivo y no se crea ningún movimiento.
- AC-48 (RF-31): Dado un usuario autenticado, cuando abre el formulario de registro, entonces el selector de moneda ofrece exactamente las monedas del catálogo, y exactamente una figura como predeterminada.
- AC-49 (RF-32): Dada una moneda agregada al catálogo únicamente como dato, cuando el usuario abre el formulario de registro y el filtro de moneda, entonces esa moneda aparece en ambos y se puede registrar un movimiento con ella, sin haber modificado el código de la aplicación.
- AC-40 (RF-23): Dado un formulario sin categoría seleccionada, cuando el usuario intenta guardar, entonces el sistema rechaza el guardado, muestra el motivo y no se crea ningún movimiento.
- AC-41 (RF-19, RF-29): Dados gastos en pesos y en dólares cargados en una misma categoría y período, cuando el usuario abre el dashboard, entonces el total en pesos de esa categoría es igual a la suma de los gastos en pesos, el total en dólares es igual a la suma de los gastos en dólares, y ningún total incluye montos de la otra moneda.
- AC-42 (RF-20, RF-29): Dados ingresos y gastos en las dos monedas dentro del período filtrado, cuando el usuario abre el dashboard, entonces ve un balance en pesos igual a los ingresos en pesos menos los gastos en pesos, y un balance en dólares igual a los ingresos en dólares menos los gastos en dólares.
- AC-43 (RF-22, RF-29): Dados ingresos y gastos en las dos monedas en el mes actual, cuando el usuario entra a la pantalla principal, entonces ve el total ingresado y el total gastado de cada moneda por separado, y ningún total mezcla las dos.
- AC-44 (RF-27): Dados dos movimientos del mismo monto, uno en pesos y otro en dólares, cuando el usuario abre el listado, entonces cada fila indica en qué moneda está su monto.
- AC-45 (RF-28): Dado que el usuario filtra el listado por dólares, cuando aplica el filtro, entonces ve únicamente movimientos en dólares; y sin filtro de moneda ve los de las dos monedas.
- AC-46 (RF-30): Dado que el usuario filtra el dashboard por dólares, cuando aplica el filtro, entonces los totales por categoría y el balance que ve son únicamente los de dólares; y sin filtro de moneda ve los de las dos monedas.
- AC-47 (RF-14): Dado un movimiento propio registrado en pesos, cuando el usuario cambia su moneda a dólares y guarda, entonces su monto deja de sumar en los totales en pesos y suma en los totales en dólares.

### No funcionales

- AC-32 (RNF-01): Dada una cuenta con 1000 movimientos, cuando se mide la carga del dashboard sobre 100 ejecuciones, entonces el percentil 95 del tiempo hasta ver los datos es < 2 s.
- AC-33 (RNF-01): Dada una cuenta con 10000 movimientos, cuando se mide la carga del dashboard sobre 100 ejecuciones, entonces el percentil 95 del tiempo hasta ver los datos es < 4 s.
- AC-34 (RNF-02): Dado el formulario de registro completo y válido, cuando se mide el guardado sobre 100 ejecuciones, entonces el percentil 95 del tiempo hasta la confirmación es < 1 s.
- AC-35 (RNF-03): Dada una cuenta recién creada, cuando se inspecciona el registro del usuario en la base de datos, entonces el campo de contraseña no contiene la contraseña en texto plano y su valor corresponde a un hash bcrypt o argon2.
- AC-36 (RNF-04): Dada una sesión iniciada sin actividad durante más de 24 h, cuando el usuario intenta acceder a una pantalla de la aplicación, entonces el sistema exige autenticarse nuevamente.
- AC-54 (RNF-05): Dados 5 intentos de login fallidos consecutivos sobre un mismo email, cuando se realiza un sexto intento —aun con la contraseña correcta—, entonces el sistema lo rechaza y sigue rechazándolo durante al menos 15 minutos, con el mismo motivo que un intento de credenciales incorrectas.
- AC-55 (RNF-06): Dado el formulario de registro de un movimiento, cuando se lo recorre y se lo envía usando únicamente el teclado, entonces se puede completar y guardar un movimiento, cada control recorrido muestra foco visible y tiene una etiqueta asociada.

## Fuera de Alcance

- Cuentas compartidas, gastos compartidos o visibilidad de los movimientos de otro usuario. Cada cuenta es individual y privada; lo que sí entra es que existan varias cuentas aisladas entre sí (RF-04).
- Conexión con bancos o tarjetas (APIs bancarias, Plaid, etc.)
- Notificaciones push o recordatorios
- Exportación a otros formatos (PDF, Excel)
- Presupuestos y alertas de tope
- Entrada de datos por voz o imagen (ej: foto de un ticket/recibo)
- Registro de movimientos por texto libre en lenguaje natural con extracción por IA
- Recuperación de contraseña olvidada y cambio de contraseña
- Modificación o eliminación de las categorías predefinidas del sistema
- Movimientos recurrentes o programados
- Conversión de divisas: no hay cotización, ni total consolidado, ni balance único. Los montos de cada moneda se suman por separado y se muestran por separado (RF-29). Lo que sí entra es registrar en varias monedas (RF-24)
- Alta, edición y baja de monedas desde la interfaz: el catálogo se administra como dato (RF-32), no hay pantalla para gestionarlo
- Adjuntar comprobantes o archivos a un movimiento
- Buscar, filtrar, agrupar o totalizar por la nota del movimiento (RF-33): la nota se lee, no se analiza. Lo que sí entra es registrarla y verla en el listado
- Etiquetas reutilizables sobre los movimientos: una segunda dimensión de clasificación además de la categoría

## Riesgos y Dependencias

- Riesgo: la categorización manual repetitiva puede generar la misma fricción que se buscaba evitar → mitigación: catálogo de categorías predefinido y acotado (RF-06). El único campo libre del formulario es la nota (RF-33), que es opcional y no clasifica: el usuario nunca tiene que escribir nada para registrar un movimiento.
- Riesgo: la nota libre (RF-33) puede convertirse en una segunda taxonomía informal ("alquiler", "Alquiler", "alq") que el sistema no entiende y que da una falsa sensación de estar clasificando → mitigación: la nota no se busca, no se filtra y no se agrupa (ver Fuera de Alcance). La categoría sigue siendo el único eje de análisis del dashboard. Si aparece la necesidad real de totalizar por algo más fino, se resuelve con un catálogo de etiquetas, no estirando la nota.
- Riesgo: permitir categorías propias (RF-07) reintroduce esa fricción si el usuario crea muchas categorías casi iguales → mitigación: las predefinidas son la opción por defecto y la creación de una categoría propia es una acción aparte, fuera del camino rápido de carga.
- Riesgo: la baja lógica de categorías (RF-09) puede hacer reaparecer categorías eliminadas en selectores o filtros si las consultas no excluyen las dadas de baja → mitigación: AC-14 verifica explícitamente el comportamiento en formulario, listado y dashboard.
- Riesgo: el objetivo de RNF-01 con 10000 movimientos puede no alcanzarse si los totales se calculan en el cliente → mitigación: agregar los totales en la consulta a la base de datos, no en el frontend.
- Riesgo: con dos monedas, un total puede sumar montos de ambas por descuido y dar un número sin significado → mitigación: RF-29 lo prohíbe explícitamente y AC-41, AC-42 y AC-43 lo verifican sobre datos cargados en las dos monedas.
- Riesgo: cargar un movimiento en la moneda equivocada por no mirar el selector → mitigación: pesos como valor por defecto (RF-25), la moneda visible en cada fila del listado (RF-27) y la posibilidad de corregirla sin borrar el movimiento (RF-14).
- Dependencia: base de datos MySQL disponible para persistir usuarios, categorías y movimientos.
- Dependencia: biblioteca de hashing de contraseñas (bcrypt o argon2) para cumplir RNF-03.

## Supuestos abiertos

Estos puntos se resolvieron para poder escribir requerimientos verificables, pero no
fueron pedidos explícitamente y conviene confirmarlos:

- Las categorías tienen tipo (gasto o ingreso) y el formulario solo ofrece las del tipo que
  se está cargando. Se deriva de que las listas de gastos e ingresos son distintas.
- Catálogo predefinido propuesto — gastos: Comida, Transporte, Vivienda, Servicios, Salud,
  Ocio, Otros; ingresos: Sueldo, Ingreso extra, Otros.
- Las categorías son compartidas entre monedas: "Comida" es la misma categoría se gaste en
  pesos o en dólares, y el dashboard la abre por moneda al totalizar. La alternativa
  (una categoría por moneda) duplicaría el catálogo predefinido.
- Los decimales admitidos son un dato de cada moneda y no una constante: pesos y dólares
  usan dos (RF-13), pero una moneda sin centavos usaría cero.
- Las monedas se identifican por su código ISO 4217 ("ARS", "USD"). "Pesos" sería ambiguo:
  hay pesos argentinos, mexicanos, chilenos y colombianos.
