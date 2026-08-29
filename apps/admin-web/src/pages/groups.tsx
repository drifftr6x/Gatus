import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { groupsApi } from '@/lib/api'
import { useState } from 'react'
import { Plus, Pencil, Trash2, FolderTree } from 'lucide-react'

export function GroupsPage() {
  const queryClient = useQueryClient()
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [editingGroup, setEditingGroup] = useState<{ id: string; name: string; description?: string } | null>(null)

  const { data: groups, isLoading, error } = useQuery({
    queryKey: ['deviceGroups'],
    queryFn: groupsApi.list,
  })

  const deleteMutation = useMutation({
    mutationFn: groupsApi.delete,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['deviceGroups'] }),
  })

  return (
    <div>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-white">Device Groups</h1>
          <p className="mt-1 text-sm text-slate-400">Organize devices into logical groups</p>
        </div>
        <button
          onClick={() => { setEditingGroup(null); setIsModalOpen(true) }}
          className="flex items-center gap-2 rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400"
        >
          <Plus className="h-4 w-4" />
          Create Group
        </button>
      </div>

      {isLoading ? (
        <div className="flex h-64 items-center justify-center">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
        </div>
      ) : error ? (
        <div className="mt-6 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Error loading groups: {error.message}
        </div>
      ) : groups?.length === 0 ? (
        <div className="mt-6 flex flex-col items-center rounded-xl border border-surface-800 bg-surface-900 py-12 shadow-lg">
          <FolderTree className="h-8 w-8 text-slate-600" />
          <p className="mt-2 text-sm text-slate-500">No groups yet. Create one to organize your devices.</p>
        </div>
      ) : (
        <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {groups?.map((group) => (
            <div key={group.id} className="rounded-xl border border-surface-800 bg-surface-900 p-5 shadow-lg">
              <div className="flex items-start justify-between">
                <div>
                  <h3 className="text-base font-semibold text-white">{group.name}</h3>
                  {group.description && (
                    <p className="mt-1 text-sm text-slate-500">{group.description}</p>
                  )}
                </div>
                <span className="rounded-full bg-surface-800 px-2.5 py-0.5 text-xs font-medium text-slate-300">
                  {group.deviceCount} devices
                </span>
              </div>
              <div className="mt-4 flex justify-end gap-2">
                <button
                  onClick={() => { setEditingGroup(group); setIsModalOpen(true) }}
                  className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-surface-800 hover:text-white"
                >
                  <Pencil className="h-3.5 w-3.5" />
                  Edit
                </button>
                <button
                  onClick={() => {
                    if (confirm(`Delete group "${group.name}"? Devices will be unassigned.`))
                      deleteMutation.mutate(group.id)
                  }}
                  className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-red-500/10 hover:text-red-400"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                  Delete
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {isModalOpen && (
        <GroupModal group={editingGroup} onClose={() => setIsModalOpen(false)} />
      )}
    </div>
  )
}

function GroupModal({
  group,
  onClose,
}: {
  group: { id: string; name: string; description?: string } | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(group?.name ?? '')
  const [description, setDescription] = useState(group?.description ?? '')

  const mutation = useMutation({
    mutationFn: (data: { name: string; description?: string }) =>
      group ? groupsApi.update(group.id, data) : groupsApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['deviceGroups'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">
          {group ? 'Edit Group' : 'Create Group'}
        </h2>
        <form
          onSubmit={(e) => { e.preventDefault(); mutation.mutate({ name, description }) }}
          className="mt-4 space-y-4"
        >
          <div>
            <label className="block text-sm font-medium text-slate-300">Name</label>
            <input
              type="text"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500"
              placeholder="e.g. Lobby Kiosks"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Description</label>
            <input
              type="text"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              className="mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500"
              placeholder="Optional description"
            />
          </div>
          <div className="flex justify-end gap-3">
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
          {mutation.isError && (
            <p className="text-sm text-red-400">{mutation.error.message}</p>
          )}
        </form>
      </div>
    </div>
  )
}
