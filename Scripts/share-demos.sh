#!/usr/bin/env bash
# share-demos.sh — Start ga-client dev server and expose via Cloudflare Tunnel
# Usage: bash scripts/share-demos.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CLIENT_DIR="$SCRIPT_DIR/../Apps/ga-client"
PORT=5173
DEV_URL="http://localhost:$PORT"

cleanup() {
  echo ""
  echo "Shutting down..."
  [[ -n "${DEV_PID:-}" ]] && kill "$DEV_PID" 2>/dev/null
  exit 0
}
trap cleanup INT TERM

# Start Vite dev server in background
echo "Starting ga-client dev server on port $PORT..."
cd "$CLIENT_DIR"
npm run dev -- --port "$PORT" &
DEV_PID=$!

# Wait for dev server to be ready
echo "Waiting for dev server..."
for i in $(seq 1 30); do
  if curl -s "$DEV_URL" >/dev/null 2>&1; then
    echo "Dev server ready."
    break
  fi
  if ! kill -0 "$DEV_PID" 2>/dev/null; then
    echo "ERROR: Dev server failed to start."
    exit 1
  fi
  sleep 1
done

# Launch Cloudflare Tunnel
echo ""
echo "Starting Cloudflare Tunnel..."
echo "Share the URL below with anyone to access the demo."
echo "Press Ctrl+C to stop."
echo ""
cloudflared tunnel --url "$DEV_URL"
