# Store listing (English, US) — piRadar

Language: **English (United States) – en-US**. Same package / app: `com.jhbelalc.piradar` (Play) and app `6817079629` (App Store). Add as an additional localization; the Spanish listing stays the default.

## App name (max 30)
```
piRadar: Expense Tracker
```
(24 characters.)

## App Store subtitle (max 30)
```
Track spending, no cloud
```
(24 characters.)

## Short description — Google Play (max 80)
```
Track expenses, scan receipts and see where your money goes. Private, no cloud.
```
(77 characters.)

## Promotional text — App Store (max 170)
```
Track expenses and income, scan receipts and see where your money goes. Everything stays on your phone: no account, no cloud, no ads.
```

## Keywords — App Store (max 100, comma separated)
```
expense,budget,spending,receipt,scanner,income,money,finance,tracker,USD,categories,savings
```
(91 characters.)

## Full description (max 4000)
```
piRadar is a personal finance app that keeps everything on your phone. No accounts, no cloud and no internet needed: your transactions, receipts and reports never leave your device.

LOG IN SECONDS
• Expenses and income in US dollars (USD) and Colombian pesos (COP).
• Categories and subcategories with icons and colors, ready to use or made your way.
• Payment methods: cash, cards, bank accounts, digital wallets like Venmo or Zelle, and your own.
• Merchant, description and notes so you can find every transaction later.

SCAN YOUR RECEIPTS
• Take a photo of the receipt or pick one from your gallery.
• The text is read on your phone and is never sent to any server.
• piRadar suggests the total, date and merchant; you confirm before saving.
• It remembers the name and category you chose last time for that merchant.
• The photo is stored next to the transaction, inside the encrypted database.

SEE WHERE YOUR MONEY GOES
• A home screen with this month's totals and how they compare with last month.
• Reports by day, week, month or year.
• Charts by category and by payment method, and over time.
• Average daily spending.
• A combined view in your base currency using the exchange rates you enter.

PRIVATE BY DESIGN
• The database is encrypted with a key kept in your phone's secure storage.
• The app does not request internet access, so it cannot send your data anywhere.
• No ads, no sign-up and no tracking.

YOUR BACKUP, IN YOUR HANDS
• Export an encrypted backup file with a password you choose.
• Save it wherever you like and restore it on another phone, even between iPhone and Android.
• piRadar reminds you to back up if more than a month has passed.

Also: light, dark or system theme, in English or Spanish, and a layout that adapts to phones and tablets.
```

## What's new (App Store, version 1.2 or later)
```
piRadar is now available in English. Choose your language in Settings, or let it follow your phone. Dollar amounts, dates, times and receipts from US stores are now supported.
```

## App Store review notes (English)
```
The app has no accounts and does not need the internet: all data is stored on the device. To try receipt scanning, use the camera or pick a receipt photo under Scan. The language can be changed in Settings > Language.
```

## Where to enter it
- **App Store Connect** → piRadar → the version → localization "English (U.S.)": name, subtitle, promotional text, description, keywords, support URL and privacy policy URL (use the English policy URL below). The App Privacy answers ("Data Not Collected") are shared by all localizations.
- **Play Console** → Store presence → Main store listing → Manage translations → English (United States): app name, short and full description, and screenshots.
- **Privacy policy (English)**: publish `privacy-policy.en.html` next to the Spanish one (for example `https://www.belvicto.com/piRadar/privacy-policy.en.html`) and use that URL in the English localization. Keep the Spanish URL for the Spanish one.
- **Screenshots (done 2026-09-30)**: `store/screenshots-en/iphone-6.9/` (1320×2868) and `store/screenshots-en/ipad-13/` (2064×2752), 8 each, in English with US demo data (dollars, fictional US merchants, a fictional US grocery receipt read by the real OCR). They go in App Store Connect only when the new version (1.2 / build 9+) exists: a version waiting for review can't take a new localization. For Google Play phone screenshots (Android, ratio ≤ 2:1, e.g. 1080×1809) the Android emulator won't boot on this Mac: take them on a real Android phone in English.
- **How they were made**: `dotnet run tools/demo-data/demo-backup-us.cs -- demo-us.piradar demo1234 <today>` builds the US backup; the receipt is `store/google-play/graphics/receipt-demo-us.html` rendered as `scan-in.jpg`; simulators set to `en-US` (`xcrun simctl spawn <UDID> defaults write -g AppleLanguages -array en-US`, then reboot); restore and screenshot with two UiScripts (the restore reloads the page, see CLAUDE.md "Idiomas") and `pick Category=Groceries` for the scan screen.
