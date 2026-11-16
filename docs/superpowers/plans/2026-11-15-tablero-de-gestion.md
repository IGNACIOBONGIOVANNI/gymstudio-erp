# Tablero de Gestión — plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un módulo «Tablero de Gestión» (Comando, Análisis, Decisiones, Exportar) que calcula en el servidor indicadores con meta, semáforo y comparación, análisis con filtros y apoyo a las decisiones, solo para Administrador y Contador.

**Architecture:** La lógica de semáforo y el catálogo de indicadores van en el dominio; los cálculos, en un servicio de Application (`TableroDeGestionService`) sobre `IAppDbContext`, con agrupaciones hechas por la base; un `TableroController` con `[Authorize(Roles = Roles.Gestion)]`. Las metas se guardan en una tabla nueva auditada por trigger. La pantalla es un módulo React con Recharts (ya instalado), jsPDF (`pdfComun`) y el escritor de Excel propio (`utils/xlsx.ts`).

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, PostgreSQL 15, xUnit (integración contra base real), React 19 + TypeScript 6, Recharts 3, `node --test` para utilidades de la pantalla.

**Spec:** `docs/superpowers/specs/2026-11-15-tablero-de-gestion-design.md`

## Global Constraints

- Facturas anuladas no cuentan nunca; importes netos de IVA (como el Resumen).
- «Hoy» = día de Argentina (UTC-3); el parámetro `hoy` opcional, como `/api/dashboard`.
- Mes `aaaa-mm`, años 2000–2100; rango de análisis ≤ 24 meses; si no, 400 con mensaje en castellano.
- Ventas, resultado, Caja y Banco tienen que dar **igual** que `/api/dashboard` para el mismo mes.
- `[Authorize(Roles = Roles.Gestion)]` en todo el controlador; guardar metas solo `Roles.Administrador`.
- Umbral de poco stock = `DashboardController.StockBajo` (5); días por vencer = `DashboardController.DiasPorVencer` (7); días sin venir = `ReglasDeAsistencia.DiasSinVenirPorDefecto`.
- Semáforo: más es mejor → verde ≥ 100 % de la meta, amarillo ≥ 90 %, rojo < 90 %; menos es mejor → verde ≤ meta, amarillo ≤ 110 %, rojo > 110 %; sin meta → `sin-meta`; sin valor → `sin-datos`.
- Las pruebas de integración usan meses de **2023** (nadie más los usa) e insertan socios, membresías, asistencias y horas por SQL (`BasesDePrueba.Ejecutar`) para controlar las fechas.
- `npm test` se corre desde PowerShell (en Git Bash falla). Después de compilar en Windows, reiniciar el backend del Docker.
- No escribir secuencias barra-u en Write/Edit (se convierten en el carácter).

## Review Focus

1. Mes sin ninguna venta: margen y costo sobre cuotas tienen que dar `sin-datos`, no dividir por cero → prueba en Task 3.
2. Mes actual sin terminar: «al cierre» es hoy, no el último día del mes → prueba en Task 3.
3. Ingreso registrado a las 23:30 de Argentina (02:30 UTC del día siguiente) cae en la franja 23 del día correcto → prueba en Task 4.
4. Meta con clave desconocida, negativa o de un % mayor a 1000 → 400 → prueba en Task 2.
5. Contador y Recepción no pueden guardar metas; Recepción no puede leer nada del tablero → pruebas en Task 2 y 5.

---

### Task 1: Catálogo de indicadores y semáforo (dominio)

**Files:**
- Create: `backend/src/EstudioContable.Domain/TableroDeGestion.cs`
- Test: `backend/tests/EstudioContable.Domain.Tests/TableroDeGestionTests.cs`

