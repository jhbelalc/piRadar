# CLAUDE.md — piRadar

App personal de finanzas para Android e iPhone: registrar gastos e ingresos (manual o desde fotos de recibos con OCR local), categorías, medios de pago y análisis por día/semana/mes/año en COP y USD. Todo en el dispositivo, sin nube.

## Nombres
- Nombre visible de la app: **piRadar**
- Solución, proyectos y namespace raíz: **PiRadar** (`PiRadar.App`, `PiRadar.Core`, `PiRadar.Data`)
- ApplicationId / bundle id: **com.jhbelalc.piradar**

## Stack
- .NET 10 MAUI Blazor Hybrid, C#
- MudBlazor (UI; dona con `MudChart`, barras con el componente propio `BarChart`)
- SQLite cifrado: `sqlite-net-base` + `SQLitePCLRaw.bundle_e_sqlite3mc` en modo compatible SQLCipher
- OCR local en el dispositivo: ML Kit (Android) y Apple Vision (iOS) detrás de `IOcrService`
- Tests: xunit.v3 sobre Microsoft.Testing.Platform (activado en `global.json`; no agregar `Microsoft.NET.Test.Sdk` ni `xunit.runner.visualstudio`)

## Restricciones del entorno (no cambiar)
- Mac Intel con Xcode 26.3; simulador iOS x64.
- En `PiRadar.App.csproj`:
  ```xml
  <TargetFrameworks>net10.0-android;net10.0-ios26.0</TargetFrameworks>
  <ValidateXcodeVersion>false</ValidateXcodeVersion>
  ```
  Sin `maccatalyst` ni `windows`.
- SDK .NET 10.0.401, workload `maui`.

## Comandos
```sh
dotnet build src/PiRadar.App -f net10.0-android              # compilar Android
dotnet build src/PiRadar.App -f net10.0-ios26.0             # compilar iOS; luego instalar en el simulador:
xcrun simctl install <UDID> src/PiRadar.App/bin/Debug/net10.0-ios26.0/iossimulator-x64/PiRadar.App.app
dotnet build src/PiRadar.App -f net10.0-android -t:Install   # compilar e instalar en el emulador Android
dotnet test                                                  # todos los tests (net10.0 puro, sin MAUI)
dotnet test --project tests/PiRadar.Data.Tests              # un proyecto de tests
tools/build-apk.sh                                           # APK Release firmado para el teléfono → ~/Desktop/piRadar-<versión>.apk
tools/build-aab.sh                                           # AAB Release para Google Play (ARM64 + x86_64) → ~/Desktop/piRadar-<versión>-<código>.aab
tools/build-ipa.sh [--upload]                                # IPA Release App Store (ios-arm64) → ~/Desktop/piRadar-<versión>-<código>.ipa; --upload la sube a TestFlight
tools/appstore/asc.sh GET /v1/apps                           # API de App Store Connect (JWT firmado con la clave .p8)
```

`-t:Run` **no** recompila (lanza lo que ya está en `bin/`), y `dotnet build` a secas no regenera el `-Signed.apk`: usar los comandos de arriba.

**APK para el teléfono (Release Android)**: solo `android-arm64` (27,9 MB; con x86_64 eran 46,7 MB); para probar Release en el emulador, `tools/build-apk.sh -p:PiRadarEmulator=true` (no `-p:RuntimeIdentifiers`: se propaga a Core y Data). Se firma con la **clave propia** `../piRadar-firma/piradar.keystore` (alias `piradar`, válida hasta 2056, fuera del repo) y su contraseña está en el llavero de macOS (servicio `piradar-android-keystore`); el script la pasa en `PIRADAR_KEYSTORE_PASS`. Sin clave o sin contraseña el build Release falla a propósito (`PiRadarCheckSigningKey`): firmado con otra clave no se instalaría encima del que tiene el usuario. El usuario tiene copia de la clave y la contraseña; nunca versionarlas. En el emulador, pasar de Debug (clave de desarrollo) a Release (clave propia) o al revés exige `adb uninstall com.jhbelalc.piradar` (se pierden los datos de prueba). **Google Play**: paquete `com.jhbelalc.piradar` registrado con el certificado de la clave propia (SHA-256 `B1:4F:…:D3:FE`); en Play App Signing usar esa misma clave, no una generada por Google, para que Play actualice encima del APK instalado. Textos de la ficha, política de privacidad y respuestas de la consola en `store/google-play/`. Subir `ApplicationVersion` en cada APK o AAB entregado (Android no instala encima una versión con número menor). La primera instalación en el Honor del usuario (ALT-NX3, MagicOS 9) fue con la clave de desarrollo: pasar a la propia exigió una vez respaldo `.piradar` → desinstalar → instalar → restaurar.

