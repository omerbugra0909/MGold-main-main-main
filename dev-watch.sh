#!/usr/bin/env bash

set -e

PORT=5139

# Clean up stale local runs so a previous crashed/watch session does not block startup.
if command -v fuser >/dev/null 2>&1; then
  PIDS="$(fuser -n tcp "${PORT}" 2>/dev/null || true)"
  if [ -n "${PIDS}" ]; then
    echo "Port ${PORT} kullanimda. Eski MGold sureci kapatiliyor: ${PIDS}"
    kill ${PIDS} 2>/dev/null || true
    sleep 1
  fi
fi

# Fallback to polling so low inotify limits do not break local development.
export DOTNET_USE_POLLING_FILE_WATCHER=1

exec dotnet watch run "$@"
