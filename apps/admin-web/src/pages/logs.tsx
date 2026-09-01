import { useQuery } from '@tanstack/react-query'
import { logsApi } from '@/lib/api'
import { useState, useEffect, useRef } from 'react'
import { Search, RefreshCw, ChevronDown, ChevronRight, AlertCircle, AlertTriangle, Info, Bug } from 'lucide-react'

const levelConfig: Record<string, { icon: typeof Info; color: string; bg: string; ring: string }> = {
  Verbose:     { icon: Bug,            color: 'text-slate-500',  bg: 'bg-slate-500/10',  ring: 'ring-slate-500/30' },
  Debug:       { icon: Bug,            color: 'text-slate-400',  bg: 'bg-slate-500/10',  ring: 'ring-slate-500/30' },
  Information: { icon: Info,           color: 'text-blue-400',   bg: 'bg-blue-500/10',   ring: 'ring-blue-500/30' },
  Warning:     { icon: AlertTriangle,  color: 'text-amber-400',  bg: 'bg-amber-500/10',  ring: 'ring-amber-500/30' },
  Error:       { icon: AlertCircle,    color: 'text-red-400',    bg: 'bg-red-500/10',    ring: 'ring-red-500/30' },
  Fatal:       { icon: AlertCircle,    color: 'text-red-500',    bg: 'bg-red-500/20',    ring: 'ring-red-500/40' },
}

const timeRanges = [
  { label: 'Last 15 min', value: 15 },
  { label: 'Last hour', value: 60 },
  { label: 'Last 6 hours', value: 360 },
  { label: 'Last 24 hours', value: 1440 },
  { label: 'All', value: undefined },
]