**App Store (iOS)**: equipo `X7DUAM44W2`, app `6817079629` en App Store Connect (es-MX, SKU `piradar`). Todo lo de firma se creó con la **API de App Store Connect** (clave de equipo `C8H93HUL6U`, rol Administrador, `../piRadar-firma/AuthKey_C8H93HUL6U.p8`; `tools/appstore/asc.sh`): bundle id, certificado **Apple Distribution** (vence 2027-09-28; clave privada y `.p12` en `../piRadar-firma`, contraseña del `.p12` en el llavero, servicio `piradar-ios-distribution-p12`) y perfil **piRadar App Store** (en `~/Library/MobileDevice/Provisioning Profiles`). El Release de iOS en el csproj usa ese certificado y perfil; al renovarlos, recrear el perfil con el certificado nuevo. El Release para dispositivo compila AOT completo: ~25 min en este Mac. `CFBundleVersion` = `ApplicationVersion` (compartido con Android): subirlo en cada envío. `PrivacyInfo.xcprivacy` declara `UserDefaults` (CA92.1) porque `ThemeState` usa `Preferences`.

Simuladores/emuladores probados en este Mac Intel:
- iOS: usar el runtime **iOS 18.1** (p. ej. iPhone 16). El simulador iOS 26.3 no arranca de forma fiable en Intel. La app compilada para `net10.0-ios26.0` corre en 18.1 (mínimo iOS 15).
- Android: AVD `piRadar_API35` (android-35, google_apis, x86_64). Instalar: `dotnet build src/PiRadar.App -f net10.0-android -t:Install`.
  - Arrancar con `emulator -avd piRadar_API35 -gpu swiftshader_indirect -no-audio`: con la GPU del host, `adb exec-out screencap` da capturas negras.
  - El emulador en este Mac es inestable: el stack de Bluetooth se cae en bucle y aparece "System UI isn't responding", que se come los toques de `adb shell input tap`. `uiautomator dump` no funciona. Para verificar, que la app escriba un archivo en `AppDataDirectory` y leerlo con `run-as`.
  - **Debug en Android corre con el intérprete de Mono** (`UseInterpreter=true`; Release usa AOT): el código de cálculo intenso es 10–30× más lento (crear un respaldo tarda ~90 s). Medir rendimiento solo en Release (`-c Release`, `adb install -r bin/Release/.../*-Signed.apk`; misma firma, conserva los datos, pero sin `run-as`).
  - Los logs de `ILogger` (proveedor Debug) no aparecen en logcat ni en la consola del simulador. En Debug, advertencias y errores van a `AppDataDirectory/app-log.txt` (`FileLoggerProvider`).
