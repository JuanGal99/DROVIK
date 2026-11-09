@echo off
rem Starts the whole application. Docker is the only requirement.
cd /d "%~dp0"
docker info >nul 2>&1 || (echo Inicia Docker Desktop y vuelve a ejecutar este comando. & exit /b 1)
docker run --rm -v "%cd%:/repo" -w /repo node:24.11.1-alpine node tools/setup-env.mjs || exit /b 1
docker compose --profile app up -d --build --wait || exit /b 1
docker run --rm -v "%cd%:/repo" -w /repo node:24.11.1-alpine node tools/setup-env.mjs --show
