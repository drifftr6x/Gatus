import { useQuery } from '@tanstack/react-query'
import { analyticsApi } from '@/lib/api'
import type { DeviceConnectivityDto } from '@/lib/api'
import { ChevronDown, ChevronRight, Wifi, WifiOff } from 'lucide-react'
import { useState } from 'react'

export function ConnectivityChart() {
  const [hours, setHours] = useState(24)
  const [expandedId, setExpandedId] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['connectivity', hours],
    queryFn: () => analyticsApi.connectivity(hours, hours <= 6 ? 15 : hours <= 24 ? 30 : 60),
    refetchInterval: 60_000, // Refresh every minute
  })

  if (isLoading) {
    return (
      <div className="flex h-32 items-center justify-center">
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
      </div>
    )
  }

  const devices = data?.devices ?? []
  if (devices.length === 0) {
    return <p className="py-8 text-center text-sm text-slate-500">No connectivity data yet. Data accumulates as the ping monitor runs.</p>
  }

  return (
    <div>
      {/* Time range selector */}
      <div className="mb-3 flex items-center gap-2">
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

      <div className="space-y-1">
        {devices.map((device) => (
          <DeviceRow
            key={device.deviceId}
            device={device}
            isExpanded={expandedId === device.deviceId}
            onToggle={() => setExpandedId(expandedId === device.deviceId ? null : device.deviceId)}
            />
        ))}
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
    <div className="rounded-lg bg-surface-850/50">
      {/* Main row */}
      <button
        onClick={onToggle}
        className="flex w-full items-center gap-3 px-3 py-2 text-left transition-colors hover:bg-surface-850"
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

        {/* Expand arrow */}
        {isExpanded ? (
          <ChevronDown className="h-3.5 w-3.5 shrink-0 text-slate-500" />
        ) : (
          <ChevronRight className="h-3.5 w-3.5 shrink-0 text-slate-500" />
        )}

        {/* Name + status */}
        <div className="min-w-0 flex-1">
          <span className="text-sm font-medium text-white">{device.deviceName}</span>
          {device.groupName && (
            <span className="ml-2 text-xs text-slate-500">{device.groupName}</span>
          )}
        </div>

        {/* Current status */}
        <span className={`flex items-center gap-1 text-xs ${
          device.currentStatus === 'Online' ? 'text-emerald-400' : 'text-red-400'
        }`}>
          {device.currentStatus === 'Online' ? <Wifi className="h-3 w-3" /> : <WifiOff className="h-3 w-3" />}
          {device.currentStatus}
        </span>
      </button>

      {/* Bar chart */}
      <div className="px-3 pb-2 pl-[4.5rem]">
        <div className="flex gap-px">
          {slots.map((slot, i) => (
            <div
              key={i}
              className={`h-5 flex-1 rounded-[1px] ${
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
        {/* Time labels */}
        <div className="mt-1 flex justify-between text-[10px] text-slate-600">
          <span>{formatSlotTime(slots[0]?.timestamp)}</span>
          <span>{formatSlotTime(slots[Math.floor(slots.length / 2)]?.timestamp)}</span>
          <span>{formatSlotTime(slots[slots.length - 1]?.timestamp)}</span>
        </div>
      </div>

      {/* Expanded detail */}
      {isExpanded && (
        <div className="border-t border-surface-800 px-3 py-2 pl-[4.5rem]">
          <div className="grid grid-cols-4 gap-2 text-xs">
            <div>
              <span className="text-slate-500">Uptime:</span>{' '}
              <span className="font-medium text-emerald-400">{device.uptimePercent}%</span>
            </div>
            <div>
              <span className="text-slate-500">Online slots:</span>{' '}
              <span className="text-slate-300">{slots.filter(s => s.status === 'online').length}</span>
            </div>
            <div>
              <span className="text-slate-500">Offline slots:</span>{' '}
              <span className="text-slate-300">{slots.filter(s => s.status === 'offline').length}</span>
            </div>
            <div>
              <span className="text-slate-500">No data:</span>{' '}
              <span className="text-slate-300">{slots.filter(s => s.status === 'unknown').length}</span>
            </div>
          </div>
          {/* Larger bar chart */}
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