- El primer arranque en el simulador iOS tarda varios minutos (pantalla en blanco); no es un error.
- **Probar el escaneo sin cámara** (solo Debug): copiar una foto de recibo como `scan-in.jpg` en `AppDataDirectory` (Android: `adb push` a `/data/local/tmp` y `run-as ... sh -c 'cat ... > files/scan-in.jpg'`) y usar el botón "Foto de prueba (Debug)" de `/scan` (`click #debug-photo`, luego `waitfor location.search.includes('scan=1')`). Las fotos del usuario están en `~/Desarrollos/Recibos/`: nunca copiarlas al repo. En el emulador Debug el primer OCR tarda minutos (verificación de clases de ML Kit + render por software); si la app deja de arrancar tras horas de uso, reiniciar el emulador en frío (`adb emu kill`, `-no-snapshot-load`).
- **Capturas sin tocar la pantalla** (solo Debug, `UiScript`): escribir `ui-script.txt` en `AppDataDirectory` (iOS: `$(xcrun simctl get_app_container <UDID> com.jhbelalc.piradar data)/Library`; Android: `files/` vía `run-as`) y abrir la app. Una orden por línea: `go /ruta`, `click selector-css`, `theme Dark|Light|System`, `wait ms`, `js expresión` (resultado en `ui-out.txt`), `fill Etiqueta=valor`, `pick Etiqueta=opción` (MudSelect). Un paso que falla anota `ERROR` en `ui-out.txt` y sigue. Ojo con selectores ambiguos (`.mud-toggle-item:last-child` coincide primero con el del primer grupo). El archivo se borra al leerlo. Corre dentro de `AppShell`, o sea después de abrir la BD (la pantalla de error del gate se toca con `adb shell input tap`).
```sh
# leer archivos de la app (build Debug)
adb shell run-as com.jhbelalc.piradar ls files
```

## Arquitectura
- **PiRadar.Core** (`net10.0`, sin MAUI): dominio, `Money`, interfaces, `ReceiptParser`, `SpendingAnalyzer`, `CurrencyConverter`, criptografía del respaldo. Toda la lógica testeable vive aquí.
- **PiRadar.Data** (`net10.0`): records de sqlite-net, repositorios, migraciones, semillas, export/import del respaldo.
- **PiRadar.App** (MAUI Blazor Hybrid): solo UI Blazor, DI y servicios de plataforma (OCR, cámara, `SecureStorage`, archivos/compartir).

```
piRadar/
├─ CLAUDE.md
├─ PiRadar.slnx
├─ Directory.Build.props
├─ src/
│  ├─ PiRadar.App/
│  │  ├─ Components/Layout/     # MainLayout (tema + gate), AppShell (barra inferior < 600 px / menú lateral), NavItems
│  │  ├─ Components/Pages/      # Dashboard, Transactions, TransactionEdit, ScanReceipt, Categories,
│  │  │                         # PaymentMethods, Analytics, ExchangeRates, Backup, Settings
│  │  ├─ Components/Shared/     # PageBar, MoneyInput, CategoryPicker, PaymentMethodPicker, PeriodSelector, CurrencyToggle,
│  │  │                         # IconAvatar, IconPicker, ColorSwatches, TransactionListItem, SignedAmount, ShareList, BarChart
│  │  ├─ Services/              # DatabaseKeyProvider, PluginOcrService, ImageOrientation, AppTheme, ThemeState, IconCatalog,
│  │  │                         # SpanishMudLocalizer, OcrLab/UiScript/FileLoggerProvider (Debug), ReceiptPhoto, ScanState,
│  │  │                         # TransactionsViewState, AnalyticsViewState, ChartFormat
│  │  ├─ Platforms/Android/     # allowBackup=false
│  │  ├─ Platforms/iOS/         # exclusión de backup de la BD
│  │  └─ MauiProgram.cs         # DI, MudBlazor, Batteries_V2.Init(), inicialización BD
│  ├─ PiRadar.Core/             # Domain/, Abstractions/, Receipts/, Images/, Analytics/, Backup/
│  └─ PiRadar.Data/             # Records/, Repositories/, Migrations/, Seed/, Backup/
├─ tools/ocr-fixtures/         # cómo generar y anonimizar fixtures de OCR (README.md)
└─ tests/
   ├─ PiRadar.Core.Tests/        # Receipts/Fixtures/{ios,android}: OCR real anonimizado de 20 recibos
   └─ PiRadar.Data.Tests/
```

