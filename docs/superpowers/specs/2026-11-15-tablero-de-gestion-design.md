# Tablero de Gestión (inteligencia de negocios dentro del sistema) — diseño

Fecha: 15/11/2026 · Rama: `tablero-de-gestion` · Estado: aprobado por el usuario («Está bien así, hacelo»)

## Por qué

El usuario defiende el proyecto también en **Sistemas de Información Organizacional** (Administración), donde se vio
Power BI y el recorrido «del dato al control». Quiere que **el propio sistema** cumpla esa función, sin depender de
Power BI: «el sistema tiene que ser el que me apruebe». Cada parte del módulo tiene que poder señalarse en la defensa
como un concepto de la materia (tablero de comando / Balanced Scorecard, ciclo de control, segmentadores, filtro
cruzado, drill-down, sistema de apoyo a las decisiones, pirámide de los sistemas de información).

## Objetivos

1. Un módulo nuevo **Tablero de Gestión**, solo para Administrador y Contador, con cuatro pestañas:
   **Comando**, **Análisis**, **Decisiones** y **Exportar**.
2. Indicadores con **meta, semáforo y comparación con el período anterior**, en las cuatro perspectivas del Balanced
   Scorecard. Las metas las carga el Administrador y quedan guardadas en la base (ciclo de control: meta → real →
   desvío → corrección).
3. Análisis interactivo como en Power BI: segmentadores (período y plan), filtro cruzado (clic en un gráfico filtra
   los demás) y bajada al detalle (año → mes → día).
4. Apoyo a las decisiones: socios con riesgo de abandono con su acción sugerida y un simulador «qué pasaría si»
   de precios de los planes.
5. Exportar el tablero en PDF y los datos en Excel (que también abre Power BI).
6. Una sección del Manual, **«GymStudio como sistema de información»**, con la pirámide (operativo / táctico /
   estratégico) y qué parte del sistema cubre cada tipo de sistema (transaccional, de información gerencial, de apoyo
   a las decisiones, para ejecutivos).

## No objetivos

- No se inserta Power BI ni ningún servicio en la nube (la defensa es sin internet, con Docker).
- No se cargan datos nuevos: todo sale de lo que el sistema ya registra.
- No se cambia el Resumen de inicio ni ningún cálculo contable existente.
- No hay predicción con modelos estadísticos: las reglas de riesgo son explícitas y explicables.

## Anti-objetivos (sería un fracaso aunque funcione)

- Que un número del tablero no coincida con el mismo número en el Resumen, los libros o Recepción.
- Que Recepción pueda ver importes o metas (por pantalla o llamando a la API).
- Que los datos personales de los socios viajen al navegador más allá de lo que ya muestra Recepción.

## Enfoques considerados

| Enfoque | A favor | En contra | Decisión |
|---|---|---|---|
| **A. El servidor calcula los indicadores y agregados** (endpoints nuevos con su permiso); la pantalla dibuja con Recharts, que ya usa el Resumen | Datos personales no salen del servidor; cada consulta con permiso y pruebas de integración; mismo origen de datos que el Resumen | El filtro cruzado pide de nuevo al servidor (respuesta chica, rápida) | **Elegido** |
| B. Mandar todos los datos crudos y agregar en el navegador (como el modelo en memoria de Power BI) | Filtros instantáneos | Expone datos de socios; difícil de probar; crece con los datos | Descartado |
| C. Insertar Power BI (publicar en la web / embebido) | Es Power BI de verdad | Cuenta, licencia e internet; la defensa es sin conexión | Descartado |

## Definiciones (el contrato de los números)

Período = un mes (`aaaa-mm`); se compara con el mes anterior. «Hoy» = día de Argentina (como el Resumen). Facturas
anuladas no cuentan nunca. Importes netos de IVA, igual que el Resumen.

**Financiera**
- *Ingresos*: neto de ventas del mes (= «Ventas» del Resumen). Más es mejor. Con meta.
- *Resultado*: ventas netas − compras netas del mes (= «Resultado» del Resumen). Más es mejor. Con meta.
- *Margen*: resultado / ventas, en %. Sin ventas → «sin datos». Más es mejor. Con meta.
- *Caja + Banco*: saldo actual de los dos libros (= los del Resumen). Foto de hoy, sin comparación.

**Socios**
- *Socios activos*: socios activos con una membresía pagada vigente al cierre del período (último día del mes, o hoy si
  el mes es el actual). Se usan las membresías (historia) y no el vencimiento actual del socio, para que un mes pasado
  dé lo que dio en su momento. Más es mejor. Con meta.
