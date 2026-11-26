# Importar desde Excel/CSV y exportar a CSV — plan de implementación (1.5.0)

**Goal:** Importar movimientos desde `.xlsx`/`.csv` con columnas propuestas y confirmadas por el usuario, deshacer la
última importación y exportar movimientos a CSV.

**Spec:** `docs/specs/2026-11-25-import-export-design.md`

**Carril:** crítico (escritura masiva, migración, archivos externos). Opus planea y revisa cada tarea; `programador`
(Sonnet) programa; `ayudante` (Haiku) corre builds y pruebas; revisor independiente (Opus) al final.

## Global Constraints
- Lógica en Core (`PiRadar.Core/Import/`) y Data; la App solo UI y plataforma.
- Sin paquetes NuGet nuevos.
- Todo texto visible y todo mensaje de error con `Pick("español", "inglés")`.
- Dinero en `Money`/`long`, nunca `double`; los números del xlsx se leen como `decimal` (`CultureInfo.InvariantCulture`).
- Nunca loguear contenido del archivo, nombres de archivo ni montos.
- El archivo real de validación del usuario **no** se copia al repo ni se citan sus datos. Los fixtures
  de prueba se generan en el test (zip + XML sintético que imita su estructura: estilos 14/164/165, fórmulas con valor
  calculado, columnas vacías, fila final con estilo y sin valor, encabezado sin tilde).
- Migración con SQL explícito; no editar `Migration001`.
- Commit y push a `main` al terminar cada tarea revisada, mensaje en inglés como el repo, terminando con
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Tareas

### 1. Lectores (Core)
`Import/CellValue.cs`, `SheetData.cs`, `XlsxReader.cs`, `CsvReader.cs`, `ImportFormatException.cs`.
Pruebas (`tests/PiRadar.Core.Tests/Import/`): strings compartidos, inlineStr, fórmulas, fechas integradas y propias,
`date1904`, celdas faltantes, varias hojas, límites (filas, tamaño, bomba zip), DTD rechazado; CSV con `;`/`,`/tab,
comillas y saltos de línea, BOM, Windows-1252. Fixture sintético "mes" con la estructura del archivo de validación (datos inventados).

### 2. Propuesta y conversión (Core)
`ImportField`, `ImportColumnGuesser`, `ImportOptions`, `ImportRowParser`, `ImportPreview`, `DuplicateKey`.
Pruebas: encabezados en otro orden, sinónimos es/en, sin encabezados (por contenido), ambiguos, fila de títulos arriba;
DMY/MDY; seriales; montos texto (`$ 1.234.567`, `1,234.56`, `(500)`); tres modos de tipo; redondeo COP; filas de
total; errores con número de fila; ambos idiomas. Con el fixture "mes": conteo, totales por tipo y
categorías calculados a mano para los datos inventados, aviso de redondeo y de saldo anterior.

### 3. Migración e importador (Data)
`Migration002_ImportBatch`, `TransactionSource.Import = 3`, records, `ITransactionImporter` (+ `UndoLastImportAsync`,
`GetLastImportAsync`), registro en `AddPiRadarData`.
Pruebas: migración desde v1 con datos; importación crea categorías/medios y movimientos en una transacción; falla a
mitad ⇒ nada guardado; duplicados contra la BD; deshacer; respaldo con la tabla nueva y restauración de un respaldo v1.

### 4. Exportador CSV (Core/Data)
`TransactionCsvExporter` (+ consulta con nombres de categoría/medio).
Pruebas: formato es y en, comillas, inyección de fórmulas, BOM; ida y vuelta exportar → importar = mismos movimientos.

### 5. Pantallas (App)
`Import.razor` (pasos), entrada en `NavItems.MoreItems`, sección en `Backup.razor` (deshacer, exportar todo),
acción en `Transactions.razor`, `FilePicker` con tipos por plataforma, archivo de prueba Debug, mapeo recordado en
`AppSetting`. Verificar tema oscuro, teléfono pequeño y tablet.

### 6. Ayuda, versión y verificación
`HelpContent`/`HelpTopics.Import` es+en, regenerar `store/help`, `CLAUDE.md`, versión 1.5.0 / código 29.
Simulador iOS (UiScript con `import-in.xlsx` = archivo de validación copiado al contenedor de la app, no al repo):
comprobar conteo y totales contra los valores esperados (guardados fuera del repo) en Inicio/Análisis, deshacer y volver a importar (duplicados), exportar
CSV. Emulador Android: lo mismo. Revisor independiente.

## Review Focus
1. Fechas: serial 46235 = 2026-08-01 con los tres estilos; `date1904`; texto `03/08/2026` en es = 3 ago, en en = 8 mar.
2. Montos binarios como `1234567.3499999996` ⇒ 1.234.567 COP sin pasar por `double`.
3. Una fila con Ingreso **y** Gasto ⇒ error de fila, no se adivina.
4. Importación que falla a mitad no deja categorías ni movimientos.
5. Deshacer solo borra el último lote.
6. CSV exportado abierto en Excel con configuración de Colombia: columnas separadas y tildes correctas.