## Decisiones técnicas
- **Cifrado de la BD**: `sqlite-net-base` (1.9.172) + `SQLitePCLRaw.bundle_e_sqlite3mc` (2.1.11), llamando `SQLitePCL.Batteries_V2.Init()` al arrancar. Modo SQLCipher vía `preKeyAction` (debe ir antes de `PRAGMA key`):
  ```csharp
  new SQLiteConnectionString(path, storeDateTimeAsTicks: true, key: dbKey,
      preKeyAction: c => c.Execute("PRAGMA cipher = 'sqlcipher'; PRAGMA legacy = 4;"));
  ```
  **No** usar `sqlite-net-sqlcipher` ni `SQLitePCLRaw.bundle_e_sqlcipher` (descontinuados). **No** agregar `sqlite-net-pcl` (trae `e_sqlite3` sin cifrado y choca con el bundle).
- **Clave de BD**: 32 bytes aleatorios generados en el primer arranque y guardados en `SecureStorage` (Keychain/Keystore) por `SecureStorageDatabaseKeyProvider`. No se respalda ni se exporta.
- **Arranque**: `DatabaseHost` (singleton, en Data) obtiene o crea la clave y abre la BD en `AppDataDirectory/db/piradar.db3`; `DatabaseGate` (en `MainLayout`) no muestra la app hasta que la BD está abierta. Si existe el archivo pero no la clave (`KeyMissing`) o la clave no abre el archivo (`Unreadable`), **nunca** se genera una clave nueva en silencio: se muestra la pantalla de error con "Empezar de cero", que requiere confirmación y llama a `DatabaseHost.ResetAsync()`. Las páginas acceden a la BD vía `DatabaseHost.Database`, que solo es válido dentro del gate.
- **Respaldo del sistema desactivado**: en Android, `android:allowBackup="false"`, `fullBackupContent="false"` y `Resources/xml/data_extraction_rules.xml`, que excluye todo de la nube y de la transferencia entre dispositivos de Android 12+ (la clave del Keystore no viaja y la BD quedaría ilegible). En iOS, `DatabaseLocation` marca la carpeta `db/` con `NSURLIsExcludedFromBackupKey`. El único respaldo soportado es el export/import propio.
- **OCR**: `Plugin.Maui.OCR` 1.1.1 detrás de `IOcrService` (adaptador `PluginOcrService`). Spike del 2026-09-23 (rama `spike/ocr`): compila en net10 para ambas plataformas y funciona **sin red** (Android usa el modelo latino de ML Kit empaquetado en la app; iOS usa Vision). Hallazgos que el código debe respetar:
  - Usar `tryHard: true`: en iOS el modo rápido rompe palabras y montos ("$82. 150", "SUPEFUIERCADO").
  - `OcrResult.Lines` viene **por columnas/bloques**, no en orden de lectura: etiqueta ("TOTAL A PAGAR") y monto ("$82.150") llegan separados. `ReceiptParser` debe reconstruir filas a partir de `Elements` agrupando por solapamiento vertical (Y/Height) y ordenando por X.
  - En iOS cada `Element` trae el recuadro de **su línea** (todas las palabras de una línea comparten X/Y/Width/Height); en Android trae el recuadro **por palabra**. El agrupador de filas debe funcionar con ambos.
  - `Confidence` no es fiable (iOS devuelve siempre 1.0).
  - Errores típicos a tolerar en el parser: `0`↔`O` ("M0LIDO"), `İ` en lugar de `Í`, palabras partidas ("ELECTRÓNI CA"), tildes perdidas.
  - El paquete `Xamarin.Google.MLKit.TextRecognition` (modelo empaquetado) depende él mismo de la variante de Play Services: de ahí la advertencia `XA4301` (`.so` duplicado). En el APK queda una sola copia; quitar la variante arriesga romper el OCR en tiempo de ejecución, así que se deja.
  - ML Kit intenta enviar telemetría de uso a Google (`com.google.android.datatransport`, visto en logcat el 2026-09-25); el texto del recibo no sale del teléfono. En Release se quitan `INTERNET` y `ACCESS_NETWORK_STATE` de todos los manifiestos (`ReleaseManifestOverlay.xml`, verificado con `aapt dump permissions`); Debug los conserva para el depurador.
  - Si el plugin deja de funcionar en una versión futura de .NET/MAUI → implementación propia con `Xamarin.Google.MLKit.TextRecognition` (Android) y `VNRecognizeTextRequest` (iOS). `ReceiptParser` no depende del motor.
  - **ML Kit (Android) ignora la orientación EXIF** y lee de lado las fotos de cámara (`Orientation = 6` en fotos verticales); Vision (iOS) sí la respeta. `PluginOcrService` rota los píxeles antes del OCR (`ImageOrientation`, que lee la etiqueta con `JpegOrientation` de Core; `Android.Media.ExifInterface` devolvió 0 con fotos del iPhone).
  - El procesamiento de fotos corre en `Task.Run` (en el hilo de la interfaz congela la app). En iOS no usar UIKit ahí (`UIImage.Draw`, `UIGraphicsImageRenderer`): en Debug tumbó Blazor (probablemente `UIKitThreadAccessException`) sin dejar rastro en `app-log.txt`; `ReceiptPhoto` usa ImageIO (`CGImageSource.CreateThumbnail`, que además aplica el EXIF).
  - Pruebas con 17 recibos reales (2026-09-24): el tamaño de la imagen casi no cambia la calidad en iOS; en Android la imagen completa acierta más totales. Pasar al OCR la foto completa (orientada) y guardar en `ReceiptImage` la versión de ~1600 px. Tiempo por imagen: ~8 s en el emulador Android, ~20 s en el simulador iOS (Vision en CPU; en el iPhone es mucho menor).
