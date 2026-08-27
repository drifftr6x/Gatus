export function DashboardPage() {
  return (
    <div>
      <h1 className="text-2xl font-semibold text-slate-900">Dashboard</h1>
      <p className="mt-2 text-slate-600">
        Welcome to Sentinel Kiosk. Select a section from the sidebar.
      </p>
      <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {['Online Devices', 'Pending Commands', 'Active Policies', 'Content Items'].map(
          (label) => (
            <div
              key={label}
              className="rounded-lg border border-slate-200 bg-white p-4"
            >
              <p className="text-sm font-medium text-slate-500">{label}</p>
              <p className="mt-1 text-3xl font-semibold text-slate-900">—</p>
            </div>
          ),
        )}
      </div>
    </div>
  )
}
