#!/usr/bin/env bash
# Builds dist/Tolmacz-macOS.dmg, a universal (Apple Silicon and Intel) installer image.
#
# Without a certificate the app gets an ad-hoc signature, which is fine for your own Mac.
# To distribute it, set these and notarize the resulting DMG:
#   CODESIGN_KEY="Developer ID Application: Your Name (TEAMID)"
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/src/Tolmacz.App/Tolmacz.App.csproj"
BUILD_DIR="$ROOT/src/Tolmacz.App/bin/Release/net10.0-maccatalyst"
DIST="$ROOT/dist"
KEY="${CODESIGN_KEY:--}"

# Notarization needs the hardened runtime and a secure timestamp. An ad-hoc signature
# cannot carry a timestamp, so this applies only to a real signing identity.
HARDENED=false
if [ "$KEY" != "-" ]; then
  HARDENED=true
fi

rm -rf "$BUILD_DIR" "$ROOT/src/Tolmacz.App/obj/Release"
dotnet publish "$PROJECT" -f net10.0-maccatalyst -c Release \
  -p:CreatePackage=false \
  -p:EnableCodeSigning=true \
  -p:CodesignKey="$KEY" \
  -p:UseHardenedRuntime="$HARDENED" \
  -p:CodesignEntitlements=Platforms/MacCatalyst/Entitlements.plist

APP="$BUILD_DIR/Tolmacz.app"
[ -d "$APP" ] || { echo "Build output not found: $APP" >&2; exit 1; }

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
ditto "$APP" "$STAGE/Tolmacz.app"
ln -s /Applications "$STAGE/Applications"
cp "$ROOT/NOTICE.md" "$STAGE/NOTICE.md"

mkdir -p "$DIST"
rm -f "$DIST/Tolmacz-macOS.dmg"
hdiutil create -volname "Tolmacz" -srcfolder "$STAGE" -ov -format ULFO "$DIST/Tolmacz-macOS.dmg"
if [ "$KEY" != "-" ]; then
  codesign --force --timestamp --sign "$KEY" "$DIST/Tolmacz-macOS.dmg"
fi
echo "Created $DIST/Tolmacz-macOS.dmg"
