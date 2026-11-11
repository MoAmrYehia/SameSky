#!/usr/bin/env bash
# Starts the API (0.0.0.0:8000) and the worker on the isolated native-dev data, detached. Stop with stop.sh.
. "$(dirname "$0")/common.sh"
api_up && { echo "API already running at $API_URL"; exit 0; }
(cd "$REPO/api" && "$PYTHON" -m alembic upgrade head)
nohup "$PYTHON" -m uvicorn samesky.main:app --app-dir "$REPO/api" --host 0.0.0.0 --port $API_PORT > "$DEV_LOGS/api.log" 2>&1 & echo $! > "$DEV_ROOT/api.pid"
(cd "$REPO/api" && nohup "$PYTHON" -m samesky.worker --interval 2 > "$DEV_LOGS/worker.log" 2>&1 & echo $! > "$DEV_ROOT/worker.pid")
for _ in $(seq 1 60); do api_up && break; sleep 0.5; done
api_up || { echo "API did not start; see $DEV_LOGS/api.log"; exit 1; }
echo "API ready: $API_URL (headset on this Wi-Fi: http://$(ipconfig getifaddr en0 2>/dev/null || hostname -I | cut -d' ' -f1):$API_PORT)"
