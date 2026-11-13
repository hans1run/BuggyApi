# buggy-mcp

MCP server that lets Claude (Claude Code, Claude Desktop, any MCP client) read and create items in Buggy.

| Tool | What it does |
|---|---|
| `buggy_list_projects` | Active projects |
| `buggy_list_members` | User display names (valid assignees) |
| `buggy_list_items` | Items in a project, filterable by status, type, assignee |
| `buggy_get_item` | One item with description and comments |
| `buggy_create_item` | Create a bug/feature/task; assignee must be an existing user |
| `buggy_add_comment` | Comment on an item |

The first four are marked read-only. Projects are given by name (case-insensitive) and items by number (`12` for `#12`).

## Setup

1. In Buggy, open **Manage users → API keys → New key**, name it (e.g. `Claude`; the name is shown as the author of what it creates) and copy the key.
2. Run the installer and paste the key when asked. It builds the server, checks the key against Buggy,
   registers the server for all your Claude Code sessions and checks that Claude Code can start it.

   ```bash
   ./install.sh
   ```

   On Windows, run it in Git Bash, or from PowerShell:

   ```powershell
   & "$env:LOCALAPPDATA\Programs\Git\bin\bash.exe" C:/Development/source/buggy-api/tools/buggy-mcp/install.sh
   ```

   Run it again to replace the key or pick up code changes. `BUGGY_URL` (default `https://buggy.wplan.no`),
   `MCP_NAME` (default `buggy`) and `MCP_SCOPE` (default `user`) can be set as environment variables.

New Claude Code sessions then have the `buggy_*` tools.

## What a key can do

Read projects, items, comments and user names; create items; add comments; upload attachments.
It cannot change or archive items, create projects, or manage users or keys. Revoke a key under
**Manage users → API keys**; it stops working immediately.