- **`ReceiptParser`** (Core/Receipts): `RowBuilder` reconstruye filas; luego total, fecha, NIT, comercio y moneda con reglas sacadas de recibos reales. Solo **propone**: devuelve candidatos (`TotalCandidates`, `MerchantCandidates`) y marcas de confianza (`TotalConfirmed`, `TaxIdVerified`, `HasTime`); el usuario confirma. Reglas clave:
  - Montos: separador + 3 dígitos = miles; + 1–2 dígitos al final = decimales; separadores mezclados ⇒ el último es decimal. COP redondea centavos. Números sin separador de más de 7 dígitos no son montos (teléfonos, códigos).
  - Total: prioridad `TOTAL A PAGAR`/`CON PROPINA` > `TOTAL FACTURA`/`VALOR TOTAL`/`TOTAL VENTA`/`ORDER TOTAL` > `TOTAL`; se excluyen subtotales, impuestos, ítems, ahorros y filas de resumen con 3+ montos; sin palabra clave ⇒ pago − cambio.
  - Fecha: día primero (mes primero si el día no es válido o el recibo es USD); se descartan resoluciones, vigencias y fechas futuras; si solo hay fechas de ese tipo no se propone ninguna.
  - NIT: el primero antes de los totales que no sea del cliente ni de un proveedor de software; se valida o calcula el DV (módulo 11 DIAN).
  - Comercio: la **marca** (nombre comercial) primero y la **razón social** (sin S.A./SAS/LTDA ni "Colombia") como alternativa. Paso 5: recordar por NIT el nombre que el usuario eligió la última vez (consultando `Receipt.ParsedTaxId` → `Transactions.Merchant`).
  - Moneda: COP si hay NIT; USD si no hay NIT y aparece `US$`/`USD` o texto en inglés de recibo.
  - No escribe logs (texto OCR y montos son datos del usuario).
