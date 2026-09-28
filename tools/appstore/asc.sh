#!/bin/zsh
# Llama a la API de App Store Connect: tools/appstore/asc.sh GET|POST|PATCH|DELETE /v1/... [cuerpo-json]
# La clave .p8 está fuera del repo, en ../piRadar-firma (como la de Android). El ID de clave y el del emisor no son secretos.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
ASC_KEY_ID=${ASC_KEY_ID:-C8H93HUL6U}
ASC_ISSUER_ID=${ASC_ISSUER_ID:-467c52c0-f3d3-422a-9dd8-ed6aa585585c}
ASC_KEY_FILE=${ASC_KEY_FILE:-$root/../piRadar-firma/AuthKey_$ASC_KEY_ID.p8}
# Se compila una vez (el intérprete de swift mezcla sus propios argumentos con los del script).
jwt="${TMPDIR:-/tmp}/piradar-asc-jwt"
[[ -x "$jwt" && "$jwt" -nt "$root/tools/appstore/asc-jwt.swift" ]] || swiftc -O -o "$jwt" "$root/tools/appstore/asc-jwt.swift"
token=$("$jwt" "$ASC_KEY_FILE" "$ASC_KEY_ID" "$ASC_ISSUER_ID")
body=()
[[ $# -ge 3 ]] && body=(--data "$3")
curl -sS -X "$1" "https://api.appstoreconnect.apple.com$2" -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" "${body[@]}"
