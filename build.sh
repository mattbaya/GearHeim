#!/usr/bin/env bash
# Build GearHeim and produce a Thunderstore/Gale-ready zip in dist/.
#   ./build.sh                      # uses libs/Managed (or $VALHEIM_MANAGED)
#   VALHEIM_MANAGED=/path/to/valheim_Data/Managed ./build.sh
set -euo pipefail
cd "$(dirname "$0")"

DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
VERSION="$(python3 -c 'import json;print(json.load(open("package/manifest.json"))["version_number"])')"

"$DOTNET" build src/GearHeim.csproj -c Release -nologo -v quiet

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
cp package/manifest.json package/README.md package/CHANGELOG.md package/icon.png "$STAGE/"
mkdir -p "$STAGE/plugins"
cp src/bin/Release/netstandard2.1/GearHeim.dll "$STAGE/plugins/"

mkdir -p dist
OUT="$PWD/dist/GearHeim-$VERSION.zip"
rm -f "$OUT"
(cd "$STAGE" && zip -qr "$OUT" .)
echo "Built $OUT"
