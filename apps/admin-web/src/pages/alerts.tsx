import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { alertsApi, escalationApi } from '@/lib/api'
import type { AlertRuleDto } from '@/lib/api'
import { useState } from 'react'
import { BellRing, CheckCircle2, Eye, Plus, Pencil, Trash2, Settings2, ArrowUpRight } from 'lucide-react'
import { clsx } from 'clsx'

export function AlertsPage() {
  const queryClient = useQueryClient()
  const [severityFilter, setSeverityFilter] = useState<string>('')
  const [statusFilter, setStatusFilter] = useState<string>('Active')
  const [showRules, setShowRules] = useState(false)

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
          <button
            onClick={() => setShowRules(!showRules)}
            className={clsx(
              'flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
              showRules
                ? 'bg-accent-500/10 text-accent-400 border border-accent-500/30'
                : 'border border-surface-700 text-slate-300 hover:bg-surface-800'
            )}
          >
            <Settings2 className="h-4 w-4" />
            Rules
          </button>
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

      {showRules && <RulesSection />}

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
                    {(alert.escalationStep ?? 0) > 0 && (
                      <span className="ml-2 inline-flex items-center gap-1 rounded-full bg-amber-500/10 px-2 py-0.5 text-xs font-medium text-amber-400 ring-1 ring-amber-500/30">
                        <ArrowUpRight className="h-3 w-3" />
                        Step {alert.escalationStep}
                      </span>
                    )}
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

const METRICS = [
  { value: 'cpu', label: 'CPU %' },
  { value: 'memory', label: 'Memory %' },
  { value: 'disk', label: 'Disk % (free below)' },
  { value: 'offline', label: 'Offline (minutes)' },
  { value: 'domain_join', label: 'Domain join mismatch' },
  { value: 'domain_trust', label: 'Domain trust broken' },
]

function RulesSection() {
  const queryClient = useQueryClient()
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [editingRule, setEditingRule] = useState<AlertRuleDto | null>(null)

  const { data: rules } = useQuery({
    queryKey: ['alertRules'],
    queryFn: alertsApi.rules,
  })

  const deleteMutation = useMutation({
    mutationFn: alertsApi.deleteRule,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['alertRules'] }),
  })

  return (
    <div className="mt-6 rounded-xl border border-surface-800 bg-surface-900 p-5 shadow-lg">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-sm font-semibold text-white">Alert Rules</h2>
          <p className="mt-0.5 text-xs text-slate-500">Rules are evaluated every 30 seconds against device state.</p>
        </div>
        <button
          onClick={() => { setEditingRule(null); setIsModalOpen(true) }}
          className="flex items-center gap-1.5 rounded-lg bg-accent-500 px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-accent-400"
        >
          <Plus className="h-3.5 w-3.5" />
          Add Rule
        </button>
      </div>

      <div className="mt-4 space-y-2">
        {rules?.map((rule) => (
          <div key={rule.id} className="flex items-center justify-between rounded-lg border border-surface-800 bg-surface-850 px-4 py-2.5">
            <div className="flex items-center gap-3">
              <SeverityBadge severity={rule.severity} />
              <div>
                <div className="flex items-center gap-2">
                  <span className="text-sm font-medium text-white">{rule.name}</span>
                  {!rule.isEnabled && (
                    <span className="rounded-full bg-red-500/10 px-2 py-0.5 text-xs text-red-400">Disabled</span>
                  )}
                </div>
                <p className="text-xs text-slate-500">
                  {METRICS.find(m => m.value === rule.metric)?.label ?? rule.metric} {rule.operator} {rule.threshold}
                  {' · '}cooldown {rule.cooldownMinutes}m
                  {rule.escalationPolicyName && ` · escalates via "${rule.escalationPolicyName}"`}
                </p>
              </div>
            </div>
            <div className="flex items-center gap-1">
              <button
                onClick={() => { setEditingRule(rule); setIsModalOpen(true) }}
                className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs text-slate-400 transition-colors hover:bg-surface-800 hover:text-white"
              >
                <Pencil className="h-3 w-3" />
                Edit
              </button>
              <button
                onClick={() => { if (confirm(`Delete rule "${rule.name}"?`)) deleteMutation.mutate(rule.id) }}
                className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs text-slate-400 transition-colors hover:bg-red-500/10 hover:text-red-400"
              >
                <Trash2 className="h-3 w-3" />
              </button>
            </div>
          </div>
        ))}
        {rules?.length === 0 && (
          <p className="py-4 text-center text-xs text-slate-500">No rules configured.</p>
        )}
      </div>

      {isModalOpen && (
        <RuleModal rule={editingRule} onClose={() => setIsModalOpen(false)} />
      )}
    </div>
  )
}

