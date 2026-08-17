import type { HubCommunityPlugin, HubCommunityRegistry, HubGitHubRepository } from './bridge.ts'

export type CommunitySort = 'recommended' | 'stars' | 'newest' | 'name'
export type CommunityTimeRange = 'all' | 'week' | 'month' | 'quarter' | 'year'

const RANGE_DAYS: Readonly<Record<Exclude<CommunityTimeRange, 'all'>, number>> = {
  week: 7,
  month: 30,
  quarter: 90,
  year: 365,
}

export function visibleCommunityPlugins(
  registry: HubCommunityRegistry,
  options: {
    readonly category: string
    readonly language: 'zh' | 'en'
    readonly query: string
    readonly sort: CommunitySort
    readonly timeRange: CommunityTimeRange
  },
): HubCommunityPlugin[] {
  const query = options.query.trim().toLocaleLowerCase()
  const cutoff = options.timeRange === 'all'
    ? undefined
    : Date.now() - RANGE_DAYS[options.timeRange] * 86_400_000
  const plugins = registry.plugins
  const filtered = plugins.filter((plugin) => {
    if (options.category !== 'all' && plugin.category !== options.category) return false
    if (cutoff !== undefined) {
      const added = typeof plugin.added === 'string' ? Date.parse(plugin.added) : Number.NaN
      if (!Number.isFinite(added) || added < cutoff) return false
    }
    if (query.length === 0) return true
    const description = plugin.description?.[options.language] ?? plugin.description?.en ?? ''
    return [plugin.name, plugin.owner, plugin.category, description, plugin.npm ?? '']
      .some(value => value.toLocaleLowerCase().includes(query))
  })
  return [...filtered].sort((left, right) => {
    if (options.sort === 'stars') return (right.stars ?? -1) - (left.stars ?? -1) || left.name.localeCompare(right.name)
    if (options.sort === 'newest') return (right.added ?? '').localeCompare(left.added ?? '') || left.name.localeCompare(right.name)
    if (options.sort === 'name') return left.name.localeCompare(right.name)
    const leftScore = (left.stars ?? 0) + recencyScore(left.added)
    const rightScore = (right.stars ?? 0) + recencyScore(right.added)
    return rightScore - leftScore || left.name.localeCompare(right.name)
  })
}

export function communityCategoryCounts(registry: HubCommunityRegistry): ReadonlyMap<string, number> {
  const counts = new Map<string, number>()
  for (const plugin of registry.plugins) counts.set(plugin.category, (counts.get(plugin.category) ?? 0) + 1)
  return counts
}

export function communityPageItems(current: number, total: number): readonly (number | '…')[] {
  if (total <= 7) return Array.from({ length: total }, (_, index) => index + 1)
  const items: Array<number | '…'> = [1]
  let start = Math.max(2, current - 1)
  let end = Math.min(total - 1, current + 1)
  if (current <= 4) end = 5
  if (current >= total - 3) start = total - 4
  if (start > 2) items.push('…')
  for (let page = start; page <= end; page++) items.push(page)
  if (end < total - 1) items.push('…')
  items.push(total)
  return items
}

export function communitySupportsOneClick(plugin: HubCommunityPlugin): boolean {
  return typeof plugin.npm === 'string' && plugin.npm.trim().length > 0
}

export function communityRepository(plugin: HubCommunityPlugin): HubGitHubRepository {
  const fullName = `${plugin.owner}/${plugin.name}`
  return {
    archived: false,
    defaultBranch: 'HEAD',
    description: plugin.description?.zh ?? plugin.description?.en ?? '',
    disabled: false,
    fork: false,
    fullName,
    name: plugin.name,
    owner: plugin.owner,
    private: false,
    repositoryUrl: plugin.url,
    stars: plugin.stars ?? 0,
    topics: [plugin.category, 'dsh-plugin'],
    ...(typeof plugin.added === 'string' ? { updatedAt: plugin.added } : {}),
  }
}

function recencyScore(value: string | null | undefined): number {
  if (typeof value !== 'string') return 0
  const time = Date.parse(value)
  if (!Number.isFinite(time)) return 0
  const ageDays = Math.max(0, (Date.now() - time) / 86_400_000)
  return Math.max(0, 80 - ageDays) * 4
}
