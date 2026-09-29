#!/bin/zsh
# Compila el .ipa Release de iOS firmado para el App Store y lo deja en el Escritorio (~/Desktop/piRadar-<versión>-<código>.ipa).
#   tools/build-ipa.sh            # solo compilar
#   tools/build-ipa.sh --upload   # compilar y subir a App Store Connect (TestFlight) con la clave de la API
# Subir ApplicationVersion antes de cada envío (App Store Connect rechaza un número de compilación repetido).
set -euo pipefail
cd "$(dirname "$0")/.."

props=(-f net10.0-ios26.0 -c Release)

# Siempre desde cero: la compilación incremental reutilizó en la 1.1 (6) código AOT generado contra una versión
# anterior de PiRadar.Core, y en el iPhone (solo AOT) la app abortaba al abrir ("Failed to load AOT module ...
# a dependency cannot be found or it is out of date"). El AOT completo tarda lo mismo de todos modos.
rm -rf src/PiRadar.App/obj/Release/net10.0-ios26.0 src/PiRadar.App/bin/Release/net10.0-ios26.0 \
    src/PiRadar.Core/obj/Release src/PiRadar.Core/bin/Release src/PiRadar.Data/obj/Release src/PiRadar.Data/bin/Release
dotnet publish src/PiRadar.App "${props[@]}"

version=$(dotnet build src/PiRadar.App "${props[@]}" -getProperty:ApplicationDisplayVersion)
code=$(dotnet build src/PiRadar.App "${props[@]}" -getProperty:ApplicationVersion)
ipa=$(ls -t src/PiRadar.App/bin/Release/net10.0-ios26.0/ios-arm64/publish/*.ipa | head -1)
out=~/Desktop/piRadar-$version-$code.ipa
cp "$ipa" "$out"
echo "IPA: $out"

if [[ "${1:-}" == "--upload" ]]; then
    # altool busca AuthKey_<id>.p8 en API_PRIVATE_KEYS_DIR.
    API_PRIVATE_KEYS_DIR="$PWD/../piRadar-firma" xcrun altool --upload-app --type ios --file "$out" \
        --apiKey C8H93HUL6U --apiIssuer 467c52c0-f3d3-422a-9dd8-ed6aa585585c
fi
