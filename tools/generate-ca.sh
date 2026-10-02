#!/usr/bin/env bash
set -euo pipefail

# Convenience wrapper to run the Certificate Authority (CaTool) generator on Linux/macOS.
# Usage: ./tools/generate-ca.sh [organization] [outDir] [rootYears] [signingYears]
# Example: ./tools/generate-ca.sh "My NGO" "data/ca" 20 2

ORGANIZATION="${1:-My Organization}"
OUT_DIR="${2:-data/ca}"
ROOT_YEARS="${3:-20}"
SIGNING_YEARS="${4:-2}"
ROOT_PASSWORD="${CA_ROOT_PASSWORD:-}"
SIGNING_PASSWORD="${CA_SIGNING_PASSWORD:-}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

echo "Running CertificateEngine.CaTool for '$ORGANIZATION'..."
ARGS=(init --organization "$ORGANIZATION" --out "$OUT_DIR" --root-years "$ROOT_YEARS" --signing-years "$SIGNING_YEARS")

if [ -n "$ROOT_PASSWORD" ]; then
    ARGS+=(--root-password "$ROOT_PASSWORD")
fi

if [ -n "$SIGNING_PASSWORD" ]; then
    ARGS+=(--signing-password "$SIGNING_PASSWORD")
fi

dotnet run --project "$SCRIPT_DIR/CertificateEngine.CaTool" -- "${ARGS[@]}"
