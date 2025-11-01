#!/usr/bin/env bash
set -euo pipefail

# Generate both C++ (Doxygen) and C# (DocFX) documentation.
# Requirements: doxygen, docfx, dotnet SDK.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCS_DIR="$ROOT_DIR/docs"

echo "==> Checking tools"
command -v doxygen >/dev/null 2>&1 || { echo "Error: doxygen not found in PATH" >&2; exit 1; }
command -v docfx >/dev/null 2>&1 || { echo "Error: docfx not found in PATH" >&2; exit 1; }
command -v dotnet >/dev/null 2>&1 || { echo "Error: dotnet not found in PATH" >&2; exit 1; }

export DOTNET_ROOT="${HOME}/.dotnet"
export PATH="${HOME}/.dotnet/tools:${DOTNET_ROOT}:${PATH}"

echo "==> Building C# projects to produce XML docs"
( cd "$ROOT_DIR" && dotnet build "$ROOT_DIR/DeDuBa/DeDuBa.csproj" )

echo "==> Generating C++ docs (Doxygen)"
( cd "$DOCS_DIR" && rm -rf doxygen && doxygen Doxyfile )

# Copy Doxygen output into DocFX site as static assets is configured in docfx.json (doxygen/**)

echo "==> Generating C# site (DocFX)"
( cd "$DOCS_DIR" && docfx docfx.json )

echo "==> Done"
