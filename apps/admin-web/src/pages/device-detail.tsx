import { useParams, Link, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft, Monitor, Cpu, MemoryStick, HardDrive, Clock,
  Globe, Tag, MapPin, Fingerprint, Wifi, AlertTriangle, Terminal, Activity, Trash2
} from 'lucide-react'
import { devicesApi, telemetryApi, alertsApi, commandsApi } from '@/lib/api'

export function DeviceDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { data: device, isLoading } = useQuery({
    queryKey: ['device', id],
    queryFn: () => devicesApi.get(id!),
    enabled: !!id,
    refetchInterval: 30_000,
  })

  const { data: telemetry } = useQuery({
    queryKey: ['device-telemetry', id],
    queryFn: () => telemetryApi.deviceSeries(id!),
    enabled: !!id,
    refetchInterval: 60_000,
  })

  const { data: deviceAlerts } = useQuery({
    queryKey: ['device-alerts', id],
    queryFn: () => alertsApi.list({ deviceId: id!, limit: 10 }),
    enabled: !!id,
  })

  const { data: deviceCommands } = useQuery({
    queryKey: ['device-commands', id],
    queryFn: () => commandsApi.history({ deviceId: id!, limit: 10 }),
    enabled: !!id,
  })

  const deleteMutation = useMutation({
    mutationFn: () => devicesApi.delete(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['devices'] })
      navigate('/devices')
    },
  })

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
      </div>
    )
  }

  if (!device) {
    return (
      <div className="py-12 text-center">
        <Monitor size={48} className="mx-auto mb-4 text-slate-700" />
        <h2 className="text-lg font-medium text-slate-300">Device not found</h2>
        <Link to="/devices" className="mt-2 inline-block text-sm text-accent-300 hover:text-accent-400">
          ← Back to devices
        </Link>
      </div>
    )
  }

  const statusColors: Record<string, string> = {
    Online: 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30',
    Offline: 'bg-slate-500/10 text-slate-400 ring-slate-500/30',
    Maintenance: 'bg-amber-500/10 text-amber-400 ring-amber-500/30',
    Error: 'bg-red-500/10 text-red-400 ring-red-500/30',
  }

  return (
    <div>
      {/* Header */}
      <div className="flex items-center gap-4">
        <Link
          to="/devices"
          className="rounded-lg border border-surface-700 p-2 text-slate-400 transition-colors hover:bg-surface-800 hover:text-slate-200"
        >
          <ArrowLeft size={18} />
        </Link>
        <div className="flex-1">
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-semibold text-white">{device.name}</h1>
            <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ${statusColors[device.status] || statusColors.Offline}`}>
              <span className="h-1.5 w-1.5 rounded-full bg-current" />
              {device.status}
            </span>
          </div>
          <p className="mt-1 text-sm text-slate-400">
            {device.hostname || device.ipAddress || 'No network info'}
            {device.lastSeenAt && ` · Last seen ${new Date(device.lastSeenAt).toLocaleString()}`}
          </p>
        </div>
      </div>

      {/* Metrics cards */}
      <div className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-6">
        <MetricCard icon={<Cpu size={18} />} label="CPU" value={device.cpuPercent} unit="%" warn={80} />
        <MetricCard icon={<MemoryStick size={18} />} label="Memory" value={device.memoryPercent} unit="%" warn={85} />
        <MetricCard icon={<HardDrive size={18} />} label="Disk Free" value={device.diskFreeGb} unit=" GB" warn={10} invert />
        <MetricCard icon={<Clock size={18} />} label="Uptime" value={device.uptimeSeconds} format="uptime" />
        <MetricCard icon={<Activity size={18} />} label="Telemetry" value={telemetry?.length ?? 0} unit=" metrics" />
        <MetricCard icon={<AlertTriangle size={18} />} label="Alerts" value={deviceAlerts?.totalCount ?? 0} unit="" warn={1} />
      </div>

      {/* Details + Telemetry side by side */}
      <div className="mt-8 grid gap-8 lg:grid-cols-3">
        {/* Device info */}
        <div className="rounded-xl border border-surface-800 bg-surface-900 p-5 shadow-lg">
          <h2 className="text-base font-semibold text-white">Device Info</h2>
          <dl className="mt-4 space-y-3 text-sm">
            <InfoRow icon={<Globe size={14} />} label="Hostname" value={device.hostname} />
            <InfoRow icon={<Wifi size={14} />} label="IP Address" value={device.ipAddress} mono />
            <InfoRow icon={<Fingerprint size={14} />} label="Serial Number" value={device.serialNumber} mono />
            <InfoRow icon={<MapPin size={14} />} label="Location" value={device.location} />
            <InfoRow icon={<Tag size={14} />} label="Group" value={device.groupName} />
            <InfoRow icon={<Tag size={14} />} label="Tags" value={device.tags} />
            <InfoRow icon={<Monitor size={14} />} label="OS" value={device.osVersion} />
            <InfoRow icon={<Terminal size={14} />} label="Firmware" value={device.firmwareVersion} />
            <InfoRow icon={<Clock size={14} />} label="Added" value={new Date(device.createdAt).toLocaleDateString()} />
          </dl>
        </div>

        {/* Telemetry charts */}
        <div className="lg:col-span-2 rounded-xl border border-surface-800 bg-surface-900 p-5 shadow-lg">
          <h2 className="text-base font-semibold text-white">Telemetry</h2>
          {telemetry && telemetry.length > 0 ? (
            <div className="mt-4 space-y-4">
              {telemetry
                .filter(s => ['cpu_usage', 'memory_usage', 'disk_free_percent', 'disk_free_mb', 'uptime', 'uptime_seconds'].includes(s.metricName))
                .slice(0, 4)
                .map((series) => (
                  <TelemetryChart key={series.metricName} series={series} />
                ))}
              {telemetry.filter(s => !['cpu_usage', 'memory_usage', 'disk_free_percent', 'disk_free_mb', 'uptime', 'uptime_seconds'].includes(s.metricName)).length > 0 && (
                <div className="mt-4 rounded-lg bg-surface-850 p-3">
                  <h3 className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">Other Metrics</h3>
                  <div className="grid grid-cols-2 gap-2">
                    {telemetry
                      .filter(s => !['cpu_usage', 'memory_usage', 'disk_free_percent', 'disk_free_mb', 'uptime', 'uptime_seconds'].includes(s.metricName))
                      .map(s => {
                        const vals = (s.points ?? []).map((p: any) => p.value)
                        const latest = vals.length > 0 ? vals[vals.length - 1] : null
                        return (
                          <div key={s.metricName} className="text-sm">
                            <span className="text-slate-500">{s.metricName}: </span>
                            <span className="text-slate-200 font-mono">{latest ?? '—'}{s.unit ? ` ${s.unit}` : ''}</span>
                          </div>
                        )
                      })}
                  </div>
                </div>
              )}
            </div>
          ) : (
            <p className="mt-4 py-8 text-center text-sm text-slate-500">
              No telemetry data. Install the agent to collect metrics.
            </p>
          )}
        </div>
      </div>

      {/* Alerts + Commands side by side */}
      <div className="mt-8 grid gap-8 lg:grid-cols-2">
        {/* Alerts */}
        <div className="rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
          <div className="border-b border-surface-800 px-5 py-3">
            <h2 className="text-base font-semibold text-white">Alerts</h2>
          </div>
          <div className="divide-y divide-surface-800">
            {deviceAlerts?.alerts?.length ? (
              deviceAlerts.alerts.map((alert: any) => (
                <div key={alert.id} className="flex items-center gap-3 px-5 py-3">
                  <span className={`h-2 w-2 rounded-full ${
                    alert.severity === 'Critical' ? 'bg-red-500' : 'bg-amber-500'
                  }`} />
                  <div className="flex-1 min-w-0">
                    <p className="text-sm text-slate-200 truncate">{alert.title}</p>
                    <p className="text-xs text-slate-500">
                      {alert.status} · {new Date(alert.raisedAt).toLocaleString()}
                    </p>
                  </div>
                  <span className={`rounded-md px-2 py-0.5 text-xs font-medium ring-1 ${
                    alert.status === 'Active'
                      ? 'bg-red-500/10 text-red-400 ring-red-500/30'
                      : alert.status === 'Acknowledged'
                        ? 'bg-amber-500/10 text-amber-400 ring-amber-500/30'
                        : 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30'
                  }`}>
                    {alert.status}
                  </span>
                </div>
              ))
            ) : (
              <p className="px-5 py-6 text-center text-sm text-slate-500">No alerts for this device</p>
            )}
          </div>
        </div>

        {/* Commands */}
        <div className="rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
          <div className="border-b border-surface-800 px-5 py-3">
            <h2 className="text-base font-semibold text-white">Command History</h2>
          </div>
          <div className="divide-y divide-surface-800">
            {deviceCommands?.commands?.length ? (
              deviceCommands.commands.map((cmd: any) => (
                <div key={cmd.id} className="flex items-center gap-3 px-5 py-3">
                  <Terminal size={14} className="text-slate-500 shrink-0" />
                  <div className="flex-1 min-w-0">
                    <p className="text-sm text-slate-200">{cmd.type}</p>
                    <p className="text-xs text-slate-500">
                      {new Date(cmd.createdAt).toLocaleString()}
                      {cmd.completedAt && ` · Completed ${new Date(cmd.completedAt).toLocaleString()}`}
                    </p>
                  </div>
                  <span className={`rounded-md px-2 py-0.5 text-xs font-medium ring-1 ${
                    cmd.status === 'Succeeded'
                      ? 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30'
                      : cmd.status === 'Failed'
                        ? 'bg-red-500/10 text-red-400 ring-red-500/30'
                        : 'bg-slate-500/10 text-slate-400 ring-slate-500/30'
                  }`}>
                    {cmd.status}
                  </span>
                </div>
              ))
            ) : (
              <p className="px-5 py-6 text-center text-sm text-slate-500">No commands sent to this device</p>
              )}
              </div>
              </div>
              </div>

              {/* Danger Zone */}
              <div className="mt-8 rounded-xl border border-red-500/30 bg-red-500/5 p-5">
              <div className="flex items-center justify-between">
              <div>
              <h2 className="text-base font-semibold text-red-400">Danger Zone</h2>
              <p className="mt-1 text-sm text-slate-400">
                Permanently delete this device and all associated telemetry, alerts, commands, and deployment history.
              </p>
              </div>
              <button
              onClick={() => {
                if (confirm(`Delete device "${device.name}"? This will remove all associated data and cannot be undone.`)) {
                  deleteMutation.mutate()
                }
              }}
              disabled={deleteMutation.isPending}
              className="flex items-center gap-2 rounded-lg border border-red-500/50 px-4 py-2 text-sm font-medium text-red-400 transition-colors hover:bg-red-500/10 disabled:opacity-50"
              >
              <Trash2 className="h-4 w-4" />
              {deleteMutation.isPending ? 'Deleting…' : 'Delete Device'}
              </button>
              </div>
              {deleteMutation.isError && (
              <p className="mt-2 text-sm text-red-400">{deleteMutation.error.message}</p>
              )}
              </div>
              </div>
              )
              }

function MetricCard({ icon, label, value, unit = '', warn, invert, format }: {
  icon: React.ReactNode; label: string; value?: number | null; unit?: string
  warn?: number; invert?: boolean; format?: string
}) {
  if (value == null) {
    return (
      <div className="rounded-xl border border-surface-800 bg-surface-900 p-4 shadow-lg">
        <div className="flex items-center gap-2 text-slate-500">{icon}<span className="text-xs font-medium">{label}</span></div>
        <p className="mt-2 text-lg text-slate-600">—</p>
      </div>
    )
  }

  let display: string
  if (format === 'uptime') {
    const h = Math.floor(value / 3600)
    const m = Math.floor((value % 3600) / 60)
    display = h > 24 ? `${Math.floor(h / 24)}d ${h % 24}h` : h > 0 ? `${h}h ${m}m` : `${m}m`
  } else {
    display = `${value % 1 !== 0 ? value.toFixed(1) : value}${unit}`
  }

  const isBad = invert ? value < (warn ?? 0) : value > (warn ?? Infinity)

  return (
    <div className="rounded-xl border border-surface-800 bg-surface-900 p-4 shadow-lg">
      <div className="flex items-center gap-2 text-slate-500">{icon}<span className="text-xs font-medium">{label}</span></div>
      <p className={`mt-2 text-lg font-semibold ${isBad ? 'text-red-400' : 'text-slate-100'}`}>
        {display}
      </p>
    </div>
  )
}

function InfoRow({ icon, label, value, mono }: {
  icon: React.ReactNode; label: string; value?: string | null; mono?: boolean
}) {
  return (
    <div className="flex items-center justify-between">
      <dt className="flex items-center gap-2 text-slate-500">{icon}{label}</dt>
      <dd className={`text-slate-200 ${mono ? 'font-mono text-xs' : ''}`}>{value || '—'}</dd>
    </div>
  )
}

function TelemetryChart({ series }: { series: any }) {
  const points = series.points ?? []
  const values = points.map((p: any) => parseFloat(p.value)).filter((v: number) => !isNaN(v))

  if (values.length === 0) {
    return (
      <div className="rounded-lg bg-surface-850 p-3">
        <div className="flex items-center justify-between">
          <span className="text-xs font-medium text-slate-400">{series.metricName}</span>
          <span className="text-sm text-slate-600">No data</span>
        </div>
      </div>
    )
  }

  const latest = values[values.length - 1]
  const min = Math.min(...values)
  const max = Math.max(...values)
  const range = max - min || 1
  const w = 300
  const h = 40

  if (values.length < 2) {
    return (
      <div className="rounded-lg bg-surface-850 p-3">
        <div className="flex items-center justify-between">
          <span className="text-xs font-medium text-slate-400">{series.metricName}</span>
          <span className="text-sm font-mono text-slate-200">{latest}{series.unit ? ` ${series.unit}` : ''}</span>
        </div>
      </div>
    )
  }

  const path = values.map((v: number, i: number) => {
    const x = (i / (values.length - 1)) * w
    const y = h - ((v - min) / range) * h
    return `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`
  }).join(' ')

  return (
    <div className="rounded-lg bg-surface-850 p-3">
      <div className="flex items-center justify-between mb-2">
        <span className="text-xs font-medium text-slate-400">{series.metricName}</span>
        <div className="text-right">
          <span className="text-sm font-mono text-slate-200">{latest}{series.unit ? ` ${series.unit}` : ''}</span>
          <span className="text-xs text-slate-500 ml-2">min {min} / max {max}</span>
        </div>
      </div>
      <svg viewBox={`0 0 ${w} ${h}`} className="w-full h-10">
        <path d={path} fill="none" stroke="currentColor" strokeWidth="1.5" className="text-accent-400" />
      </svg>
    </div>
  )
}
