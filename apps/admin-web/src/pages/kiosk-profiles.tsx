import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { devicesApi } from '@/lib/api'
import { ShieldCheck, Save } from 'lucide-react'
import { useState } from 'react'

const inputClass = 'mt-1 block w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500'

export function KioskProfilesPage() {
  const queryClient = useQueryClient()
  const [deviceId, setDeviceId] = useState('')
  const [homeUrl, setHomeUrl] = useState('')
  const [allowedUrls, setAllowedUrls] = useState('')
  const [inactivity, setInactivity] = useState(300)
  const [kioskEnabled, setKioskEnabled] = useState(true)

  const { data: devices } = useQuery({ queryKey: ['devices', 'all'], queryFn: devicesApi.listAll, staleTime: 30_000 })
  const { data: policy, isLoading } = useQuery({
    queryKey: ['device-policy', deviceId],
    queryFn: () => devicesApi.getPolicy(deviceId),
    enabled: !!deviceId,
  })

  const save = useMutation({
    mutationFn: () => devicesApi.updatePolicy(deviceId, {
      homeUrl: homeUrl || undefined,
      allowedUrls: allowedUrls.split('\n').map((v) => v.trim()).filter(Boolean),
      inactivityResetSeconds: inactivity,
      kioskEnabled,
      restartOnExit: true,
    }),
    onSuccess: (updated) => {
      setHomeUrl(updated.homeUrl ?? '')
      setAllowedUrls(updated.allowedUrls.join('\n'))
      setInactivity(updated.inactivityResetSeconds)
      queryClient.invalidateQueries({ queryKey: ['device-policy', deviceId] })
    },
  })

  const selectDevice = (id: string) => {
    setDeviceId(id)
    const selected = devices?.find((d) => d.id === id)
    if (selected) setHomeUrl('')
  }

  const applyPolicy = (value: typeof policy) => {
    if (!value) return
    setHomeUrl(value.homeUrl ?? '')
    setAllowedUrls(value.allowedUrls.join('\n'))
    setInactivity(value.inactivityResetSeconds)
    setKioskEnabled(value.kioskEnabled)
  }

  return (
    <div>
      <h1 className="text-xl font-semibold text-white">Kiosk Profiles</h1>
      <p className="mt-1 text-sm text-slate-400">Configure browser, session, and lockdown behavior for a device.</p>
      <div className="mt-6 grid gap-6 lg:grid-cols-[280px_1fr]">
        <div className="rounded-xl border border-surface-800 bg-surface-900 p-4 shadow-lg">
          <h2 className="mb-3 font-semibold text-white">Devices</h2>
          <div className="space-y-1">
            {(devices ?? []).map((device) => (
              <button key={device.id} onClick={() => { selectDevice(device.id); void devicesApi.getPolicy(device.id).then(applyPolicy) }} className={`w-full rounded-lg px-3 py-2 text-left text-sm ${device.id === deviceId ? 'bg-accent-500/15 text-accent-300' : 'text-slate-400 hover:bg-surface-800'}`}>
                <span className="block font-medium">{device.name}</span>
                <span className="text-xs text-slate-500">{device.status} · {device.groupName ?? 'No group'}</span>
              </button>
            ))}
          </div>
        </div>
        <div className="rounded-xl border border-surface-800 bg-surface-900 p-6 shadow-lg">
          {!deviceId ? <div className="py-12 text-center text-sm text-slate-500"><ShieldCheck className="mx-auto mb-3 h-8 w-8 text-slate-700" />Select a device to edit its kiosk profile.</div> : isLoading ? <p className="text-sm text-slate-500">Loading profile…</p> : (
            <form onSubmit={(e) => { e.preventDefault(); save.mutate() }} className="space-y-5">
              <div><h2 className="font-semibold text-white">Browser and session policy</h2><p className="mt-1 text-xs text-slate-500">Saved through the existing device policy API and synchronized by the agent.</p></div>
              <label className="block text-sm text-slate-300">Homepage<input className={inputClass} value={homeUrl} onChange={(e) => setHomeUrl(e.target.value)} placeholder="https://portal.example.com" /></label>
              <label className="block text-sm text-slate-300">Allowed URLs <span className="text-xs text-slate-500">(one per line)</span><textarea className={inputClass} rows={4} value={allowedUrls} onChange={(e) => setAllowedUrls(e.target.value)} placeholder="https://portal.example.com/*" /></label>
              <label className="block text-sm text-slate-300">Inactivity reset (seconds)<input type="number" min={0} className={inputClass} value={inactivity} onChange={(e) => setInactivity(Number(e.target.value))} /></label>
              <label className="flex items-center gap-2 text-sm text-slate-300"><input type="checkbox" checked={kioskEnabled} onChange={(e) => setKioskEnabled(e.target.checked)} /> Enable kiosk lockdown</label>
              <button type="submit" disabled={save.isPending} className="inline-flex items-center gap-2 rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white hover:bg-accent-400 disabled:opacity-50"><Save className="h-4 w-4" />{save.isPending ? 'Saving…' : 'Save profile'}</button>
              {save.isError && <p className="text-sm text-red-400">{save.error.message}</p>}
              {save.isSuccess && <p className="text-sm text-emerald-400">Profile saved and queued for agent synchronization.</p>}
            </form>
          )}
        </div>
      </div>
    </div>
  )
}
