#!/bin/bash
# Validate proof envelope test fixtures against the JSON schema

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "Validating proof envelope fixtures..."
echo

# Check if ajv-cli is installed
if ! command -v ajv &> /dev/null; then
    echo "Error: ajv-cli is not installed"
    echo "Install with: npm install -g ajv-cli ajv-formats"
    exit 1
fi

# Validate valid-proof-envelope.json
echo "✓ Validating tests/fixtures/valid-proof-envelope.json"
ajv validate \
    -s "$REPO_ROOT/policies/schemas/proof-envelope.schema.json" \
    -d "$REPO_ROOT/tests/fixtures/valid-proof-envelope.json" \
    --spec=draft7 \
    -c ajv-formats

# Validate expired-proof-envelope.json
echo "✓ Validating tests/fixtures/expired-proof-envelope.json"
ajv validate \
    -s "$REPO_ROOT/policies/schemas/proof-envelope.schema.json" \
    -d "$REPO_ROOT/tests/fixtures/expired-proof-envelope.json" \
    --spec=draft7 \
    -c ajv-formats

echo
echo "All fixtures validated successfully!"
