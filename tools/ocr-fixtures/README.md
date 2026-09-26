# Fixtures de OCR para `ReceiptParser`

Los JSON de `tests/PiRadar.Core.Tests/Receipts/Fixtures/{ios,android}` son `OcrPage` reales producidos por la app
(Vision en el simulador iOS 18.1, ML Kit en el AVD `piRadar_API35`) a partir de fotos de recibos, **anonimizados**.
Las fotos nunca se versionan.

## Agregar recibos

1. Copiar las fotos a una carpeta fuera del repo (las HEIC del iPhone se pasan antes a JPEG con
   `sips -s format jpeg foto.HEIC --out foto.jpg`) y convertirlas a JPEG derecho (aplica la rotación EXIF):
   ```sh
   swiftc -O tools/ocr-fixtures/bake.swift -o /tmp/bake
   /tmp/bake foto.jpg ocr-in/nombre_full.jpg 0      # 0 = tamaño original; o p. ej. 1600
   ```
2. Build **Debug** de la app. La página Diagnóstico (`/diagnostics`) corre `OcrLab` sobre
   `AppDataDirectory/ocr-in/*.jpg` y escribe `ocr-out/<nombre>.json` y `ocr-out/_log.txt`. Para abrirla sin tocar la
   pantalla, escribir `go /diagnostics` en `ui-script.txt` (ver `UiScript` en CLAUDE.md) y abrir la app.
   - iOS: `xcrun simctl get_app_container <UDID> com.jhbelalc.piradar data` → copiar a `Library/ocr-in/`.
   - Android: `adb push ocr-in /data/local/tmp/` y
     `adb shell run-as com.jhbelalc.piradar sh -c 'mkdir -p files/ocr-in; cat /data/local/tmp/ocr-in/X.jpg > files/ocr-in/X.jpg'`
     por archivo (`cp -r` falla por permisos); leer con
     `adb shell run-as com.jhbelalc.piradar cat files/ocr-out/<nombre>.json`.
3. Anonimizar con `tools/ocr-fixtures/anonymize.py` (con los nombres y documentos de esos recibos en
   `tools/ocr-fixtures/private-terms.txt`, que git ignora) y revisar a mano que no quede nada personal:
   nombres, cédulas, correos, tarjetas, códigos de autorización, CUFE, número de socio.
4. Agregar el caso a `ReceiptParserFixtureTests` con los valores **reales** del recibo; los campos que el OCR no
   alcanza a leer se dejan sin verificar (`null`) con un comentario del motivo.
5. Borrar `ocr-in/` y `ocr-out/` del simulador y del emulador.
