#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION_FILE="$SCRIPT_DIR/ProjectSettings/ProjectVersion.txt"

if [ ! -f "$VERSION_FILE" ]; then
    echo "ERROR: Could not find $VERSION_FILE" >&2
    exit 1
fi

UNITY_VERSION="$(awk '/^m_EditorVersion:/ { print $2; exit }' "$VERSION_FILE")"

if [ -z "$UNITY_VERSION" ]; then
    echo "ERROR: Could not parse m_EditorVersion from $VERSION_FILE" >&2
    exit 1
fi

# Editor install roots to check, in order. Set UNITY_HUB_EDITOR_DIR to check a
# custom location first (e.g. an external drive or non-default Hub install path).
EDITOR_ROOTS=()
if [ -n "${UNITY_HUB_EDITOR_DIR:-}" ]; then
    EDITOR_ROOTS+=("$UNITY_HUB_EDITOR_DIR")
fi
EDITOR_ROOTS+=(
    "/Applications/Unity/Hub/Editor"
    "$HOME/Unity/Hub/Editor"
)

UNITY_APP=""
for ROOT in "${EDITOR_ROOTS[@]}"; do
    CANDIDATE="$ROOT/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
    if [ -x "$CANDIDATE" ]; then
        UNITY_APP="$CANDIDATE"
        break
    fi
done

if [ -z "$UNITY_APP" ]; then
    echo "ERROR: Unity $UNITY_VERSION is not installed. Checked:" >&2
    for ROOT in "${EDITOR_ROOTS[@]}"; do
        echo "  $ROOT/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity" >&2
    done
    exit 1
fi

"$UNITY_APP" -projectPath "$SCRIPT_DIR" -executeMethod UnityEditorDevelopmentBenchmark.Editor.Benchmarking.BenchmarkRunner.StartBenchmark
