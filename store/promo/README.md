# Video de promoción 1.5

`promo-1.5.html` es la animación (1080×1920, 26 s; `?lang=en` para inglés). `render.js` la graba cuadro a cuadro a MP4:

```sh
cd <carpeta temporal> && npm i puppeteer-core ffmpeg-static && cp <repo>/store/promo/* .
node render.js es ~/Desktop/piRadar-1.5-promo-es.mp4
node render.js en ~/Desktop/piRadar-1.5-promo-en.mp4
node render.js es stills/es --stills 5,12,20   # cuadros sueltos para revisar
node render.js es ~/Desktop/piRadar-1.5-promo-es-horizontal.mp4 --h   # 1920×1080 para YouTube
```

## Con voz (`--voice`, 33 s)
`?voice=1` estira la animación (mapa `K` al final del HTML) para que cada texto coincida con su frase. `voice.py` arma la
pista de narración a partir de la grabación (`~/Desktop/voz-es.m4a`, `voz-en.m4a`; no se versionan): tramos de voz
sacados con `silencedetect`, agrupados por frase del guion y ubicados en `AT`; luego filtro de ruido, compresor y
`loudnorm` a −16 LUFS.

```sh
python3 voice.py ~/Desktop/voz-es.m4a voice-es.wav es
node render.js es tmp.mp4 --voice [--h]
ffmpeg -i tmp.mp4 -i voice-es.wav -map 0:v -map 1:a -c:v copy -c:a aac -b:a 192k -shortest out.mp4
```
Con una grabación nueva hay que volver a medir los tramos (`-af silencedetect=noise=-35dB:d=0.6`) y ajustar `G` en `voice.py`.
