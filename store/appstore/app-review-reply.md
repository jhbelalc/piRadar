# App Review reply — piRadar 1.1 (7), Guideline 2.1 "Information Needed"

Paste the **Reply** into the Resolution Center (attach the screen recording), and the **Notes** into App Store Connect → the version → App Review Information → Notes. Same text for both is fine.

## Reply / Notes (English; the reviewer reads English)

```
Thank you for the review. Here is the information requested.

1. SCREEN RECORDING
Attached: a screen recording on a physical iPhone, starting from launching the app and showing the typical flow (add a transaction, scan a receipt, review analytics, categories, encrypted backup, settings). The app has no account registration, login, user-generated content, social features, or paid content/features, so none of those flows exist.

2. PURPOSE AND TARGET AUDIENCE
piRadar is a personal finance app for adults (18+) who want to track their own expenses and income. It solves the problem of seeing where one's money goes without sharing financial data with anyone: the user records transactions (manually or by photographing a receipt), organizes them by category and payment method, and sees reports by day, week, month and year in Colombian pesos (COP) and US dollars (USD). All data stays on the device.

3. HOW TO ACCESS THE MAIN FEATURES
No account, login or credentials are needed: every feature is available as soon as the app opens. Note: this version (1.1, build 7) has a Spanish user interface. English labels for the main screens:
- Inicio = Home, Movimientos = Transactions, Análisis = Analytics, Más = More.
- "Registrar" (Home) or the green "+" = add a transaction. Fill the amount, choose a category, tap "Guardar" (Save).
- "Escanear" (Home) = scan a receipt: tap "Tomar foto" (take photo) or "Elegir de la galería" (choose from gallery), pick any photo of a printed receipt, and the app proposes the total, date and merchant; the user confirms and saves. A sample receipt image is attached to this message (fictional store, fictional data).
- Más > Categorías = categories, Medios de pago = payment methods, Tasas de cambio = exchange rates, Respaldo = encrypted backup, Ajustes = settings (theme, base currency), Acerca de = about.
- Más > Respaldo > "Crear respaldo" creates an encrypted backup file with a password the user chooses; "Restaurar un respaldo" restores one. A sample backup file with demo data is attached (password: demo1234); restoring it replaces the data on the device, which is useful to see the app with 6 months of fictional transactions.

4. EXTERNAL SERVICES, TOOLS OR PLATFORMS
None. The app has no server, no accounts, no analytics, no ads and no network requests. Everything runs on the device:
- Data is stored in an encrypted SQLite database (SQLite3 Multiple Ciphers, SQLCipher-compatible); the key is generated on the device and kept in the iOS Keychain.
- Receipt text recognition uses Apple's Vision framework on the device (no cloud OCR). The photo and the recognized text are not sent anywhere.
- Backups are files encrypted on the device (AES-256-GCM); the user chooses where to save or share them with the system share sheet.
- UI components: .NET MAUI Blazor Hybrid and MudBlazor (open source, run locally).

5. REGIONAL DIFFERENCES
None. The app works the same in all regions. It supports two currencies (COP and USD) in every region, with totals kept separate per currency; exchange rates are entered manually by the user.

6. REGULATED INDUSTRY / PROTECTED MATERIAL
Not applicable. piRadar is a personal record-keeping tool: it does not connect to banks, move money, give loans, trade securities or crypto, or provide financial advice. It contains no protected third-party content (open-source libraries are listed in the app under Más > Acerca de > Licencias de terceros).

Contact: belvictodeveloper@gmail.com. Privacy policy: https://www.belvicto.com/piRadar/politica-privacidad.html
```

## Attachments to add to the reply
1. The screen recording (see below).
2. `store/google-play/graphics/receipt-demo-us.html` rendered as an image (fictional receipt) — I can export the PNG on request. (Or the Spanish one, `receipt-demo.html`.)
3. A sample backup file: `dotnet run tools/demo-data/demo-backup-us.cs -- piradar-demo.piradar demo1234 <today>` (fictional data). Skip it if you prefer not to ask the reviewer to restore anything.

## Screen recording (you, on your iPhone — about 2–3 minutes)
Start recording from Control Center (the red record button) **before** opening piRadar, and show:
1. Launching the app from the Home Screen (Inicio opens).
2. "Registrar" → enter an amount, choose a category, "Guardar".
3. "Escanear" → "Tomar foto" and photograph a receipt (or choose one from the gallery) → the app proposes total/date/merchant → "Guardar".
4. Movimientos (the list), then Análisis (charts, switch Day/Week/Month/Year).
5. Más → Categorías, then Más → Respaldo → "Crear respaldo" (enter a password, share sheet), then close the sheet.
6. Más → Ajustes.
Use a test amount, not real data, or record with the demo backup restored. Keep the file small; if the reply form rejects it, upload it somewhere and paste the link.

## Optional: also answer about the English build
This build (1.1, build 7) is Spanish-only. English (US) ships next (build 9+). If you prefer the reviewer to see English, submit that build instead; it needs an iPhone check first (TestFlight).
