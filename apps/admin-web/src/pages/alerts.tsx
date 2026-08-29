import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { alertsApi } from '@/lib/api'
import { useState } from 'react'
import { BellRing, CheckCircle2, Eye } from 'lucide-react'
import { clsx } from 'clsx'

export function AlertsPage() {
  const queryClient = useQueryClient()
  const [severityFilter, setSeverityFilter] = useState<string>('')
  const [statusFilter, setStatusFilter] = useState<string>('Active')

  const { data, isLoading, error } = useQuery({
    queryKey: ['alerts', severityFilter, statusFilter],
    queryFn: () => alertsApi.list({
      severity: severityFilter || undefined,
      status: statusFilter || undefined,
      limit: 100,
    }),
    refetchInterval: 15000,
  })

  const ackMutation = useMutation({
    mutationFn: alertsApi.acknowledge,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['alerts'] }),
  })
  const resolveMutation = useMutation({
    mutationFn: alertsApi.resolve,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['alerts'] }),
  })

  return (
    <div>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-white">Alerts</h1>
          <p className="mt-1 text-sm text-slate-400">
            Fleet health alerts {data ? `— ${data.activeCount} active` : ''}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <select
            value={severityFilter}
            onChange={(e) => setSeverityFilter(e.target.value)}
            className="rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500"
          >
            <option value="">All severities</option>
            <option value="Critical">Critical</option>
            <option value="Warning">Warning</option>
            <option value="Info">Info</option>
          </select>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500"
          >
            <option value="">All statuses</option>
            <option value="Active">Active</option>
            <option value="Acknowledged">Acknowledged</option>
            <option value="Resolved">Resolved</option>
          </select>
        </div>
      </div>

      {isLoading ? (
        <div className="flex h-64 items-center justify-center">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
        </div>
      ) : error ? (
        <div className="mt-6 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Error loading alerts: {error.message}
        </div>
      ) : (
        <div className="mt-6 overflow-hidden rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
          <table className="min-w-full divide-y divide-surface-800">
            <thead>
              <tr className="bg-surface-850">
                {['Severity', 'Alert', 'Device', 'Raised', 'Status', 'Actions'].map((h) => (
                  <th key={h} className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wider text-slate-500 last:text-right">
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-surface-800">
              {data?.alerts.map((alert) => (
                <tr key={alert.id} className="transition-colors hover:bg-surface-850">
                  <td className="px-6 py-4 whitespace-nowrap">
                    <SeverityBadge severity={alert.severity} />
                  </td>
                  <td className="px-6 py-4">
                    <div className="text-sm font-medium text-slate-100">{alert.title}</div>
                    {alert.message && <div className="text-xs text-slate-500">{alert.message}</div>}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-400">{alert.deviceName}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-400">
                    {new Date(alert.raisedAt).toLocaleString()}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <StatusBadge status={alert.status} autoResolved={alert.autoResolved} />
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-right">
                    {alert.status === 'Active' && (
                      <button
                        onClick={() => ackMutation.mutate(alert.id)}
                        className="mr-2 inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-surface-800 hover:text-white"
                      >
                        <Eye className="h-3.5 w-3.5" />
                        Ack
                      </button>
                    )}
                    {alert.status !== 'Resolved' && (
                      <button
                        onClick={() => resolveMutation.mutate(alert.id)}
                        className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-emerald-500/10 hover:text-emerald-400"
                      >
                        <CheckCircle2 className="h-3.5 w-3.5" />
                        Resolve
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {data?.alerts.length === 0 && (
            <div className="flex flex-col items-center py-12 text-center">
              <BellRing className="h-8 w-8 text-slate-600" />
              <p className="mt-2 text-sm text-slate-500">No alerts match the current filters.</p>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function SeverityBadge({ severity }: { severity: string }) {
  const styles: Record<string, string> = {
    Critical: 'bg-red-500/10 text-red-400 ring-red-500/30',
    Warning: 'bg-amber-500/10 text-amber-400 ring-amber-500/30',
    Info: 'bg-blue-500/10 text-blue-400 ring-blue-500/30',
  }
  return (
    <span className={clsx('inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1', styles[severity] ?? styles.Info)}>
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      {severity}
    </span>
  )
}

function StatusBadge({ status, autoResolved }: { status: string; autoResolved: boolean }) {
  const styles: Record<string, string> = {
    Active: 'bg-red-500/10 text-red-400 ring-red-500/30',
    Acknowledged: 'bg-amber-500/10 text-amber-400 ring-amber-500/30',
    Resolved: 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30',
  }
  return (
    <span className={clsx('inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1', styles[status] ?? styles.Active)}>
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      {status}{status === 'Resolved' && autoResolved ? ' (auto)' : ''}
    </span>
  )
}
