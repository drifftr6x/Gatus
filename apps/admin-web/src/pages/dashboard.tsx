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
      color: 'text-green-600',
    },
    {
      label: 'Devices in Error',
      value: summary?.devicesInError,
      icon: AlertTriangle,
      color: 'text-red-600',
    },
    {
      label: 'Active Schedules',
      value: summary?.activeSchedules,
      icon: CalendarClock,
      color: 'text-blue-600',
    },
    {
      label: 'Content Items',
      value: summary?.activeContent,
      icon: FolderOpen,
      color: 'text-purple-600',
    },
  ]

  return (
    <div>
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-slate-900">Dashboard</h1>
        <div className="flex items-center gap-2 text-sm">
          {isConnected ? (
            <>
              <Wifi className="h-4 w-4 text-green-600" />
              <span className="text-green-600">Live</span>
            </>
          ) : (
            <>
              <WifiOff className="h-4 w-4 text-slate-400" />
              <span className="text-slate-400">Connecting…</span>
            </>
          )}
        </div>
      </div>

      <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((stat) => (
          <div
            key={stat.label}
            className="rounded-lg border border-slate-200 bg-white p-4"
          >
            <div className="flex items-center justify-between">
              <p className="text-sm font-medium text-slate-500">{stat.label}</p>
              <stat.icon className={`h-5 w-5 ${stat.color}`} />
            </div>
            <p className="mt-1 text-3xl font-semibold text-slate-900">
              {isLoading ? '—' : (stat.value ?? 0)}
              {stat.total !== undefined && (
                <span className="text-lg font-normal text-slate-400"> / {stat.total}</span>
              )}
            </p>
          </div>
        ))}
      </div>

      <div className="mt-8">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-slate-900">Device Status</h2>
          <Link to="/devices" className="text-sm text-slate-600 hover:text-slate-900">
            View all →
          </Link>
        </div>
        <div className="bg-white shadow rounded-lg overflow-hidden">
          <table className="min-w-full divide-y divide-slate-200">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                  Device
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                  Status
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                  Location
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 uppercase tracking-wider">
                  Last Seen
                </th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-slate-200">
              {devicesData?.devices.map((device) => (
                <tr key={device.id}>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm font-medium text-slate-900">{device.name}</div>
                    <div className="text-xs text-slate-400">{device.serialNumber}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <span
                      className={`px-2 inline-flex text-xs leading-5 font-semibold rounded-full ${
                        device.status === 'Online'
                          ? 'bg-green-100 text-green-800'
                          : device.status === 'Offline'
                            ? 'bg-slate-100 text-slate-800'
                            : device.status === 'Error'
                              ? 'bg-red-100 text-red-800'
                              : 'bg-yellow-100 text-yellow-800'
                      }`}
                    >
                      {device.status}
                    </span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500">
                    {device.location || '-'}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500">
                    {device.lastSeenAt
                      ? new Date(device.lastSeenAt).toLocaleString()
                      : 'Never'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {devicesData?.devices.length === 0 && (
            <div className="text-center py-8 text-slate-500">
              No devices registered yet.
            </div>
          )}
        </div>
      </div>

      {summary && summary.telemetryPointsLast24h > 0 && (
        <div className="mt-8">
          <h2 className="text-lg font-semibold text-slate-900 mb-4">Fleet Telemetry (24h)</h2>
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
  if (values.length > 1) {
    const min = 0
    const max = Math.max(100, ...values)
    const xStep = (width - padding * 2) / (values.length - 1)
    const yScale = (height - padding * 2) / (max - min)
    path = values
      .map((v, i) => {
        const x = padding + i * xStep
        const y = height - padding - (v - min) * yScale
        return `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`
      })
      .join(' ')
  }

  const latest = values.length > 0 ? values[values.length - 1] : null

  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4">
      <div className="flex items-center justify-between mb-2">
        <p className="text-sm font-medium text-slate-700">{deviceName}</p>
        <p className="text-sm text-slate-500">
          CPU: {latest !== null ? `${latest.toFixed(0)}%` : '—'}
        </p>
      </div>
      <svg viewBox={`0 0 ${width} ${height}`} className="w-full h-20">
        {path ? (
          <path d={path} fill="none" stroke="#0f172a" strokeWidth="2" />
        ) : (
          <text x={width / 2} y={height / 2} textAnchor="middle" className="fill-slate-400 text-xs">
            No data
          </text>
        )}
      </svg>
    </div>
  )
}
