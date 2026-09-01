import { useQuery, useMutation } from '@tanstack/react-query'
import { devicesApi, commandsApi } from '@/lib/api'
import { Send, RefreshCw, RotateCcw, Power, FileText, Terminal } from 'lucide-react'
import { useState } from 'react'

const actions = [
  { type: 'RefreshKiosk', label: 'Refresh Kiosk', icon: RefreshCw },
  { type: 'ReloadPolicy', label: 'Refresh Configuration', icon: RotateCcw },
  { type: 'RestartKioskRuntime', label: 'Restart Kiosk', icon: RotateCcw },
  { type: 'RebootWindows', label: 'Reboot PC', icon: Power },
  { type: 'ShutdownWindows', label: 'Shutdown', icon: Power },
  { type: 'CollectDiagnostics', label: 'Collect Logs', icon: FileText },
]

export function RemoteActionsPage() {
  const [deviceId, setDeviceId] = useState('')
  const [message, setMessage] = useState('')
  const { data: devices } = useQuery({ queryKey: ['devices', 'all'], queryFn: devicesApi.listAll, staleTime: 30_000 })
  const { data: history } = useQuery({ queryKey: ['remote-actions', deviceId], queryFn: () => commandsApi.history({ deviceId, limit: 15 }), enabled: !!deviceId })
  const action = useMutation({
    mutationFn: (type: string) => commandsApi.issue(deviceId, { type, timeoutSeconds: 120, expiresInMinutes: 10 }),
    onSuccess: (result) => setMessage(`${result.type} queued successfully.`),
    onError: (error) => setMessage(error.message),
  })

  return (
    <div>
      <h1 className="text-xl font-semibold text-white">Remote Actions</h1>
      <p className="mt-1 text-sm text-slate-400">Run essential, audited actions against an enrolled kiosk.</p>
      <div className="mt-6 rounded-xl border border-surface-800 bg-surface-900 p-6 shadow-lg">
        <label className="block max-w-lg text-sm text-slate-300">Target device<select className="mt-1 block w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white" value={deviceId} onChange={(e) => { setDeviceId(e.target.value); setMessage('') }}><option value="">Select a device…</option>{(devices ?? []).map((d) => <option key={d.id} value={d.id}>{d.name} — {d.status}</option>)}</select></label>
        <div className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{actions.map(({ type, label, icon: Icon }) => <button key={type} disabled={!deviceId || action.isPending} onClick={() => { if ((type === 'RebootWindows' || type === 'ShutdownWindows') && !confirm(`Send ${label} to this device?`)) return; action.mutate(type) }} className="flex items-center gap-3 rounded-lg border border-surface-700 px-4 py-3 text-left text-sm text-slate-300 hover:bg-surface-800 disabled:cursor-not-allowed disabled:opacity-40"><Icon className="h-4 w-4 text-accent-400" />{label}</button>)}</div>
        {message && <p className={`mt-4 text-sm ${message.includes('queued') ? 'text-emerald-400' : 'text-red-400'}`}>{message}</p>}
      </div>
      {deviceId && <div className="mt-6 rounded-xl border border-surface-800 bg-surface-900 shadow-lg"><div className="border-b border-surface-800 px-5 py-3"><h2 className="flex items-center gap-2 text-base font-semibold text-white"><Terminal className="h-4 w-4 text-accent-400" />Recent Actions</h2></div>{history?.commands.map((cmd) => <div key={cmd.id} className="flex items-center justify-between border-b border-surface-800 px-5 py-3 text-sm"><span className="text-slate-300">{cmd.type}</span><span className="text-slate-500">{cmd.status} · {new Date(cmd.createdAt).toLocaleString()}</span></div>)}{history?.commands.length === 0 && <p className="px-5 py-6 text-sm text-slate-500">No actions sent to this device.</p>}</div>}
    </div>
  )
}
