#!/usr/bin/env bash
#
# build-plugin.sh — build a single sink and drop the output into the
# host's plugin directory. Used to exercise the plugin loader without
# adding a ProjectReference from Server.csproj (which would defeat the
# test by pulling the sink into the default load context).
#
# Layout assumption:
#   <repo-root>/
#     Modules/
#       Herald.Sinks/      ← this repo (where the script lives)
#         src/Herald.Sinks.<Name>/
#         tools/build-plugin.sh
#       Server/            ← consumer host (cheat-mode runs from here)
#         plugins/         ← drop target; created if missing
#           Herald.Sinks.<Name>/
#             Herald.Sinks.<Name>.dll
#             Herald.Sinks.<Name>.deps.json
#
# Usage:
#   bash tools/build-plugin.sh HelloWorld
#   bash tools/build-plugin.sh HelloWorld --debug
#   bash tools/build-plugin.sh HelloWorld --tfm net9.0
#   bash tools/build-plugin.sh HelloWorld --dest /custom/path/plugins
#
# The script clears <plugins>/Herald.Sinks.<Name>/ before copying so a
# stale dll from a previous build does not race with the hot-reload
# watcher. The plugin loader is first-wins on registration, so dropping
# a new version requires removing the old folder first; this script
# does that for you on every invocation.

set -euo pipefail

# ── Args ──────────────────────────────────────────────────────────

if [[ $# -lt 1 ]]; then
    cat >&2 <<EOF
Usage: bash $0 <SinkName> [options]

Build Herald.Sinks.<SinkName> and copy the output dll into the host's
plugin directory.

Options:
  --debug              Build Debug instead of Release.
  --tfm <framework>    Target framework (default: net8.0).
  --dest <path>        Override destination (default: auto-detect
                       <repo-root>/Modules/Server/plugins/).
  --help               Show this help.

Example:
  bash $0 HelloWorld
EOF
    exit 1
fi

SINK_NAME="$1"
shift

CONFIG="Release"
TFM="net8.0"
DEST_OVERRIDE=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --debug)   CONFIG="Debug"; shift ;;
        --release) CONFIG="Release"; shift ;;
        --tfm)     TFM="$2"; shift 2 ;;
        --dest)    DEST_OVERRIDE="$2"; shift 2 ;;
        --help|-h)
            grep '^# ' "$0" | sed 's/^# //'
            exit 0
            ;;
        *) echo "Unknown option: $1" >&2; exit 1 ;;
    esac
done

# ── Resolve paths ─────────────────────────────────────────────────

# Script lives at <Herald.Sinks>/tools/. Sinks repo root is one up.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SINKS_REPO="$(cd "$SCRIPT_DIR/.." && pwd)"
SOURCE_DIR="$SINKS_REPO/src/Herald.Sinks.$SINK_NAME"
PROJECT_FILE="$SOURCE_DIR/Herald.Sinks.$SINK_NAME.csproj"

if [[ ! -f "$PROJECT_FILE" ]]; then
    echo "Error: project file not found: $PROJECT_FILE" >&2
    exit 1
fi

# Default destination: <outer-repo>/Modules/Server/plugins/Herald.Sinks.<Name>/
# Resolved relative to the Sinks repo root; works whether the outer
# repo is the Herald monorepo or a clone where Server lives elsewhere.
if [[ -n "$DEST_OVERRIDE" ]]; then
    DEST_BASE="$DEST_OVERRIDE"
else
    DEST_BASE="$SINKS_REPO/../Server/plugins"
fi

DEST_DIR="$DEST_BASE/Herald.Sinks.$SINK_NAME"

# ── Build ─────────────────────────────────────────────────────────

echo "Building Herald.Sinks.$SINK_NAME ($CONFIG, $TFM)..."
dotnet build "$PROJECT_FILE" -c "$CONFIG" -f "$TFM" --nologo

BUILD_OUTPUT="$SOURCE_DIR/bin/$CONFIG/$TFM"
MAIN_DLL="$BUILD_OUTPUT/Herald.Sinks.$SINK_NAME.dll"

if [[ ! -f "$MAIN_DLL" ]]; then
    echo "Error: build did not produce $MAIN_DLL" >&2
    exit 1
fi

# ── Copy ──────────────────────────────────────────────────────────

# Wipe + recreate the per-plugin folder. The plugin loader's first-wins
# rule means an in-place overwrite would not pick up a new version
# while the host runs; the host watcher needs to see a fresh "create"
# event on a clean folder.
echo "Clearing destination: $DEST_DIR"
rm -rf "$DEST_DIR"
mkdir -p "$DEST_DIR"

# The minimum the plugin loader needs is the main dll. The .deps.json
# is read by AssemblyDependencyResolver to share the host's
# Herald.Core copy — copy it when present so the resolver has its
# canonical input. The pdb is optional but nice when stepping through
# plugin code from a debugger.
cp "$MAIN_DLL" "$DEST_DIR/"
[[ -f "$BUILD_OUTPUT/Herald.Sinks.$SINK_NAME.deps.json" ]] && \
    cp "$BUILD_OUTPUT/Herald.Sinks.$SINK_NAME.deps.json" "$DEST_DIR/"
[[ -f "$BUILD_OUTPUT/Herald.Sinks.$SINK_NAME.pdb" ]] && \
    cp "$BUILD_OUTPUT/Herald.Sinks.$SINK_NAME.pdb" "$DEST_DIR/"

echo
echo "Plugin dropped:"
ls -la "$DEST_DIR/" | tail -n +2 | awk '{ printf "  %s\n", $NF }'
echo
echo "If Herald.Server is running with --plugins-dir plugins, the watcher"
echo "should pick this up within a second. To remove (hot-remove test):"
echo "  rm -rf \"$DEST_DIR\""
