#!/usr/bin/env bash
# new-session.sh — create a new session from the template
set -euo pipefail

SESSION_ID="${1:-session-$(date +%Y%m%d-%H%M)}"
TARGET="sessions/$SESSION_ID"

if [ -d "$TARGET" ]; then
  echo "Session '$SESSION_ID' already exists at $TARGET"
  exit 1
fi

cp -r sessions/template/ "$TARGET"

# Use Python for placeholder replacement — safe for values containing / & \ etc.
DATE=$(date +%Y-%m-%d)
AUTHOR=$(git config user.name 2>/dev/null || echo "unknown")

python3 - "$SESSION_ID" "$DATE" "$AUTHOR" << 'PY'
import sys

session_id, date, author = sys.argv[1], sys.argv[2], sys.argv[3]
path = f"sessions/{session_id}/SESSION.md"

with open(path) as fh:
    content = fh.read()

content = content.replace("{{SESSION_ID}}", session_id)
content = content.replace("{{DATE}}", date)
content = content.replace("{{AUTHOR}}", author)

with open(path, "w") as fh:
    fh.write(content)
PY

echo "✓ Session created at $TARGET"
echo "  Edit $TARGET/SESSION.md to fill in goals, module focus, etc."
