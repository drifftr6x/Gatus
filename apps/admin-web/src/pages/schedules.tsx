import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { schedulesApi, devicesApi, contentApi } from '@/lib/api'
import type { ScheduleDto } from '@/lib/api'
import { useState } from 'react'

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

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-slate-900" />
      </div>
    )
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded">
        Error loading schedules: {error.message}
      </div>
    )
  }

  return (
    <div>
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-2xl font-semibold text-slate-900">Schedules</h1>
        <button
          onClick={() => {
            setEditingSchedule(null)
            setIsModalOpen(true)
          }}
          className="px-4 py-2 bg-slate-900 text-white rounded-md hover:bg-slate-800"
        >
          Add Schedule
        </button>
      </div>

      <div className="bg-white shadow rounded-lg overflow-hidden">
        <table className="min-w-full divide-y divide-slate-200">
          <thead className="bg-slate-50">
            <tr>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                Name
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                Device
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                Content
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                Time Range
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                Recurrence
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                Status
              </th>
              <th className="px-6 py-3 text-right text-xs font-medium text-slate-500 uppercase tracking-wider">
                Actions
              </th>
            </tr>
          </thead>
          <tbody className="bg-white divide-y divide-slate-200">
            {data?.schedules.map((schedule) => (
              <tr key={schedule.id}>
                <td className="px-6 py-4 whitespace-nowrap">
                  <div className="text-sm font-medium text-slate-900">{schedule.name}</div>
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500">
                  {schedule.deviceName}
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500">
                  {schedule.contentName}
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500">
                  {new Date(schedule.startTime).toLocaleString()} —{' '}
                  {new Date(schedule.endTime).toLocaleString()}
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500">
                  {schedule.recurrence}
                </td>
                <td className="px-6 py-4 whitespace-nowrap">
                  <span
                    className={`px-2 inline-flex text-xs leading-5 font-semibold rounded-full ${
                      schedule.isActive
                        ? 'bg-green-100 text-green-800'
                        : 'bg-slate-100 text-slate-800'
                    }`}
                  >
                    {schedule.isActive ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium">
                  <button
                    onClick={() => {
                      setEditingSchedule(schedule)
                      setIsModalOpen(true)
                    }}
                    className="text-slate-600 hover:text-slate-900 mr-4"
                  >
                    Edit
                  </button>
                  <button
                    onClick={() => handleDelete(schedule.id)}
                    className="text-red-600 hover:text-red-900"
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {data?.schedules.length === 0 && (
          <div className="text-center py-8 text-slate-500">
            No schedules found. Create your first schedule to get started.
          </div>
        )}
      </div>

      {isModalOpen && (
        <ScheduleModal
          schedule={editingSchedule}
          onClose={() => setIsModalOpen(false)}
        />
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
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-lg max-w-lg w-full p-6 max-h-[90vh] overflow-y-auto">
        <h2 className="text-lg font-semibold mb-4">
          {schedule ? 'Edit Schedule' : 'Add Schedule'}
        </h2>
        {submitError && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded mb-4">
            {submitError}
          </div>
        )}
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700">Name</label>
            <input
              type="text"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              className="mt-1 block w-full px-3 py-2 border border-slate-300 rounded-md"
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700">Device</label>
            <select
              value={formData.deviceId}
              onChange={(e) => setFormData({ ...formData, deviceId: e.target.value })}
              className="mt-1 block w-full px-3 py-2 border border-slate-300 rounded-md"
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
            <label className="block text-sm font-medium text-slate-700">Content</label>
            <select
              value={formData.contentId}
              onChange={(e) => setFormData({ ...formData, contentId: e.target.value })}
              className="mt-1 block w-full px-3 py-2 border border-slate-300 rounded-md"
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
              <label className="block text-sm font-medium text-slate-700">Start Time</label>
              <input
                type="datetime-local"
                value={formData.startTime}
                onChange={(e) => setFormData({ ...formData, startTime: e.target.value })}
                className="mt-1 block w-full px-3 py-2 border border-slate-300 rounded-md"
                required
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">End Time</label>
              <input
                type="datetime-local"
                value={formData.endTime}
                onChange={(e) => setFormData({ ...formData, endTime: e.target.value })}
                className="mt-1 block w-full px-3 py-2 border border-slate-300 rounded-md"
                required
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-700">Recurrence</label>
              <select
                value={formData.recurrence}
                onChange={(e) => setFormData({ ...formData, recurrence: e.target.value })}
                className="mt-1 block w-full px-3 py-2 border border-slate-300 rounded-md"
              >
                <option value="Once">Once</option>
                <option value="Daily">Daily</option>
                <option value="Weekly">Weekly</option>
                <option value="Monthly">Monthly</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Priority</label>
              <input
                type="number"
                value={formData.priority}
                onChange={(e) => setFormData({ ...formData, priority: parseInt(e.target.value) || 0 })}
                className="mt-1 block w-full px-3 py-2 border border-slate-300 rounded-md"
              />
            </div>
          </div>
          {schedule && (
            <div className="flex items-center">
              <input
                id="isActive"
                type="checkbox"
                checked={formData.isActive}
                onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                className="h-4 w-4 text-slate-900 border-slate-300 rounded"
              />
              <label htmlFor="isActive" className="ml-2 block text-sm text-slate-700">
                Active
              </label>
            </div>
          )}
          <div className="flex justify-end space-x-3 mt-6">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 border border-slate-300 rounded-md text-slate-700 hover:bg-slate-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={mutation.isPending}
              className="px-4 py-2 bg-slate-900 text-white rounded-md hover:bg-slate-800 disabled:opacity-50"
            >
              {mutation.isPending ? 'Saving...' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
