import { useQuery } from '@tanstack/react-query'
import { analyticsApi } from '@/lib/api'
import type { DeviceConnectivityDto } from '@/lib/api'
import { ChevronDown, ChevronRight, Wifi, WifiOff, FolderTree } from 'lucide-react'
import { useState, useMemo } from 'react'

export function ConnectivityChart() {
  const [hours, setHours] = useState(24)
  const [expandedDeviceId, setExpandedDeviceId] = useState<string | null>(null)
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set())

  const { data, isLoading } = useQuery({
    queryKey: ['connectivity', hours],
    queryFn: () => analyticsApi.connectivity(hours, hours <= 6 ? 15 : hours <= 24 ? 30 : 60),
    refetchInterval: 60_000,
  })

  // Group devices by groupName
  const grouped = useMemo(() => {
    const devices = data?.devices ?? []
    const map = new Map<string, DeviceConnectivityDto[]>()

    for (const d of devices) {
      const key = d.groupName || 'Ungrouped'
      if (!map.has(key)) map.set(key, [])
      map.get(key)!.push(d)
    }

    // Sort groups alphabetically, devices within groups by name
    return [...map.entries()]
      .sort(([a], [b]) => a === 'Ungrouped' ? 1 : b === 'Ungrouped' ? -1 : a.localeCompare(b))
      .map(([name, devs]) => ({
        name,
        devices: devs.sort((a, b) => a.deviceName.localeCompare(b.deviceName)),
        avgUptime: devs.length > 0 ? devs.reduce((sum, d) => sum + d.uptimePercent, 0) / devs.length : 0,
        onlineCount: devs.filter(d => d.currentStatus === 'Online').length,
      }))
  }, [data])

  const toggleGroup = (name: string) => {
    setExpandedGroups(prev => {
      const next = new Set(prev)
      if (next.has(name)) next.delete(name)
      else next.add(name)
      return next
    })
  }

  const expandAll = () => setExpandedGroups(new Set(grouped.map(g => g.name)))
  const collapseAll = () => setExpandedGroups(new Set())

  if (isLoading) {
    return (
      <div className="flex h-32 items-center justify-center">
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
      </div>
    )
  }

  if (grouped.length === 0) {
    return <p className="py-8 text-center text-sm text-slate-500">No connectivity data yet. Data accumulates as the ping monitor runs.</p>
  }

  return (
    <div>
      {/* Controls */}
      <div className="mb-3 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className="text-xs text-slate-500">Range:</span>
          {[6, 24, 72, 168].map((h) => (
            <button
              key={h}
              onClick={() => setHours(h)}
              className={`rounded-md px-2 py-1 text-xs font-medium transition-colors ${
                hours === h
                  ? 'bg-accent-500/20 text-accent-300'
                  : 'text-slate-400 hover:bg-surface-800 hover:text-white'
              }`}
            >
              {h === 6 ? '6h' : h === 24 ? '24h' : h === 72 ? '3d' : '7d'}
            </button>
          ))}
        </div>
        <div className="flex gap-2">
          <button onClick={expandAll} className="text-xs text-accent-400 hover:text-accent-300">Expand all</button>
          <button onClick={collapseAll} className="text-xs text-slate-500 hover:text-slate-300">Collapse all</button>
        </div>
      </div>

      {/* Groups */}
      <div className="space-y-2">
        {grouped.map((group) => {
          const isExpanded = expandedGroups.has(group.name)
          return (
            <div key={group.name} className="rounded-lg border border-surface-800 overflow-hidden">
              {/* Group header */}
              <button
                onClick={() => toggleGroup(group.name)}
                className="flex w-full items-center gap-3 px-3 py-2.5 text-left transition-colors hover:bg-surface-850"
              >
                {isExpanded
                  ? <ChevronDown className="h-4 w-4 shrink-0 text-slate-400" />
                  : <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" />
                }
                <FolderTree className="h-4 w-4 shrink-0 text-slate-500" />
                <span className="text-sm font-medium text-white">{group.name}</span>
                <span className="text-xs text-slate-500">
                  {group.devices.length} device{group.devices.length !== 1 ? 's' : ''}
                </span>

                <div className="flex-1" />

                {/* Group avg uptime */}
                <span className={`rounded px-1.5 py-0.5 text-xs font-bold ${
                  group.avgUptime >= 99 ? 'bg-emerald-500/20 text-emerald-400'
                  : group.avgUptime >= 90 ? 'bg-amber-500/20 text-amber-400'
                  : 'bg-red-500/20 text-red-400'
                }`}>
                  {group.avgUptime.toFixed(0)}%
                </span>

                {/* Online count */}
                <span className="flex items-center gap-1 text-xs text-emerald-400">
                  <Wifi className="h-3 w-3" />
                  {group.onlineCount}/{group.devices.length}
                </span>

                {/* Group-level mini bar: aggregate all devices */}
                <div className="hidden sm:flex gap-px w-32 shrink-0">
                  {group.devices[0]?.slots.map((_, i) => {
                    // A slot is online for the group if ANY device was online
                    const anyOnline = group.devices.some(d => d.slots[i]?.status === 'online')
                    const anyData = group.devices.some(d => d.slots[i]?.status !== 'unknown')
                    return (
                      <div
                        key={i}
                        className={`h-4 flex-1 rounded-[1px] ${
                          anyOnline ? 'bg-emerald-500' : anyData ? 'bg-red-500' : 'bg-surface-700'
                        }`}
                      />
                    )
                  })}
                </div>
              </button>

              {/* Devices within group */}
              {isExpanded && (
                <div className="border-t border-surface-800">
                  {group.devices.map((device) => (
                    <DeviceRow
                      key={device.deviceId}
                      device={device}
                      isExpanded={expandedDeviceId === device.deviceId}
                      onToggle={() => setExpandedDeviceId(
                        expandedDeviceId === device.deviceId ? null : device.deviceId
                      )}
                    />
                  ))}
                </div>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}

function DeviceRow({
  device,
  isExpanded,
  onToggle,
}: {
  device: DeviceConnectivityDto
  isExpanded: boolean
  onToggle: () => void
}) {
  const slots = device.slots

  return (
    <div className="border-b border-surface-800/50 last:border-0">
      {/* Device row */}
      <button
        onClick={onToggle}
        className="flex w-full items-center gap-3 px-3 py-2 pl-10 text-left transition-colors hover:bg-surface-850"
      >
        {/* Uptime % */}
        <span
          className={`w-14 shrink-0 rounded px-1.5 py-0.5 text-center text-xs font-bold ${
            device.uptimePercent >= 99
              ? 'bg-emerald-500/20 text-emerald-400'
              : device.uptimePercent >= 90
                ? 'bg-amber-500/20 text-amber-400'
                : 'bg-red-500/20 text-red-400'
          }`}
        >
          {device.uptimePercent.toFixed(0)}%
        </span>

        {isExpanded ? (
          <ChevronDown className="h-3.5 w-3.5 shrink-0 text-slate-500" />
        ) : (
          <ChevronRight className="h-3.5 w-3.5 shrink-0 text-slate-500" />
        )}

        <span className="min-w-0 flex-1 truncate text-sm font-medium text-white">
          {device.deviceName}
        </span>

        <span className={`flex items-center gap-1 text-xs ${
          device.currentStatus === 'Online' ? 'text-emerald-400' : 'text-red-400'
        }`}>
          {device.currentStatus === 'Online' ? <Wifi className="h-3 w-3" /> : <WifiOff className="h-3 w-3" />}
          {device.currentStatus}
        </span>
      </button>

      {/* Bar chart */}
      <div className="px-3 pb-2 pl-[5.5rem]">
        <div className="flex gap-px">
          {slots.map((slot, i) => (
            <div
              key={i}
              className={`h-4 flex-1 rounded-[1px] ${
                slot.status === 'online'
                  ? 'bg-emerald-500'
                  : slot.status === 'offline'
                    ? 'bg-red-500'
                    : 'bg-surface-700'
              }`}
              title={`${formatSlotTime(slot.timestamp)} — ${slot.status}${slot.avgResponseMs ? ` (${slot.avgResponseMs}ms)` : ''}`}
            />
          ))}
        </div>
        <div className="mt-0.5 flex justify-between text-[10px] text-slate-600">
          <span>{formatSlotTime(slots[0]?.timestamp)}</span>
          <span>{formatSlotTime(slots[Math.floor(slots.length / 2)]?.timestamp)}</span>
          <span>{formatSlotTime(slots[slots.length - 1]?.timestamp)}</span>
        </div>
      </div>

      {/* Expanded detail */}
      {isExpanded && (
        <div className="border-t border-surface-800/50 bg-surface-850/50 px-3 py-3 pl-[5.5rem]">
          <div className="grid grid-cols-4 gap-2 text-xs">
            <div>
              <span className="text-slate-500">Uptime:</span>{' '}
              <span className="font-medium text-emerald-400">{device.uptimePercent}%</span>
            </div>
            <div>
              <span className="text-slate-500">Online:</span>{' '}
              <span className="text-slate-300">{slots.filter(s => s.status === 'online').length} slots</span>
            </div>
            <div>
              <span className="text-slate-500">Offline:</span>{' '}
              <span className="text-red-400">{slots.filter(s => s.status === 'offline').length} slots</span>
            </div>
            <div>
              <span className="text-slate-500">No data:</span>{' '}
              <span className="text-slate-300">{slots.filter(s => s.status === 'unknown').length} slots</span>
            </div>
          </div>
          <div className="mt-2 flex gap-px">
            {slots.map((slot, i) => (
              <div
                key={i}
                className={`h-8 flex-1 rounded-[1px] ${
                  slot.status === 'online'
                    ? 'bg-emerald-500'
                    : slot.status === 'offline'
                      ? 'bg-red-500'
                      : 'bg-surface-700'
                }`}
                title={`${formatSlotTime(slot.timestamp)} — ${slot.status}${slot.avgResponseMs ? ` (${slot.avgResponseMs}ms)` : ''}`}
              />
            ))}
          </div>
          <div className="mt-1 flex justify-between text-[10px] text-slate-600">
            <span>{formatSlotTime(slots[0]?.timestamp)}</span>
            <span>{formatSlotTime(slots[Math.floor(slots.length / 4)]?.timestamp)}</span>
            <span>{formatSlotTime(slots[Math.floor(slots.length / 2)]?.timestamp)}</span>
            <span>{formatSlotTime(slots[Math.floor(slots.length * 3 / 4)]?.timestamp)}</span>
            <span>{formatSlotTime(slots[slots.length - 1]?.timestamp)}</span>
          </div>
        </div>
      )}
    </div>
  )
}

function formatSlotTime(iso?: string): string {
  if (!iso) return ''
  try {
    const d = new Date(iso)
    return d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: false })
  } catch {
    return ''
  }
}
