import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { devicesApi, enrollmentApi, commandsApi } from '@/lib/api'
import type { DeviceDto } from '@/lib/api'
import { useState } from 'react'
import { Plus, Pencil, Trash2, KeyRound, Copy, Check, Send } from 'lucide-react'

export function DevicesPage() {
  const queryClient = useQueryClient()
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [isEnrollOpen, setIsEnrollOpen] = useState(false)
  const [commandDevice, setCommandDevice] = useState<DeviceDto | null>(null)
  const [editingDevice, setEditingDevice] = useState<DeviceDto | null>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: ['devices'],
    queryFn: () => devicesApi.list(),
  })

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
        <div className="mt-6 overflow-hidden rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
          <table className="min-w-full divide-y divide-surface-800">
            <thead>
              <tr className="bg-surface-850">
                {['Name', 'Hostname', 'IP Address', 'Serial Number', 'Status', 'Location', 'Last Seen', ''].map((h) => (
                  <th
                    key={h}
                    className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wider text-slate-500 last:text-right"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-surface-800">
              {data?.devices.map((device) => (
                <tr key={device.id} className="transition-colors hover:bg-surface-850">
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm font-medium text-slate-100">{device.name}</div>
                    {device.description && (
                      <div className="text-xs text-slate-500">{device.description}</div>
                    )}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap font-mono text-sm text-slate-400">
                    {device.hostname || '—'}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap font-mono text-sm text-slate-400">
                    {device.ipAddress || '—'}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap font-mono text-sm text-slate-400">
                    {device.serialNumber}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <StatusBadge status={device.status} />
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
        <EnrollTokenModal onClose={() => setIsEnrollOpen(false)} />
      )}
      {commandDevice && (
        <CommandModal device={commandDevice} onClose={() => setCommandDevice(null)} />
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

      function EnrollTokenModal({ onClose }: { onClose: () => void }) {
      const queryClient = useQueryClient()
      const [label, setLabel] = useState('')
      const [expiresInHours, setExpiresInHours] = useState(24)
      const [copied, setCopied] = useState(false)

      const { data: tokens } = useQuery({
      queryKey: ['enrollment-tokens'],
      queryFn: () => enrollmentApi.list(),
      })

      const createMutation = useMutation({
      mutationFn: () => enrollmentApi.create({ label: label || undefined, expiresInHours }),
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

function StatusBadge({ status }: { status: string }) {
  const styles: Record<string, string> = {
    Online: 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30',
    Offline: 'bg-slate-500/10 text-slate-400 ring-slate-500/30',
    Error: 'bg-red-500/10 text-red-400 ring-red-500/30',
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
  const [formData, setFormData] = useState({
    name: device?.name || '',
    serialNumber: device?.serialNumber || '',
    description: device?.description || '',
    location: device?.location || '',
    hostname: device?.hostname || '',
    ipAddress: device?.ipAddress || '',
    macAddress: device?.macAddress || '',
  })

  const mutation = useMutation({
    mutationFn: (data: typeof formData) =>
      device ? devicesApi.update(device.id, data) : devicesApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['devices'] })
      onClose()
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    mutation.mutate(formData)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-2xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">
          {device ? 'Edit Device' : 'Add Device'}
        </h2>
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
            <label className="block text-sm font-medium text-slate-300">Serial Number</label>
            <input
              type="text"
              value={formData.serialNumber}
              onChange={(e) => setFormData({ ...formData, serialNumber: e.target.value })}
              className={`${inputClass} font-mono disabled:opacity-50`}
              required
              disabled={!!device}
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
            <label className="block text-sm font-medium text-slate-300">Hostname (FQDN)</label>
            <input
              type="text"
              value={formData.hostname}
              onChange={(e) => setFormData({ ...formData, hostname: e.target.value })}
              className={inputClass}
              placeholder="kiosk01.example.local"
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-300">IP Address</label>
              <input
                type="text"
                value={formData.ipAddress}
                onChange={(e) => setFormData({ ...formData, ipAddress: e.target.value })}
                className={`${inputClass} font-mono`}
                placeholder="192.168.1.100"
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
              {mutation.isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
