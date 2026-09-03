import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { devicesApi, enrollmentApi, api } from '@/lib/api'
import { Rocket, Copy, Check, Monitor, Globe } from 'lucide-react'

interface ServerInfo {
  requestHost: string
  requestScheme: string
  port: number
  hostName: string
  lanIps: string[]
}

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
  const [serverOverride, setServerOverride] = useState<string | null>(null)

  const isLocalhost = /^(localhost|127\.0\.0\.1|\[::1\])/.test(window.location.hostname)

  const { data: serverInfo } = useQuery<ServerInfo>({
    queryKey: ['deploy-server-info'],
    queryFn: () => api.get<ServerInfo>('/api/deploy/server-info'),
    staleTime: 60_000,
  })

  // Best default: if browsing via localhost, use the server's LAN IP
  const detectedUrl = isLocalhost && serverInfo?.lanIps?.length
    ? `http://${serverInfo.lanIps[0]}:${serverInfo.port}`
    : window.location.origin.replace(':5173', ':5163')

  const serverUrl = serverOverride ?? detectedUrl

  const DOMAIN_SUFFIX = '.internal.livingspaces.com'

  const toFqdn = (name: string) => {
    const trimmed = name.trim()
    if (!trimmed) return ''
    if (trimmed.includes('.')) return trimmed // already FQDN
    return trimmed + DOMAIN_SUFFIX
  }

  const deployMutation = useMutation({
    mutationFn: async () => {
      // 1. Create the device
      const device = await devicesApi.create({
        name: deviceName,
        hostname: toFqdn(hostname || deviceName),
      })

      // 2. Generate enrollment token linked to the device
      const tokenResult = await enrollmentApi.create({
        label: deviceName,
        expiresInHours: 72,
        deviceId: device.id,
      })

      // 3. Build the one-liner (no extra API call needed)
      const scriptUrl = `${serverUrl}/api/deploy/script?token=${encodeURIComponent(tokenResult.token)}`
      const command = `powershell -ExecutionPolicy Bypass -Command "irm '${scriptUrl}' | iex"`

      return { token: tokenResult.token, command, scriptUrl, deviceName }
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
                  onBlur={(e) => {
                    const v = e.target.value.trim()
                    if (v && !v.includes('.')) setHostname(v + DOMAIN_SUFFIX)
                  }}
                  placeholder={`Defaults to device name + ${DOMAIN_SUFFIX}`}
                  className="mt-1.5 block w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white placeholder-slate-500 outline-none focus:border-accent-500 focus:ring-1 focus:ring-accent-500"
                />
                <p className="mt-1 text-xs text-slate-500">Short names get {DOMAIN_SUFFIX} appended automatically.</p>
              </div>
              <div className="rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-xs text-slate-400">
                <div className="flex items-center gap-2">
                  <Globe className="h-4 w-4 shrink-0" />
                  <span>Server (as seen from the kiosk PC):</span>
                  <input
                    type="text"
                    value={serverUrl}
                    onChange={(e) => setServerOverride(e.target.value)}
                    className="min-w-0 flex-1 rounded border border-surface-600 bg-surface-900 px-2 py-1 font-mono text-slate-200 outline-none focus:border-accent-500"
                  />
                </div>
                {isLocalhost && serverOverride === null && (
                  <p className="mt-1.5 text-amber-400/90">
                    You're browsing via localhost — remote PCs can't use that. We auto-selected this machine's LAN IP; verify it's correct.
                  </p>
                )}
                {serverInfo && serverInfo.lanIps.length > 1 && (
                  <div className="mt-1.5 flex flex-wrap gap-1">
                    {serverInfo.lanIps.map((ip) => (
                      <button
                        key={ip}
                        type="button"
                        onClick={() => setServerOverride(`http://${ip}:${serverInfo.port}`)}
                        className={`rounded px-1.5 py-0.5 font-mono ${serverUrl.includes(ip) ? 'bg-accent-600 text-white' : 'bg-surface-800 text-slate-400 hover:text-slate-200'}`}
                      >
                        {ip}
                      </button>
                    ))}
                  </div>
                )}
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
              <h3 className="text-sm font-semibold text-white">Your deploy command:</h3>
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
            </div>

            {/* How to run it on the remote PC */}
            <div className="rounded-xl border border-surface-700 bg-surface-900 p-6 shadow-lg">
              <h3 className="text-sm font-semibold text-white">How to run this on the kiosk PC</h3>
              <div className="mt-4 space-y-4">
                <div className="flex gap-3">
                  <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-accent-500/20 text-xs font-bold text-accent-400">1</span>
                  <div>
                    <p className="text-sm font-medium text-white">Open PowerShell as Administrator on the kiosk PC</p>
                    <p className="mt-1 text-xs text-slate-400">
                      Press <kbd className="rounded bg-surface-800 px-1.5 py-0.5 text-xs">Win + X</kbd> → <strong>Terminal (Admin)</strong> or search "PowerShell" → right-click → <strong>Run as Administrator</strong>
                    </p>
                  </div>
                </div>
                <div className="flex gap-3">
                  <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-accent-500/20 text-xs font-bold text-accent-400">2</span>
                  <div>
                    <p className="text-sm font-medium text-white">Paste the command above and press Enter</p>
                    <p className="mt-1 text-xs text-slate-400">
                      The script downloads the bundle from this server, extracts it, writes the config, and runs setup — all automatically.
                    </p>
                  </div>
                </div>
                <div className="flex gap-3">
                  <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-accent-500/20 text-xs font-bold text-accent-400">3</span>
                  <div>
                    <p className="text-sm font-medium text-white">Watch the dashboard</p>
                    <p className="mt-1 text-xs text-slate-400">
                      The device should appear as <strong className="text-emerald-400">Online</strong> within 30 seconds.
                    </p>
                  </div>
                </div>
              </div>

              <div className="mt-5 border-t border-surface-800 pt-4">
                <p className="text-xs font-medium uppercase tracking-wider text-slate-500">Alternative methods</p>
                <div className="mt-3 space-y-2 text-xs text-slate-400">
                  <p>
                    <strong className="text-slate-300">RDP / Remote Session:</strong> If you're RDP'd into the kiosk PC, copy the command from this page and paste it in the remote session's PowerShell.
                  </p>
                  <p>
                    <strong className="text-slate-300">USB drive:</strong>{' '}
                    <a href={`${serverUrl}/api/bundle/download`} className="text-accent-400 hover:underline">Download the bundle zip</a>,
                    copy to USB, extract on the PC, then run <code className="rounded bg-surface-800 px-1">.\setup.ps1</code> as Administrator.
                  </p>
                  <p>
                    <strong className="text-slate-300">PsExec / SCCM:</strong> Push the command remotely:{' '}
                    <code className="mt-1 block break-all rounded bg-black/40 px-2 py-1 font-mono">
                      psexec \\PCNAME -s powershell -Command "{deployResult.command}"
                    </code>
                  </p>
                </div>
              </div>
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
