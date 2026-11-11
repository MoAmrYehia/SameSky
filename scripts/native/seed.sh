#!/usr/bin/env bash
# Development-only accounts and rooms (scripts/native/dev-seed.json), created through the public API.
. "$(dirname "$0")/common.sh"
api_up || "$(dirname "$0")/start.sh"
"$PYTHON" "$(dirname "$0")/seed_dev.py" --api "$API_URL" --seed "$(dirname "$0")/dev-seed.json"
