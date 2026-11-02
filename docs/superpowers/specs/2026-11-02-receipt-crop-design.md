# Recorte de la foto del recibo (automático y manual) — 1.2.3

Fecha: 2026-11-02. Android e iPhone.

## Intención
Hay fotos de recibos con mucho alrededor (mesa, otros papeles, la mano) que no vale la pena guardar. Recortarlas baja
el tamaño de la foto guardada y, al escanear, evita que el texto de alrededor confunda la lectura del total.

## Decidido con el usuario
- Al escanear (cámara **y** galería, mismo flujo): se lee la foto completa como hoy y **siempre** aparece la pantalla de
  recorte con un **marco automático** ajustado al texto. Botones: **Continuar**, **Ajustar automático**, **Usar foto
  completa**. El marco se ajusta arrastrando bordes y esquinas.
- Solo el texto **dentro del marco** se usa para proponer total, fecha, comercio; solo esa parte de la foto se guarda.
- En un movimiento **ya guardado**: botón **Recortar** junto a la foto, misma pantalla. El marco automático se calcula
  leyendo la foto guardada otra vez (local). Al confirmar, la foto recortada reemplaza a la anterior; monto, fecha y
  comercio no cambian.
- **Rectángulo recto** (sin enderezar perspectiva ni rotar).
- Enfoque **A**: marco propio en la pantalla (script propio como `image-zoom.js`), sin bibliotecas.

## Enfoques considerados
- **A. Marco propio (elegido)**: igual en ambas plataformas, sin dependencias, recibe el marco automático.
- B. Cropper.js: maduro, pero agrega una dependencia y funciones que no se usan.
- Descartado: recortadores nativos (iOS no trae uno libre; Android necesita biblioteca nativa; no reciben el marco).

## Decisiones mías
- **Marco automático** = el **bloque principal de texto** (grupos de recuadros del OCR cercanos entre sí; gana el de más
  caracteres) más un margen; el texto suelto y alejado (otro papel al lado) no lo estira. Sin texto ⇒ foto completa.
- Se recorta la **foto guardada (~1600 px)**: misma nitidez que hoy y el tamaño baja en proporción a lo recortado.
- Marco normalizado (0–1) sobre la foto orientada; tamaño mínimo 10 % por lado.
- Un elemento del OCR cuenta como dentro si su centro está dentro del marco.
- En recibos guardados el reemplazo se guarda al confirmar el recorte (con aviso), no al tocar Guardar del formulario.
- Ayuda en español e inglés; regenerar `store/help/*.html`.

## Fuera de alcance
Enderezar perspectiva, rotar, recortar varias fotos a la vez, recortar antes de leer (la lectura sigue sobre la foto
completa original).
