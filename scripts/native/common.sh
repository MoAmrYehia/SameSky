#!/usr/bin/env bash
# Shared settings for the native Quest scripts on macOS (Apple Silicon) and Linux. Source it: . "$(dirname "$0")/common.sh"
set -euo pipefail
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT="$REPO/native/quest"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt")"
UNITY="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
PYTHON="$REPO/api/.venv/bin/python"
DEV_ROOT="$REPO/api/data/native-dev"; DEV_LOGS="$DEV_ROOT/logs"; mkdir -p "$DEV_LOGS"
[ -f "$DEV_ROOT/secret.txt" ] || head -c 48 /dev/urandom | base64 | tr -d '\n' > "$DEV_ROOT/secret.txt"
export DATABASE_URL="sqlite:///$DEV_ROOT/samesky.db" SAMESKY_MEDIA_ROOT="$DEV_ROOT/media" SAMESKY_SECRET_KEY="$(cat "$DEV_ROOT/secret.txt")" SAMESKY_PUBLIC_API_URL="" SAMESKY_CORS_ORIGINS="http://localhost:5173"
API_PORT=8000; API_URL="http://127.0.0.1:$API_PORT"
api_up() { curl -fsS "$API_URL/v1/health" >/dev/null 2>&1; }
