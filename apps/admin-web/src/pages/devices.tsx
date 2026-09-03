import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { devicesApi, enrollmentApi, commandsApi, groupsApi } from '@/lib/api'
import type { DeviceDto, DeviceListResponse } from '@/lib/api'
import { useState, useRef, useMemo } from 'react'
import { Plus, Pencil, Trash2, KeyRound, Copy, Check, Send, Upload, Download, FileSpreadsheet, ArrowUpDown, ArrowUp, ArrowDown, Search } from 'lucide-react'

type SortField = 'name' | 'group' | 'hostname' | 'ipAddress' | 'status' | 'location' | 'lastSeen'
type SortDir = 'asc' | 'desc'

export function DevicesPage() {
  const queryClient = useQueryClient()
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [isEnrollOpen, setIsEnrollOpen] = useState(false)
  const [isBulkModalOpen, setIsBulkModalOpen] = useState(false)
  const [isImportOpen, setIsImportOpen] = useState(false)
  const [commandDevice, setCommandDevice] = useState<DeviceDto | null>(null)
  const [editingDevice, setEditingDevice] = useState<DeviceDto | null>(null)
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [groupFilter, setGroupFilter] = useState('')
  const [search, setSearch] = useState('')
  const [sortField, setSortField] = useState<SortField>('name')
  const [sortDir, setSortDir] = useState<SortDir>('asc')

  const { data, isLoading, error } = useQuery({
  queryKey: ['devices'],
  queryFn: () => devicesApi.list({ pageSize: 500 }),
  staleTime: 30_000,
  })

  const { data: groups } = useQuery({
  queryKey: ['deviceGroups'],
  queryFn: groupsApi.list,
  staleTime: 30_000,
  })

  const toggleSort = (field: SortField) => {
    if (sortField === field) {
      setSortDir(sortDir === 'asc' ? 'desc' : 'asc')
    } else {
      setSortField(field)
      setSortDir('asc')
    }
  }

  const sortedDevices = useMemo(() => {
    const q = search.toLowerCase().trim()
    const filtered = data?.devices.filter(d => {
      if (groupFilter && d.groupId !== groupFilter) return false
      if (q && !(
        d.name.toLowerCase().includes(q) ||
        (d.hostname ?? '').toLowerCase().includes(q) ||
        (d.ipAddress ?? '').includes(q) ||
        (d.location ?? '').toLowerCase().includes(q) ||
        (d.serialNumber ?? '').toLowerCase().includes(q) ||
        (d.groupName ?? '').toLowerCase().includes(q) ||
        d.status.toLowerCase().includes(q)
      )) return false
      return true
    }) ?? []

    return [...filtered].sort((a, b) => {
      let cmp = 0
      switch (sortField) {
        case 'name': cmp = a.name.localeCompare(b.name); break
        case 'group': cmp = (a.groupName ?? '').localeCompare(b.groupName ?? ''); break
        case 'hostname': cmp = (a.hostname ?? '').localeCompare(b.hostname ?? ''); break
        case 'ipAddress': cmp = (a.ipAddress ?? '').localeCompare(b.ipAddress ?? ''); break
        case 'status': cmp = a.status.localeCompare(b.status); break
        case 'location': cmp = (a.location ?? '').localeCompare(b.location ?? ''); break
        case 'lastSeen': {
          const aTime = a.lastSeenAt ? new Date(a.lastSeenAt).getTime() : 0
          const bTime = b.lastSeenAt ? new Date(b.lastSeenAt).getTime() : 0
          cmp = aTime - bTime
          break
        }
      }
      return sortDir === 'asc' ? cmp : -cmp
    })
  }, [data, groupFilter, search, sortField, sortDir])

  const filteredDevices = sortedDevices

  const deleteMutation = useMutation({
    mutationFn: devicesApi.delete,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['devices'] })
    },
  })

  const handleDelete = (id: string) => {
    if (confirm('Are you sure you want to delete this device?')) {
      deleteMutation.mutate(id)
    }
  }

  return (
    <div>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-white">Devices</h1>
          <p className="mt-1 text-sm text-slate-400">Manage your kiosk fleet</p>
        </div>
        <div className="flex items-center gap-2">
          {selectedIds.size > 0 && (
            <button
              onClick={() => setIsBulkModalOpen(true)}
              className="flex items-center gap-2 rounded-lg border border-accent-500/50 bg-accent-500/10 px-4 py-2 text-sm font-medium text-accent-400 transition-colors hover:bg-accent-500/20"
            >
              <Send className="h-4 w-4" />
              Bulk ({selectedIds.size})
            </button>
          )}
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
            <input
              type="text"
              placeholder="Search devices..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-56 rounded-lg border border-surface-700 bg-surface-850 py-2 pl-9 pr-3 text-sm text-white placeholder-slate-500 outline-none focus:border-accent-500 focus:ring-1 focus:ring-accent-500"
            />
          </div>
          <select
            value={groupFilter}
            onChange={(e) => setGroupFilter(e.target.value)}
            className="rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500"
          >
            <option value="">All groups</option>
            {groups?.map((g) => (
              <option key={g.id} value={g.id}>{g.name}</option>
            ))}
          </select>
          <button
            onClick={() => setIsImportOpen(true)}
            className="flex items-center gap-2 rounded-lg border border-surface-700 px-4 py-2 text-sm font-medium text-slate-200 transition-colors hover:bg-surface-800"
          >
            <Upload className="h-4 w-4" />
            Import
          </button>
          <button
            onClick={() => setIsEnrollOpen(true)}
            className="flex items-center gap-2 rounded-lg border border-surface-700 px-4 py-2 text-sm font-medium text-slate-200 transition-colors hover:bg-surface-800"
          >
            <KeyRound className="h-4 w-4" />
            Enroll Device
          </button>
          <button
            onClick={() => {
              setEditingDevice(null)
              setIsModalOpen(true)
            }}
            className="flex items-center gap-2 rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400"
          >
            <Plus className="h-4 w-4" />
            Add Device
          </button>
        </div>
        </div>

      {isLoading ? (
        <div className="flex h-64 items-center justify-center">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
        </div>
      ) : error ? (
        <div className="mt-6 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Error loading devices: {error.message}
        </div>
      ) : (
        <div className="mt-6 overflow-x-auto rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
          <table className="min-w-full divide-y divide-surface-800">
            <thead>
              <tr className="bg-surface-850">
                <th className="px-4 py-3 text-left">
                  <input
                    type="checkbox"
                    checked={filteredDevices.length > 0 && filteredDevices.every(d => selectedIds.has(d.id))}
                    onChange={(e) => {
                      if (e.target.checked) {
                        setSelectedIds(new Set(filteredDevices.map(d => d.id)))
                      } else {
                        setSelectedIds(new Set())
                      }
                    }}
                    className="h-4 w-4 rounded border-surface-600 bg-surface-800 text-accent-500 focus:ring-accent-500"
                  />
                </th>
                {([
                  { field: 'status' as SortField, label: 'Status' },
                  { field: 'name' as SortField, label: 'Name' },
                  { field: 'group' as SortField, label: 'Group' },
                  { field: 'hostname' as SortField, label: 'Hostname' },
                  { field: 'ipAddress' as SortField, label: 'IP' },
                  { field: null, label: 'Domain' },
                  { field: null, label: 'Metrics' },
                  { field: 'location' as SortField, label: 'Location' },
                  { field: 'lastSeen' as SortField, label: 'Last Seen' },
                  { field: null, label: '' },
                ]).map(({ field, label }) => (
                  <th
                    key={label}
                    className={`px-6 py-3 text-left text-xs font-semibold uppercase tracking-wider last:text-right ${
                      field ? 'cursor-pointer select-none hover:text-slate-300' : ''
                    } ${sortField === field ? 'text-accent-400' : 'text-slate-500'}`}
                    onClick={field ? () => toggleSort(field) : undefined}
                  >
                    <span className="inline-flex items-center gap-1">
                      {label}
                      {field && sortField === field ? (
                        sortDir === 'asc' ? <ArrowUp className="h-3 w-3" /> : <ArrowDown className="h-3 w-3" />
                      ) : field ? (
                        <ArrowUpDown className="h-3 w-3 opacity-30" />
                      ) : null}
                    </span>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-surface-800">
              {filteredDevices.map((device) => (
                <tr key={device.id} className="transition-colors hover:bg-surface-850">
                  <td className="px-4 py-4 whitespace-nowrap">
                    <input
                      type="checkbox"
                      checked={selectedIds.has(device.id)}
                      onChange={(e) => {
                        const next = new Set(selectedIds)
                        if (e.target.checked) next.add(device.id)
                        else next.delete(device.id)
                        setSelectedIds(next)
                      }}
                      className="h-4 w-4 rounded border-surface-600 bg-surface-800 text-accent-500 focus:ring-accent-500"
                    />
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                    <StatusBadge status={device.status} />
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                    <Link to={`/devices/${device.id}`} className="text-sm font-medium text-slate-100 hover:text-accent-300 transition-colors">
                      {device.name}
                    </Link>
                    {device.description && (
                      <div className="text-xs text-slate-500">{device.description}</div>
                    )}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-400">
                    {device.groupName || '—'}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap font-mono text-sm text-slate-400">
                    {device.hostname || '—'}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap font-mono text-sm text-slate-400">
                      {device.ipAddress || '—'}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <DomainBadge device={device} />
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <DeviceMetrics device={device} />
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-400">
                    {device.location || '—'}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-400">
                    {device.lastSeenAt ? new Date(device.lastSeenAt).toLocaleString() : 'Never'}
                    </td>
                  <td className="px-6 py-4 whitespace-nowrap text-right">
                    <button
                      onClick={() => setCommandDevice(device)}
                      className="mr-2 inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-surface-800 hover:text-white"
                    >
                      <Send className="h-3.5 w-3.5" />
                      Command
                    </button>
                    <button
                      onClick={() => {
                        setEditingDevice(device)
                        setIsModalOpen(true)
                      }}
                      className="mr-2 inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-surface-800 hover:text-white"
                    >
                      <Pencil className="h-3.5 w-3.5" />
                      Edit
                    </button>
                    <button
                      onClick={() => handleDelete(device.id)}
                      className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-red-500/10 hover:text-red-400"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {data?.devices.length === 0 && (
            <div className="py-12 text-center text-sm text-slate-500">
              No devices found. Add your first device to get started.
            </div>
          )}
        </div>
      )}

      {isModalOpen && (
        <DeviceModal device={editingDevice} onClose={() => setIsModalOpen(false)} />
      )}
      {isEnrollOpen && (
        <EnrollTokenModal devices={data} onClose={() => setIsEnrollOpen(false)} />
      )}
      {isImportOpen && (
        <ImportModal onClose={() => setIsImportOpen(false)} />
      )}
      {commandDevice && (
        <CommandModal device={commandDevice} onClose={() => setCommandDevice(null)} />
      )}
      {isBulkModalOpen && (
        <BulkModal
          deviceIds={[...selectedIds]}
          groups={groups ?? []}
          onClose={() => { setIsBulkModalOpen(false); setSelectedIds(new Set()) }}
        />
      )}
      </div>
      )
      }

      const COMMAND_TYPES = [
      'RefreshKiosk', 'RestartKioskRuntime', 'ClearBrowserSession', 'ReloadPolicy',
      'SynchronizeContent', 'RebootWindows', 'ShutdownWindows', 'LogOffKioskSession',
      'EnterMaintenanceMode', 'CollectDiagnostics', 'UploadLogs',
      ]

      function CommandStatusBadge({ status }: { status: string }) {
      const styles: Record<string, string> = {
      Queued: 'bg-slate-500/10 text-slate-400 ring-slate-500/30',
      Delivered: 'bg-blue-500/10 text-blue-400 ring-blue-500/30',
      Acknowledged: 'bg-blue-500/10 text-blue-400 ring-blue-500/30',
      Running: 'bg-amber-500/10 text-amber-400 ring-amber-500/30',
      Succeeded: 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30',
      Failed: 'bg-red-500/10 text-red-400 ring-red-500/30',
      Rejected: 'bg-red-500/10 text-red-400 ring-red-500/30',
      Expired: 'bg-slate-500/10 text-slate-400 ring-slate-500/30',
      TimedOut: 'bg-red-500/10 text-red-400 ring-red-500/30',
      Cancelled: 'bg-slate-500/10 text-slate-400 ring-slate-500/30',
      }
      return (
      <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ${styles[status] ?? styles.Queued}`}>
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      {status}
      </span>
      )
      }

      function CommandModal({ device, onClose }: { device: DeviceDto; onClose: () => void }) {
      const queryClient = useQueryClient()
      const [commandType, setCommandType] = useState(COMMAND_TYPES[0])

      const { data: history, refetch } = useQuery({
      queryKey: ['commands', device.id],
      queryFn: () => commandsApi.history({ deviceId: device.id, limit: 10 }),
      refetchInterval: 5000,
      })

      const issueMutation = useMutation({
      mutationFn: () => commandsApi.issue(device.id, { type: commandType }),
      onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['commands', device.id] })
      refetch()
      },
      })

      return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-2xl rounded-2xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">Send Command — {device.name}</h2>
        <p className="mt-1 text-sm text-slate-400">
          Issue an allowlisted remote command. The agent picks it up within ~15 seconds.
        </p>

        <div className="mt-5 flex items-end gap-3">
          <div className="flex-1">
            <label className="block text-sm font-medium text-slate-300">Command</label>
            <select
              value={commandType}
              onChange={(e) => setCommandType(e.target.value)}
              className={inputClass}
            >
              {COMMAND_TYPES.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </select>
          </div>
          <button
            onClick={() => issueMutation.mutate()}
            disabled={issueMutation.isPending}
            className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400 disabled:opacity-50"
          >
            {issueMutation.isPending ? 'Sending…' : 'Send'}
          </button>
        </div>

        {(commandType === 'RebootWindows' || commandType === 'ShutdownWindows') && (
          <p className="mt-2 rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-xs text-amber-300">
            ⚠ This will {commandType === 'RebootWindows' ? 'reboot' : 'shut down'} the device. Confirm you intend to disrupt the kiosk session.
          </p>
        )}

        <div className="mt-6">
          <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500">Recent commands</h3>
          <div className="mt-2 overflow-hidden rounded-lg border border-surface-800">
            <table className="min-w-full divide-y divide-surface-800">
              <thead>
                <tr className="bg-surface-850">
                  {['Type', 'Status', 'Sent', 'Result'].map((h) => (
                    <th key={h} className="px-4 py-2 text-left text-xs font-semibold uppercase tracking-wider text-slate-500">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-surface-800">
                {history?.commands.map((c) => (
                  <tr key={c.id}>
                    <td className="px-4 py-2 text-sm text-slate-200">{c.type}</td>
                    <td className="px-4 py-2"><CommandStatusBadge status={c.status} /></td>
                    <td className="px-4 py-2 text-xs text-slate-400">{new Date(c.createdAt).toLocaleTimeString()}</td>
                    <td className="max-w-[200px] truncate px-4 py-2 text-xs text-slate-500">{c.resultMessage || '—'}</td>
                  </tr>
                ))}
                {history?.commands.length === 0 && (
                  <tr><td colSpan={4} className="px-4 py-6 text-center text-sm text-slate-500">No commands sent yet.</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="mt-6 flex justify-end">
          <button
            onClick={onClose}
            className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 transition-colors hover:bg-surface-800"
          >
            Close
          </button>
        </div>
      </div>
      </div>
      )
      }

      function EnrollTokenModal({ devices: deviceList, onClose }: { devices?: DeviceListResponse; onClose: () => void }) {
      const queryClient = useQueryClient()
      const [label, setLabel] = useState('')
      const [expiresInHours, setExpiresInHours] = useState(24)
      const [selectedDeviceId, setSelectedDeviceId] = useState<string>('')
      const [copied, setCopied] = useState(false)

      const { data: tokens } = useQuery({
      queryKey: ['enrollment-tokens'],
      queryFn: () => enrollmentApi.list(),
      })

      const createMutation = useMutation({
      mutationFn: () => enrollmentApi.create({
        label: label || undefined,
        expiresInHours,
        deviceId: selectedDeviceId || undefined,
      }),
      onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['enrollment-tokens'] })
      },
      })

      const newToken = createMutation.data

      const copyToken = async () => {
      if (newToken) {
      await navigator.clipboard.writeText(newToken.token)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
      }
      }

      return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-2xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">Enroll a Device</h2>
        <p className="mt-1 text-sm text-slate-400">
          Generate a one-time enrollment token, then start the agent with it on the target machine.
        </p>

        {!newToken ? (
          <div className="mt-5 space-y-4">
            <div>
              <label className="block text-sm font-medium text-slate-300">Label (optional)</label>
              <input
                type="text"
                value={label}
                onChange={(e) => setLabel(e.target.value)}
                className={inputClass}
                placeholder="e.g. Lobby kiosk batch 1"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300">Link to existing device (optional)</label>
              <select
                value={selectedDeviceId}
                onChange={(e) => setSelectedDeviceId(e.target.value)}
                className={inputClass}
              >
                <option value="">— Create new device —</option>
                {deviceList?.devices.map((d) => (
                  <option key={d.id} value={d.id}>{d.name}</option>
                ))}
              </select>
              <p className="mt-1 text-xs text-slate-500">
                When set, the agent will link to this device instead of creating a new one.
              </p>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300">Expires in (hours)</label>
              <input
                type="number"
                min={1}
                max={720}
                value={expiresInHours}
                onChange={(e) => setExpiresInHours(Number(e.target.value))}
                className={inputClass}
              />
            </div>
            <div className="mt-6 flex justify-end gap-3">
              <button
                type="button"
                onClick={onClose}
                className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 transition-colors hover:bg-surface-800"
              >
                Cancel
              </button>
              <button
                onClick={() => createMutation.mutate()}
                disabled={createMutation.isPending}
                className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400 disabled:opacity-50"
              >
                {createMutation.isPending ? 'Generating…' : 'Generate Token'}
              </button>
            </div>

            {tokens && tokens.length > 0 && (
              <div className="mt-6 border-t border-surface-800 pt-4">
                <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500">Recent tokens</h3>
                <ul className="mt-2 space-y-1 text-xs text-slate-400">
                  {tokens.slice(0, 5).map((t) => (
                    <li key={t.id} className="flex justify-between">
                      <span>{t.label || t.id.slice(0, 8)}</span>
                      <span className={t.isUsed ? 'text-emerald-400' : t.isRevoked ? 'text-red-400' : 'text-slate-400'}>
                        {t.isUsed ? 'Used' : t.isRevoked ? 'Revoked' : `Expires ${new Date(t.expiresAt).toLocaleDateString()}`}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        ) : (
          <div className="mt-5">
            <div className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 p-4">
              <p className="text-xs font-medium uppercase tracking-wider text-emerald-300">Token (shown once)</p>
              <div className="mt-2 flex items-center gap-2">
                <code className="flex-1 break-all rounded bg-black/40 px-3 py-2 font-mono text-xs text-emerald-200">
                  {newToken.token}
                </code>
                <button
                  onClick={copyToken}
                  className="shrink-0 rounded-lg border border-surface-700 p-2 text-slate-300 transition-colors hover:bg-surface-800"
                  title="Copy token"
                >
                  {copied ? <Check className="h-4 w-4 text-emerald-400" /> : <Copy className="h-4 w-4" />}
                </button>
              </div>
              <p className="mt-2 text-xs text-emerald-300/80">
                Expires {new Date(newToken.expiresAt).toLocaleString()}
              </p>
            </div>

            <div className="mt-4 rounded-lg border border-surface-700 bg-surface-850 p-4 text-xs text-slate-300">
              <p className="font-medium text-slate-200">Run the agent with:</p>
              <code className="mt-2 block break-all rounded bg-black/40 px-3 py-2 font-mono">
                SentinelKiosk.Agent.exe --enroll {newToken.token.slice(0, 12)}…
              </code>
              <p className="mt-2 text-slate-500">
                Or place the token in the agent's config and start the service.
              </p>
            </div>

            <div className="mt-6 flex justify-end">
              <button
                onClick={onClose}
                className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-accent-400"
              >
                Done
              </button>
            </div>
          </div>
        )}
      </div>
      </div>
      )
      }

function BulkModal({
  deviceIds,
  groups,
  onClose,
}: {
  deviceIds: string[]
  groups: { id: string; name: string }[]
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [mode, setMode] = useState<'command' | 'group' | 'tag'>('command')
  const [commandType, setCommandType] = useState(COMMAND_TYPES[0])
  const [payload, setPayload] = useState('')
  const [groupId, setGroupId] = useState('')
  const [tags, setTags] = useState('')

  const mutation = useMutation({
    mutationFn: () => {
      if (mode === 'command') return devicesApi.bulkCommand({ deviceIds, commandType, payload: payload || undefined })
      if (mode === 'group') return devicesApi.bulkAssignGroup({ deviceIds, groupId: groupId || null })
      return devicesApi.bulkTag({ deviceIds, tags })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['devices'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">
          Bulk Operations ({deviceIds.length} devices)
        </h2>
        <div className="mt-4 flex gap-2">
          {(['command', 'group', 'tag'] as const).map((m) => (
            <button
              key={m}
              onClick={() => setMode(m)}
              className={`rounded-lg px-3 py-1.5 text-sm font-medium capitalize transition-colors ${
                mode === m
                  ? 'bg-accent-500/20 text-accent-400'
                  : 'text-slate-400 hover:bg-surface-800'
              }`}
            >
              {m}
            </button>
          ))}
        </div>

        {mode === 'command' && (
          <div className="mt-4 space-y-4">
            <div>
              <label className="block text-sm font-medium text-slate-300">Command</label>
              <select
                value={commandType}
                onChange={(e) => setCommandType(e.target.value)}
                className="mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500"
              >
                {COMMAND_TYPES.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300">Payload (JSON, optional)</label>
              <textarea
                value={payload}
                onChange={(e) => setPayload(e.target.value)}
                rows={3}
                className="mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 font-mono text-sm text-white outline-none focus:border-accent-500"
                placeholder='{"key": "value"}'
              />
            </div>
          </div>
        )}

        {mode === 'group' && (
          <div className="mt-4">
            <label className="block text-sm font-medium text-slate-300">Assign to Group</label>
            <select
              value={groupId}
              onChange={(e) => setGroupId(e.target.value)}
              className="mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500"
            >
              <option value="">— Remove from group —</option>
              {groups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
            </select>
          </div>
        )}

        {mode === 'tag' && (
          <div className="mt-4">
            <label className="block text-sm font-medium text-slate-300">Tags (comma-separated)</label>
            <input
              type="text"
              value={tags}
              onChange={(e) => setTags(e.target.value)}
              className="mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500"
              placeholder="lobby, floor-2, touch-enabled"
            />
          </div>
        )}

        <div className="mt-6 flex justify-end gap-3">
          <button
            onClick={onClose}
            className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 transition-colors hover:bg-surface-800"
          >
            Cancel
          </button>
          <button
            onClick={() => mutation.mutate()}
            disabled={mutation.isPending}
            className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400 disabled:opacity-50"
          >
            {mutation.isPending ? 'Applying…' : 'Apply'}
          </button>
        </div>
        {mutation.isError && (
          <p className="mt-2 text-sm text-red-400">{mutation.error.message}</p>
        )}
      </div>
    </div>
  )
}

function StatusBadge({ status }: { status: string }) {
  const styles: Record<string, string> = {
    Online: 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30',
    Offline: 'bg-red-500/10 text-red-400 ring-red-500/30',
    Error: 'bg-red-500/20 text-red-300 ring-red-500/40',
    Maintenance: 'bg-amber-500/10 text-amber-400 ring-amber-500/30',
  }
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ${styles[status] ?? styles.Offline}`}
    >
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      {status}
      </span>
      )
      }

      function DomainBadge({ device }: { device: DeviceDto }) {
      const status = device.domainJoinStatus
      if (!status) {
      return <span className="text-xs text-slate-600">—</span>
      }
      const healthy = device.domainSecureChannelHealthy
      const styles =
      status === 'Domain' && healthy === false
      ? 'bg-red-500/10 text-red-400 ring-red-500/30'
      : status === 'Domain'
        ? 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30'
        : status === 'Workgroup'
          ? 'bg-amber-500/10 text-amber-400 ring-amber-500/30'
          : 'bg-slate-500/10 text-slate-400 ring-slate-500/30'
      const label =
      status === 'Domain'
      ? healthy === false
        ? `${device.domainName || 'Domain'} (trust broken)`
        : device.domainName || 'Domain'
      : status === 'Workgroup'
        ? device.domainName ? `WG: ${device.domainName}` : 'Workgroup'
        : status
      return (
      <span className={`inline-flex max-w-[14rem] items-center gap-1.5 truncate rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ${styles}`} title={label}>
      <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-current" />
      {label}
      </span>
      )
      }

      const inputClass =
  'mt-1.5 block w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white placeholder-slate-500 outline-none transition-colors focus:border-accent-500 focus:ring-1 focus:ring-accent-500'

function DeviceModal({
  device,
  onClose,
}: {
  device: DeviceDto | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [createdDeviceId, setCreatedDeviceId] = useState<string | null>(null)
  const [tokenData, setTokenData] = useState<{ token: string; expiresAt: string } | null>(null)
  const [copied, setCopied] = useState(false)
  const [formData, setFormData] = useState({
    name: device?.name || '',
    serialNumber: device?.serialNumber || '',
    description: device?.description || '',
    location: device?.location || '',
    hostname: device?.hostname || '',
    ipAddress: device?.ipAddress || '',
    macAddress: device?.macAddress || '',
    latitude: device?.latitude?.toString() || '',
    longitude: device?.longitude?.toString() || '',
    groupId: device?.groupId || '',
  })

  const { data: groups } = useQuery({
    queryKey: ['deviceGroups'],
    queryFn: groupsApi.list,
  })

  const enrollMutation = useMutation({
    mutationFn: (deviceId: string) =>
      enrollmentApi.create({ label: formData.name || 'New device', expiresInHours: 72, deviceId }),
    onSuccess: (data) => {
      setTokenData({ token: data.token, expiresAt: data.expiresAt })
      queryClient.invalidateQueries({ queryKey: ['enrollment-tokens'] })
    },
  })

  const mutation = useMutation({
    mutationFn: (data: Partial<DeviceDto> & { groupId?: string }) =>
      device ? devicesApi.update(device.id, data) : devicesApi.create(data as any),
    onSuccess: (result: any) => {
      queryClient.invalidateQueries({ queryKey: ['devices'] })
      queryClient.invalidateQueries({ queryKey: ['deviceGroups'] })
      if (!device && result?.id) {
        // New device — auto-generate enrollment token
        setCreatedDeviceId(result.id)
        enrollMutation.mutate(result.id)
      } else {
        onClose()
      }
    },
  })

  const copyToken = async () => {
    if (tokenData) {
      await navigator.clipboard.writeText(tokenData.token)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    }
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const payload = {
      ...formData,
      latitude: formData.latitude ? parseFloat(formData.latitude) : undefined,
      longitude: formData.longitude ? parseFloat(formData.longitude) : undefined,
      groupId: formData.groupId || undefined,
    }
    mutation.mutate(payload)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-2xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">
          {device ? 'Edit Device' : tokenData ? 'Device Created' : 'Add Device'}
        </h2>

        {/* Token display after device creation */}
        {tokenData && !device && (
          <div className="mt-5">
            <div className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 p-4">
              <p className="text-xs font-medium uppercase tracking-wider text-emerald-300">
                Enrollment Token (shown once)
              </p>
              <div className="mt-2 flex items-center gap-2">
                <code className="flex-1 break-all rounded bg-black/40 px-3 py-2 font-mono text-xs text-emerald-200">
                  {tokenData.token}
                </code>
                <button
                  onClick={copyToken}
                  className="shrink-0 rounded-lg border border-surface-700 p-2 text-slate-300 transition-colors hover:bg-surface-800"
                  title="Copy token"
                >
                  {copied ? <Check className="h-4 w-4 text-emerald-400" /> : <Copy className="h-4 w-4" />}
                </button>
              </div>
              <p className="mt-2 text-xs text-emerald-300/80">
                Expires {new Date(tokenData.expiresAt).toLocaleString()}
              </p>
            </div>

            <div className="mt-4 rounded-lg border border-surface-700 bg-surface-850 p-4 text-xs text-slate-300">
              <p className="font-medium text-slate-200">Deploy on the kiosk PC — pick one:</p>

              <div className="mt-3 space-y-3">
                <div>
                  <p className="font-medium text-slate-200">Option A — One-liner (easiest)</p>
                  <p className="mt-1 text-slate-400">On the PC, open PowerShell as Administrator and paste:</p>
                  <code className="mt-1 block break-all rounded bg-black/40 px-3 py-2 font-mono text-emerald-300">
                    irm "{window.location.origin.replace(':5173', ':5163')}/api/deploy/script?token={encodeURIComponent(tokenData.token)}" | iex
                  </code>
                </div>

                <div>
                  <p className="font-medium text-slate-200">Option B — USB / manual</p>
                  <p className="mt-1 text-slate-400">
                    Download the{' '}
                    <a
                      href={`${window.location.origin.replace(':5173', ':5163')}/api/bundle/download`}
                      className="text-accent-400 hover:underline"
                    >
                      bundle zip
                    </a>
                    , copy to the PC via USB/network, extract, then run as Administrator:
                  </p>
                  <code className="mt-1 block rounded bg-black/40 px-3 py-2 font-mono">
                    .\setup.ps1 -EnrollmentToken {tokenData.token}
                  </code>
                </div>
              </div>

              <p className="mt-3 text-slate-500">
                Or go to the <a href="/deploy" className="text-accent-400 hover:underline">Deploy page</a> for the full guided flow.
              </p>
            </div>

            <div className="mt-6 flex justify-end">
              <button
                onClick={onClose}
                className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-accent-400"
              >
                Done
              </button>
            </div>
          </div>
        )}

        {/* Enrollment pending spinner */}
        {createdDeviceId && !tokenData && !device && (
          <div className="mt-8 flex flex-col items-center gap-3 py-4">
            <div className="h-8 w-8 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
            <p className="text-sm text-slate-400">Generating enrollment token...</p>
          </div>
        )}

        {/* Device form — hidden after creation */}
        {!createdDeviceId && (
        <form onSubmit={handleSubmit} className="mt-5 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-300">Name</label>
            <input
              type="text"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              className={inputClass}
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Serial Number <span className="text-slate-500">(optional)</span></label>
            <input
              type="text"
              value={formData.serialNumber}
              onChange={(e) => setFormData({ ...formData, serialNumber: e.target.value })}
              className={`${inputClass} font-mono`}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Description</label>
            <input
              type="text"
              value={formData.description}
              onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              className={inputClass}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Location</label>
            <input
              type="text"
              value={formData.location}
              onChange={(e) => setFormData({ ...formData, location: e.target.value })}
              className={inputClass}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Group <span className="text-slate-500">(optional)</span></label>
            <select
              value={formData.groupId}
              onChange={(e) => setFormData({ ...formData, groupId: e.target.value })}
              className={inputClass}
            >
              <option value="">No group</option>
              {groups?.map((g) => (
                <option key={g.id} value={g.id}>{g.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Hostname (FQDN) <span className="text-red-400">*</span></label>
            <input
              type="text"
              value={formData.hostname}
              onChange={(e) => setFormData({ ...formData, hostname: e.target.value })}
              className={inputClass}
              placeholder="kiosk01.example.local"
              required
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-300">IP Address <span className="text-red-400">*</span></label>
              <input
                type="text"
                value={formData.ipAddress}
                onChange={(e) => setFormData({ ...formData, ipAddress: e.target.value })}
                className={`${inputClass} font-mono`}
                placeholder="192.168.1.100"
                required
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300">MAC Address</label>
              <input
                type="text"
                value={formData.macAddress}
                onChange={(e) => setFormData({ ...formData, macAddress: e.target.value })}
                className={`${inputClass} font-mono`}
                placeholder="00:1A:2B:3C:4D:5E"
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-300">Latitude <span className="text-slate-500">(optional)</span></label>
              <input
                type="number"
                step="any"
                value={formData.latitude}
                onChange={(e) => setFormData({ ...formData, latitude: e.target.value })}
                className={`${inputClass} font-mono`}
                placeholder="33.7490"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300">Longitude <span className="text-slate-500">(optional)</span></label>
              <input
                type="number"
                step="any"
                value={formData.longitude}
                onChange={(e) => setFormData({ ...formData, longitude: e.target.value })}
                className={`${inputClass} font-mono`}
                placeholder="-117.1897"
              />
            </div>
          </div>
          <div className="mt-6 flex justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 transition-colors hover:bg-surface-800"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={mutation.isPending}
              className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400 disabled:opacity-50"
            >
              {mutation.isPending ? 'Saving…' : device ? 'Save' : 'Create & Enroll'}
            </button>
          </div>
        </form>
        )}
        </div>
        </div>
        )
        }

        function ImportModal({ onClose }: { onClose: () => void }) {
          const queryClient = useQueryClient()
            const fileRef = useRef<HTMLInputElement>(null)
            const [parsedRows, setParsedRows] = useState<Record<string, string>[]>([])
            const [parseError, setParseError] = useState('')
            const [fileName, setFileName] = useState('')
            const [importResult, setImportResult] = useState<import('@/lib/api').ImportDevicesResponse | null>(null)

            const { data: groupsList } = useQuery({
              queryKey: ['deviceGroups'],
              queryFn: groupsApi.list,
            })

          const importMutation = useMutation({
            mutationFn: (devices: import('@/lib/api').ImportDeviceRow[]) =>
              devicesApi.import({ devices }),
            onSuccess: (data) => {
              setImportResult(data)
              queryClient.invalidateQueries({ queryKey: ['devices'] })
            },
          })

          const COLUMN_MAP: Record<string, string> = {
            name: 'name', serial: 'serialNumber', 'serial number': 'serialNumber', serialnumber: 'serialNumber',
            description: 'description', location: 'location', hostname: 'hostname', host: 'hostname', fqdn: 'hostname',
            ip: 'ipAddress', 'ip address': 'ipAddress', ipaddress: 'ipAddress',
            mac: 'macAddress', 'mac address': 'macAddress', macaddress: 'macAddress',
            firmware: 'firmwareVersion', 'firmware version': 'firmwareVersion', firmwareversion: 'firmwareVersion',
            group: 'group', 'group name': 'group',
            latitude: 'latitude', lat: 'latitude',
            longitude: 'longitude', lng: 'longitude', lon: 'longitude',
            }

          const handleFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
            const file = e.target.files?.[0]
            if (!file) return
            setFileName(file.name)
            setParseError('')
            setParsedRows([])
            setImportResult(null)

            try {
              const XLSX = await import('xlsx')
              const buffer = await file.arrayBuffer()
              const wb = XLSX.read(buffer)
              const ws = wb.Sheets[wb.SheetNames[0]]
              const raw: Record<string, string>[] = XLSX.utils.sheet_to_json(ws, { defval: '' })

              if (raw.length === 0) { setParseError('No data rows found in the file.'); return }
              if (raw.length > 500) { setParseError('Maximum 500 rows per import.'); return }

              // Normalize column headers
              const normalized = raw.map((row) => {
                const out: Record<string, string> = {}
                for (const [key, val] of Object.entries(row)) {
                  const mapped = COLUMN_MAP[key.toLowerCase().trim()]
                  if (mapped) out[mapped] = String(val).trim()
                }
                return out
              }).filter((r) => r.name) // must have a name

              if (normalized.length === 0) { setParseError('No valid rows found. Ensure a "Name" column exists.'); return }
              const noContact = normalized.filter((r) => !r.hostname && !r.ipAddress)
              if (noContact.length > 0) {
                setParseError(`${noContact.length} row(s) missing both Hostname and IP Address (one is required): ${noContact.slice(0, 3).map((r) => r.name).join(', ')}${noContact.length > 3 ? '…' : ''}`)
                return
              }
              setParsedRows(normalized)
            } catch (err) {
              setParseError(`Failed to parse file: ${err instanceof Error ? err.message : 'Unknown error'}`)
            }
          }

          const downloadTemplate = async () => {
              const XLSX = await import('xlsx')
              const wb = XLSX.utils.book_new()

              // Main sheet with headers + example rows
              const headers = ['Name', 'Serial Number', 'Description', 'Location', 'Hostname', 'IP Address', 'MAC Address', 'Firmware Version', 'Group']
              const groupNames = (groupsList ?? []).map(g => g.name)
              const exampleRows = [
                ['PCS3DR000001', 'SN001', 'Main lobby display', null, null, '192.168.1.100', '00:1A:2B:3C:4D:5E', '1.2.3', groupNames[0] ?? ''],
                ['PCS3DR000002', 'SN002', 'Secondary lobby display', null, null, '192.168.1.101', '00:1A:2B:3C:4D:5F', '1.2.3', groupNames[0] ?? ''],
              ]
              const ws = XLSX.utils.aoa_to_sheet([headers, ...exampleRows])
              ws['!cols'] = [
                { wch: 22 }, { wch: 14 }, { wch: 24 }, { wch: 20 }, { wch: 38 }, { wch: 16 }, { wch: 20 }, { wch: 16 }, { wch: 36 },
              ]

              // Hostname prefill: formula in column E for rows 2-200
              // =IF(A2="","",UPPER(A2)&".internal.livingspaces.com")
              // User can type over it if they need a custom hostname
              const range = XLSX.utils.decode_range(ws['!ref'] ?? 'A1')
              range.e.r = Math.max(range.e.r, 199) // extend to row 200
              ws['!ref'] = XLSX.utils.encode_range(range)
              for (let row = 2; row <= 200; row++) {
                  const cellRef = `E${row}`
                  ws[cellRef] = {
                    t: 's',
                    v: '',
                    f: `IF(A${row}="","",UPPER(A${row})&".internal.livingspaces.com")`,
                  }
                  // Location prefill: extract "City, ST" from Group column
                  // "Store 05 - Monrovia, CA" → "Monrovia, CA"
                  const locRef = `D${row}`
                  ws[locRef] = {
                    t: 's',
                    v: '',
                    f: `IF(OR(I${row}="",ISERROR(FIND(" - ",I${row}))),"",TRIM(RIGHT(I${row},LEN(I${row})-FIND(" - ",I${row})-2)))`,
                  }
                }

              // Groups reference sheet (hidden)
              const groupsWs = XLSX.utils.aoa_to_sheet([['Groups'], ...groupNames.map(n => [n])])
              XLSX.utils.book_append_sheet(wb, ws, 'Devices')
              XLSX.utils.book_append_sheet(wb, groupsWs, 'GroupsRef')

              // Hide GroupsRef sheet
              if (!wb.Workbook) wb.Workbook = {}
              wb.Workbook.Sheets = [{ Hidden: 0 }, { Hidden: 1 }]

              // Write to array buffer
              const wbout = XLSX.write(wb, { bookType: 'xlsx', type: 'array' })

              // Post-process: inject data validation XML into the Devices sheet.
              // Schema order: dataValidations must come right after </sheetData>.
              const JSZip = (await import('jszip')).default
              const zip = await JSZip.loadAsync(wbout)
              const devicesSheetFile = 'xl/worksheets/sheet1.xml'
              let sheetXml = await zip.file(devicesSheetFile)!.async('string')

              const lastGroupRow = Math.max(groupNames.length, 1) + 1
              const dvXml = '<dataValidations count="1">'
                + '<dataValidation type="list" allowBlank="1" showErrorMessage="1" '
                + 'errorTitle="Invalid Group" error="Select a group from the list or type a new group name." '
                + 'errorStyle="warning" sqref="I2:I200">'
                + `<formula1>GroupsRef!$A$2:$A$${lastGroupRow}</formula1>`
                + '</dataValidation></dataValidations>'

              if (sheetXml.includes('</sheetData>')) {
                sheetXml = sheetXml.replace('</sheetData>', '</sheetData>' + dvXml)
              }
              zip.file(devicesSheetFile, sheetXml)

              // Download
              const blob = await zip.generateAsync({ type: 'blob', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
              const url = URL.createObjectURL(blob)
              const a = document.createElement('a')
              a.href = url; a.download = 'device-import-template.xlsx'; a.click()
              URL.revokeObjectURL(url)
            }

          const handleImport = () => {
            const devices = parsedRows.map((r) => ({
              name: r.name, serialNumber: r.serialNumber || undefined, description: r.description || undefined,
              location: r.location || undefined, hostname: r.hostname || undefined, ipAddress: r.ipAddress || undefined,
              macAddress: r.macAddress || undefined, firmwareVersion: r.firmwareVersion || undefined,
              group: r.group || undefined,
              latitude: r.latitude ? parseFloat(r.latitude) : undefined,
              longitude: r.longitude ? parseFloat(r.longitude) : undefined,
            }))
            importMutation.mutate(devices)
          }

          return (
            <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
              <div className="w-full max-w-4xl rounded-2xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
                <h2 className="text-lg font-semibold text-white">Import Devices</h2>
                <p className="mt-1 text-sm text-slate-400">
                  Upload an Excel (.xlsx) or CSV file with device data. <strong className="text-slate-300">Name</strong> plus <strong className="text-slate-300">Hostname or IP Address</strong> are required. Missing groups are created automatically. <strong className="text-slate-300">Hostname</strong> and <strong className="text-slate-300">Location</strong> auto-fill from Name and Group — you can override any cell.
                </p>

                {!importResult ? (
                  <>
                    <div className="mt-5 flex items-center gap-3">
                      <input ref={fileRef} type="file" accept=".xlsx,.xls,.csv" onChange={handleFile} className="hidden" />
                      <button
                        onClick={() => fileRef.current?.click()}
                        className="flex items-center gap-2 rounded-lg border border-surface-700 px-4 py-2.5 text-sm font-medium text-slate-200 transition-colors hover:bg-surface-800"
                      >
                        <FileSpreadsheet className="h-4 w-4" />
                        {fileName || 'Choose File'}
                      </button>
                      <button
                        onClick={downloadTemplate}
                        className="flex items-center gap-2 rounded-lg border border-surface-700 px-4 py-2.5 text-sm text-slate-400 transition-colors hover:bg-surface-800"
                      >
                        <Download className="h-4 w-4" />
                        Template
                      </button>
                    </div>

                    {parseError && (
                      <div className="mt-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
                        {parseError}
                      </div>
                    )}

                    {parsedRows.length > 0 && (
                      <div className="mt-4">
                                                <p className="text-sm text-slate-300">{parsedRows.length} device(s) ready to import:</p>
                                                <div className="mt-2 max-h-80 overflow-y-auto overflow-x-auto rounded-lg border border-surface-700">
                                                  <table className="min-w-full divide-y divide-surface-800 text-xs">
                                                    <thead>
                                                      <tr className="bg-surface-850 sticky top-0">
                                                        <th className="px-3 py-2 text-left text-slate-400">#</th>
                                                        <th className="px-3 py-2 text-left text-slate-400">Name</th>
                                                        <th className="px-3 py-2 text-left text-slate-400">Group</th>
                                                        <th className="px-3 py-2 text-left text-slate-400">Location</th>
                                                        <th className="px-3 py-2 text-left text-slate-400">Hostname</th>
                                                        <th className="px-3 py-2 text-left text-slate-400">IP</th>
                                                        <th className="px-3 py-2 text-left text-slate-400">Serial</th>
                                                        <th className="px-3 py-2 text-left text-slate-400">Description</th>
                                                      </tr>
                                                    </thead>
                                                    <tbody className="divide-y divide-surface-800">
                                                      {parsedRows.map((r, i) => (
                                                        <tr key={i}>
                                                          <td className="px-3 py-1.5 text-slate-500 font-mono">{i + 2}</td>
                                                          <td className="px-3 py-1.5 text-white">{r.name}</td>
                                                          <td className="px-3 py-1.5 text-slate-400">{r.group || '—'}</td>
                                                          <td className="px-3 py-1.5 text-slate-400">{r.location || '—'}</td>
                                                          <td className="px-3 py-1.5 text-slate-400 font-mono text-[11px]">{r.hostname || '—'}</td>
                                                          <td className="px-3 py-1.5 text-slate-400 font-mono">{r.ipAddress || '—'}</td>
                                                          <td className="px-3 py-1.5 text-slate-400">{r.serialNumber || '—'}</td>
                                                          <td className="px-3 py-1.5 text-slate-400">{r.description || '—'}</td>
                                                        </tr>
                                                      ))}
                                                    </tbody>
                                                  </table>
                                                </div>
                                              </div>
                    )}

                    <div className="mt-6 flex justify-end gap-3">
                      <button onClick={onClose} className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 transition-colors hover:bg-surface-800">Cancel</button>
                      <button
                        onClick={handleImport}
                        disabled={parsedRows.length === 0 || importMutation.isPending}
                        className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400 disabled:opacity-50"
                      >
                        {importMutation.isPending ? 'Importing…' : `Import ${parsedRows.length} Device(s)`}
                      </button>
                    </div>
                    {importMutation.isError && (
                      <p className="mt-2 text-sm text-red-400">{(importMutation.error as Error).message}</p>
                    )}
                  </>
                ) : (
                  <div className="mt-5">
                    {/* Progress bar */}
                    {(() => {
                      const total = importResult.imported + importResult.skipped + importResult.failed
                      const pct = (n: number) => total > 0 ? Math.round((n / total) * 100) : 0
                      const importedPct = pct(importResult.imported)
                      const skippedPct = pct(importResult.skipped)
                      const failedPct = pct(importResult.failed)
                      return (
                        <div>
                          <div className="flex h-3 w-full overflow-hidden rounded-full bg-surface-800">
                            {importedPct > 0 && (
                              <div className="bg-emerald-500 transition-all" style={{ width: `${importedPct}%` }} />
                            )}
                            {skippedPct > 0 && (
                              <div className="bg-amber-500 transition-all" style={{ width: `${skippedPct}%` }} />
                            )}
                            {failedPct > 0 && (
                              <div className="bg-red-500 transition-all" style={{ width: `${failedPct}%` }} />
                            )}
                          </div>
                          <div className="mt-2 flex items-center justify-between text-xs text-slate-400">
                            <span>{total} rows processed</span>
                            <span>
                              {importResult.failed === 0 && importResult.skipped === 0
                                ? <span className="text-emerald-400 font-medium">All devices imported successfully</span>
                                : importResult.failed === 0
                                  ? <span className="text-amber-400 font-medium">{importResult.imported} imported, {importResult.skipped} skipped</span>
                                  : <span className="text-red-400 font-medium">{importResult.failed} failed — see details below</span>
                              }
                            </span>
                          </div>
                        </div>
                      )
                    })()}

                    {/* Counts */}
                    <div className="mt-4 grid grid-cols-3 gap-4 text-center">
                      <div className="rounded-lg bg-emerald-500/10 p-3">
                        <p className="text-2xl font-bold text-emerald-400">{importResult.imported}</p>
                        <p className="text-xs text-slate-400">Imported</p>
                      </div>
                      <div className="rounded-lg bg-amber-500/10 p-3">
                        <p className="text-2xl font-bold text-amber-400">{importResult.skipped}</p>
                        <p className="text-xs text-slate-400">Skipped (duplicate)</p>
                      </div>
                      <div className="rounded-lg bg-red-500/10 p-3">
                        <p className="text-2xl font-bold text-red-400">{importResult.failed}</p>
                        <p className="text-xs text-slate-400">Failed</p>
                      </div>
                    </div>

                    {/* Row-level detail with Excel cell references */}
                    {importResult.results.filter(r => r.status !== 'created').length > 0 && (
                      <div className="mt-4 max-h-48 overflow-y-auto rounded-lg border border-surface-700">
                        <table className="w-full text-xs">
                          <thead>
                            <tr className="border-b border-surface-700 bg-surface-800/50">
                              <th className="px-3 py-2 text-left text-slate-500">Cell</th>
                              <th className="px-3 py-2 text-left text-slate-500">Name</th>
                              <th className="px-3 py-2 text-left text-slate-500">Status</th>
                              <th className="px-3 py-2 text-left text-slate-500">Reason</th>
                            </tr>
                          </thead>
                          <tbody>
                            {importResult.results.filter(r => r.status !== 'created').map((r) => (
                              <tr key={r.row} className={`border-b border-surface-800/50 ${r.status === 'error' ? 'text-red-400' : 'text-amber-400'}`}>
                                <td className="px-3 py-1.5 font-mono font-medium">A{r.row + 1}</td>
                                <td className="px-3 py-1.5">{r.name}</td>
                                <td className="px-3 py-1.5">
                                  <span className={`inline-flex rounded-full px-1.5 py-0.5 text-[10px] font-medium ${
                                    r.status === 'error' ? 'bg-red-500/10 ring-1 ring-red-500/30' : 'bg-amber-500/10 ring-1 ring-amber-500/30'
                                  }`}>
                                    {r.status === 'error' ? 'Failed' : 'Skipped'}
                                  </span>
                                </td>
                                <td className="px-3 py-1.5 text-slate-500">{r.message}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}

                    <div className="mt-6 flex justify-end">
                      <button onClick={onClose} className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-accent-400">Done</button>
                    </div>
                  </div>
                )}
              </div>
            </div>
          )
        }

                function DeviceMetrics({ device }: { device: any }) {
        const hasAny = device.cpuPercent != null || device.memoryPercent != null ||
        device.diskFreePercent != null || device.diskFreeGb != null || device.uptimeSeconds != null

        if (!hasAny) {
        return <span className="text-xs text-slate-600">—</span>
        }

        const formatUptime = (seconds: number) => {
        const h = Math.floor(seconds / 3600)
        const m = Math.floor((seconds % 3600) / 60)
        if (h > 24) return `${Math.floor(h / 24)}d ${h % 24}h`
        if (h > 0) return `${h}h ${m}m`
        return `${m}m`
        }

        const metric = (label: string, value: number | null | undefined, unit: string, warn?: number, invert?: boolean) => {
        if (value == null) return null
        const isBad = invert ? value < (warn ?? 0) : value > (warn ?? Infinity)
        return (
        <span key={label} className={`inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs font-mono ${
        isBad ? 'bg-red-500/10 text-red-400' : 'bg-surface-800 text-slate-300'
        }`} title={label}>
        {label === 'Disk' ? `${value.toFixed(1)}${unit}` : `${value.toFixed(0)}${unit}`}
        </span>
        )
        }

        return (
        <div className="flex flex-wrap gap-1">
        {metric('CPU', device.cpuPercent, '%', 80)}
        {metric('Mem', device.memoryPercent, '%', 85)}
        {metric('Disk', device.diskFreeGb ?? device.diskFreePercent, device.diskFreeGb != null ? ' GB' : '%', 10, true)}
        {device.uptimeSeconds != null && (
        <span className="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs font-mono bg-surface-800 text-slate-300" title="Uptime">
        ↑{formatUptime(device.uptimeSeconds)}
        </span>
        )}
        {device.osVersion && (
        <span className="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs bg-surface-800 text-slate-400" title="OS">
        Win {device.osVersion}
        </span>
        )}
        </div>
        )
        }
