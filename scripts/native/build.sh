#!/usr/bin/env bash
# Quest APK from macOS: ./build.sh [api-url] [--release]
. "$(dirname "$0")/common.sh"
API="${1:-http://$(ipconfig getifaddr en0 2>/dev/null):$API_PORT}"; REL=""; [ "${2:-}" = "--release" ] && REL="-sameskyRelease"
export SAMESKY_COMMIT="$(git -C "$REPO" rev-parse --short HEAD)"
OUT="$PROJECT/Builds/Android/SameSky-$(date +%Y%m%d-%H%M).apk"
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -executeMethod SameSky.EditorTools.BuildScript.Android "-sameskyApi=$API" "-sameskyOut=$OUT" $REL -logFile "$PROJECT/Builds/build-android.log" -quit
shasum -a 256 "$OUT"
