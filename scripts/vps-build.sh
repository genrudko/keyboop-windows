#!/usr/bin/env bash
# Isolated Linux -> Windows build on v10360. No root, no production service changes.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BASE="${KEYBOOP_VPS_HOME:-/home/admin/experiments/keyboop-windows}"
DOTNET="${KEYBOOP_DOTNET:-$BASE/toolchain/dotnet/dotnet}"
OUT_ROOT="${KEYBOOP_ARTIFACTS:-$BASE/artifacts/builds}"

if [[ ! -x "$DOTNET" ]]; then
    echo "Missing .NET SDK executable: $DOTNET" >&2
    exit 2
fi

mkdir -p "$BASE/tmp" "$BASE/dotnet-home" "$BASE/nuget" "$OUT_ROOT"
export TMPDIR="$BASE/tmp"
export DOTNET_CLI_HOME="$BASE/dotnet-home"
export NUGET_PACKAGES="$BASE/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

cd "$ROOT"
REV="$(git rev-parse HEAD)"
SHORT="${REV:0:12}"
TARGET="$OUT_ROOT/$SHORT"
mkdir -p "$TARGET"

echo "SOURCE_COMMIT=$REV"
echo "DOTNET_SDK=$("$DOTNET" --version)"
echo "=== Core verification ==="
"$DOTNET" run --project tests/Keyboop.Core.Smoke/Keyboop.Core.Smoke.csproj -c Release

echo "=== Cross-compile WPF ==="
"$DOTNET" build src/Keyboop.Windows/Keyboop.Windows.csproj -c Release -p:EnableWindowsTargeting=true --nologo

echo "=== Publish Windows x64 self-contained ==="
"$DOTNET" publish src/Keyboop.Windows/Keyboop.Windows.csproj -c Release -r win-x64 --self-contained true \
    -p:EnableWindowsTargeting=true -o "$TARGET/app" --nologo

EXE="$TARGET/app/Keyboop.Windows.exe"
if [[ ! -f "$EXE" ]]; then echo "Missing Windows PE executable!" >&2; exit 3; fi
file "$EXE"
file "$EXE" | grep -q 'PE32+ executable.*MS Windows'

ARCHIVE="$TARGET/keyboop-windows-$SHORT-win-x64.zip"
(cd "$TARGET/app" && zip -q -r "$ARCHIVE" .)

{
    printf 'COMMIT=%s\n' "$REV"
    printf 'SDK=%s\n' "$("$DOTNET" --version)"
    printf 'PLATFORM=win-x64\nSELF_CONTAINED=true\n'
    printf 'CORE_SMOKE=PASS\nCROSS_BUILD=PASS\n'
    printf 'WINDOWS_INTERACTIVE_TESTS=NOT_RUN\n'
    sha256sum "$ARCHIVE"
} | tee "$TARGET/build-receipt.txt"

echo "ARTIFACT=$ARCHIVE"
echo "SUCCESS: Linux cross-build + core smoke. Windows interactive testing NOT performed."
