#!/usr/bin/env bash
# build.sh - Build every sink in the Herald.Sinks monorepo.
#
# Every csproj under src/ and tests/ builds; if --test is passed, every
# test project runs. This matches the shape of Herald.Core's build.sh.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION="$SCRIPT_DIR/Herald.Sinks.slnx"

RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
RESET='\033[0m'

info()    { printf "${CYAN}[INFO]${RESET}  %s\n" "$*"; }
success() { printf "${GREEN}[OK]${RESET}    %s\n" "$*"; }
fail()    { printf "${RED}[FAIL]${RESET}  %s\n" "$*"; exit 1; }

CONFIG="Debug"
RUN_TESTS=false
RUN_CLEAN=false
SHOW_HELP=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --help|-h) SHOW_HELP=true; shift ;;
        --debug)   CONFIG="Debug"; shift ;;
        --release) CONFIG="Release"; shift ;;
        --test)    RUN_TESTS=true; shift ;;
        --clean)   RUN_CLEAN=true; shift ;;
        *) fail "Unknown flag: $1" ;;
    esac
done

if [[ "$SHOW_HELP" == true ]]; then
    cat <<'HELP'
build.sh - Build Herald.Sinks monorepo

FLAGS
    --release / --debug    Configuration (default: Debug)
    --test                 Run tests after build
    --clean                Clean bin/obj before build
HELP
    exit 0
fi

printf "${WHITE}----------------------------------------${RESET}\n"
info "Building Herald.Sinks ($CONFIG)..."

if [[ "$RUN_CLEAN" == true ]]; then
    info "Cleaning bin/obj..."
    find "$SCRIPT_DIR/src" "$SCRIPT_DIR/tests" \
        -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null || true
fi

# Build the whole solution. dotnet resolves every csproj under src/ and
# tests/ via the .sln file; adding a new sink only requires updating the
# solution and does not change this script.
dotnet build "$SOLUTION" \
    -c "$CONFIG" \
    --nologo \
    || fail ".NET build failed"

success "Herald.Sinks build complete."

if [[ "$RUN_TESTS" == true ]]; then
    info "Running tests..."
    dotnet test "$SOLUTION" \
        -c "$CONFIG" \
        --nologo \
        --no-build \
        || fail "Tests failed"
    success "Tests passed."
fi

# Validate CAPABILITY.yaml files if a product-sheet tool is available.
# A missing tool is not a build failure — the product sheet is
# generated in CI, not on every local build.
if command -v python >/dev/null 2>&1 && [[ -f "$SCRIPT_DIR/tools/product-sheet.py" ]]; then
    info "Validating CAPABILITY.yaml manifests..."
    python "$SCRIPT_DIR/tools/product-sheet.py" --validate-only \
        || fail "Capability manifest validation failed"
    success "Capability manifests valid."
fi
