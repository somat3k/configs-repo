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

# Replace template placeholders
DATE=$(date +%Y-%m-%d)
AUTHOR=$(git config user.name 2>/dev/null || echo "unknown")

sed -i.bak \
  -e "s/{{SESSION_ID}}/$SESSION_ID/g" \
  -e "s/{{DATE}}/$DATE/g" \
  -e "s/{{AUTHOR}}/$AUTHOR/g" \
  "$TARGET/SESSION.md"
rm -f "$TARGET/SESSION.md.bak"

echo "✓ Session created at $TARGET"
echo "  Edit $TARGET/SESSION.md to fill in goals, module focus, etc."
