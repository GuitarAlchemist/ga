param(
    [switch]$SkipNpmInstall
)

$ErrorActionPreference = 'Stop'

Write-Host "=== GA Guitar Web WASM demo setup ==="

$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}
Set-Location $scriptRoot

Write-Host "== Ensuring wasm32-unknown-unknown target =="
rustup target add wasm32-unknown-unknown

Write-Host "== Building Rust WASM engine =="
Push-Location .\rust-engine
cargo build --release --target wasm32-unknown-unknown
$wasmPath = Join-Path (Resolve-Path .\target\wasm32-unknown-unknown\release) 'guitar_engine.wasm'
Pop-Location

Write-Host "== Copying WASM to public/guitar_engine.wasm =="
New-Item -ItemType Directory -Force -Path .\public | Out-Null
Copy-Item $wasmPath -Destination .\public\guitar_engine.wasm -Force

if (-not $SkipNpmInstall) {
    Write-Host "== Running npm install =="
    npm install
}

Write-Host "== Starting Vite dev server (npm run dev) =="
npm run dev

