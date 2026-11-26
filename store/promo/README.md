# Video de promoción 1.5

`promo-1.5.html` es la animación (1080×1920, 26 s; `?lang=en` para inglés). `render.js` la graba cuadro a cuadro a MP4:

```sh
cd <carpeta temporal> && npm i puppeteer-core ffmpeg-static && cp <repo>/store/promo/* .
node render.js es ~/Desktop/piRadar-1.5-promo-es.mp4
node render.js en ~/Desktop/piRadar-1.5-promo-en.mp4
node render.js es stills/es --stills 5,12,20   # cuadros sueltos para revisar
node render.js es ~/Desktop/piRadar-1.5-promo-es-horizontal.mp4 --h   # 1920×1080 para YouTube
```
