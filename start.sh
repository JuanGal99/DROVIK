#!/usr/bin/env bash
# Starts the whole application. Docker is the only requirement.
set -euo pipefail
cd "$(dirname "$0")"
docker info > /dev/null 2>&1 || { echo "Inicia Docker y vuelve a ejecutar este comando." >&2; exit 1; }
# MSYS_NO_PATHCONV keeps Git Bash on Windows from rewriting the container paths.
export MSYS_NO_PATHCONV=1
docker run --rm -v "$PWD:/repo" -w /repo node:24.11.1-alpine node tools/setup-env.mjs
docker compose --profile app up -d --build --wait
docker run --rm -v "$PWD:/repo" -w /repo node:24.11.1-alpine node tools/setup-env.mjs --show
