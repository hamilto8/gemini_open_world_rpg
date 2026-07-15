#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

dotnet build Meridian.sln --no-restore -m:1 /nodeReuse:false
dotnet test Meridian.sln --no-build --no-restore -m:1 /nodeReuse:false
dotnet format Meridian.sln whitespace --verify-no-changes --no-restore
git diff --check

GODOT_BIN="${GODOT_BIN:-}"
if [[ -z "$GODOT_BIN" && -x /Applications/Godot_mono.app/Contents/MacOS/Godot ]]; then
  GODOT_BIN=/Applications/Godot_mono.app/Contents/MacOS/Godot
fi

if [[ -n "$GODOT_BIN" ]]; then
  GODOT_OUTPUT="$("$GODOT_BIN" --headless --path . --quit-after 5 2>&1)"
  printf '%s\n' "$GODOT_OUTPUT"
  if [[ "$GODOT_OUTPUT" == *"ERROR:"* ]]; then
    echo "Godot emitted runtime errors." >&2
    exit 1
  fi
else
  echo "Godot executable not found; set GODOT_BIN to run the scene boot gate." >&2
fi
