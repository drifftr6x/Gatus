import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { groupsApi, devicesApi, alertsApi } from '@/lib/api'
import { useState, useMemo } from 'react'
import { Plus, Pencil, Trash2, FolderTree, ChevronDown, ChevronRight, Monitor, Search, ArrowUpDown, ArrowUp, ArrowDown } from 'lucide-react'
import { Link } from 'react-router-dom'

type SortField = 'name' | 'devices' | 'alerts' | 'online'
type SortDir = 'asc' | 'desc'

export function GroupsPage() {
  const queryClient = useQueryClient()
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [editingGroup, setEditingGroup] = useState<{ id: string; name: string; description?: string } | null>(null)
  const [expandedGroupId, setExpandedGroupId] = useState<string | null>(null)
  const [sortField, setSortField] = useState<SortField>('name')
  const [sortDir, setSortDir] = useState<SortDir>('asc')
  const [searchFilter, setSearchFilter] = useState('')

  const { data: groups, isLoading, error } = useQuery({
    queryKey: ['deviceGroups'],
    queryFn: groupsApi.list,
  })

  const { data: allDevices } = useQuery({
    queryKey: ['devices', 'all'],
    queryFn: () => devicesApi.list({ pageSize: 500 }),
  })

  const { data: alertsData } = useQuery({
    queryKey: ['alerts', 'active'],
    queryFn: () => alertsApi.list({ status: 'Active', limit: 500 }),
  })

  const deleteMutation = useMutation({
    mutationFn: groupsApi.delete,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['deviceGroups'] }),
  })

  const toggleExpand = (groupId: string) => {
    setExpandedGroupId(expandedGroupId === groupId ? null : groupId)
  }

  const toggleSort = (field: SortField) => {
    if (sortField === field) {
      setSortDir(sortDir === 'asc' ? 'desc' : 'asc')
    } else {
      setSortField(field)
      setSortDir(field === 'name' ? 'asc' : 'desc')
    }
  }

  // Compute per-group stats and sort
  const sortedGroups = useMemo(() => {
    if (!groups) return []

    const devices = allDevices?.devices ?? []
    const alerts = alertsData?.alerts ?? []

    const enriched = groups.map(g => {
      const gDevices = devices.filter(d => d.groupId === g.id)
      const gAlerts = alerts.filter(a => gDevices.some(d => d.id === a.deviceId))
      const online = gDevices.filter(d => d.status === 'Online').length
      return { ...g, _devices: gDevices.length, _alerts: gAlerts.length, _online: online, _offline: gDevices.length - online }
    })

    // Filter
    const filtered = searchFilter
      ? enriched.filter(g => g.name.toLowerCase().includes(searchFilter.toLowerCase()) || g.description?.toLowerCase().includes(searchFilter.toLowerCase()))
      : enriched

    // Sort
    const sorted = [...filtered].sort((a, b) => {
      let cmp = 0
      switch (sortField) {
        case 'name': cmp = a.name.localeCompare(b.name); break
        case 'devices': cmp = a._devices - b._devices; break
        case 'alerts': cmp = a._alerts - b._alerts; break
        case 'online': cmp = a._online - b._online; break
      }
      return sortDir === 'asc' ? cmp : -cmp
    })

    return sorted
  }, [groups, allDevices, alertsData, sortField, sortDir, searchFilter])

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

      {/* Toolbar: search + sort */}
      <div className="mt-4 flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-[200px] max-w-sm">
          <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-500" />
          <input
            type="text"
            value={searchFilter}
            onChange={(e) => setSearchFilter(e.target.value)}
            placeholder="Filter groups…"
            className="w-full rounded-lg border border-surface-700 bg-surface-850 py-2 pl-9 pr-3 text-sm text-white placeholder-slate-500 outline-none focus:border-accent-500"
          />
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-xs text-slate-500">Sort:</span>
          {([
            { field: 'name' as SortField, label: 'Name' },
            { field: 'devices' as SortField, label: 'Devices' },
            { field: 'alerts' as SortField, label: 'Alerts' },
            { field: 'online' as SortField, label: 'Online' },
          ]).map(({ field, label }) => (
            <button
              key={field}
              onClick={() => toggleSort(field)}
              className={`inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium transition-colors ${
                sortField === field
                  ? 'bg-accent-500/20 text-accent-300'
                  : 'text-slate-400 hover:bg-surface-800 hover:text-white'
              }`}
            >
              {label}
              {sortField === field ? (
                sortDir === 'asc' ? <ArrowUp className="h-3 w-3" /> : <ArrowDown className="h-3 w-3" />
              ) : (
                <ArrowUpDown className="h-3 w-3 opacity-40" />
              )}
            </button>
          ))}
        </div>
      </div>

      {isLoading ? (
        <div className="flex h-64 items-center justify-center">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
        </div>
      ) : error ? (
        <div className="mt-6 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Error loading groups: {error.message}
        </div>
      ) : sortedGroups.length === 0 ? (
        <div className="mt-6 flex flex-col items-center rounded-xl border border-surface-800 bg-surface-900 py-12 shadow-lg">
          <FolderTree className="h-8 w-8 text-slate-600" />
          <p className="mt-2 text-sm text-slate-500">
            {searchFilter ? 'No groups match your filter.' : 'No groups yet. Create one to organize your devices.'}
          </p>
        </div>
      ) : (
        <div className="mt-4 space-y-3">
          {sortedGroups.map((group) => {
            const isExpanded = expandedGroupId === group.id
            const groupDevices = allDevices?.devices.filter(d => d.groupId === group.id) ?? []

            return (
              <div key={group.id} className="rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
                {/* Group header — clickable to expand */}
                <button
                  onClick={() => toggleExpand(group.id)}
                  className="flex w-full items-center justify-between p-5 text-left transition-colors hover:bg-surface-850"
                >
                  <div className="flex items-center gap-3">
                    {isExpanded
                      ? <ChevronDown className="h-4 w-4 text-slate-400" />
                      : <ChevronRight className="h-4 w-4 text-slate-400" />
                    }
                    <div>
                      <h3 className="text-base font-semibold text-white">{group.name}</h3>
                      {group.description && (
                        <p className="mt-0.5 text-sm text-slate-500">{group.description}</p>
                      )}
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="rounded-full bg-surface-800 px-2.5 py-0.5 text-xs font-medium text-slate-300">
                      {group.deviceCount} device{group.deviceCount !== 1 ? 's' : ''}
                    </span>
                    {(group as any)._online > 0 && (
                      <span className="rounded-full bg-emerald-500/10 px-2.5 py-0.5 text-xs font-medium text-emerald-400 ring-1 ring-emerald-500/30">
                        {(group as any)._online} online
                      </span>
                    )}
                    {(group as any)._alerts > 0 && (
                      <span className="rounded-full bg-red-500/10 px-2.5 py-0.5 text-xs font-medium text-red-400 ring-1 ring-red-500/30">
                        {(group as any)._alerts} alert{(group as any)._alerts !== 1 ? 's' : ''}
                      </span>
                    )}
                    <div className="flex gap-1" onClick={(e) => e.stopPropagation()}>
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
                </button>

                {/* Expanded device list */}
                {isExpanded && (
                  <div className="border-t border-surface-800">
                    {groupDevices.length === 0 ? (
                      <p className="px-5 py-4 text-sm text-slate-500">No devices in this group.</p>
                    ) : (
                      <table className="min-w-full divide-y divide-surface-800">
                        <thead>
                          <tr className="bg-surface-850">
                            <th className="px-5 py-2.5 text-left text-xs font-semibold uppercase tracking-wider text-slate-500">Device</th>
                            <th className="px-5 py-2.5 text-left text-xs font-semibold uppercase tracking-wider text-slate-500">Hostname</th>
                            <th className="px-5 py-2.5 text-left text-xs font-semibold uppercase tracking-wider text-slate-500">IP</th>
                            <th className="px-5 py-2.5 text-left text-xs font-semibold uppercase tracking-wider text-slate-500">Status</th>
                            <th className="px-5 py-2.5 text-left text-xs font-semibold uppercase tracking-wider text-slate-500">Last Seen</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-surface-800">
                          {groupDevices.map((device) => (
                            <tr key={device.id} className="transition-colors hover:bg-surface-850">
                              <td className="px-5 py-2.5">
                                <Link
                                  to={`/devices/${device.id}`}
                                  className="flex items-center gap-2 text-sm font-medium text-white hover:text-accent-400"
                                >
                                  <Monitor className="h-3.5 w-3.5 text-slate-500" />
                                  {device.name}
                                </Link>
                              </td>
                              <td className="px-5 py-2.5 text-sm text-slate-400">{device.hostname || '—'}</td>
                              <td className="px-5 py-2.5 font-mono text-sm text-slate-400">{device.ipAddress || '—'}</td>
                              <td className="px-5 py-2.5">
                                <GroupDeviceStatus status={device.status} />
                              </td>
                              <td className="px-5 py-2.5 text-sm text-slate-500">
                                {device.lastSeenAt ? new Date(device.lastSeenAt).toLocaleString() : 'Never'}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    )}
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}

      {isModalOpen && (
        <GroupModal group={editingGroup} onClose={() => setIsModalOpen(false)} />
      )}
    </div>
  )
}

function GroupDeviceStatus({ status }: { status: string }) {
  const styles: Record<string, string> = {
    Online: 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30',
    Offline: 'bg-slate-500/10 text-slate-400 ring-slate-500/30',
    Error: 'bg-red-500/10 text-red-400 ring-red-500/30',
    Maintenance: 'bg-amber-500/10 text-amber-400 ring-amber-500/30',
  }
  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ${styles[status] ?? styles.Offline}`}>
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      {status}
    </span>
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
  const [deviceSearch, setDeviceSearch] = useState('')
  const [selectedDeviceIds, setSelectedDeviceIds] = useState<Set<string>>(new Set())
  const [originalDeviceIds, setOriginalDeviceIds] = useState<Set<string>>(new Set())

  const { data: allDevices } = useQuery({
    queryKey: ['devices', 'all'],
    queryFn: () => devicesApi.list({ pageSize: 500 }),
  })

  // Initialize selected devices from group's current devices
  useState(() => {
    if (group && allDevices?.devices) {
      const groupDevices = allDevices.devices.filter(d => d.groupId === group.id).map(d => d.id)
      setSelectedDeviceIds(new Set(groupDevices))
      setOriginalDeviceIds(new Set(groupDevices))
    }
  })

  // Also update when allDevices loads (async)
  if (group && allDevices?.devices && originalDeviceIds.size === 0) {
    const groupDevices = allDevices.devices.filter(d => d.groupId === group.id).map(d => d.id)
    if (groupDevices.length > 0) {
      setSelectedDeviceIds(new Set(groupDevices))
      setOriginalDeviceIds(new Set(groupDevices))
    }
  }

  const mutation = useMutation({
    mutationFn: async (data: { name: string; description?: string }) => {
      // Save group info
      const result = group
        ? await groupsApi.update(group.id, data)
        : await groupsApi.create(data)

      const groupId = group?.id ?? result.id

      // Sync device assignments
      const toAdd = [...selectedDeviceIds].filter(id => !originalDeviceIds.has(id))
      const toRemove = [...originalDeviceIds].filter(id => !selectedDeviceIds.has(id))

      if (toAdd.length > 0) {
        await devicesApi.bulkAssignGroup({ deviceIds: toAdd, groupId })
      }
      if (toRemove.length > 0) {
        await devicesApi.bulkAssignGroup({ deviceIds: toRemove, groupId: null })
      }

      return result
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['deviceGroups'] })
      queryClient.invalidateQueries({ queryKey: ['devices'] })
      onClose()
    },
  })

  const filteredDevices = (allDevices?.devices ?? []).filter(d =>
    !deviceSearch ||
    d.name.toLowerCase().includes(deviceSearch.toLowerCase()) ||
    d.hostname?.toLowerCase().includes(deviceSearch.toLowerCase()) ||
    d.ipAddress?.includes(deviceSearch)
  )

  const toggleDevice = (deviceId: string) => {
    const next = new Set(selectedDeviceIds)
    if (next.has(deviceId)) next.delete(deviceId)
    else next.add(deviceId)
    setSelectedDeviceIds(next)
  }

  const toggleAll = () => {
    if (selectedDeviceIds.size === filteredDevices.length) {
      setSelectedDeviceIds(new Set())
    } else {
      setSelectedDeviceIds(new Set(filteredDevices.map(d => d.id)))
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
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

          {/* Device picker */}
          <div>
            <div className="flex items-center justify-between">
              <label className="block text-sm font-medium text-slate-300">
                Devices
                {selectedDeviceIds.size > 0 && (
                  <span className="ml-2 rounded-full bg-accent-500/20 px-2 py-0.5 text-xs text-accent-300">
                    {selectedDeviceIds.size} selected
                  </span>
                )}
              </label>
              <button
                type="button"
                onClick={toggleAll}
                className="text-xs text-accent-400 hover:text-accent-300"
              >
                {selectedDeviceIds.size === filteredDevices.length && filteredDevices.length > 0 ? 'Deselect all' : 'Select all'}
              </button>
            </div>

            {/* Search */}
            <div className="relative mt-2">
              <Search className="absolute left-2.5 top-2.5 h-3.5 w-3.5 text-slate-500" />
              <input
                type="text"
                value={deviceSearch}
                onChange={(e) => setDeviceSearch(e.target.value)}
                className="w-full rounded-lg border border-surface-700 bg-surface-850 py-2 pl-8 pr-3 text-sm text-white outline-none placeholder:text-slate-500 focus:border-accent-500"
                placeholder="Search devices…"
              />
            </div>

            {/* Device list */}
            <div className="mt-2 max-h-48 overflow-y-auto rounded-lg border border-surface-700">
              {filteredDevices.length === 0 ? (
                <p className="px-3 py-4 text-center text-xs text-slate-500">No devices found</p>
              ) : (
                filteredDevices.map((d) => {
                  const isSelected = selectedDeviceIds.has(d.id)
                  return (
                    <label
                      key={d.id}
                      className={`flex cursor-pointer items-center gap-3 border-b border-surface-800 px-3 py-2 transition-colors last:border-0 ${
                        isSelected ? 'bg-accent-500/10' : 'hover:bg-surface-850'
                      }`}
                    >
                      <input
                        type="checkbox"
                        checked={isSelected}
                        onChange={() => toggleDevice(d.id)}
                        className="h-4 w-4 rounded border-surface-600 bg-surface-800 text-accent-500 focus:ring-accent-500"
                      />
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm text-white">{d.name}</p>
                        <p className="truncate text-xs text-slate-500">
                          {d.hostname || d.ipAddress || '—'}
                          {d.groupName && d.groupId !== group?.id && (
                            <span className="ml-1 text-amber-400">· in {d.groupName}</span>
                          )}
                        </p>
                      </div>
                      <span className={`h-2 w-2 shrink-0 rounded-full ${
                        d.status === 'Online' ? 'bg-emerald-400' :
                        d.status === 'Error' ? 'bg-red-400' :
                        d.status === 'Maintenance' ? 'bg-amber-400' : 'bg-slate-500'
                      }`} />
                    </label>
                  )
                })
              )}
            </div>
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
              disabled={mutation.isPending || !name.trim()}
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