- **Repositorios**: interfaces en `PiRadar.Core/Abstractions`, implementaciones `internal` en `PiRadar.Data/Repositories`, registradas con `services.AddPiRadarData()`. Toman la conexión de `DatabaseHost.Database` en cada llamada (siguen sirviendo tras `ResetAsync`). Cada escritura valida y escribe dentro de una misma transacción de BD. Las reglas de negocio lanzan `DomainValidationException` con un mensaje en español que se puede mostrar al usuario. Las categorías y los medios de pago con movimientos (o con subcategorías) se archivan en vez de borrarse; uno archivado no se puede elegir en movimientos nuevos, pero los movimientos viejos lo conservan. `CreatedAt`/`UpdatedAt` los fija el repositorio con `IClock`.
- **Tabla de movimientos**: `[Table("Transactions")]`, porque `TRANSACTION` es palabra reservada en SQLite.
- **Monedas**: totales siempre separados por moneda; vista consolidada opcional en la moneda base usando tasas ingresadas manualmente (tabla `ExchangeRate`).
- **UI** (MudBlazor 9, sin Bootstrap): tema en `AppTheme` con la fuente del sistema (nada se descarga; la app funciona sin red). Cultura `es-CO` fijada en `MauiProgram`; textos internos de MudBlazor en `SpanishMudLocalizer`. En `MudText`, `Color.Secondary` es el color de acento (ámbar): para texto atenuado usar `Class="mud-text-secondary"`. Cada página pone su `PageBar` (título, atrás, acciones); los formularios de edición son `MudDialog` que llaman al repositorio y devuelven el mensaje para el snackbar. Tema Sistema/Claro/Oscuro en `AppSetting` (`Theme`), aplicado por `ThemeState` también a la parte nativa (`UserAppTheme`, íconos de la barra de estado en Android).
- **Áreas seguras**: en iOS el WebView ocupa toda la pantalla (`ContentInsetAdjustmentBehavior = Never`, si no la barra inferior flota) y el CSS usa `env(safe-area-inset-*)`. En Android 15 (edge-to-edge) el WebView no informa esas variables: `MainPage` usa `SafeAreaEdges="All"` solo en Android.
- **Íconos** de categorías y medios de pago: la BD guarda el nombre Material (`shopping_cart`); `IconCatalog` lo traduce al SVG. Nunca quitar nombres del catálogo.

## Modelo de datos
| Tabla | Campos | Notas |
|---|---|---|
| `Category` | `Id` int PK, `Name`, `Kind` (Expense/Income), `Icon`, `Color`, `ParentId?`, `SortOrder`, `IsArchived`, `IsSystem` | Semilla por defecto. Se archiva si tiene movimientos. |
| `PaymentMethod` | `Id` int PK, `Name`, `Kind` (Cash, DebitCard, CreditCard, BankAccount, DigitalWallet, Other), `Icon`, `LastFourDigits?`, `SortOrder`, `IsArchived`, `IsSystem` | Semilla: Efectivo, Tarjeta débito, Tarjeta crédito, Cuenta bancaria, Nequi/Daviplata. |
| `Transactions` | `Id` Guid PK, `Type` (Expense/Income), `AmountMinor` long, `Currency` (COP/USD), `OccurredAt` (local), `CategoryId`, `PaymentMethodId?`, `Merchant?`, `Description?`, `Notes?`, `ReceiptId?`, `Source` (Manual/Ocr), `CreatedAt` (UTC), `UpdatedAt` (UTC) | Índices: `OccurredAt`, (`CategoryId`,`OccurredAt`), `PaymentMethodId`, `Currency`. Monto positivo; el signo lo da `Type`. |
| `Receipt` | `Id` Guid PK, `CapturedAt` (UTC), `OcrText`, `ParsedTotalMinor?`, `ParsedCurrency?`, `ParsedDate?`, `ParsedMerchant?`, `ParsedTaxId?`, `OcrEngine` | Resultado del OCR para auditoría/re-parseo. |
| `ReceiptImage` | `ReceiptId` PK, `Jpeg` BLOB, `Width`, `Height` | Separada para no cargar BLOBs al listar. ~1600 px, JPEG ~80 %. |
| `ExchangeRate` | `Id` int PK, `FromCurrency`, `ToCurrency`, `RateMicros` long (tasa × 10⁶), `EffectiveDate` | Se usa la última tasa con `EffectiveDate ≤ OccurredAt`; sin tasa → excluido del consolidado con aviso. |
| `AppSetting` | `Key` PK, `Value` | Moneda base, tema, fecha del último respaldo. |

