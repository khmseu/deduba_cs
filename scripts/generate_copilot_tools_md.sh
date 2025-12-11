#!/usr/bin/env bash
set -euo pipefail
# Small helper to run the extraction -> csv -> md pipeline.
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "${ROOT}"

echo "[1/3] Running extract_copilot_tools.py"
python3 scripts/extract_copilot_tools.py

echo "[2/3] Running generate_copilot_tools_csv.py"
python3 scripts/generate_copilot_tools_csv.py

echo "[3/3] Running convert-csv-to-md.sh"
bash scripts/convert-csv-to-md.sh

echo "Done: .github/copilot-languageModelTools.md"
