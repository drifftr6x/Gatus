import { useQuery } from '@tanstack/react-query'
import { telemetryApi, devicesApi } from '@/lib/api'
import { useSignalR } from '@/hooks/useSignalR'
import { Monitor, CalendarClock, FolderOpen, AlertTriangle, Wifi, WifiOff } from 'lucide-react'
import { Link } from 'react-router-dom'

export function DashboardPage() {
  const { isConnected } = useSignalR()

  const { data: summary, isLoading } = useQuery({
    queryKey: ['telemetry-summary'],
    queryFn: telemetryApi.summary,
    refetchInterval: 30_000,
  })

  const { data: devicesData } = useQuery({
    queryKey: ['devices'],
    queryFn: () => devicesApi.list({ pageSize: 10 }),
  })

  const stats = [
    {
      label: 'Online Devices',
      value: summary?.onlineDevices,
      total: summary?.totalDevices,
      icon: Monitor,
      accent: 'text-emerald-400',
      ring: 'from-emerald-500/20',
    },
    {
      label: 'Devices in Error',
      value: summary?.devicesInError,
      icon: AlertTriangle,
      accent: 'text-red-400',
      ring: 'from-red-500/20',
    },
    {
      label: 'Active Schedules',
      value: summary?.activeSchedules,
      icon: CalendarClock,
      accent: 'text-sky-400',
      ring: 'from-sky-500/20',
    },
    {
      label: 'Content Items',
      value: summary?.activeContent,
      icon: FolderOpen,
      accent: 'text-violet-400',
      ring: 'from-violet-500/20',
    },
  ]

  return (
    <div>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-white">Dashboard</h1>
          <p className="mt-1 text-sm text-slate-400">Fleet overview and live status</p>
        </div>
        <div
          className={`flex items-center gap-2 rounded-full px-3 py-1 text-xs font-medium ${
            isConnected
              ? 'bg-emerald-500/10 text-emerald-400 ring-1 ring-emerald-500/30'
              : 'bg-slate-500/10 text-slate-400 ring-1 ring-slate-500/30'
          }`}
        >
          {isConnected ? <Wifi className="h-3.5 w-3.5" /> : <WifiOff className="h-3.5 w-3.5" />}
          {isConnected ? 'Live' : 'Connecting…'}
        </div>
      </div>

      {/* Stat cards */}
      <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((stat) => (
          <div
            key={stat.label}
            className="rounded-xl border border-surface-800 bg-surface-900 p-5 shadow-lg"
          >
            <div className="flex items-center justify-between">
              <p className="text-sm font-medium text-slate-400">{stat.label}</p>
              <div className={`rounded-lg bg-gradient-to-br ${stat.ring} to-transparent p-2`}>
                <stat.icon className={`h-5 w-5 ${stat.accent}`} />
              </div>
            </div>
            <p className="mt-2 text-3xl font-semibold tracking-tight text-white">
              {isLoading ? '—' : (stat.value ?? 0)}
              {stat.total !== undefined && (
                <span className="text-lg font-normal text-slate-500"> / {stat.total}</span>
              )}
            </p>
          </div>
        ))}
      </div>

      {/* Device status table */}
      <div className="mt-8">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-base font-semibold text-white">Device Status</h2>
          <Link to="/devices" className="text-sm text-accent-400 hover:text-accent-300">
            View all →
          </Link>
        </div>
        <div className="overflow-hidden rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
          <table className="min-w-full divide-y divide-surface-800">
            <thead>
              <tr className="bg-surface-850">
                {['Device', 'Status', 'Location', 'Last Seen'].map((h) => (
                  <th
                    key={h}
                    className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wider text-slate-500"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-surface-800">
              {devicesData?.devices.map((device) => (
                <tr key={device.id} className="transition-colors hover:bg-surface-850">
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm font-medium text-slate-100">{device.name}</div>
                    <div className="text-xs text-slate-500">{device.serialNumber}</div>
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
                </tr>
              ))}
            </tbody>
          </table>
          {devicesData?.devices.length === 0 && (
            <div className="py-12 text-center text-sm text-slate-500">No devices registered yet.</div>
          )}
        </div>
      </div>

      {/* Telemetry sparklines */}
      {summary && summary.telemetryPointsLast24h > 0 && (
        <div className="mt-8">
          <h2 className="mb-3 text-base font-semibold text-white">Fleet Telemetry (24h)</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            {devicesData?.devices
              .filter((d) => d.status === 'Online')
              .slice(0, 2)
              .map((device) => (
                <TelemetrySparkline key={device.id} deviceId={device.id} deviceName={device.name} />
              ))}
          </div>
        </div>
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

function TelemetrySparkline({ deviceId, deviceName }: { deviceId: string; deviceName: string }) {
  const { data: series } = useQuery({
    queryKey: ['telemetry', deviceId],
    queryFn: () => telemetryApi.deviceSeries(deviceId, 'cpu_percent'),
    refetchInterval: 60_000,
  })

  const cpuSeries = series?.find((s) => s.metricName === 'cpu_percent')
  const points = cpuSeries?.points ?? []
  const values = points.map((p) => parseFloat(p.value)).filter((v) => !isNaN(v))

  const width = 400
  const height = 80
  const padding = 4

  let path = ''
  let areaPath = ''
  if (values.length > 1) {
    const max = Math.max(100, ...values)
    const xStep = (width - padding * 2) / (values.length - 1)
    const yScale = (height - padding * 2) / max
    const coords = values.map((v, i) => ({
      x: padding + i * xStep,
      y: height - padding - v * yScale,
    }))
    path = coords.map((c, i) => `${i === 0 ? 'M' : 'L'}${c.x.toFixed(1)},${c.y.toFixed(1)}`).join(' ')
    areaPath = `${path} L${coords[coords.length - 1].x.toFixed(1)},${height - padding} L${padding},${height - padding} Z`
  }

  const latest = values.length > 0 ? values[values.length - 1] : null

  return (
    <div className="rounded-xl border border-surface-800 bg-surface-900 p-4 shadow-lg">
      <div className="mb-2 flex items-center justify-between">
        <p className="text-sm font-medium text-slate-200">{deviceName}</p>
        <p className="text-sm text-slate-400">
          CPU: <span className="font-semibold text-sky-400">{latest !== null ? `${latest.toFixed(0)}%` : '—'}</span>
        </p>
      </div>
      <svg viewBox={`0 0 ${width} ${height}`} className="h-20 w-full">
        <defs>
          <linearGradient id={`grad-${deviceId}`} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="#38bdf8" stopOpacity="0.35" />
            <stop offset="100%" stopColor="#38bdf8" stopOpacity="0" />
          </linearGradient>
        </defs>
        {path ? (
          <>
            <path d={areaPath} fill={`url(#grad-${deviceId})`} />
            <path d={path} fill="none" stroke="#38bdf8" strokeWidth="2" strokeLinejoin="round" />
          </>
        ) : (
          <text x={width / 2} y={height / 2} textAnchor="middle" className="fill-slate-600 text-xs">
            No data
          </text>
        )}
      </svg>
    </div>
  )
}