**Interfaces:**
- Produces:
  - `enum SentidoDelIndicador { MasEsMejor, MenosEsMejor }`
  - `record DefinicionDeIndicador(string Clave, string Nombre, string Perspectiva, SentidoDelIndicador Sentido, bool EsPorcentaje, bool AdmiteMeta)`
  - `static class TableroDeGestion` con `IReadOnlyList<DefinicionDeIndicador> Indicadores`, `DefinicionDeIndicador? Buscar(string clave)`, `string Semaforo(decimal? valor, decimal? meta, SentidoDelIndicador sentido)`, `decimal? Variacion(decimal? actual, decimal? anterior, bool esPorcentaje)` (en % del anterior; si es porcentaje, diferencia en puntos; `null` si falta alguno o el anterior es 0 y no es porcentaje).
  - Claves y perspectivas: `ingresos`, `resultado`, `margen`(%), `liquidez` (Financiera; liquidez sin meta); `socios-activos`, `altas`(sin meta), `bajas`(sin meta, menos es mejor), `retencion`(%), `cuotas-vencidas`(%, menos) (Socios); `asistencias`(sin meta), `asistencia-por-socio`, `stock-critico`(menos) (Procesos); `horas-entrenadores`(sin meta), `costo-entrenadores`(sin meta, menos), `costo-sobre-cuotas`(%, menos) (Personas).

- [ ] **Step 1: Tests** — `Semaforo(100,100,MasEsMejor)=="verde"`, `(90,100,Mas)=="amarillo"`, `(89.99m,100,Mas)=="rojo"`, `(100,100,Menos)=="verde"`, `(110,100,Menos)=="amarillo"`, `(110.01m,100,Menos)=="rojo"`, `(5,null,Mas)=="sin-meta"`, `(null,100,Mas)=="sin-datos"`, `(0,0,Menos)=="verde"`; `Variacion(120,100,false)==20`, `Variacion(30,25,true)==5`, `Variacion(5,0,false)==null`; `Buscar("retencion")!.EsPorcentaje`, `Buscar("nada")==null`, claves únicas, 15 indicadores.
- [ ] **Step 2:** `dotnet test backend/tests/EstudioContable.Domain.Tests --filter TableroDeGestionTests` → falla (no compila).
- [ ] **Step 3:** Implementar. Meta 0 con «más es mejor»: verde si valor ≥ 0.
- [ ] **Step 4:** Mismo comando → pasa.
- [ ] **Step 5:** Commit «Tablero: catálogo de indicadores y semáforo».

### Task 2: Metas guardadas (tabla, migración y API)

**Files:**
- Create: `backend/src/EstudioContable.Domain/Entities/MetaDeIndicador.cs`, migración `MetasDeIndicadores` (+ trigger `trg_auditar_metas_indicadores` con `contabilidad.fn_auditar()`; Down lo borra), `backend/src/EstudioContable.API/Controllers/TableroController.cs`
- Modify: `IAppDbContext` + `AppDbContext` (`DbSet<MetaDeIndicador> MetasDeIndicadores`, esquema `contabilidad`, PK = `Clave` varchar(40), `Valor numeric(18,2)`, `ActualizadoPor varchar(100)`, `ActualizadoEn timestamptz`)
- Test: `backend/tests/EstudioContable.IntegrationTests/TableroMetasTests.cs`

**Interfaces:**
- Produces: `GET /api/tablero/metas` → `[{ clave, valor, actualizadoPor, actualizadoEn }]`; `PUT /api/tablero/metas/{clave}` body `{ valor: decimal | null }` (null borra la meta) → 204; Administrador solo. Entidad `MetaDeIndicador(string clave, decimal valor, string usuario, DateTime ahora)` con `Cambiar(decimal valor, string usuario, DateTime ahora)`; valida valor ≥ 0 y, si es %, ≤ 1000 (lanza `ArgumentException` → 400 por el middleware).

- [ ] **Step 1: Tests** — jefe guarda `ingresos=300000` y el GET la devuelve con `actualizadoPor == "jefe@gym.com"`; guardar `null` la borra; clave `nada` → 400; `-1` → 400; `retencion=1001` → 400; Contador PUT → 403 y GET → 200; Recepción GET → 403; queda un registro en `RegistrosAuditoria` con tabla `MetasDeIndicadores`.
- [ ] **Step 2:** `dotnet test backend/tests/EstudioContable.IntegrationTests --filter TableroMetasTests` → falla.
- [ ] **Step 3:** Entidad, configuración, `dotnet ef migrations add MetasDeIndicadores` (build antes, luego `--no-build`), agregar el trigger en `Up`, controlador.
- [ ] **Step 4:** Mismo comando → pasa.
- [ ] **Step 5:** Commit «Tablero: metas de los indicadores, auditadas».

