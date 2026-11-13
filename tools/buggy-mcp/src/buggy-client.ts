// Thin client for the Buggy REST API, authenticated with an X-Api-Key.

export const ITEM_TYPES = ['Bug', 'Feature', 'Task'] as const
export const PRIORITIES = ['None', 'Low', 'Medium', 'High', 'Critical'] as const
export const STATUSES = ['Backlog', 'ToDo', 'InProgress', 'InReview', 'Done'] as const

export type ItemType = (typeof ITEM_TYPES)[number]
export type Priority = (typeof PRIORITIES)[number]
export type Status = (typeof STATUSES)[number]

export interface Project {
  id: string
  name: string
  itemCounter: number
  createdDate: string
  isArchived: boolean
}

export interface Item {
  id: string
  projectId: string
  itemNumber: number
  type: ItemType
  title: string
  description: string | null
  priority: Priority
  status: Status
  assignedTo: string | null
  createdBy: string
  createdDate: string
  updatedDate: string | null
  isArchived: boolean
  tags: string[]
  commentCount: number
  attachmentCount: number
  /** Set when the item is a subtask (one level deep). */
  parentId: string | null
  parentNumber: number | null
  parentTitle: string | null
  sortOrder: number
  /** Non-archived subtasks, and how many of them are Done. */
  subtaskCount: number
  subtaskDoneCount: number
}

export interface Comment {
  id: string
  text: string
  createdBy: string
  createdDate: string
}

export interface NewItem {
  type: ItemType
  title: string
  description?: string
  priority: Priority
  status: Status
  assignedTo?: string
  tags?: string[]
  /** Create the item as a subtask of this item number. */
  parentNumber?: number
}

/** An error message meant to be shown to the model as-is. */
export class BuggyError extends Error {}

export class BuggyClient {
  constructor(
    readonly baseUrl: string,
    private readonly apiKey: string
  ) {}

  listProjects = () => this.request<Project[]>('GET', '/api/projects')

  listItems = (projectId: string) => this.request<Item[]>('GET', `/api/projects/${projectId}/items`)

  getItem = (projectId: string, itemNumber: number) =>
    this.request<Item>('GET', `/api/projects/${projectId}/items/${itemNumber}`, undefined,
      `There is no item #${itemNumber} in this project (it may be archived).`)

  createItem = (projectId: string, item: NewItem) =>
    this.request<Item>('POST', `/api/projects/${projectId}/items`, item)

  listSubtasks = (projectId: string, itemNumber: number) =>
    this.request<Item[]>('GET', `/api/projects/${projectId}/items/${itemNumber}/subtasks`)

  setStatus = (projectId: string, itemNumber: number, status: Status) =>
    this.request<Item>('PUT', `/api/projects/${projectId}/items/${itemNumber}/status`, { status },
      `There is no item #${itemNumber} in this project.`)

  /** Makes the item a subtask of parentNumber, or a normal item when parentNumber is null. */
  setParent = (projectId: string, itemNumber: number, parentNumber: number | null) =>
    this.request<Item>('PUT', `/api/projects/${projectId}/items/${itemNumber}/parent`, { parentNumber },
      `There is no item #${itemNumber} in this project.`)

  listComments = (itemId: string) => this.request<Comment[]>('GET', `/api/items/${itemId}/comments`)

  addComment = (itemId: string, text: string) =>
    this.request<Comment>('POST', `/api/items/${itemId}/comments`, { text })

  listMembers = () => this.request<{ displayName: string }[]>('GET', '/api/members')

  itemUrl = (item: Pick<Item, 'projectId' | 'itemNumber'>) =>
    `${this.baseUrl}/projects/${item.projectId}/items/${item.itemNumber}`

  /** Finds a non-archived project by name (case-insensitive) or id. */
  async resolveProject(nameOrId: string): Promise<Project> {
    const projects = (await this.listProjects()).filter(p => !p.isArchived)
    const wanted = nameOrId.trim().toLowerCase()
    const match = projects.find(p => p.id === wanted || p.name.toLowerCase() === wanted)
    if (!match) {
      const names = projects.map(p => `"${p.name}"`).join(', ') || '(none)'
      throw new BuggyError(`No project named "${nameOrId}". Available projects: ${names}.`)
    }
    return match
  }

  /** Returns the member's exact display name (case-insensitive match), or throws. */
  async resolveMember(name: string): Promise<string> {
    const members = (await this.listMembers()).map(m => m.displayName)
    const match = members.find(m => m.toLowerCase() === name.trim().toLowerCase())
    if (!match) {
      throw new BuggyError(
        `"${name}" is not a Buggy user. Users: ${members.map(m => `"${m}"`).join(', ')}. ` +
          'Ask the user rather than guessing; an admin can add users under Manage users.'
      )
    }
    return match
  }

  private async request<T>(method: string, path: string, body?: unknown, notFoundMessage?: string): Promise<T> {
    let response: Response
    try {
      response = await fetch(this.baseUrl + path, {
        method,
        headers: {
          'X-Api-Key': this.apiKey,
          Accept: 'application/json',
          ...(body !== undefined ? { 'Content-Type': 'application/json' } : {})
        },
        body: body !== undefined ? JSON.stringify(body) : undefined,
        signal: AbortSignal.timeout(20_000)
      })
    } catch (err) {
      throw new BuggyError(`Could not reach Buggy at ${this.baseUrl}: ${(err as Error).message}`)
    }

    if (response.status === 401)
      throw new BuggyError('Buggy rejected the API key (401). Check BUGGY_API_KEY, or whether the key was revoked.')
    if (response.status === 404) throw new BuggyError(notFoundMessage ?? `Not found: ${method} ${path}`)
    if (!response.ok) throw new BuggyError(`Buggy returned ${response.status}: ${await problemText(response)}`)

    return (await response.json()) as T
  }
}

async function problemText(response: Response): Promise<string> {
  const text = await response.text()
  try {
    const problem = JSON.parse(text) as { title?: string; errors?: Record<string, string[]> }
    const details = problem.errors ? Object.values(problem.errors).flat().join(' ') : ''
    return [problem.title, details].filter(Boolean).join(' ') || text
  } catch {
    return text || response.statusText
  }
}
