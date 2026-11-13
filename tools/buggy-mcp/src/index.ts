#!/usr/bin/env node
// MCP server exposing Buggy (projects, items, subtasks, comments) as tools.
// Config: BUGGY_API_KEY (required, created under Manage users > API keys)
//         BUGGY_URL     (default https://buggy.wplan.no)

import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js'
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js'
import { z } from 'zod'
import {
  BuggyClient,
  BuggyError,
  ITEM_TYPES,
  PRIORITIES,
  STATUSES,
  type Item
} from './buggy-client.js'

const apiKey = process.env.BUGGY_API_KEY
if (!apiKey) {
  console.error('BUGGY_API_KEY is not set. Create a key in Buggy under Manage users > API keys.')
  process.exit(1)
}
const client = new BuggyClient((process.env.BUGGY_URL ?? 'https://buggy.wplan.no').replace(/\/+$/, ''), apiKey)

const server = new McpServer({ name: 'buggy', version: '1.1.0' })

type ToolResult = { content: { type: 'text'; text: string }[]; isError?: boolean }

/** Runs a tool body, turning errors into a readable tool error instead of a protocol failure. */
async function run(body: () => Promise<string>): Promise<ToolResult> {
  try {
    return { content: [{ type: 'text', text: await body() }] }
  } catch (err) {
    const message = err instanceof BuggyError ? err.message : `Unexpected error: ${(err as Error).message}`
    return { content: [{ type: 'text', text: message }], isError: true }
  }
}

function summary(item: Item): string {
  const assignee = item.assignedTo ? ` → ${item.assignedTo}` : ''
  const parent = item.parentNumber !== null ? ` (subtask of #${item.parentNumber})` : ''
  const subtasks = item.subtaskCount > 0 ? ` [subtasks ${item.subtaskDoneCount}/${item.subtaskCount} done]` : ''
  return `#${item.itemNumber} [${item.type} | ${item.status} | ${item.priority}] ${item.title}${assignee}${parent}${subtasks}`
}

const projectArg = z.string().min(1).describe('Project name (case-insensitive) or id')
const numberArg = z.number().int().positive().describe('Item number within the project, e.g. 12 for #12')

server.registerTool(
  'buggy_list_projects',
  {
    title: 'List Buggy projects',
    description: 'Lists the active (non-archived) projects in Buggy with their item counts.',
    inputSchema: {},
    annotations: { readOnlyHint: true }
  },
  () =>
    run(async () => {
      const projects = (await client.listProjects()).filter(p => !p.isArchived)
      if (projects.length === 0) return 'There are no active projects.'
      return projects.map(p => `${p.name} (${p.itemCounter} items created) id=${p.id}`).join('\n')
    })
)

server.registerTool(
  'buggy_list_members',
  {
    title: 'List Buggy users',
    description: 'Lists the display names of Buggy users. Items can only be assigned to these names.',
    inputSchema: {},
    annotations: { readOnlyHint: true }
  },
  () => run(async () => (await client.listMembers()).map(m => m.displayName).join('\n') || 'No users.')
)

server.registerTool(
  'buggy_list_items',
  {
    title: 'List items in a Buggy project',
    description:
      'Lists the non-archived items (bugs, features, tasks) in a project, one line each: ' +
      'number, type, status, priority, title and assignee. Optional filters narrow the list.',
    inputSchema: {
      project: projectArg,
      status: z.enum(STATUSES).optional().describe('Only items with this status'),
      type: z.enum(ITEM_TYPES).optional().describe('Only items of this type'),
      assignedTo: z.string().optional().describe('Only items assigned to this user (case-insensitive)')
    },
    annotations: { readOnlyHint: true }
  },
  ({ project, status, type, assignedTo }) =>
    run(async () => {
      const p = await client.resolveProject(project)
      const items = (await client.listItems(p.id))
        .filter(i => !status || i.status === status)
        .filter(i => !type || i.type === type)
        .filter(i => !assignedTo || i.assignedTo?.toLowerCase() === assignedTo.toLowerCase())
        .sort((a, b) => a.itemNumber - b.itemNumber)
      if (items.length === 0) return `No matching items in "${p.name}".`
      return `${items.length} item(s) in "${p.name}":\n` + items.map(summary).join('\n')
    })
)

