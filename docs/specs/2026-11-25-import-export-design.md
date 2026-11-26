# Importar movimientos desde Excel/CSV y exportar a CSV — 1.5.0

Fecha: 2026-11-25. Android e iPhone.

## Intención
Hay usuarios con años de ingresos y gastos en Excel. Queremos subir esa historia a piRadar sin que tengan que rehacer
su archivo, y poder sacar los movimientos a CSV para que cada quien haga sus propios análisis.

## Decidido con el usuario
- Formatos de entrada: **`.xlsx`** y **`.csv`**. **No** `.xls` (Excel 97–2003): se pide guardarlo como `.xlsx`.
- Lector **propio** en Core (zip + XML), sin bibliotecas.
- Columnas en **cualquier orden y cantidad**: piRadar **propone** la asignación (sinónimos es/en, sin tildes ni
  mayúsculas, y por contenido si el nombre no basta) y el usuario confirma o corrige. Las no asignadas se ignoran.
- Montos: columna con signo, **o** dos columnas Ingreso/Gasto, **o** monto + columna de tipo.
- **Deshacer última importación** (migración `ImportBatchId`).
- **Exportar a CSV** (lo que muestra Movimientos, o todo el historial desde Respaldo), sin cifrar, con aviso.
- Versión **1.5.0**.

## Archivo de validación
El usuario dio un `.xlsx` real de un mes (fuera del repo; **nunca** copiarlo al repo ni a los fixtures; los totales
esperados se guardan fuera del repo). Su estructura, que los fixtures sintéticos imitan:
- Una hoja. Encabezados: `Descripción`, `Fecha`, `Ingreso`, `Gasto`, `Categoria` (sin tilde), `Medio de pago`, `Moneda`.
- Fechas seriales con tres formatos: integrado 14, `dd/mm/yyyy` y `dd/mm/yy` (propios).
- Montos con formato contable; algunas celdas con fórmula: se usa el valor calculado.
- Ingresos con decimales en COP (binarios, p. ej. `…9.3499999996`): se redondean a pesos, con aviso en la vista previa.
- `Medio de pago` y `Moneda` vacías en todas las filas ⇒ sin medio de pago y moneda base.
- Última fila con una celda con estilo pero sin valor ⇒ se ignora. Descripciones con espacio final.
- Una fila de "saldo del mes anterior" como ingreso del día 1: la vista previa avisa que piRadar ya calcula el saldo
  inicial solo y que, si ya hay movimientos anteriores, ese ingreso lo duplicaría.
- Categorías: unas coinciden con la semilla (Salario, Mercado, Restaurantes…) y otras son nuevas, de gasto e ingreso.

## Diseño

### Lectura (Core/Import)
- `XlsxReader`: `ZipArchive` + `XmlReader`. Lee `workbook.xml` (hojas y `date1904`), `sharedStrings.xml`,
  `styles.xml` (qué estilos son fecha: formatos integrados 14–22, 45–47 y formatos propios con `d`/`m`/`y` fuera de
  comillas y corchetes) y la hoja elegida. Tipos de celda: `s`, `str`, `inlineStr`, `b`, `n` (número o fecha según
  el estilo), `e` (error ⇒ vacía). Fórmulas: se toma `<v>`. Referencias `A1` ⇒ columna; celdas faltantes ⇒ vacías.
- `CsvReader`: RFC 4180 (comillas, comillas dobles, saltos de línea dentro de comillas), separador `;`, `,` o tab
  detectado en la primera línea, UTF-8 con o sin BOM (si no es UTF-8 válido, Windows-1252).
- Resultado común `SheetData`: filas de `CellValue` (Texto, Número `decimal`, Fecha `DateTime`, Vacía).
- Límites: archivo ≤ 20 MB, XML descomprimido ≤ 200 MB por parte, ≤ 50 000 filas de datos, ≤ 100 columnas. Si se pasa
  ⇒ `ImportFormatException` con mensaje es/en. Sin DTD (`DtdProcessing.Prohibit`).

### Propuesta de columnas
- `ImportField`: Date, Amount, Income, Expense, Type, Category, Subcategory, PaymentMethod, Currency, Description,
  Merchant, Notes, Ignore.
- Fila de encabezados: la primera fila (de las 20 primeras) con ≥ 2 celdas de texto seguida de una fila con alguna
  fecha o número. Si no hay, sin encabezados (columnas "Columna A", "B"…).
- `ImportColumnGuesser`: sinónimos normalizados (sin tildes, minúsculas, sin signos); coincidencia exacta gana sobre
  "contiene". Si Fecha o Monto quedan sin asignar, se usa la columna con ≥ 80 % de fechas / números. Cada campo a una
  sola columna.
- El CSV de piRadar se reconoce por sus encabezados exactos y se asigna completo.
- La asignación confirmada se guarda en `AppSetting` (`ImportMapping`) por la huella de los encabezados.

### Conversión de filas
- `ImportRowParser` con `ImportOptions` (asignación, orden de fecha DMY/MDY, moneda base, negativo = gasto).
- Fecha: celda fecha; número 20 000–80 000 en una columna de fechas ⇒ serial; texto con `DateLabels`/formatos
  `d/M/yyyy`, `yyyy-MM-dd`, `d-M-yy`, con o sin hora. DMY por defecto en español, MDY en inglés; se propone el que deja
  más fechas válidas.
