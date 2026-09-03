import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { devicesApi, enrollmentApi, api } from '@/lib/api'
import { Rocket, Copy, Check, Monitor, Globe } from 'lucide-react'

export function DeployPage() {
  const queryClient = useQueryClient()
  const [deviceName, setDeviceName] = useState('')
  const [hostname, setHostname] = useState('')
  const [deployResult, setDeployResult] = useState<{
    token: string
    command: string
    scriptUrl: string
    deviceName: string
  } | null>(null)
  const [copied, setCopied] = useState(false)
  const [error, setError] = useState('')

  const serverUrl = window.location.origin.replace(':5173', ':5163')

  const deployMutation = useMutation({
    mutationFn: async () => {
      // 1. Create the device
      const device = await devicesApi.create({
        name: deviceName,
        hostname: hostname || deviceName,
      })

      // 2. Generate enrollment token linked to the device
      const tokenResult = await enrollmentApi.create({
        label: deviceName,
        expiresInHours: 72,
        deviceId: device.id,
      })

      // 3. Get the one-liner command
      const cmdResult = await fetch(
        `/api/deploy/command?token=${encodeURIComponent(tokenResult.token)}&serverUrl=${encodeURIComponent(serverUrl)}`,
        { headers: { Authorization: `Bearer ${api.token}` } }
      )
      if (!cmdResult.ok) throw new Error('Failed to generate deploy command')
      const cmd = await cmdResult.json()

      return { token: tokenResult.token, command: cmd.command, scriptUrl: cmd.scriptUrl, deviceName }
    },
    onSuccess: (data) => {
      setDeployResult(data)
      setError('')
      queryClient.invalidateQueries({ queryKey: ['devices'] })
    },
    onError: (err: any) => setError(err?.message || 'Deployment setup failed'),
  })

  const handleDeploy = (e: React.FormEvent) => {
    e.preventDefault()
    if (!deviceName.trim()) return
    deployMutation.mutate()
  }

  const copyCommand = async () => {
    if (deployResult) {
      await navigator.clipboard.writeText(deployResult.command)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    }
  }

  return (
    <div className="p-6">
      <div className="flex items-center gap-3">
        <Rocket className="h-7 w-7 text-accent-400" />
        <div>
          <h1 className="text-2xl font-bold text-white">Deploy</h1>
          <p className="mt-1 text-sm text-slate-400">Generate a one-liner to install the kiosk agent on a new PC</p>
        </div>
      </div>

      <div className="mt-8 max-w-2xl">
        {/* Step 1: Device info */}
        {!deployResult && (
          <form onSubmit={handleDeploy} className="rounded-xl border border-surface-800 bg-surface-900 p-6 shadow-lg">
            <h2 className="text-lg font-semibold text-white">
              <Monitor className="mr-2 inline h-5 w-5 text-slate-400" />
              New Kiosk Device
            </h2>
            <p className="mt-1 text-sm text-slate-400">
              Enter the device details, then paste the generated command on the kiosk PC.
            </p>

            {error && <p className="mt-3 text-sm text-red-400">{error}</p>}

            <div className="mt-4 space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-300">Device Name</label>
                <input
                  type="text"
                  value={deviceName}
                  onChange={(e) => setDeviceName(e.target.value)}
                  placeholder="e.g. Lobby Kiosk 1"
                  required
                  className="mt-1.5 block w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white placeholder-slate-500 outline-none focus:border-accent-500 focus:ring-1 focus:ring-accent-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-300">Hostname (optional)</label>
                <input
                  type="text"
                  value={hostname}
                  onChange={(e) => setHostname(e.target.value)}
                  placeholder="Defaults to device name"
                  className="mt-1.5 block w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white placeholder-slate-500 outline-none focus:border-accent-500 focus:ring-1 focus:ring-accent-500"
                />
              </div>
              <div className="flex items-center gap-2 rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-xs text-slate-400">
                <Globe className="h-4 w-4 shrink-0" />
                <span>Server: <code className="text-slate-300">{serverUrl}</code></span>
              </div>
            </div>

            <button
              type="submit"
              disabled={deployMutation.isPending || !deviceName.trim()}
              className="mt-6 w-full rounded-lg bg-accent-600 px-4 py-2.5 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-500 disabled:opacity-50"
            >
              {deployMutation.isPending ? 'Setting up...' : 'Generate Deploy Command'}
            </button>
          </form>
        )}

        {/* Step 2: Command ready */}
        {deployResult && (
          <div className="space-y-6">
            {/* Success banner */}
            <div className="rounded-xl border border-emerald-500/30 bg-emerald-500/10 p-5">
              <h2 className="text-lg font-semibold text-emerald-300">Ready to deploy: {deployResult.deviceName}</h2>
              <p className="mt-1 text-sm text-emerald-300/80">
                Device created and enrollment token generated. Paste the command below on the kiosk PC.
              </p>
            </div>

            {/* The one-liner */}
            <div className="rounded-xl border border-surface-700 bg-surface-900 p-6 shadow-lg">
              <h3 className="text-sm font-semibold text-white">Paste this on the kiosk PC (PowerShell as Administrator):</h3>
              <div className="mt-3 flex items-start gap-2">
                <code className="flex-1 break-all rounded-lg bg-black/50 p-4 font-mono text-sm text-emerald-300 leading-relaxed">
                  {deployResult.command}
                </code>
                <button
                  onClick={copyCommand}
                  className="shrink-0 rounded-lg border border-surface-700 p-2.5 text-slate-300 transition-colors hover:bg-surface-800"
                  title="Copy command"
                >
                  {copied ? <Check className="h-4 w-4 text-emerald-400" /> : <Copy className="h-4 w-4" />}
                </button>
              </div>
              <p className="mt-3 text-xs text-slate-500">
                This downloads the bundle, extracts it, writes the server config, and runs setup.ps1 — all in one step.
              </p>
            </div>

            {/* Token (for reference) */}
            <details className="rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
              <summary className="cursor-pointer px-6 py-4 text-sm font-medium text-slate-300 hover:text-white">
                Advanced: token details
              </summary>
              <div className="border-t border-surface-800 px-6 py-4">
                <p className="text-xs font-medium uppercase tracking-wider text-slate-500">Enrollment Token</p>
                <code className="mt-1 block break-all rounded bg-black/40 px-3 py-2 font-mono text-xs text-amber-300">
                  {deployResult.token}
                </code>
                <p className="mt-2 text-xs text-slate-500">
                  Single-use, expires in 72 hours. Can also be used manually with setup.ps1.
                </p>
              </div>
            </details>

            {/* Start over */}
            <button
              onClick={() => { setDeployResult(null); setDeviceName(''); setHostname(''); setError('') }}
              className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 transition-colors hover:bg-surface-800"
            >
              Deploy Another Device
            </button>
          </div>
        )}
      </div>
    </div>
  )
}