- *Altas*: socios con `FechaAlta` dentro del mes.
- *Bajas*: socios que tenían membresía vigente al inicio del mes y no tienen ninguna vigente al cierre: no renovaron.
- *Retención*: 1 − bajas / socios con cuota vigente al inicio del mes, en %. Sin socios al inicio → «sin datos».
  Más es mejor. Con meta.
- *Cuotas vencidas*: % de socios activos con cuota vencida hoy (foto de hoy). Menos es mejor. Con meta.

**Procesos**
- *Asistencias*: ingresos registrados en el mes; *por socio activo* = asistencias / socios activos. Más es mejor. Con meta
  (sobre «por socio activo»).
- *Horario pico*: la franja de una hora con más ingresos del mes (hora de Argentina de `RegistradaEn`).
- *Productos sin stock o con poco stock*: cantidad con stock ≤ 5 (= umbral del Resumen). Foto de hoy. Menos es mejor. Con meta.

**Personas**
- *Horas de entrenadores*: suma de horas cumplidas cargadas en el mes.
- *Costo de entrenadores*: Σ horas × valor de la hora guardado en cada día.
- *Costo sobre cuotas*: costo de entrenadores / neto de facturas de cuota del mes (factura con socio), en %.
  Menos es mejor. Con meta.

**Semáforo** (solo si hay meta): más es mejor → verde ≥ 100 % de la meta, amarillo ≥ 90 %, rojo < 90 %.
Menos es mejor → verde ≤ meta, amarillo ≤ 110 % de la meta, rojo > 110 %. Sin meta → gris «sin meta».
La variación contra el mes anterior se muestra en % (o en puntos, si el indicador ya es un %).

## Análisis

- Segmentadores: **desde / hasta** (meses, máximo 24) y **plan** (todos o uno).
- Gráficos: evolución (ventas, compras, cuotas, resultado) por mes; **si desde = hasta, por día** (drill-down);
  ingresos por plan; mapa de calor de asistencia (lunes a domingo × 06 a 22 h); 5 productos más vendidos; compras por
  proveedor (en qué se gasta).
- Ingresos por plan = ítems de las facturas de cuota (con socio), agrupados por la descripción «Cuota Membresía - X»,
  que guarda el nombre del plan cuando se cobró (historia fiel aunque el plan cambie de nombre o de precio).
- Filtro cruzado: clic en un mes de la evolución → baja a ese mes (por día); clic en un plan → filtra ese plan
  (evolución de cuotas y mapa de asistencia, por el plan actual del socio). Una barra de «filtros activos» permite quitarlos.

## Decisiones

- **Socios en riesgo**: socios al día que no vienen hace N días (la regla que ya usa Recepción) y socios cuya cuota vence
  en los próximos 7 días. Cada fila: motivo, días, plan y **acción sugerida** con enlace de WhatsApp (como en Recepción).
  Solo nombre, plan y teléfono, que es lo que ya ve Recepción.
- **¿Qué pasaría si…?**: por plan, socios vigentes y recaudación mensual equivalente (precio / duración en meses).
  El usuario mueve el % de aumento de cada plan y el % de socios que estima perder; se recalcula en la pantalla la
  recaudación mensual proyectada y la diferencia. No guarda nada.

## Exportar

- **PDF** del tablero de comando del período (blanco y negro, con el logo y los datos del negocio, como los otros PDF).
- **Excel** con hojas: Indicadores, Evolución, Ingresos por plan, Asistencia, Productos, Compras por proveedor.
  Se arma con la utilidad de Excel que ya tiene el sistema. El texto aclara que el archivo se puede abrir en Power BI.

## Seguridad y datos

- `TableroController` con `[Authorize(Roles = Roles.Gestion)]`; guardar metas solo `Roles.Administrador`.
- Tabla nueva `MetasDeIndicadores` (clave del indicador, valor, quién y cuándo) con el trigger de auditoría
  `fn_auditar` como las demás tablas; la clave se valida contra la lista fija de indicadores; valor ≥ 0, y los % ≤ 1000.
- Parámetros validados (formato `aaaa-mm`, años 2000–2100, rango ≤ 24 meses, plan existente).
- El menú muestra «Tablero de Gestión» solo a Administrador y Contador; el servidor lo controla igual (pruebas de 403).

## Pruebas

- Integración (base real de pruebas): cada indicador con datos armados a mano y su valor esperado; coincidencia con
  el Resumen (ventas, resultado, caja); semáforos; metas (guardar, validar, 403 para Contador y Recepción); análisis
  (serie mensual y diaria, plan, mapa de calor por hora de Argentina); anuladas excluidas.
- Dominio: semáforo y variación.
- Pantalla: cálculo del simulador y armado del semáforo (Vitest).

## Lo que queda a criterio de quien lo construya

Diseño visual dentro del estilo del sistema; nombres de endpoints; orden exacto de las tarjetas; textos de ayuda.
