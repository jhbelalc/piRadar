#!/bin/zsh
# Compila el Android App Bundle (.aab) Release para Google Play, firmado con la clave propia de piRadar, y lo deja
# en el Escritorio (~/Desktop/piRadar-<versión>-<código>.aab). Subir ApplicationVersion antes de cada envío a Play.
#
# La clave y su contraseña: ver tools/build-apk.sh.
set -euo pipefail
cd "$(dirname "$0")/.."

PIRADAR_KEYSTORE_PASS=$(security find-generic-password -s piradar-android-keystore -w) || {
    echo "No está la contraseña de la clave en el llavero (servicio piradar-android-keystore)." >&2
    exit 1
}
export PIRADAR_KEYSTORE_PASS

props=(-f net10.0-android -c Release -p:PiRadarPlayStore=true)
dotnet publish src/PiRadar.App "${props[@]}"

version=$(dotnet build src/PiRadar.App "${props[@]}" -getProperty:ApplicationDisplayVersion)
code=$(dotnet build src/PiRadar.App "${props[@]}" -getProperty:ApplicationVersion)
aab=src/PiRadar.App/bin/Release/net10.0-android/publish/com.jhbelalc.piradar-Signed.aab
cp "$aab" ~/Desktop/piRadar-$version-$code.aab
echo "AAB: ~/Desktop/piRadar-$version-$code.aab"
