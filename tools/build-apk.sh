#!/bin/zsh
# Compila el APK Release de Android firmado con la clave propia de piRadar y lo deja en el Escritorio.
#
#   tools/build-apk.sh                                  # ARM64, para teléfonos → ~/Desktop/piRadar-<versión>.apk
#   tools/build-apk.sh -p:PiRadarEmulator=true            # para el emulador (no se copia al Escritorio)
#
# La clave está en ../piRadar-firma/piradar.keystore y su contraseña en el llavero de macOS
# (servicio "piradar-android-keystore"). En otro Mac: copiar el archivo y guardar la contraseña con
#   security add-generic-password -U -s piradar-android-keystore -a piradar -w '<contraseña>'
set -euo pipefail
cd "$(dirname "$0")/.."

PIRADAR_KEYSTORE_PASS=$(security find-generic-password -s piradar-android-keystore -w) || {
    echo "No está la contraseña de la clave en el llavero (servicio piradar-android-keystore)." >&2
    exit 1
}
export PIRADAR_KEYSTORE_PASS

dotnet build src/PiRadar.App -f net10.0-android -c Release "$@"

apk=src/PiRadar.App/bin/Release/net10.0-android/com.jhbelalc.piradar-Signed.apk
if [[ $# -eq 0 ]]; then
    version=$(dotnet build src/PiRadar.App -f net10.0-android -c Release -getProperty:ApplicationDisplayVersion)
    cp "$apk" ~/Desktop/piRadar-$version.apk
    echo "APK: ~/Desktop/piRadar-$version.apk"
else
    echo "APK: $apk"
fi