export function LogsPage() {
  const [level, setLevel] = useState<string>('')
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [timeRange, setTimeRange] = useState<number | undefined>(undefined)
  const [autoRefresh, setAutoRefresh] = useState(false)
  const [expandedIdx, setExpandedIdx] = useState<number | null>(null)
  const [source, setSource] = useState<'server' | 'audit'>('server')
  const bottomRef = useRef<HTMLDivElement>(null)

  // Debounce search
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), 300)
    return () => clearTimeout(timer)
  }, [search])

  const { data, isLoading, error, refetch, isFetching } = useQuery({
    queryKey: ['logs', level, debouncedSearch, timeRange, source],
    queryFn: () => logsApi.list({
      level: level || undefined,
      search: debouncedSearch || undefined,
      limit: 300,
      lastMinutes: timeRange,
      source: source === 'audit' ? 'audit' : undefined,
    }),
    refetchInterval: autoRefresh ? 5000 : false,
  })

  const levels = ['', 'Information', 'Warning', 'Error', 'Fatal']

  return (
    <div>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-white">Logs</h1>
          <p className="mt-1 text-sm text-slate-400">
            {source === 'audit' ? 'User action audit trail' : 'API server logs'} — {data?.totalMatched ?? 0} entries
          </p>
        </div>
        <div className="flex items-center gap-2">
          <label className="flex items-center gap-2 text-sm text-slate-400">
            <input
              type="checkbox"
              checked={autoRefresh}
              onChange={(e) => setAutoRefresh(e.target.checked)}
              className="h-4 w-4 rounded border-surface-600 bg-surface-800 text-accent-500"
            />
            Auto-refresh
          </label>
          <button
            onClick={() => refetch()}
            disabled={isFetching}
            className="flex items-center gap-1.5 rounded-lg border border-surface-700 px-3 py-1.5 text-sm text-slate-300 transition-colors hover:bg-surface-800"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${isFetching ? 'animate-spin' : ''}`} />
            Refresh
          </button>
        </div>
      </div>

      {/* Source tabs */}
      <div className="mt-4 flex gap-1 border-b border-surface-800 pb-0">
        {([
          { key: 'server' as const, label: 'Server Logs' },
          { key: 'audit' as const, label: 'User Actions' },
        ]).map((tab) => (
          <button
            key={tab.key}
            onClick={() => setSource(tab.key)}
            className={`px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px ${
              source === tab.key
                ? 'border-accent-500 text-accent-400'
                : 'border-transparent text-slate-400 hover:text-white'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Filters */}
      <div className="mt-4 flex flex-wrap items-center gap-3">
        {/* Search */}
        <div className="relative flex-1 min-w-[200px] max-w-md">
          <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-500" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search logs…"
            className="w-full rounded-lg border border-surface-700 bg-surface-850 py-2 pl-9 pr-3 text-sm text-white placeholder-slate-500 outline-none focus:border-accent-500"
          />
        </div>

        {/* Level filter */}
        <div className="flex gap-1">
          {levels.map((l) => (
            <button
              key={l || 'all'}
              onClick={() => setLevel(l)}
              className={`rounded-md px-2.5 py-1.5 text-xs font-medium transition-colors ${
                level === l
                  ? l === '' ? 'bg-accent-500/20 text-accent-300'
                    : `${levelConfig[l]?.bg} ${levelConfig[l]?.color} ring-1 ${levelConfig[l]?.ring}`
                  : 'text-slate-400 hover:bg-surface-800 hover:text-white'
              }`}
            >
              {l || 'All'}
            </button>
          ))}
        </div>

        {/* Time range */}
        <select
          value={timeRange ?? ''}
          onChange={(e) => setTimeRange(e.target.value ? Number(e.target.value) : undefined)}
          className="rounded-lg border border-surface-700 bg-surface-850 px-3 py-1.5 text-sm text-slate-300 outline-none focus:border-accent-500"
        >
          {timeRanges.map((r) => (
            <option key={r.label} value={r.value ?? ''}>{r.label}</option>
          ))}
        </select>
      </div>

      {/* Log entries */}
      {error && (
        <div className="mt-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-300">
          Failed to load logs: {error.message}
        </div>
      )}
      <div className="mt-4 overflow-hidden rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
        {isLoading ? (
          <div className="flex h-64 items-center justify-center">
            <div className="h-8 w-8 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
          </div>
        ) : !data?.entries.length ? (
          <div className="py-12 text-center text-sm text-slate-500">No log entries found.</div>
        ) : (
          <div className="max-h-[70vh] overflow-y-auto font-mono text-sm">
            {data.entries.map((entry, i) => {
              const config = levelConfig[entry.level] ?? levelConfig.Information
              const LevelIcon = config.icon
              const isExpanded = expandedIdx === i
              const hasDetail = entry.exception || entry.correlationId || entry.requestPath

              return (
                <div key={i}>
                  <button
                    onClick={() => hasDetail ? setExpandedIdx(isExpanded ? null : i) : undefined}
                    className={`flex w-full items-start gap-3 border-b border-surface-800/50 px-4 py-2 text-left transition-colors ${
                      hasDetail ? 'cursor-pointer hover:bg-surface-850' : 'cursor-default'
                    } ${entry.level === 'Error' || entry.level === 'Fatal' ? 'bg-red-500/5' : ''}`}
                  >
                    {/* Level icon */}
                    <span className={`mt-0.5 shrink-0 rounded p-0.5 ${config.bg}`}>
                      <LevelIcon className={`h-3.5 w-3.5 ${config.color}`} />
                    </span>

                    {/* Timestamp */}
                    <span className="shrink-0 text-xs text-slate-500 leading-5">
                      {formatTime(entry.timestamp)}
                    </span>

                    {/* Level badge */}
                    <span className={`shrink-0 rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase leading-3 ${config.bg} ${config.color}`}>
                      {entry.level.slice(0, 4)}
                    </span>

                    {/* Message */}
                    <span className="min-w-0 flex-1 truncate text-slate-300 leading-5">
                      {entry.message}
                    </span>

                    {/* Status code / elapsed for HTTP requests */}
                    {entry.statusCode && (
                      <span className={`shrink-0 rounded px-1.5 py-0.5 text-xs font-medium ${
                        entry.statusCode >= 500 ? 'text-red-400' :
                        entry.statusCode >= 400 ? 'text-amber-400' : 'text-emerald-400'
                      }`}>
                        {entry.statusCode}
                      </span>
                    )}
                    {entry.elapsed != null && (
                      <span className="shrink-0 text-xs text-slate-500">{entry.elapsed.toFixed(0)}ms</span>
                    )}

                    {hasDetail && (
                      isExpanded
                        ? <ChevronDown className="h-3.5 w-3.5 shrink-0 text-slate-500 mt-1" />
                        : <ChevronRight className="h-3.5 w-3.5 shrink-0 text-slate-500 mt-1" />
                    )}
                  </button>

                  {/* Expanded detail */}
                  {isExpanded && (
                    <div className="border-b border-surface-800 bg-surface-850 px-4 py-3 pl-16 space-y-2">
                      {entry.correlationId && (
                        <div className="text-xs">
                          <span className="text-slate-500">Correlation ID:</span>{' '}
                          <span className="text-slate-300">{entry.correlationId}</span>
                        </div>
                      )}
                      {entry.requestPath && (
                        <div className="text-xs">
                          <span className="text-slate-500">Path:</span>{' '}
                          <span className="text-slate-300">{entry.requestPath}</span>
                        </div>
                      )}
                      {entry.source && (
                        <div className="text-xs">
                          <span className="text-slate-500">Source:</span>{' '}
                          <span className="text-slate-300">{entry.source}</span>
                        </div>
                      )}
                      {entry.exception && (
                        <pre className="mt-2 max-h-48 overflow-y-auto rounded-lg bg-surface-900 p-3 text-xs text-red-300 whitespace-pre-wrap">
                          {entry.exception}
                        </pre>
                      )}
                    </div>
                  )}
                </div>
              )
            })}
            <div ref={bottomRef} />
          </div>
        )}
      </div>
    </div>
  )
}

function formatTime(iso: string): string {
  try {
    const d = new Date(iso)
    return d.toLocaleTimeString('en-US', { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' })
  } catch {
    return iso.slice(11, 19)
  }
}
