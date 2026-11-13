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
2. Build the server once (and again after pulling changes):

   ```bash
   cd tools/buggy-mcp
   npm install
   npm run build
   ```

3. Register it for all your Claude Code sessions:

   ```bash
   claude mcp add buggy --scope user -e BUGGY_API_KEY=<key> -- node C:/Development/source/buggy-api/tools/buggy-mcp/dist/index.js
   ```

   `BUGGY_URL` defaults to `https://buggy.wplan.no`; pass `-e BUGGY_URL=http://localhost:5244` to use a local API.

## What a key can do

Read projects, items, comments and user names; create items; add comments; upload attachments.
It cannot change or archive items, create projects, or manage users or keys. Revoke a key under
**Manage users → API keys**; it stops working immediately.