- Monto: número ⇒ decimal; texto ⇒ `MoneyParser` (quita `$`, `COP`, `US$`, espacios, paréntesis = negativo). Moneda:
  columna (COP/USD, `$`/`US$`) o moneda base. COP redondea a pesos (`MidpointRounding.AwayFromZero`), USD a centavos.
- Tipo: Ingreso/Gasto ⇒ el que tenga valor ≠ 0 (ambos ⇒ error de fila); signo ⇒ negativo = gasto (opción invertible);
  columna Tipo ⇒ sinónimos (ingreso/income/entrada/+, gasto/expense/egreso/salida/-).
- Fila vacía ⇒ se salta sin error. Fila sin monto y con texto tipo "total/suma/saldo" en alguna celda ⇒ se salta.
  Monto 0 ⇒ se salta con aviso.
- Errores por fila con número de fila de Excel y motivo es/en; no detienen la importación de las demás.
- `ImportPreview`: filas válidas, errores, avisos (redondeo, "saldo anterior"), totales y rango por moneda, valores
  distintos de categoría (con su tipo) y de medio de pago.

### Categorías y medios de pago
- Cada valor distinto se propone unido a la existente (no archivada) con el mismo nombre normalizado y el mismo tipo;
  si no hay, **crear nueva** (ícono y color por defecto). El usuario puede elegir otra existente.
- Sin columna de categoría o celda vacía ⇒ "Otros gastos"/"Otros ingresos" (o la que elija el usuario).
- Subcategoría (si hay columna) se crea bajo su categoría.
- Medio de pago vacío ⇒ ninguno; valor ⇒ unir o crear (tipo Other).

### Duplicados
- Clave: día de `OccurredAt`, tipo, monto, moneda y descripción/comercio normalizados.
- Coincide con un movimiento ya guardado ⇒ marcado "posible duplicado", **excluido por defecto**; el usuario puede
  incluirlos todos. Se compara por conteo: con 2 filas iguales en el archivo y 1 guardada solo se marca la primera.
  Filas repetidas dentro del archivo sin equivalente guardado **no** se marcan (dos compras iguales el mismo día son
  legítimas).

### Guardado (Data)
- `Migration002_ImportBatch`: `ALTER TABLE Transactions ADD COLUMN ImportBatchId varchar(36)` + índice. Tabla
  `ImportBatch` (`Id` varchar(36) PK, `ImportedAt` bigint UTC, `FileName` varchar, `RowCount` integer).
- `TransactionSource.Import = 3`.
- `ITransactionImporter.ImportAsync(ImportPlan)`: en **una** transacción crea categorías/medios, inserta los movimientos
  (`InsertAll`), crea el `ImportBatch`. Falla algo ⇒ no queda nada.
- `UndoLastImportAsync()`: borra los movimientos del último lote (también los que el usuario editó después; se avisa
  con el conteo) y el lote; categorías y medios creados se dejan (el usuario los archiva si quiere).
- El respaldo exporta la tabla y la columna nuevas (`TableDump` es genérico); un respaldo viejo restaura por
  `Migrator.MigrateTo`.

### Exportar CSV (Core/Data)
- `TransactionCsvExporter`: columnas Fecha (`yyyy-MM-dd`), Hora (`HH:mm`), Tipo, Monto (positivo, sin miles), Moneda,
  Categoría, Subcategoría, Medio de pago, Comercio, Descripción, Notas. Encabezados en el idioma de la app.
- Español: `;` y coma decimal. Inglés: `,` y punto decimal. UTF-8 con BOM, CRLF. Comillas cuando hace falta.
  Celdas que empiezan con `=`, `+`, `-`, `@` en texto ⇒ prefijo `'` (inyección de fórmulas).
- Ida y vuelta: exportar → importar en una BD vacía da los mismos movimientos (prueba).

### Pantallas (App)
- `/import` (Más > **Importar movimientos**, y enlace en Respaldo), pasos: archivo (+ hoja) → columnas (cada columna con
  3 valores de ejemplo y selector de campo; orden de fecha; moneda) → categorías y medios → vista previa (totales,
  errores, avisos, duplicados) → **Importar** con progreso → resultado con **Ver movimientos**.
- Respaldo: sección "Importaciones" con la última (fecha, archivo, cantidad) y **Deshacer última importación**
  (confirmación).
- Movimientos: acción **Exportar CSV** con lo que muestra la lista (periodo o búsqueda). Respaldo: **Exportar todo a
  CSV**. Aviso: "El CSV no va cifrado". Archivo en la caché + compartir; se borra igual que los `.piradar`.
- `FilePicker`: iOS `org.openxmlformats.spreadsheetml.sheet`, `public.comma-separated-values-text`,
  `public.plain-text`; Android `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `text/*`.
- Debug: `import-in.xlsx`/`import-in.csv` en `AppDataDirectory` y botón "Archivo de prueba (Debug)" (`#debug-import`).
- Ayuda es+en (`HelpTopics.Import`, botón "?" en `/import`).

## Privacidad
Todo local. Sin logs de contenido, nombres de archivo ni montos. El archivo se lee en memoria y no se guarda.

## Fuera de alcance
`.xls`, `.ods`, `.numbers`, exportar a `.xlsx`, importar fotos, recibir el archivo por "Compartir con piRadar",
importar tasas de cambio.
