#!/usr/bin/env bash
# Build Holdhint for Windows from macOS or Windows (Git Bash).
# Needs the .NET 8 SDK. The installers also need NSIS (makensis).
set -euo pipefail

cd "$(dirname "$0")"
export PATH="${HOME}/.dotnet:${PATH}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

VER="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)"
if [[ -z "${VER}" ]]; then
  echo "Version is missing from Directory.Build.props" >&2
  exit 1
fi

python3 make_icon.py
dotnet test src/Holdhint.Tests/Holdhint.Tests.csproj -c Release --nologo

publish_one() {
  local rid="$1"
  local arch="${rid#win-}"
  local out="dist/publish-${rid}"
  rm -rf "${out}"
  dotnet publish src/Holdhint.App/Holdhint.App.csproj \
    -c Release \
    -r "${rid}" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    -p:SatelliteResourceLanguages=en \
    -o "${out}"
  python3 pack_pe.py "${out}/Holdhint.exe" "${arch}"
  if ! grep -a -q -i "PerMonitorV2" "${out}/Holdhint.exe"; then
    echo "Holdhint.exe is missing the PerMonitorV2 DPI manifest" >&2
    exit 1
  fi
}

publish_one win-x64
publish_one win-arm64

stage_one() {
  local rid="$1"
  local arch="${rid#win-}"
  local dir="dist/stage-${arch}"
  rm -rf "${dir}"
  mkdir -p "${dir}" "dist"
  cp "dist/publish-${rid}/Holdhint.exe" "${dir}/Holdhint.exe"
  cp "src/Holdhint.App/holdhint.ico" "${dir}/holdhint.ico"
  cp "../LICENSE" "${dir}/LICENSE.txt"
  rm -f "dist/Holdhint-${VER}-${rid}.zip"
  zip -q -9 -j "dist/Holdhint-${VER}-${rid}.zip" \
    "${dir}/Holdhint.exe" "${dir}/holdhint.ico" "${dir}/LICENSE.txt"
}

stage_one win-x64
stage_one win-arm64

if ! command -v makensis >/dev/null 2>&1; then
  echo "makensis was not found. The zip files are in dist/. Install NSIS to build the setup programs." >&2
  exit 1
fi

for arch in x64 arm64; do
  makensis -NOCD \
    -DARCH="${arch}" \
    -DVER="${VER}" \
    -DSTAGE="$(pwd)/dist/stage-${arch}" \
    -DOUTDIR="$(pwd)/dist" \
    -DICON="$(pwd)/src/Holdhint.App/holdhint.ico" \
    installer/holdhint.nsi
done

echo
echo "Holdhint ${VER} Windows builds:"
ls -lh dist/Holdhint-"${VER}"-win-*
