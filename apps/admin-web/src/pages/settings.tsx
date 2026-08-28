import { useQuery } from '@tanstack/react-query'
import { usersApi } from '@/lib/api'
import { useAuth } from '@/hooks/useAuth'

export function SettingsPage() {
  const { user } = useAuth()

  const { data: users, isLoading } = useQuery({
    queryKey: ['users'],
    queryFn: usersApi.list,
  })

  return (
    <div>
      <div>
        <h1 className="text-xl font-semibold text-white">Settings</h1>
        <p className="mt-1 text-sm text-slate-400">Account and user management</p>
      </div>

      {/* Current user */}
      <div className="mt-6 rounded-xl border border-surface-800 bg-surface-900 p-5 shadow-lg">
        <h2 className="text-base font-semibold text-white">Your Account</h2>
        <dl className="mt-4 grid grid-cols-2 gap-4 text-sm">
          <div>
            <dt className="text-slate-500">Name</dt>
            <dd className="mt-0.5 text-slate-200">{user?.displayName}</dd>
          </div>
          <div>
            <dt className="text-slate-500">Email</dt>
            <dd className="mt-0.5 text-slate-200">{user?.email}</dd>
          </div>
          <div>
            <dt className="text-slate-500">Role</dt>
            <dd className="mt-0.5">
              <span className="rounded-md bg-accent-500/15 px-2 py-0.5 text-xs font-medium text-accent-300 ring-1 ring-accent-500/30">
                {user?.role}
              </span>
            </dd>
          </div>
          <div>
            <dt className="text-slate-500">Last Login</dt>
            <dd className="mt-0.5 text-slate-200">
              {user?.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString() : '—'}
            </dd>
          </div>
        </dl>
      </div>

      {/* Users table */}
      <div className="mt-8">
        <h2 className="mb-3 text-base font-semibold text-white">Users</h2>
        <div className="overflow-hidden rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
          {isLoading ? (
            <div className="flex h-32 items-center justify-center">
              <div className="h-6 w-6 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
            </div>
          ) : (
            <table className="min-w-full divide-y divide-surface-800">
              <thead>
                <tr className="bg-surface-850">
                  {['Name', 'Email', 'Role', 'Status'].map((h) => (
                    <th
                      key={h}
                      className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wider text-slate-500"
                    >
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-surface-800">
                {users?.map((u) => (
                  <tr key={u.id} className="transition-colors hover:bg-surface-850">
                    <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-slate-100">
                      {u.displayName}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-400">{u.email}</td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <span className="rounded-md bg-accent-500/15 px-2 py-0.5 text-xs font-medium text-accent-300 ring-1 ring-accent-500/30">
                        {u.role}
                      </span>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <span
                        className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ${
                          u.isActive
                            ? 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/30'
                            : 'bg-slate-500/10 text-slate-400 ring-slate-500/30'
                        }`}
                      >
                        <span className="h-1.5 w-1.5 rounded-full bg-current" />
                        {u.isActive ? 'Active' : 'Disabled'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  )
}
