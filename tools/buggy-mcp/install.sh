#!/bin/bash
# Build buggy-mcp and register it with Claude Code, so Claude can read and create Buggy items.
# Works in Git Bash on Windows, and on macOS/Linux.
#
# Usage:  ./install.sh          (asks for the API key from Buggy > Manage users > API keys)
#   From PowerShell:  & "$env:LOCALAPPDATA\Programs\Git\bin\bash.exe" C:/Development/source/buggy-api/tools/buggy-mcp/install.sh
#
# Optional environment variables:
#   BUGGY_API_KEY  the key (skips the prompt)
#   BUGGY_URL      Buggy address              (default https://buggy.wplan.no)
#   MCP_NAME       name of the MCP server     (default buggy)
#   MCP_SCOPE      user | local | project     (default user = all your projects)
# Run it again to replace the key or pick up code changes.

set -euo pipefail

BUGGY_URL="${BUGGY_URL:-https://buggy.wplan.no}"
BUGGY_URL="${BUGGY_URL%/}"
MCP_NAME="${MCP_NAME:-buggy}"
MCP_SCOPE="${MCP_SCOPE:-user}"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

fail() { echo "ERROR: $*" >&2; exit 1; }

# --- prerequisites ---------------------------------------------------------
command -v node >/dev/null || fail "Node.js is not installed (https://nodejs.org, version 20 or newer)."
NODE_MAJOR=$(node -p 'process.versions.node.split(".")[0]')
[ "$NODE_MAJOR" -ge 20 ] || fail "Node.js 20 or newer is required (found $(node --version))."
command -v npm >/dev/null || fail "npm was not found."
command -v claude >/dev/null || fail "The Claude Code CLI (claude) was not found on PATH."
command -v curl >/dev/null || fail "curl was not found."

# --- build -----------------------------------------------------------------
echo "==> Building buggy-mcp in $DIR"
(cd "$DIR" && npm ci --no-audit --no-fund --loglevel=error && npm run build --silent)

# Claude Code is a native program on Windows: give it C:/... paths, not Git Bash's /c/...
SERVER_JS="$DIR/dist/index.js"
if command -v cygpath >/dev/null; then SERVER_JS=$(cygpath -m "$SERVER_JS"); fi
[ -f "$DIR/dist/index.js" ] || fail "Build did not produce dist/index.js."

# --- API key ---------------------------------------------------------------
if [ -z "${BUGGY_API_KEY:-}" ]; then
  echo "Create a key in Buggy under Manage users > API keys (e.g. named \"Claude\")."
  read -r -s -p "Paste the API key: " BUGGY_API_KEY; echo
fi
BUGGY_API_KEY="${BUGGY_API_KEY//[[:space:]]/}"
[ -n "$BUGGY_API_KEY" ] || fail "No API key given."

echo "==> Checking the key against $BUGGY_URL"
STATUS=$(curl -s -o /dev/null -w '%{http_code}' --max-time 20 -H "X-Api-Key: $BUGGY_API_KEY" "$BUGGY_URL/api/projects" || true)
case "$STATUS" in
  200) ;;
  401) fail "Buggy rejected the key (401). Check that you copied all of it and that it has not been revoked." ;;
  000) fail "Could not reach $BUGGY_URL." ;;
  *)   fail "Unexpected response from Buggy: HTTP $STATUS." ;;
esac

# --- register --------------------------------------------------------------
echo "==> Registering MCP server \"$MCP_NAME\" ($MCP_SCOPE scope)"
claude mcp remove "$MCP_NAME" --scope "$MCP_SCOPE" >/dev/null 2>&1 || true   # replace any earlier install
claude mcp add "$MCP_NAME" --scope "$MCP_SCOPE" \
  -e "BUGGY_API_KEY=$BUGGY_API_KEY" -e "BUGGY_URL=$BUGGY_URL" \
  -- node "$SERVER_JS" >/dev/null

echo "==> Checking that Claude Code can start it"
if claude mcp list 2>/dev/null | grep -E "^$MCP_NAME:" | grep -q "Connected"; then
  echo "==> Done. Start a new Claude Code session to use the buggy_* tools."
else
  claude mcp list 2>/dev/null | grep -E "^$MCP_NAME:" >&2 || true
  fail "\"$MCP_NAME\" is registered but did not connect. Try: claude mcp get $MCP_NAME"
fi