Versión de esquema en `PRAGMA user_version`.

**Flujo OCR**: `/scan` (`MediaPicker`: cámara o galería; la foto de la caché se borra al leerla) → `ReceiptPhoto.Prepare` (orientada, lado mayor 1600 px, JPEG 80 %) → `IOcrService.RecognizeAsync` (foto completa) → `ReceiptParser` → `ScanState` (solo en memoria) → `TransactionEdit?scan=1` pre-llenado, con chips de otros totales y comercios y avisos si el total no cuadró o no hay fecha. Si el NIT ya se escaneó, `GetLatestForTaxIdAsync` propone el comercio y la categoría de la última vez (`MerchantSuggestions.Combine`) → confirmación del usuario → `ITransactionRepository.AddWithReceiptAsync` (`Receipt` con las filas en `OcrText` + `ReceiptImage` + `Transactions`, en una sola transacción de BD). Permisos: `CAMERA` (y `READ_EXTERNAL_STORAGE` hasta API 32) en Android; `NSCameraUsageDescription` y `NSPhotoLibraryUsageDescription` en iOS.

**Inicio** (`Dashboard`): mes en curso con totales por moneda (`CurrencyTotals`), variación del gasto frente al mismo tramo del mes anterior (`ExpenseComparison`: del 1 al día de hoy), "En qué gastas" por categoría principal (`CategoryShare`, las subcategorías suman en su padre; top 4 + "Otras") y los últimos 5 movimientos.

**Análisis** (`/analytics`): periodo (día/semana/mes/año), moneda (una opción por moneda con movimientos y "Todo en <base>" si hay varias) y Gastos/Ingresos, guardados en `AnalyticsViewState`. `SpendingAnalyzer` agrega en memoria: barras por hora/día/mes (`Series`), por categoría principal (`CategoryShare.ByCategory`) y por medio de pago; promedio diario sobre los días transcurridos. El consolidado usa `CurrencyConverter` (última tasa con fecha ≤ la del movimiento, o el inverso del par contrario); los movimientos sin tasa quedan fuera con aviso y enlace a Tasas de cambio.
- Las barras son `BarChart` (HTML/CSS): el gráfico de barras de `MudChart` se aplasta en el WebView de iOS (el área de dibujo queda arriba y el eje X muy abajo, también con `MatchBoundsToSize`). La dona de `MudChart` sí funciona en ambas plataformas.

## Respaldo exportable/importable
- Archivo `.piradar` (`BackupCrypto`, Core): `PIRADAR` (7 B) + versión de formato (1 B) + versión de esquema (int32) + iteraciones (int32) + salt (16 B) + nonce (12 B) + cuerpo cifrado con **AES-256-GCM** + tag (16 B); la cabecera va como datos asociados (si se altera, el tag no cuadra). Cuerpo = JSON de todas las tablas (`{app, schemaVersion, createdAt, tables: {Tabla: {columns, rows}}}`, BLOB en base64) comprimido con GZip. Clave: **PBKDF2-SHA256** con 600 000 iteraciones sobre la contraseña en UTF-8 normalizada a NFC (el mismo texto con tildes llega distinto según el teclado).
  - En **Android** PBKDF2 es la implementación propia `Pbkdf2Sha256` (código `unsafe`, por eso `AllowUnsafeBlocks` en Core): la del sistema pasa cada HMAC por Java (~12 s en el emulador en Release, contra ~2,7 s la propia). En iOS se usa `Rfc2898DeriveBytes` (CommonCrypto). Los tests verifican que ambas dan lo mismo (vectores de RFC 7914 y comparación con .NET).
  - AES-GCM nativo de Android **aborta el proceso** (SIGABRT en `CipherUpdate`) con cuerpo vacío: `BackupCrypto` no cifra cuerpos vacíos y rechaza archivos sin bytes cifrados.
