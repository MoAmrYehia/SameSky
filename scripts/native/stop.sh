#!/usr/bin/env bash
. "$(dirname "$0")/common.sh"
for n in worker api; do [ -f "$DEV_ROOT/$n.pid" ] && kill "$(cat "$DEV_ROOT/$n.pid")" 2>/dev/null && echo "stopped $n"; rm -f "$DEV_ROOT/$n.pid"; done