### Task 3: Indicadores del tablero de comando

**Files:**
- Create: `backend/src/EstudioContable.Application/Services/TableroDeGestionService.cs` (registrado en la inyección de dependencias como los demás servicios)
- Modify: `TableroController.cs`
- Test: `backend/tests/EstudioContable.IntegrationTests/TableroIndicadoresTests.cs`

**Interfaces:**
- Produces: `ITableroDeGestionService.IndicadoresAsync(DateOnly primerDia, DateOnly hoy, CancellationToken)` → `IndicadoresDelMes(string Mes, string MesAnterior, DateOnly Cierre, IReadOnlyList<Indicador> Indicadores)` con `Indicador(string Clave, string Nombre, string Perspectiva, string Sentido, bool EsPorcentaje, decimal? Valor, decimal? Anterior, decimal? Variacion, decimal? Meta, string Semaforo, string? Detalle)`. `GET /api/tablero/indicadores?mes=&hoy=`. `Detalle` lleva el horario pico en `asistencias` («18 a 19 h») y el texto de foto de hoy en los indicadores sin comparación.
- Definiciones exactas: las de la spec. Cierre = min(último día del mes, hoy). «Vigente en D» = membresía pagada con `FechaInicio <= D < FechaVencimiento`, comparando por fecha (día) de Argentina. Indicadores de foto de hoy (`liquidez`, `cuotas-vencidas`, `stock-critico`) no llevan `Anterior` ni `Variacion`.

- [ ] **Step 1: Tests** (cada uno en su mes de 2023):
  - `Ventas_resultado_y_caja_coinciden_con_el_Resumen` (2023-01): una venta neto 300 y una compra neto 100 por `/api/facturas`; `ingresos==300`, `resultado==200`, `margen==66.67`, `liquidez == cajaSaldo + bancoSaldo` del `/api/dashboard?mes=2023-01`.
  - `Un_mes_sin_ventas_da_sin_datos_en_margen` (2023-02): `margen.valor == null` y `semaforo == "sin-datos"`.
  - `Socios_altas_bajas_y_retencion_salen_de_las_membresias` (2023-03): por SQL, 4 socios con alta 2023-03-05; 4 membresías pagadas vigentes el 2023-03-01, de las cuales 1 vence el 2023-03-15 sin renovar; `socios-activos==3`, `altas==4`, `bajas==1`, `retencion==75`.
  - `El_mes_actual_cierra_hoy` (2023-04 con `hoy=2023-04-10`): membresía que vence el 2023-04-20 cuenta como activa y no como baja.
  - `Asistencia_por_socio_y_horario_pico` (2023-05): 3 asistencias de 2 socios activos, dos a las 18 h de Argentina → `asistencias==3`, `asistencia-por-socio==1.5`, `detalle` contiene `18`.
  - `Costo_de_entrenadores_sobre_cuotas` (2023-06): horas 10 × 1000 por SQL y una factura de cuota (con `SocioId`) neto 20000 por SQL → `costo-entrenadores==10000`, `costo-sobre-cuotas==50`.
  - `La_meta_pone_el_semaforo_y_compara_con_el_mes_anterior` (2023-07 y 2023-08): ventas 100 y 120; meta `ingresos=150` → `variacion==20`, `semaforo=="rojo"`.
- [ ] **Step 2:** `--filter TableroIndicadoresTests` → falla.
- [ ] **Step 3:** Implementar con agrupaciones en la base (como `DashboardController`); reutilizar las mismas consultas de saldo de Caja y Banco.
- [ ] **Step 4:** → pasa. Correr también `--filter DashboardTests` → pasa.
- [ ] **Step 5:** Commit «Tablero: indicadores del Balanced Scorecard con meta y comparación».

### Task 4: Análisis con segmentadores y bajada al detalle

**Files:** Modify `TableroDeGestionService.cs`, `TableroController.cs`; Test `TableroAnalisisTests.cs`

