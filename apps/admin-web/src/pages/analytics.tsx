import { useQuery } from '@tanstack/react-query'
import { analyticsApi } from '@/lib/api'
import { useState } from 'react'
import { Clock, TrendingUp, Activity, Monitor } from 'lucide-react'
import { clsx } from 'clsx'

export function AnalyticsPage() {
  const [uptimeDays, setUptimeDays] = useState(7)
  const [trendDays, setTrendDays] = useState(30)
  const [telemetryHours, setTelemetryHours] = useState(24)

  const { data: uptime } = useQuery({
    queryKey: ['analytics', 'uptime', uptimeDays],
    queryFn: () => analyticsApi.uptime(uptimeDays),
  })

  const { data: trends } = useQuery({
    queryKey: ['analytics', 'trends', trendDays],
    queryFn: () => analyticsApi.alertTrends(trendDays),
  })

  const { data: telemetry } = useQuery({
    queryKey: ['analytics', 'telemetry', telemetryHours],
    queryFn: () => analyticsApi.telemetry(telemetryHours),
  })

  const { data: health } = useQuery({
    queryKey: ['analytics', 'health'],
    queryFn: analyticsApi.deviceHealth,
    refetchInterval: 30_000,
  })

  return (
    <div>
      <div>
        <h1 className="text-xl font-semibold text-white">Analytics</h1>
        <p className="mt-1 text-sm text-slate-400">Fleet health reports and trends</p>
      </div>

      {/* Uptime Report */}
      <section className="mt-8">
        <div className="flex items-center justify-between mb-4">
          <h2 className="flex items-center gap-2 text-base font-semibold text-white">
            <Clock className="h-4 w-4 text-accent-400" />
            Device Uptime
          </h2>
          <select
            value={uptimeDays}
            onChange={(e) => setUptimeDays(Number(e.target.value))}
            className="rounded-lg border border-surface-700 bg-surface-850 px-3 py-1.5 text-sm text-white outline-none focus:border-accent-500"
          >
            {[7, 14, 30].map(d => <option key={d} value={d}>{d} days</option>)}
          </select>
        </div>
        <div className="rounded-xl border border-surface-800 bg-surface-900 p-5 shadow-lg">
          <div className="flex items-baseline gap-4">
            <span className="text-3xl font-semibold text-white">{uptime?.overallUptimePercent ?? '—'}%</span>
            <span className="text-sm text-slate-400">overall uptime across {uptime?.totalDevices ?? 0} devices</span>
          </div>
          <div className="mt-4 space-y-2">
            {uptime?.devices.map((d) => (
              <div key={d.deviceId} className="flex items-center gap-3">
                <span className="w-32 truncate text-sm text-slate-300">{d.deviceName}</span>
                <div className="flex-1">
                  <div className="h-4 w-full rounded-full bg-surface-800">
                    <div
                      className={clsx(
                        'h-4 rounded-full transition-all',
                        d.uptimePercent >= 95 ? 'bg-emerald-500' :
                        d.uptimePercent >= 80 ? 'bg-amber-500' : 'bg-red-500'
                      )}
                      style={{ width: `${Math.min(d.uptimePercent, 100)}%` }}
                    />
                  </div>
                </div>
                <span className={clsx(
                  'w-14 text-right text-sm font-medium',
                  d.uptimePercent >= 95 ? 'text-emerald-400' :
                  d.uptimePercent >= 80 ? 'text-amber-400' : 'text-red-400'
                )}>
                  {d.uptimePercent}%
                </span>
                <span className={clsx(
                  'w-20 text-right text-xs',
                  d.status === 'Online' ? 'text-emerald-400' :
                  d.status === 'Error' ? 'text-red-400' : 'text-slate-500'
                )}>
                  {d.status}
                </span>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Alert Trends */}
      <section className="mt-8">
        <div className="flex items-center justify-between mb-4">
          <h2 className="flex items-center gap-2 text-base font-semibold text-white">
            <TrendingUp className="h-4 w-4 text-accent-400" />
            Alert Trends
          </h2>
          <select
            value={trendDays}
            onChange={(e) => setTrendDays(Number(e.target.value))}
            className="rounded-lg border border-surface-700 bg-surface-850 px-3 py-1.5 text-sm text-white outline-none focus:border-accent-500"
          >
            {[7, 14, 30].map(d => <option key={d} value={d}>{d} days</option>)}
          </select>
        </div>
        <div className="rounded-xl border border-surface-800 bg-surface-900 p-5 shadow-lg">
          <div className="flex gap-6 mb-4">
            <div className="text-center">
              <p className="text-2xl font-semibold text-white">{trends?.totalAlerts ?? 0}</p>
              <p className="text-xs text-slate-500">Total</p>
            </div>
            <div className="text-center">
              <p className="text-2xl font-semibold text-red-400">{trends?.activeAlerts ?? 0}</p>
              <p className="text-xs text-slate-500">Active</p>
            </div>
            <div className="text-center">
              <p className="text-2xl font-semibold text-emerald-400">{trends?.resolvedAlerts ?? 0}</p>
              <p className="text-xs text-slate-500">Resolved</p>
            </div>
          </div>
          {/* SVG bar chart */}
          {trends && trends.points.length > 0 && (
            <AlertTrendChart points={trends.points} />
          )}
        </div>
      </section>

      {/* Telemetry Aggregation */}
      <section className="mt-8">
        <div className="flex items-center justify-between mb-4">
          <h2 className="flex items-center gap-2 text-base font-semibold text-white">
            <Activity className="h-4 w-4 text-accent-400" />
            Telemetry Summary
          </h2>
          <select
            value={telemetryHours}
            onChange={(e) => setTelemetryHours(Number(e.target.value))}
            className="rounded-lg border border-surface-700 bg-surface-850 px-3 py-1.5 text-sm text-white outline-none focus:border-accent-500"
          >
            {[1, 6, 24, 72].map(h => <option key={h} value={h}>{h}h</option>)}
          </select>
        </div>
        <div className="overflow-hidden rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
          <table className="min-w-full divide-y divide-surface-800">
            <thead>
              <tr className="bg-surface-850">
                {['Metric', 'Min', 'Avg', 'Max', 'Latest', 'Samples'].map(h => (
                  <th key={h} className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wider text-slate-500">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-surface-800">
              {telemetry?.metrics.map((m) => (
                <tr key={m.metricName} className="hover:bg-surface-850">
                  <td className="px-6 py-3 text-sm font-medium text-slate-200">{m.metricName}</td>
                  <td className="px-6 py-3 text-sm text-slate-400">{m.min.toFixed(1)}{m.unit}</td>
                  <td className="px-6 py-3 text-sm text-accent-400 font-medium">{m.avg.toFixed(1)}{m.unit}</td>
                  <td className="px-6 py-3 text-sm text-slate-400">{m.max.toFixed(1)}{m.unit}</td>
                  <td className="px-6 py-3 text-sm text-slate-300">{m.latest.toFixed(1)}{m.unit}</td>
                  <td className="px-6 py-3 text-sm text-slate-500">{m.sampleCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {telemetry?.metrics.length === 0 && (
            <p className="px-6 py-8 text-center text-sm text-slate-500">No telemetry data in this window</p>
          )}
        </div>
      </section>

      {/* Device Health */}
      <section className="mt-8">
        <h2 className="flex items-center gap-2 text-base font-semibold text-white mb-4">
          <Monitor className="h-4 w-4 text-accent-400" />
          Device Health
        </h2>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {health?.map((d) => (
            <div key={d.deviceId} className="rounded-xl border border-surface-800 bg-surface-900 p-4 shadow-lg">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium text-white">{d.deviceName}</span>
                <span className={clsx(
                  'rounded-full px-2 py-0.5 text-xs font-medium',
                  d.status === 'Online' ? 'bg-emerald-500/10 text-emerald-400' :
                  d.status === 'Error' ? 'bg-red-500/10 text-red-400' :
                  'bg-slate-500/10 text-slate-400'
                )}>
                  {d.status}
                </span>
              </div>
              <div className="mt-3 grid grid-cols-3 gap-2">
                <HealthMetric label="CPU" value={d.cpuAvg} unit="%" warn={80} />
                <HealthMetric label="Mem" value={d.memoryAvg} unit="%" warn={85} />
                <HealthMetric label="Disk" value={d.diskFreeAvg} unit="%" warn={10} invert />
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  )
}

function HealthMetric({ label, value, unit, warn, invert }: {
  label: string; value?: number; unit: string; warn: number; invert?: boolean
}) {
  if (value === undefined) {
    return (
      <div className="text-center">
        <p className="text-xs text-slate-500">{label}</p>
        <p className="text-sm text-slate-600">—</p>
      </div>
    )
  }
  const isBad = invert ? value < warn : value > warn
  return (
    <div className="text-center">
      <p className="text-xs text-slate-500">{label}</p>
      <p className={clsx('text-sm font-medium', isBad ? 'text-red-400' : 'text-emerald-400')}>
        {value.toFixed(0)}{unit}
      </p>
    </div>
  )
}

function AlertTrendChart({ points }: { points: { date: string; raised: number; resolved: number; critical: number }[] }) {
  const maxVal = Math.max(...points.map(p => p.raised), 1)
  const height = 120
  const barWidth = Math.max(2, Math.floor(600 / points.length) - 2)
  const width = points.length * (barWidth + 2)

  return (
    <svg viewBox={`0 0 ${width} ${height}`} className="w-full">
      {points.map((p, i) => {
        const barHeight = (p.raised / maxVal) * (height - 20)
        const x = i * (barWidth + 2)
        return (
          <g key={p.date}>
            <rect
              x={x}
              y={height - barHeight - 16}
              width={barWidth}
              height={barHeight}
              rx={2}
              className={clsx(
                p.critical > 0 ? 'fill-red-500/60' :
                p.raised > 0 ? 'fill-amber-500/60' : 'fill-surface-700'
              )}
            />
            {p.raised > 0 && (
              <text
                x={x + barWidth / 2}
                y={height - barHeight - 20}
                textAnchor="middle"
                className="fill-slate-400 text-[8px]"
              >
                {p.raised}
              </text>
            )}
          </g>
        )
      })}
      {/* X axis labels */}
      {points.filter((_, i) => i % Math.ceil(points.length / 7) === 0).map((p) => {
        const idx = points.indexOf(p)
        const x = idx * (barWidth + 2) + barWidth / 2
        return (
          <text key={p.date} x={x} y={height - 2} textAnchor="middle" className="fill-slate-500 text-[8px]">
            {p.date.slice(5)}
          </text>
        )
      })}
    </svg>
  )
}
