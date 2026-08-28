import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { schedulesApi, devicesApi, contentApi } from '@/lib/api'
import type { ScheduleDto } from '@/lib/api'
import { useState } from 'react'
import { Plus, Pencil, Trash2 } from 'lucide-react'

export function SchedulesPage() {
  const queryClient = useQueryClient()
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [editingSchedule, setEditingSchedule] = useState<ScheduleDto | null>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: ['schedules'],
    queryFn: () => schedulesApi.list(),
  })

  const deleteMutation = useMutation({
    mutationFn: schedulesApi.delete,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['schedules'] })
    },
  })

  const handleDelete = (id: string) => {
    if (confirm('Are you sure you want to delete this schedule?')) {
      deleteMutation.mutate(id)
    }
  }

  return (
    <div>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-white">Schedules</h1>
          <p className="mt-1 text-sm text-slate-400">Assign content playback windows to devices</p>
        </div>
        <button
          onClick={() => {
            setEditingSchedule(null)
            setIsModalOpen(true)
          }}
          className="flex items-center gap-2 rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400"
        >
          <Plus className="h-4 w-4" />
          Add Schedule
        </button>
      </div>

      {isLoading ? (
        <div className="flex h-64 items-center justify-center">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
        </div>
      ) : error ? (
        <div className="mt-6 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Error loading schedules: {error.message}
        </div>
      ) : (
        <div className="mt-6 overflow-hidden rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
          <table className="min-w-full divide-y divide-surface-800">
            <thead>
              <tr className="bg-surface-850">
                {['Name', 'Device', 'Content', 'Time Range', 'Recurrence', 'Status', ''].map((h) => (
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
              {data?.schedules.map((schedule) => (
                <tr key={schedule.id} className="transition-colors hover:bg-surface-850">
                  <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-slate-100">
                    {schedule.name}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-400">
                    {schedule.deviceName}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-400">
                    {schedule.contentName}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-400">
                    {new Date(schedule.startTime).toLocaleString()} —{' '}
                    {new Date(schedule.endTime).toLocaleString()}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <span className="rounded-md bg-surface-800 px-2 py-0.5 text-xs text-slate-300">
                      {schedule.recurrence}
                    </span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <span
                      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ${
                        schedule.isActive
                          ? 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30'
                          : 'bg-slate-500/10 text-slate-400 ring-slate-500/30'
                      }`}
                    >
                      <span className="h-1.5 w-1.5 rounded-full bg-current" />
                      {schedule.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-right">
                    <button
                      onClick={() => {
                        setEditingSchedule(schedule)
                        setIsModalOpen(true)
                      }}
                      className="mr-2 inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-surface-800 hover:text-white"
                    >
                      <Pencil className="h-3.5 w-3.5" />
                      Edit
                    </button>
                    <button
                      onClick={() => handleDelete(schedule.id)}
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
          {data?.schedules.length === 0 && (
            <div className="py-12 text-center text-sm text-slate-500">
              No schedules found. Create your first schedule to get started.
            </div>
          )}
        </div>
      )}

      {isModalOpen && (
        <ScheduleModal schedule={editingSchedule} onClose={() => setIsModalOpen(false)} />
      )}
    </div>
  )
}

function toLocalInputValue(iso?: string): string {
  if (!iso) return ''
  const d = new Date(iso)
  const pad = (n: number) => n.toString().padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

const inputClass =
  'mt-1.5 block w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white placeholder-slate-500 outline-none transition-colors focus:border-accent-500 focus:ring-1 focus:ring-accent-500'

function ScheduleModal({
  schedule,
  onClose,
}: {
  schedule: ScheduleDto | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [submitError, setSubmitError] = useState('')

  const { data: devicesData } = useQuery({
    queryKey: ['devices'],
    queryFn: () => devicesApi.list({ pageSize: 100 }),
  })

  const { data: contentData } = useQuery({
    queryKey: ['content'],
    queryFn: () => contentApi.list({ pageSize: 100 }),
  })

  const [formData, setFormData] = useState({
    deviceId: schedule?.deviceId || '',
    contentId: schedule?.contentId || '',
    name: schedule?.name || '',
    description: schedule?.description || '',
    startTime: toLocalInputValue(schedule?.startTime),
    endTime: toLocalInputValue(schedule?.endTime),
    priority: schedule?.priority ?? 0,
    recurrence: schedule?.recurrence || 'Once',
    isActive: schedule?.isActive ?? true,
  })

  const mutation = useMutation({
    mutationFn: (data: typeof formData) => {
      const payload = {
        ...data,
        startTime: new Date(data.startTime).toISOString(),
        endTime: new Date(data.endTime).toISOString(),
      }
      return schedule
        ? schedulesApi.update(schedule.id, payload)
        : schedulesApi.create(payload)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['schedules'] })
      onClose()
    },
    onError: (err) => {
      setSubmitError(err instanceof Error ? err.message : 'Failed to save schedule')
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitError('')
    mutation.mutate(formData)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-2xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">
          {schedule ? 'Edit Schedule' : 'Add Schedule'}
        </h2>
        {submitError && (
          <div className="mt-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
            {submitError}
          </div>
        )}
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
            <label className="block text-sm font-medium text-slate-300">Device</label>
            <select
              value={formData.deviceId}
              onChange={(e) => setFormData({ ...formData, deviceId: e.target.value })}
              className={`${inputClass} disabled:opacity-50`}
              required
              disabled={!!schedule}
            >
              <option value="">Select a device</option>
              {devicesData?.devices.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name} ({d.serialNumber})
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Content</label>
            <select
              value={formData.contentId}
              onChange={(e) => setFormData({ ...formData, contentId: e.target.value })}
              className={`${inputClass} disabled:opacity-50`}
              required
              disabled={!!schedule}
            >
              <option value="">Select content</option>
              {contentData?.contents.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name} ({c.type})
                </option>
              ))}
            </select>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-300">Start Time</label>
              <input
                type="datetime-local"
                value={formData.startTime}
                onChange={(e) => setFormData({ ...formData, startTime: e.target.value })}
                className={inputClass}
                required
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300">End Time</label>
              <input
                type="datetime-local"
                value={formData.endTime}
                onChange={(e) => setFormData({ ...formData, endTime: e.target.value })}
                className={inputClass}
                required
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-300">Recurrence</label>
              <select
                value={formData.recurrence}
                onChange={(e) => setFormData({ ...formData, recurrence: e.target.value })}
                className={inputClass}
              >
                <option value="Once">Once</option>
                <option value="Daily">Daily</option>
                <option value="Weekly">Weekly</option>
                <option value="Monthly">Monthly</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300">Priority</label>
              <input
                type="number"
                value={formData.priority}
                onChange={(e) => setFormData({ ...formData, priority: parseInt(e.target.value) || 0 })}
                className={inputClass}
              />
            </div>
          </div>
          {schedule && (
            <div className="flex items-center gap-2">
              <input
                id="isActive"
                type="checkbox"
                checked={formData.isActive}
                onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                className="h-4 w-4 rounded border-surface-700 bg-surface-850 text-accent-500 focus:ring-accent-500"
              />
              <label htmlFor="isActive" className="text-sm text-slate-300">
                Active
              </label>
            </div>
          )}
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