**Interfaces:**
- Produces: `AnalisisAsync(DateOnly desde, DateOnly hasta, long? planId, CancellationToken)` → `Analisis(string Granularidad /* "mes" | "dia" */, IReadOnlyList<PuntoDeEvolucion> Evolucion /* Periodo, Ventas, Compras, Cuotas, Resultado */, IReadOnlyList<IngresoPorPlan> IngresosPorPlan /* Plan, Neto, Cobros */, int[][] Asistencia /* [7][17]: lunes..domingo × 06..22 */, IReadOnlyList<ProductoVendido> TopProductos /* Producto, Cantidad, Total; 5 */, IReadOnlyList<CompraPorProveedor> ComprasPorProveedor /* Proveedor, Neto; 8 */)`. `GET /api/tablero/analisis?desde=aaaa-mm&hasta=aaaa-mm&planId=`.
- Plan del ingreso = texto después de «Cuota Membresía - » en el ítem de una factura con `SocioId`. Con `planId`: cuotas y asistencia solo de ese plan (por nombre del plan para las cuotas; por `PlanActualId` del socio para la asistencia); ventas, compras, productos y proveedores no se filtran.
- Hora de la asistencia = hora de Argentina de `RegistradaEn` (UTC − 3); fuera de 06..22 no entra al mapa.

- [ ] **Step 1: Tests** (meses 2023-09 a 2023-12): serie mensual de 2023-09 a 2023-10 con 2 puntos y ventas exactas; `desde==hasta` → `granularidad=="dia"` con un punto por día del mes; cuotas de dos planes por SQL → `ingresosPorPlan` con ambos y `planId` deja uno; asistencia a las 02:30 UTC del jueves 2023-11-16 (23:30 del miércoles en Argentina) no entra al mapa y una a las 21:30 UTC del miércoles cae en `[2][12]` (miércoles, 18 h); anulada excluida; rango de 25 meses → 400; `desde > hasta` → 400.
- [ ] **Step 2–4:** fallar, implementar, pasar.
- [ ] **Step 5:** Commit «Tablero: análisis con segmentadores y bajada al detalle».

### Task 5: Decisiones (socios en riesgo y base del simulador)

**Files:** Modify `TableroDeGestionService.cs`, `TableroController.cs`; Test `TableroDecisionesTests.cs`

**Interfaces:**
- Consumes: `IAsistenciasService.SociosEnRiesgoAsync(hoy, ReglasDeAsistencia.DiasSinVenirPorDefecto, ct)`.
- Produces: `DecisionesAsync(DateOnly hoy, CancellationToken)` → `Decisiones(IReadOnlyList<SocioParaAccionar> Socios /* Id, Nombre, Apellido, Telefono, Plan, Motivo ("no-viene" | "por-vencer"), Dias, Accion */, IReadOnlyList<PlanParaSimular> Planes /* Id, Nombre, PrecioMensual, DuracionMeses, SociosVigentes, RecaudacionMensual = Precio / Duracion × SociosVigentes */)`. `GET /api/tablero/decisiones?hoy=`. Sin DNI en la respuesta.

- [ ] **Step 1: Tests** — un socio con cuota que vence en 3 días (relativo a `hoy`) aparece con motivo `por-vencer` y `dias==3`; un plan con 2 socios vigentes, precio 30000 y 3 meses da `recaudacionMensual==20000`; Recepción → 403 en indicadores, análisis y decisiones; la respuesta no tiene la propiedad `dni`.
- [ ] **Step 2–4:** fallar, implementar, pasar.
- [ ] **Step 5:** Commit «Tablero: socios para accionar y base del simulador».

### Task 6: Pantalla del Tablero de Gestión

**Files:**
- Create: `frontend/src/api/tableroApi.ts`, `frontend/src/features/tablero/tableroUtil.ts`, `ModuloTablero.tsx`, `PestanaComando.tsx`, `PestanaAnalisis.tsx`, `PestanaDecisiones.tsx`, `PestanaExportar.tsx`, `frontend/src/utils/generarTableroPDF.ts`
- Modify: `frontend/src/App.tsx` (menú «Tablero de Gestión», `roles: GESTION`, ícono `Gauge`, sección «Inicio» debajo de Resumen)
- Test: `frontend/tests/tablero.test.ts`

