import { useQuery } from '@tanstack/react-query'
import { telemetryApi, devicesApi, alertsApi, deploymentsApi } from '@/lib/api'
import { Monitor, CalendarClock, FolderOpen, AlertTriangle, BellRing } from 'lucide-react'
import { Link } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { DeviceMap } from '@/components/device-map'
import { ConnectivityChart } from '@/components/connectivity-chart'

export function DashboardPage() {

  const { data: summary, isLoading } = useQuery({
  queryKey: ['telemetry-summary'],
  queryFn: telemetryApi.summary,
  staleTime: 15_000,
  })

  const { data: allDevices } = useQuery({
  queryKey: ['devices', 'all'],
  queryFn: devicesApi.listAll,
  staleTime: 30_000,
  })

  const { data: alertCount } = useQuery({
    queryKey: ['alerts', 'count'],
    queryFn: alertsApi.count,
    refetchInterval: 15_000,
  })

  const queryClient = useQueryClient()

  const rollbackMutation = useMutation({
    mutationFn: (deploymentId: string) => deploymentsApi.rollback(deploymentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['recent-deployments'] })
      queryClient.invalidateQueries({ queryKey: ['deployments'] })
    },
  })

  const { data: recentAlerts } = useQuery({
    queryKey: ['alerts', 'recent'],
    queryFn: () => alertsApi.list({ status: 'Active', limit: 5 }),
    refetchInterval: 15_000,
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
      label: 'Active Alerts',
      value: alertCount?.active,
      icon: BellRing,
      accent: (alertCount?.critical ?? 0) > 0 ? 'text-red-400' : 'text-amber-400',
      ring: (alertCount?.critical ?? 0) > 0 ? 'from-red-500/20' : 'from-amber-500/20',
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

      const { data: recentDeployments } = useQuery({
      queryKey: ['recent-deployments'],
      queryFn: () => deploymentsApi.list({ limit: 5 }),
      })

  return (
    <div>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-white">Dashboard</h1>
          <p className="mt-1 text-sm text-slate-400">Fleet overview and live status</p>
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

      {/* Device map */}
      <div className="mt-8">
        <h2 className="mb-3 text-base font-semibold text-white">Device Locations</h2>
        <DeviceMap devices={allDevices ?? []} />
        </div>

        {/* Connectivity timeline */}
        <div className="mt-8">
        <h2 className="mb-3 text-base font-semibold text-white">Connectivity</h2>
        <div className="rounded-xl border border-surface-800 bg-surface-900 p-4 shadow-lg">
          <ConnectivityChart />
        </div>
        </div>

      {/* Recent deployments */}
      {(recentDeployments?.length ?? 0) > 0 && (
        <div className="mt-8">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-base font-semibold text-white">Recent Deployments</h2>
            <Link to="/content" className="text-sm text-accent-400 hover:text-accent-300">
              Manage →
            </Link>
          </div>
          <div className="space-y-2">
            {recentDeployments?.map((dep) => {
              const total = dep.results.length
              const completed = dep.results.filter(r => r.status === 'Completed').length
              const failed = dep.results.filter(r => r.status === 'Failed').length
              const inProgress = dep.results.filter(r => r.status === 'InProgress' || r.status === 'Pending').length
              return (
                <div
                  key={dep.id}
                  className="flex items-center gap-3 rounded-xl border border-surface-800 bg-surface-900 px-4 py-3 shadow-lg"
                >
                  <span
                    className={`h-2 w-2 shrink-0 rounded-full ${
                      dep.status === 'Completed' ? 'bg-emerald-400'
                      : dep.status === 'Failed' ? 'bg-red-400'
                      : dep.status === 'InProgress' ? 'bg-blue-400 animate-pulse'
                      : 'bg-slate-500'
                    }`}
                  />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium text-slate-100">{dep.name}</p>
                    <p className="truncate text-xs text-slate-500">
                      {dep.contentName} v{dep.contentVersion} · {completed}/{total} done
                      {failed > 0 && ` · ${failed} failed`}
                      {inProgress > 0 && ` · ${inProgress} in progress`}
                    </p>
                  </div>
                  {/* Progress bar */}
                  <div className="w-24 shrink-0">
                    <div className="h-1.5 rounded-full bg-surface-700 overflow-hidden">
                      <div
                        className={`h-full rounded-full transition-all ${
                          failed > 0 ? 'bg-red-500' : 'bg-emerald-500'
                        }`}
                        style={{ width: `${total > 0 ? (completed / total) * 100 : 0}%` }}
                      />
                    </div>
                  </div>
                  <span
                    className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ${
                      dep.status === 'Completed'
                        ? 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30'
                        : dep.status === 'Failed'
                          ? 'bg-red-500/10 text-red-400 ring-red-500/30'
                          : dep.status === 'InProgress'
                            ? 'bg-blue-500/10 text-blue-400 ring-blue-500/30'
                            : dep.status === 'Scheduled'
                              ? 'bg-amber-500/10 text-amber-400 ring-amber-500/30'
                              : 'bg-slate-500/10 text-slate-400 ring-slate-500/30'
                    }`}
                  >
                    {dep.status}
                  </span>
                  {(dep.status === 'Completed' || dep.status === 'PartiallyCompleted') && (
                    <button
                      onClick={() => rollbackMutation.mutate(dep.id)}
                      disabled={rollbackMutation.isPending}
                      className="shrink-0 rounded-lg border border-surface-700 px-2 py-1 text-xs text-slate-400 hover:bg-surface-800 hover:text-white transition-colors"
                      title="Rollback to previous version"
                    >
                      ↩ Rollback
                    </button>
                  )}
                  </div>
              )
            })}
          </div>
        </div>
      )}

      {/* Recent alerts */}
      {(recentAlerts?.alerts?.length ?? 0) > 0 && (
        <div className="mt-8">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-base font-semibold text-white">Recent Alerts</h2>
            <Link to="/alerts" className="text-sm text-accent-400 hover:text-accent-300">
              View all →
            </Link>
          </div>
          <div className="space-y-2">
            {recentAlerts?.alerts.map((alert) => (
              <div
                key={alert.id}
                className="flex items-center gap-3 rounded-xl border border-surface-800 bg-surface-900 px-4 py-3 shadow-lg"
              >
                <span
                  className={`h-2 w-2 shrink-0 rounded-full ${
                    alert.severity === 'Critical'
                      ? 'bg-red-400'
                      : alert.severity === 'Warning'
                        ? 'bg-amber-400'
                        : 'bg-blue-400'
                  }`}
                />
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-slate-100">{alert.title}</p>
                  <p className="truncate text-xs text-slate-500">
                    {alert.deviceName} · {new Date(alert.raisedAt).toLocaleTimeString()}
                  </p>
                </div>
                <span
                  className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ${
                    alert.severity === 'Critical'
                      ? 'bg-red-500/10 text-red-400 ring-red-500/30'
                      : alert.severity === 'Warning'
                        ? 'bg-amber-500/10 text-amber-400 ring-amber-500/30'
                        : 'bg-blue-500/10 text-blue-400 ring-blue-500/30'
                  }`}
                >
                  {alert.severity}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Device health cards */}
      <div className="mt-8">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-base font-semibold text-white">Device Health</h2>
          <Link to="/devices" className="text-sm text-accent-400 hover:text-accent-300">
            View all →
          </Link>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          {allDevices?.map((device) => (
            <Link
              key={device.id}
              to={`/devices/${device.id}`}
              className="rounded-xl border border-surface-800 bg-surface-900 p-4 shadow-lg transition-colors hover:border-surface-700 hover:bg-surface-850"
            >
              <div className="flex items-center justify-between mb-3">
                <div>
                  <p className="text-sm font-medium text-slate-100">{device.name}</p>
                  <p className="text-xs text-slate-500">{device.hostname || device.ipAddress || '—'}</p>
                </div>
                <StatusBadge status={device.status} />
              </div>
              <div className="grid grid-cols-4 gap-2">
                <HealthChip label="CPU" value={device.cpuPercent} unit="%" warn={80} />
                <HealthChip label="Mem" value={device.memoryPercent} unit="%" warn={85} />
                <HealthChip label="Disk" value={device.diskFreeGb} unit=" GB" invert warn={10} />
                <HealthChip label="Up" value={device.uptimeSeconds} format="uptime" />
              </div>
              {device.lastSeenAt && (
                <p className="mt-2 text-xs text-slate-600">
                  Last seen {new Date(device.lastSeenAt).toLocaleTimeString()}
                </p>
              )}
            </Link>
          ))}
        </div>
        {allDevices?.length === 0 && (
          <div className="py-12 text-center text-sm text-slate-500">No devices registered yet.</div>
        )}
      </div>

      {/* Telemetry sparklines */}
      {summary && summary.telemetryPointsLast24h > 0 && (
        <div className="mt-8">
          <h2 className="mb-3 text-base font-semibold text-white">Fleet Telemetry (24h)</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            {allDevices
              ?.filter((d) => d.status === 'Online')
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

function HealthChip({ label, value, unit = '', warn, invert, format }: {
  label: string; value?: number | null; unit?: string; warn?: number; invert?: boolean; format?: string
}) {
  if (value == null) {
    return (
      <div className="rounded-lg bg-surface-800 px-2 py-1.5 text-center">
        <p className="text-xs text-slate-600">{label}</p>
        <p className="text-xs font-mono text-slate-600">—</p>
      </div>
    )
  }

  let display: string
  if (format === 'uptime') {
    const h = Math.floor(value / 3600)
    const m = Math.floor((value % 3600) / 60)
    display = h > 24 ? `${Math.floor(h / 24)}d` : h > 0 ? `${h}h` : `${m}m`
  } else {
    display = `${value % 1 !== 0 ? value.toFixed(1) : value}${unit}`
  }

  const isBad = invert ? value < (warn ?? 0) : value > (warn ?? Infinity)

  return (
    <div className={`rounded-lg px-2 py-1.5 text-center ${isBad ? 'bg-red-500/10' : 'bg-surface-800'}`}>
      <p className={`text-xs ${isBad ? 'text-red-400' : 'text-slate-500'}`}>{label}</p>
      <p className={`text-xs font-mono font-medium ${isBad ? 'text-red-400' : 'text-slate-200'}`}>{display}</p>
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
    queryFn: () => telemetryApi.deviceSeries(deviceId),
    refetchInterval: 60_000,
  })

  const cpuSeries = series?.find((s) => s.metricName === 'cpu_percent' || s.metricName === 'cpu_usage')
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