- Es independiente de la clave de la BD, así que se puede restaurar en otro dispositivo (probado Android ↔ iOS).
- **Exportar** (`BackupService`, Data): lee todas las tablas en una transacción (`TableDump`, SQL genérico, sin records) → JSON → GZip → cifrado → archivo en la caché → `Share.RequestAsync`. Luego guarda `LastBackupAt`. Los `.piradar` de la caché se borran al volver a la página si tienen más de 1 hora (la app que lo recibió puede seguir leyéndolo). Recordatorio en Inicio si no hay respaldo o pasaron > 30 días (`BackupReminder`).
- **Importar**: `FilePicker` (todos los tipos; se valida al abrir) → contraseña → descifrar (tag inválido ⇒ "Contraseña incorrecta o archivo dañado.") → versión de esquema más nueva ⇒ rechazo → se recrea el esquema de esa versión en una BD **en memoria** (`Migrator.MigrateTo`), se cargan las filas con las claves foráneas validadas y se aplican las mismas migraciones que a la BD real → resumen (movimientos, fechas, recibos) y confirmación "Reemplazar" → borrado y carga en **una transacción** (`defer_foreign_keys`), con `LastBackupAt` = fecha del respaldo restaurado. Nombres de tabla y columna del archivo se validan contra el esquema antes de armar el SQL.
- Probar sin selector de archivos (solo Debug): copiar el archivo como `backup-in.piradar` en `AppDataDirectory` y usar "Restaurar archivo de prueba (Debug)" (`#debug-backup`).

## Convenciones
- Identificadores de código en inglés; textos de UI en español con cultura `es-CO`.
- `Nullable` e `ImplicitUsings` habilitados; namespaces file-scoped; métodos asíncronos con sufijo `Async`; servicios por interfaz inyectados vía DI.
- **Dinero**: nunca `double`/`float`. Montos como `long` en unidades menores (`AmountMinor`): COP escala 0, USD escala 2. Usar el tipo `Money` de Core. Formato `es-CO` (`$ 1.234.567`, `US$ 12,50`).
- **Fechas**: `CreatedAt`/`UpdatedAt` siempre en UTC (`DateTime.UtcNow`; al leer, tratarlas como `DateTimeKind.Utc`). `OccurredAt` en hora local del dispositivo y es la base de las agregaciones. La semana inicia el lunes (ISO).
- **Seguridad**: nunca loguear la clave de la BD, la contraseña del respaldo, texto OCR ni montos. Las imágenes de recibos se guardan como BLOB dentro de la BD cifrada, nunca como archivos sueltos.
- **Migraciones**: manuales con `PRAGMA user_version`, una clase `MigrationNNN_*` por versión en `PiRadar.Data/Migrations`, registrada al final de `Migrator.All`. Usar SQL explícito (tipos de columna iguales a los de sqlite-net: `integer`, `bigint`, `varchar`, `varchar(36)` para Guid, `blob`), nunca `CreateTable<T>` ni inserts con records, para que una migración vieja no dependa de la forma actual de las clases. Nunca editar una migración ya publicada.
- **Records vs dominio**: `PiRadar.Data/Records` tiene las clases tabla de sqlite-net (`internal`); el dominio de Core no depende de sqlite-net. `RecordMapping` convierte y restaura `DateTimeKind` (sqlite-net guarda ticks sin Kind).
- **Enums**: se guardan como entero con valores explícitos: no reordenar ni reutilizar números. `Currency` se guarda como texto ISO (`COP`/`USD`) y se lee con `CurrencyExtensions.ParseCode` (no `Enum.TryParse`, que acepta "1").
- **UI responsive**: `MudGrid` y breakpoints de MudBlazor; verificar en teléfono y tablet, vertical y horizontal.
- El OCR solo **propone** valores; el usuario siempre confirma antes de guardar.