**Interfaces:**
- Consumes: los cuatro endpoints de las Tasks 2–5.
- Produces (puras, en `tableroUtil.ts`): `simular(planes: PlanParaSimular[], aumentos: Record<number, number>, perdida: number) => { actual: number; proyectada: number; diferencia: number }` (proyectada = Σ precio × (1 + aumento/100) / duración × socios × (1 − perdida/100), redondeada a 2 decimales); `textoDeVariacion(v: number | null, esPorcentaje: boolean) => string` («▲ 12 %», «▼ 3 pts», «—»); `hojasDelTablero(ind: IndicadoresDelMes, an: Analisis): Hoja[]` (seis hojas con los nombres de la spec).
- Comando: selector de mes, cuatro tarjetas por perspectiva, cada indicador con valor, meta, semáforo (color + texto, accesible) y variación; el Administrador edita la meta en el lugar (lápiz → campo con `numeroDecimal`) y el Contador la ve sin editar. Texto corto que explica el ciclo de control.
- Análisis: desde/hasta, plan; barra de filtros activos; `ComposedChart` de evolución (clic en un mes → `desde=hasta=ese mes`); barras de ingresos por plan (clic → `planId`); mapa de calor como tabla con intensidad de color y el número en cada celda; top productos y compras por proveedor en barras horizontales.
- Decisiones: tabla de socios con motivo, días y botón de WhatsApp (mismo armado de enlace que Recepción); simulador con un campo de % por plan y uno de pérdida estimada.
- Exportar: «Descargar PDF» (`generarTableroPDF(ind)`, A4 vertical, `encabezadosYPies` + `autoTable` por perspectiva) y «Descargar Excel» (`descargarXlsx(hojasDelTablero(...), 'Tablero_de_Gestion_aaaa-mm')`), con la aclaración de que el Excel abre en Power BI.

- [ ] **Step 1: Tests** — `simular` con un plan 30000/3 meses, 2 socios, +10 % y 50 % de pérdida → actual 20000, proyectada 11000, diferencia −9000; sin cambios → proyectada == actual; `textoDeVariacion(12,false)=="▲ 12 %"`, `(-3,true)=="▼ 3 pts"`, `(null,false)=="—"`; `hojasDelTablero` devuelve 6 hojas con esos nombres.
- [ ] **Step 2:** `npm test` (PowerShell) → falla.
- [ ] **Step 3:** Implementar utilidades, API y componentes.
- [ ] **Step 4:** `npm test`, `npx tsc -b`, `npm run lint` → pasan.
- [ ] **Step 5:** Commit «Tablero de Gestión: pantalla con comando, análisis, decisiones y exportación».

### Task 7: Manual, documentación y verificación completa

**Files:** Modify `frontend/src/features/manual/secciones.tsx` (sección «GymStudio como sistema de información»: pirámide operativo/táctico/estratégico con la tabla Transaccional → Recepción, Facturar, Caja; Información gerencial → Resumen, libros, reportes; Apoyo a las decisiones → Decisiones del tablero, Control de integridad; Para la dirección → Tablero de comando; y la sección «Tablero de Gestión» con cómo se usa, incluido el ciclo de control), `README.md` (módulo nuevo), `docs/BASE_DE_DATOS.md` (tabla `MetasDeIndicadores`).

- [ ] **Step 1:** Escribir el manual y la documentación.
- [ ] **Step 2:** Correr todas las pruebas: dominio, integración, `npm test`, `tsc`, `lint`, `npm run build` → todo pasa.
- [ ] **Step 3:** Probar en una copia del sistema en Docker (no en el real): ejercicio de la cátedra + datos demo, recorrer las cuatro pestañas como Administrador, Contador (sin editar metas) y Recepción (sin menú y 403).
- [ ] **Step 4:** Commit «Tablero de Gestión: manual y documentación».
- [ ] **Step 5:** Pedir al usuario permiso para fusionar en main (copia de seguridad antes).
