# Ficha de Google Play — piRadar

Idioma predeterminado de la ficha: **Español (Latinoamérica) – es-419**.
Paquete: `com.jhbelalc.piradar`. Categoría: **Finanzas**. Etiquetas sugeridas: presupuesto, gastos, finanzas personales.

## Nombre de la app (máx. 30)
```
piRadar: gastos e ingresos
```
(26 caracteres. Alternativa corta: `piRadar`.)

## Descripción breve (máx. 80)
```
Registra gastos e ingresos, escanea recibos y analiza tu dinero. Sin nube.
```
(74 caracteres.)

## Descripción completa (máx. 4000)
```
piRadar es una app de finanzas personales que guarda todo en tu teléfono. No hay cuentas, no hay nube y no necesita Internet: tus movimientos, tus recibos y tus análisis nunca salen del dispositivo.

REGISTRA EN SEGUNDOS
• Gastos e ingresos en pesos colombianos (COP) y dólares (USD).
• Categorías y subcategorías con ícono y color, listas para usar o hechas a tu medida.
• Medios de pago: efectivo, tarjetas, cuentas bancarias, Nequi, Daviplata y los que agregues.
• Comercio, descripción y notas para encontrar cada movimiento después.

ESCANEA TUS RECIBOS
• Toma una foto del recibo o elígela de la galería.
• El texto se lee en el propio teléfono, sin enviarlo a ningún servidor.
• piRadar propone el total, la fecha, el comercio y el NIT; tú confirmas antes de guardar.
• Recuerda el nombre y la categoría que elegiste la última vez para ese comercio.
• La foto queda guardada junto al movimiento, dentro de la base de datos cifrada.

ENTIENDE EN QUÉ SE VA TU DINERO
• Inicio con los totales del mes y la comparación con el mes anterior.
• Análisis por día, semana, mes o año.
• Gráficos por categoría, por medio de pago y en el tiempo.
• Promedio de gasto diario.
• Vista consolidada en tu moneda base con las tasas de cambio que tú ingresas.

PRIVADA POR DISEÑO
• La base de datos está cifrada con una clave que se guarda en el almacén seguro del teléfono.
• La app no pide permiso de Internet: no puede enviar tus datos a ninguna parte.
• Sin anuncios, sin registro y sin rastreo.

TU RESPALDO, EN TUS MANOS
• Exporta un archivo de respaldo cifrado con la contraseña que elijas.
• Guárdalo donde quieras y restáuralo en otro teléfono, incluso entre Android e iPhone.
• piRadar te recuerda hacer un respaldo si pasa más de un mes sin hacerlo.

Además: tema claro, oscuro o el del sistema, y diseño que se adapta a teléfonos y tabletas.
```

## Recursos gráficos
Listos en `store/google-play/`:
| Recurso | Archivo | Requisito de Play |
|---|---|---|
| Ícono | `graphics/icon-512.png` | 512×512 PNG |
| Imagen destacada | `graphics/feature-graphic-1024x500.png` | 1024×500 sin transparencia |
| Capturas de teléfono (8) | `screenshots/1-inicio.png` … `8-inicio-oscuro.png`, 1080×1809 | 2–8, proporción ≤ 2:1 |

Orden sugerido: Inicio, Nuevo desde recibo, Movimientos, Análisis, Por categoría, Categorías, Respaldo, Inicio oscuro.

Cómo se hicieron (para rehacerlas):
- Ícono e imagen destacada: `tools/store-graphics.sh` (Chrome sin ventana sobre los HTML de `graphics/`).
- Datos **ficticios**: `dotnet run tools/demo-data/demo-backup.cs -- demo.piradar demo1234 <fecha>` genera un respaldo con
  ~400 movimientos inventados; se restaura en el emulador Debug como `backup-in.piradar`. El recibo es
  `graphics/receipt-demo.html` (comercio y NIT inventados) renderizado como `scan-in.jpg` y leído por el OCR real.
- Capturas: `tools/demo-data/screenshots-ui-script.txt` como `ui-script.txt` con `adb shell wm size 1080x1920`
  (el 20:9 del emulador pasa el límite 2:1 de Play), `adb exec-out screencap` en cada línea `SHOT` y recorte de la
  franja vacía de la barra de estado (111 px). Borrar `backup-in.piradar` antes de la captura de Respaldo para que no
  aparezca el botón Debug. Nunca usar datos ni recibos reales.

## Datos de contacto (públicos en la ficha)
- Correo: belvictodeveloper@gmail.com (obligatorio y visible para todos).
- Sitio web: opcional.
- Política de privacidad: URL pública de `politica-privacidad.md` (ver `formularios.md`).
