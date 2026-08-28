import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { devicesApi } from '@/lib/api'
import type { DeviceDto } from '@/lib/api'
import { useState } from 'react'
import { Plus, Pencil, Trash2 } from 'lucide-react'

export function DevicesPage() {
  const queryClient = useQueryClient()
  const [isModalOpen, setIsModalOpen] = useState(false)
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