server.registerTool(
  'buggy_get_item',
  {
    title: 'Get a Buggy item',
    description: 'Gets one item with its full description, tags, parent, subtasks and comments.',
    inputSchema: { project: projectArg, number: numberArg },
    annotations: { readOnlyHint: true }
  },
  ({ project, number }) =>
    run(async () => {
      const p = await client.resolveProject(project)
      const item = await client.getItem(p.id, number)
      const comments = item.commentCount > 0 ? await client.listComments(item.id) : []
      const subtasks = item.subtaskCount > 0 ? await client.listSubtasks(p.id, item.itemNumber) : []
      const lines = [
        summary(item),
        `Project: ${p.name}`,
        `Created by ${item.createdBy} on ${item.createdDate}` +
          (item.updatedDate ? `, updated ${item.updatedDate}` : '')
      ]
      if (item.parentNumber !== null) lines.push(`Subtask of #${item.parentNumber} "${item.parentTitle}"`)
      if (item.tags.length) lines.push(`Tags: ${item.tags.join(', ')}`)
      if (item.attachmentCount) lines.push(`Attachments: ${item.attachmentCount} (view in Buggy)`)
      lines.push(`Link: ${client.itemUrl(item)}`, '', 'Description:', item.description?.trim() || '(none)')
      if (subtasks.length) {
        lines.push('', `Subtasks (${item.subtaskDoneCount}/${item.subtaskCount} done):`)
        for (const s of subtasks) lines.push(`- [${s.status === 'Done' ? 'x' : ' '}] ${summary(s)}`)
      }
      if (comments.length) {
        lines.push('', `Comments (${comments.length}):`)
        for (const c of comments) lines.push(`- ${c.createdBy}, ${c.createdDate}: ${c.text}`)
      }
      return lines.join('\n')
    })
)

server.registerTool(
  'buggy_create_item',
  {
    title: 'Create a Buggy item',
    description:
      'Creates a bug, feature or task in a project and returns its number and link. ' +
      'Give parent to create it as a subtask of that item (subtasks are one level deep). ' +
      'assignedTo must be an existing Buggy user (see buggy_list_members); never invent one.',
    inputSchema: {
      project: projectArg,
      title: z.string().min(1).max(200).describe('Short title'),
      description: z.string().max(4000).optional().describe('Details; plain text, line breaks are kept'),
      type: z.enum(ITEM_TYPES).default('Task'),
      priority: z.enum(PRIORITIES).default('None'),
      status: z.enum(STATUSES).default('Backlog').describe('Board column to put the item in'),
      assignedTo: z.string().optional().describe('Display name of an existing Buggy user'),
      tags: z.array(z.string().min(1).max(50)).max(20).optional(),
      parent: numberArg.optional().describe('Create as a subtask of this item number, e.g. 12 for #12')
    },
    annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false }
  },
  ({ project, title, description, type, priority, status, assignedTo, tags, parent }) =>
    run(async () => {
      const p = await client.resolveProject(project)
      const assignee = assignedTo ? await client.resolveMember(assignedTo) : undefined
      const item = await client.createItem(p.id, {
        type,
        title,
        description,
        priority,
        status,
        assignedTo: assignee,
        tags,
        parentNumber: parent
      })
      return `Created ${summary(item)} in "${p.name}".\nLink: ${client.itemUrl(item)}`
    })
)

server.registerTool(
  'buggy_add_comment',
  {
    title: 'Comment on a Buggy item',
    description: 'Adds a comment to an item.',
    inputSchema: {
      project: projectArg,
      number: numberArg,
      text: z.string().min(1).max(4000).describe('Comment text')
    },
    annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false }
  },
  ({ project, number, text }) =>
    run(async () => {
      const p = await client.resolveProject(project)
      const item = await client.getItem(p.id, number)
      await client.addComment(item.id, text)
      return `Comment added to #${item.itemNumber} "${item.title}".\nLink: ${client.itemUrl(item)}`
    })
)

server.registerTool(
  'buggy_set_status',
  {
    title: 'Set the status of a Buggy item',
    description:
      'Moves an item (or subtask) to another board column. Use status Done to tick off a subtask.',
    inputSchema: {
      project: projectArg,
      number: numberArg,
      status: z.enum(STATUSES).describe('New status')
    },
    annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true }
  },
  ({ project, number, status }) =>
    run(async () => {
      const p = await client.resolveProject(project)
      const item = await client.setStatus(p.id, number, status)
      return `Updated ${summary(item)}.\nLink: ${client.itemUrl(item)}`
    })
)

server.registerTool(
  'buggy_set_parent',
  {
    title: 'Make a Buggy item a subtask, or detach it',
    description:
      'Makes an existing item a subtask of another item, or a normal item again when parent is null. ' +
      'Subtasks are one level deep: the parent cannot itself be a subtask, and an item with subtasks cannot become one.',
    inputSchema: {
      project: projectArg,
      number: numberArg,
      parent: z
        .number()
        .int()
        .positive()
        .nullable()
        .describe('Item number of the new parent, or null to detach')
    },
    annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true }
  },
  ({ project, number, parent }) =>
    run(async () => {
      const p = await client.resolveProject(project)
      const item = await client.setParent(p.id, number, parent)
      return `Updated ${summary(item)}.\nLink: ${client.itemUrl(item)}`
    })
)

await server.connect(new StdioServerTransport())
