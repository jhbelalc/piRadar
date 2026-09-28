#!/bin/zsh
# Renderiza con Chrome sin ventana el ícono (512×512) y la imagen destacada (1024×500) de Google Play
# a partir de los HTML de store/google-play/graphics/.
set -euo pipefail
cd "$(dirname "$0")/../store/google-play/graphics"
chrome="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
render() { "$chrome" --headless --disable-gpu --hide-scrollbars --force-device-scale-factor=1 \
    --window-size=$3 --screenshot="$PWD/$2" "file://$PWD/$1" 2>/dev/null; }
render icon.html icon-512.png 512,512
render feature-graphic.html feature-graphic-1024x500.png 1024,500
sips -g pixelWidth -g pixelHeight icon-512.png feature-graphic-1024x500.png