function RuleModal({ rule, onClose }: { rule: AlertRuleDto | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(rule?.name ?? '')
  const [metric, setMetric] = useState(rule?.metric ?? 'cpu')
  const [operator, setOperator] = useState(rule?.operator ?? 'gt')
  const [threshold, setThreshold] = useState(rule?.threshold ?? 90)
  const [severity, setSeverity] = useState(rule?.severity ?? 'Warning')
  const [isEnabled, setIsEnabled] = useState(rule?.isEnabled ?? true)
  const [cooldownMinutes, setCooldownMinutes] = useState(rule?.cooldownMinutes ?? 15)
  const [escalationPolicyId, setEscalationPolicyId] = useState(rule?.escalationPolicyId ?? '')

  const { data: policies } = useQuery({
    queryKey: ['escalationPolicies'],
    queryFn: escalationApi.list,
  })

  const mutation = useMutation({
    mutationFn: () => {
      if (rule) {
        return alertsApi.updateRule(rule.id, {
          name, threshold, severity, isEnabled, cooldownMinutes,
          escalationPolicyId: escalationPolicyId || undefined,
        })
      }
      return alertsApi.createRule({
        name, metric, operator, threshold, severity, cooldownMinutes,
        escalationPolicyId: escalationPolicyId || undefined,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['alertRules'] })
      onClose()
    },
  })

  const inputCls = 'mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">{rule ? 'Edit Rule' : 'Create Alert Rule'}</h2>
        <form onSubmit={(e) => { e.preventDefault(); mutation.mutate() }} className="mt-4 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-300">Name</label>
            <input type="text" required value={name} onChange={(e) => setName(e.target.value)} className={inputCls} placeholder="e.g. High CPU" />
          </div>
          {!rule && (
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-sm font-medium text-slate-300">Metric</label>
                <select value={metric} onChange={(e) => setMetric(e.target.value)} className={inputCls}>
                  {METRICS.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-300">Operator</label>
                <select value={operator} onChange={(e) => setOperator(e.target.value)} className={inputCls}>
                  <option value="gt">Greater than</option>
                  <option value="lt">Less than</option>
                </select>
              </div>
            </div>
          )}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-slate-300">Threshold</label>
              <input type="number" step="any" required value={threshold} onChange={(e) => setThreshold(parseFloat(e.target.value) || 0)} className={inputCls} />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300">Severity</label>
              <select value={severity} onChange={(e) => setSeverity(e.target.value)} className={inputCls}>
                <option value="Info">Info</option>
                <option value="Warning">Warning</option>
                <option value="Critical">Critical</option>
              </select>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-slate-300">Cooldown (minutes)</label>
              <input
                type="number" min={1} required value={cooldownMinutes}
                onChange={(e) => setCooldownMinutes(parseInt(e.target.value) || 15)}
                className={inputCls}
              />
              <p className="mt-1 text-xs text-slate-500">Min time between re-notifications</p>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300">Escalation policy</label>
              <select
                value={escalationPolicyId}
                onChange={(e) => setEscalationPolicyId(e.target.value)}
                className={inputCls}
              >
                <option value="">None</option>
                {policies?.filter(p => p.isEnabled).map(p => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
              <p className="mt-1 text-xs text-slate-500">Escalates unacknowledged alerts</p>
            </div>
          </div>
          {rule && (
            <div className="flex items-center gap-2">
              <input type="checkbox" id="ruleEnabled" checked={isEnabled} onChange={(e) => setIsEnabled(e.target.checked)} className="h-4 w-4 rounded border-surface-600 bg-surface-800 text-accent-500 focus:ring-accent-500" />
              <label htmlFor="ruleEnabled" className="text-sm text-slate-300">Enabled</label>
            </div>
          )}
          <div className="flex justify-end gap-3">
            <button type="button" onClick={onClose} className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 transition-colors hover:bg-surface-800">Cancel</button>
            <button type="submit" disabled={mutation.isPending} className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400 disabled:opacity-50">
              {mutation.isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
          {mutation.isError && <p className="text-sm text-red-400">{mutation.error.message}</p>}
        </form>
      </div>
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
