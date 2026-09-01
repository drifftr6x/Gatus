import { Link } from 'react-router-dom'
import { ShieldCheck, ArrowRight } from 'lucide-react'

export function KioskProfilesPage() {
  return (
    <div>
      <h1 className="text-xl font-semibold text-white">Kiosk Profiles</h1>
      <p className="mt-1 text-sm text-slate-400">
        Configure the browser, session, and lockdown behavior assigned to your kiosks.
      </p>
      <div className="mt-6 rounded-xl border border-surface-800 bg-surface-900 p-6 shadow-lg">
        <div className="flex items-start gap-4">
          <div className="rounded-lg bg-accent-500/10 p-3 text-accent-400">
            <ShieldCheck className="h-6 w-6" />
          </div>
          <div>
            <h2 className="font-semibold text-white">Device kiosk policies</h2>
            <p className="mt-1 max-w-2xl text-sm text-slate-400">
              EdgeWatch Lite uses the existing policy and agent synchronization flow. Open a device to review or update its homepage, URL rules, session settings, and kiosk lockdown profile.
            </p>
            <Link to="/devices" className="mt-4 inline-flex items-center gap-2 rounded-lg bg-accent-500 px-3 py-2 text-sm font-medium text-white hover:bg-accent-400">
              Manage device profiles <ArrowRight className="h-4 w-4" />
            </Link>
          </div>
        </div>
      </div>
    </div>
  )
}
