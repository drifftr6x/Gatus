import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Pencil, Trash2, X } from 'lucide-react'
import { usersApi, settingsApi, agentUpdatesApi } from '@/lib/api'
import type { AgentUpdateDto } from '@/lib/api'
import { useAuth } from '@/hooks/useAuth'

export function SettingsPage() {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [showAdd, setShowAdd] = useState(false)
  const [editingUser, setEditingUser] = useState<any>(null)
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null)
  const [toast, setToast] = useState<{ type: 'success' | 'error'; message: string } | null>(null)

  const { data: users, isLoading } = useQuery({
    queryKey: ['users'],
    queryFn: usersApi.list,
  })

  const showToast = (type: 'success' | 'error', message: string) => {
    setToast({ type, message })
    setTimeout(() => setToast(null), 4000)
  }

  const deleteMutation = useMutation({
    mutationFn: usersApi.delete,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] })
      setConfirmDelete(null)
      showToast('success', 'User deleted')
    },
    onError: () => showToast('error', 'Failed to delete user'),
  })

  const roleMutation = useMutation({
    mutationFn: ({ id, role }: { id: string; role: string }) => usersApi.updateRole(id, role),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] })
      showToast('success', 'Role updated')
    },
    onError: () => showToast('error', 'Failed to update role'),
  })

  return (
    <div>
      {/* Toast */}
      {toast && (
        <div className={`fixed top-6 right-6 z-50 flex items-center gap-3 rounded-xl px-5 py-3 shadow-2xl ring-1 ${
          toast.type === 'success'
            ? 'bg-emerald-500/15 text-emerald-400 ring-emerald-500/30'
            : 'bg-red-500/15 text-red-400 ring-red-500/30'
        }`}>
          <span className="text-sm font-medium">{toast.message}</span>
          <button onClick={() => setToast(null)} className="text-slate-400 hover:text-white"><X size={16} /></button>
        </div>
      )}

      <div>
        <h1 className="text-xl font-semibold text-white">Settings</h1>
        <p className="mt-1 text-sm text-slate-400">Account, domain health, and user management</p>
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

      <DomainHealthSection showToast={showToast} />

      {user?.role !== 'Viewer' && <AgentUpdatesSection showToast={showToast} />}

      {/* Users table */}
      <div className="mt-8">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-base font-semibold text-white">Users</h2>
          <button
            onClick={() => setShowAdd(true)}
            className="flex items-center gap-1.5 rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400"
          >
            <Plus size={16} />
            Add User
          </button>
        </div>
        <div className="overflow-hidden rounded-xl border border-surface-800 bg-surface-900 shadow-lg">
          {isLoading ? (
            <div className="flex h-32 items-center justify-center">
              <div className="h-6 w-6 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
            </div>
          ) : (
            <table className="min-w-full divide-y divide-surface-800">
              <thead>
                <tr className="bg-surface-850">
                  {['Name', 'Email', 'Role', 'Status', ''].map((h) => (
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
                {users?.map((u: any) => (
                  <tr key={u.id} className="transition-colors hover:bg-surface-850">
                    <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-slate-100">
                      {u.displayName}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-400">{u.email}</td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      {u.id === user?.id ? (
                        <span className="rounded-md bg-accent-500/15 px-2 py-0.5 text-xs font-medium text-accent-300 ring-1 ring-accent-500/30">
                          {u.role}
                        </span>
                      ) : (
                        <select
                          value={u.role}
                          onChange={(e) => roleMutation.mutate({ id: u.id, role: e.target.value })}
                          className="rounded-md border border-surface-700 bg-surface-800 px-2 py-1 text-xs text-slate-300 focus:outline-none focus:ring-1 focus:ring-accent-500/50"
                        >
                          <option value="Viewer">Viewer</option>
                          <option value="Editor">Editor</option>
                          <option value="SuperAdmin">SuperAdmin</option>
                        </select>
                      )}
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
                    <td className="px-6 py-4 whitespace-nowrap text-right">
                      {u.id !== user?.id && (
                        <div className="flex items-center justify-end gap-2">
                          <button
                            onClick={() => setEditingUser(u)}
                            className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-surface-800 hover:text-slate-200"
                            title="Edit"
                          >
                            <Pencil size={14} />
                          </button>
                          <button
                            onClick={() => setConfirmDelete(u.id)}
                            className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-red-500/10 hover:text-red-400"
                            title="Delete"
                          >
                            <Trash2 size={14} />
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* Add User Modal */}
      {showAdd && (
        <UserFormModal
          onClose={() => setShowAdd(false)}
          onSaved={() => {
            setShowAdd(false)
            queryClient.invalidateQueries({ queryKey: ['users'] })
            showToast('success', 'User created')
          }}
          onError={(msg) => showToast('error', msg)}
        />
      )}

      {/* Edit User Modal */}
      {editingUser && (
        <UserFormModal
          user={editingUser}
          onClose={() => setEditingUser(null)}
          onSaved={() => {
            setEditingUser(null)
            queryClient.invalidateQueries({ queryKey: ['users'] })
            showToast('success', 'User updated')
          }}
          onError={(msg) => showToast('error', msg)}
        />
      )}

      {/* Delete Confirmation */}
      {confirmDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="rounded-xl border border-surface-800 bg-surface-900 p-6 shadow-2xl w-96">
            <h3 className="text-base font-semibold text-white">Delete User</h3>
            <p className="mt-2 text-sm text-slate-400">
              Are you sure you want to delete this user? This action cannot be undone.
            </p>
            <div className="mt-4 flex justify-end gap-3">
              <button
                onClick={() => setConfirmDelete(null)}
                className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 hover:bg-surface-800"
              >
                Cancel
              </button>
              <button
                onClick={() => deleteMutation.mutate(confirmDelete)}
                disabled={deleteMutation.isPending}
                className="rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-500 disabled:opacity-50"
              >
                {deleteMutation.isPending ? 'Deleting…' : 'Delete'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

function UserFormModal({
  user,
  onClose,
  onSaved,
  onError,
}: {
  user?: any
  onClose: () => void
  onSaved: () => void
  onError: (msg: string) => void
}) {
  const isEdit = !!user
  const [form, setForm] = useState({
    email: user?.email ?? '',
    firstName: user?.firstName ?? '',
    lastName: user?.lastName ?? '',
    password: '',
    role: user?.role ?? 'Viewer',
  })

  const createMutation = useMutation({
    mutationFn: (data: any) => usersApi.create(data),
    onSuccess: onSaved,
    onError: (err: any) => onError(err?.response?.data?.error ?? 'Failed to create user'),
  })

  const updateMutation = useMutation({
    mutationFn: (data: any) => usersApi.update(user.id, data),
    onSuccess: onSaved,
    onError: (err: any) => onError(err?.response?.data?.error ?? 'Failed to update user'),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (isEdit) {
      updateMutation.mutate({
        email: form.email,
        firstName: form.firstName,
        lastName: form.lastName,
      })
    } else {
      createMutation.mutate({
        email: form.email,
        firstName: form.firstName,
        lastName: form.lastName,
        password: form.password,
        role: form.role,
      })
    }
  }

  const isPending = createMutation.isPending || updateMutation.isPending
  const inputClass = 'w-full rounded-lg border border-surface-700 bg-surface-800 px-4 py-2.5 text-sm text-slate-200 placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-accent-500/50'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="w-full max-w-md rounded-xl border border-surface-800 bg-surface-900 p-6 shadow-2xl">
        <h3 className="text-base font-semibold text-white">{isEdit ? 'Edit User' : 'Add User'}</h3>
        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-300">Email</label>
            <input
              type="email"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
              className={inputClass}
              placeholder="user@example.com"
              required
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-300">First Name</label>
              <input
                type="text"
                value={form.firstName}
                onChange={(e) => setForm({ ...form, firstName: e.target.value })}
                className={inputClass}
                required
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300">Last Name</label>
              <input
                type="text"
                value={form.lastName}
                onChange={(e) => setForm({ ...form, lastName: e.target.value })}
                className={inputClass}
                required
              />
            </div>
          </div>
          {!isEdit && (
            <div>
              <label className="block text-sm font-medium text-slate-300">Password</label>
              <input
                type="password"
                value={form.password}
                onChange={(e) => setForm({ ...form, password: e.target.value })}
                className={inputClass}
                placeholder="Min 8 characters"
                minLength={8}
                required
              />
            </div>
          )}
          {!isEdit && (
            <div>
              <label className="block text-sm font-medium text-slate-300">Role</label>
              <select
                value={form.role}
                onChange={(e) => setForm({ ...form, role: e.target.value })}
                className={inputClass}
              >
                <option value="Viewer">Viewer — read-only access</option>
                <option value="Editor">Editor — can manage devices, content, schedules</option>
                <option value="SuperAdmin">SuperAdmin — full access including user management</option>
              </select>
            </div>
          )}
          <div className="flex justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 hover:bg-surface-800"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 hover:bg-accent-400 disabled:opacity-50"
            >
              {isPending ? 'Saving…' : isEdit ? 'Update' : 'Create User'}
              </button>
              </div>
              </form>
              </div>
              </div>
              )
              }

              function DomainHealthSection({
              showToast,
              }: {
              showToast: (type: 'success' | 'error', message: string) => void
              }) {
              const queryClient = useQueryClient()
              const { data, isLoading } = useQuery({
              queryKey: ['settings-domain-health'],
              queryFn: settingsApi.getDomainHealth,
              })
              const [expectedDomain, setExpectedDomain] = useState('')
              const [alertOnMismatch, setAlertOnMismatch] = useState(false)
              const [alertOnTrustBroken, setAlertOnTrustBroken] = useState(false)

              useEffect(() => {
                if (!data) return
                setExpectedDomain(data.expectedDomain ?? '')
                setAlertOnMismatch(!!data.alertOnMismatch)
                setAlertOnTrustBroken(!!data.alertOnTrustBroken)
              }, [data])

              const saveMutation = useMutation({
              mutationFn: () =>
              settingsApi.updateDomainHealth({
              expectedDomain: expectedDomain.trim() || null,
              alertOnMismatch,
              alertOnTrustBroken,
              }),
              onSuccess: () => {
              queryClient.invalidateQueries({ queryKey: ['settings-domain-health'] })
              showToast('success', 'Domain health settings saved')
              },
              onError: () => showToast('error', 'Failed to save domain health settings'),
              })

              return (
              <div className="mt-6 rounded-xl border border-surface-800 bg-surface-900 p-5 shadow-lg">
              <h2 className="text-base font-semibold text-white">Domain Health Monitoring</h2>
              <p className="mt-1 text-sm text-slate-400">
              Agents always report AD join status. Configure the expected domain and which conditions should raise alerts.
              </p>

              {isLoading ? (
              <div className="mt-4 h-6 w-6 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
              ) : (
              <div className="mt-4 space-y-4">
              <div>
              <label className="block text-xs font-medium text-slate-400 mb-1">Expected domain</label>
              <input
              type="text"
              value={expectedDomain}
              onChange={(e) => setExpectedDomain(e.target.value)}
              placeholder="livingspaces.com"
              className="w-full max-w-md rounded-lg border border-surface-700 bg-surface-800 px-3 py-2 text-sm text-slate-200 placeholder:text-slate-600 focus:outline-none focus:ring-1 focus:ring-accent-500/50"
              />
              <p className="mt-1 text-xs text-slate-500">
              Agents report the DNS domain when a DC is reachable (fallback: NetBIOS). Matching is case-insensitive and treats LSF and livingspaces.com as the same forest if one is a suffix of the other. Leave blank to skip mismatch alerts.
              </p>
              </div>
              <label className="flex items-start gap-3 text-sm text-slate-300">
              <input
              type="checkbox"
              checked={alertOnMismatch}
              onChange={(e) => setAlertOnMismatch(e.target.checked)}
              className="mt-0.5 h-4 w-4 rounded border-surface-600 bg-surface-800 text-accent-500 focus:ring-accent-500"
              />
              <span>
              <span className="font-medium text-slate-200">Alert on domain mismatch</span>
              <span className="block text-xs text-slate-500">
              Warn when a device is in a workgroup or joined to a different domain than expected.
              </span>
              </span>
              </label>
              <label className="flex items-start gap-3 text-sm text-slate-300">
              <input
              type="checkbox"
              checked={alertOnTrustBroken}
              onChange={(e) => setAlertOnTrustBroken(e.target.checked)}
              className="mt-0.5 h-4 w-4 rounded border-surface-600 bg-surface-800 text-accent-500 focus:ring-accent-500"
              />
              <span>
              <span className="font-medium text-slate-200">Alert on broken trust relationship</span>
              <span className="block text-xs text-slate-500">
              Critical alert when a domain-joined device cannot reach a domain controller.
              </span>
              </span>
              </label>
              <button
              onClick={() => saveMutation.mutate()}
              disabled={saveMutation.isPending}
              className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400 disabled:opacity-50"
              >
              {saveMutation.isPending ? 'Saving…' : 'Save domain settings'}
              </button>
              </div>
              )}
              </div>
              )
              }

function AgentUpdatesSection({ showToast }: { showToast: (type: 'success' | 'error', message: string) => void }) {
  const queryClient = useQueryClient()
  const [showUpload, setShowUpload] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null)

  const { data: updates } = useQuery({
    queryKey: ['agent-updates'],
    queryFn: agentUpdatesApi.list,
  })

  const activateMutation = useMutation({
    mutationFn: (id: string) => agentUpdatesApi.activate(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['agent-updates'] })
      showToast('success', 'Update activated — agents will pick it up on their next hourly check')
    },
    onError: (e: Error) => showToast('error', e.message),
  })

  const deactivateMutation = useMutation({
    mutationFn: (id: string) => agentUpdatesApi.deactivate(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['agent-updates'] })
      showToast('success', 'Update deactivated')
    },
    onError: (e: Error) => showToast('error', e.message),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => agentUpdatesApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['agent-updates'] })
      setConfirmDelete(null)
      showToast('success', 'Update deleted')
    },
    onError: (e: Error) => showToast('error', e.message),
  })

  return (
    <div className="mt-8">
      <div className="mb-3 flex items-center justify-between">
        <div>
          <h2 className="text-base font-semibold text-white">Agent Updates</h2>
          <p className="text-xs text-slate-500">
            Signed self-update packages. Agents check hourly and only accept packages signed by this server.
          </p>
        </div>
        <button
          onClick={() => setShowUpload(true)}
          className="flex items-center gap-2 rounded-lg bg-accent-500 px-3 py-1.5 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400"
        >
          <Plus className="h-4 w-4" />
          Upload update
        </button>
      </div>

      <div className="overflow-hidden rounded-xl border border-surface-700 bg-surface-900 shadow-2xl">
        <table className="w-full text-sm">
          <thead className="bg-surface-850 text-left text-xs uppercase tracking-wider text-slate-500">
            <tr>
              <th className="px-4 py-3">Version</th>
              <th className="px-4 py-3">Size</th>
              <th className="px-4 py-3">Rollout</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Uploaded</th>
              <th className="px-4 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-surface-700">
            {updates?.map((u: AgentUpdateDto) => (
              <tr key={u.id} className="transition-colors hover:bg-surface-850/50">
                <td className="px-4 py-3 font-mono font-medium text-white">
                  {u.version}
                  {u.minVersion && (
                    <span className="ml-2 text-xs text-slate-500">min {u.minVersion}</span>
                  )}
                  {u.notes && <p className="mt-0.5 font-sans text-xs font-normal text-slate-500">{u.notes}</p>}
                </td>
                <td className="px-4 py-3 text-slate-300">{(u.fileSizeBytes / 1048576).toFixed(1)} MB</td>
                <td className="px-4 py-3 text-slate-300">{u.rolloutPercent}%</td>
                <td className="px-4 py-3">
                  {u.isActive ? (
                    <span className="rounded-full bg-emerald-500/10 px-2.5 py-0.5 text-xs font-medium text-emerald-400 ring-1 ring-emerald-500/30">
                      Active
                    </span>
                  ) : (
                    <span className="rounded-full bg-surface-700 px-2.5 py-0.5 text-xs text-slate-400">Inactive</span>
                  )}
                </td>
                <td className="px-4 py-3 text-slate-400">{new Date(u.createdAt).toLocaleDateString()}</td>
                <td className="px-4 py-3">
                  <div className="flex items-center justify-end gap-1">
                    {u.isActive ? (
                      <button
                        onClick={() => deactivateMutation.mutate(u.id)}
                        className="rounded-lg p-2 text-slate-400 hover:bg-surface-700 hover:text-amber-400"
                        title="Deactivate (stop offering)"
                      >
                        <X className="h-4 w-4" />
                      </button>
                    ) : (
                      <button
                        onClick={() => activateMutation.mutate(u.id)}
                        className="rounded-lg px-2 py-1 text-xs font-medium text-emerald-400 hover:bg-surface-700"
                        title="Activate (offer to agents)"
                      >
                        Activate
                      </button>
                    )}
                    <button
                      onClick={() => setConfirmDelete(u.id)}
                      className="rounded-lg p-2 text-slate-400 hover:bg-surface-700 hover:text-red-400"
                      title="Delete"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {(!updates || updates.length === 0) && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-slate-500">
                  No agent updates uploaded. Build one with{' '}
                  <code className="text-xs">infrastructure/scripts/publish-agent-update.ps1</code> then upload the zip
                  here.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {showUpload && (
        <AgentUpdateUploadModal
          onClose={() => setShowUpload(false)}
          onUploaded={() => {
            queryClient.invalidateQueries({ queryKey: ['agent-updates'] })
            setShowUpload(false)
            showToast('success', 'Update uploaded and signed — it is now the active update')
          }}
        />
      )}
      {confirmDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="w-96 rounded-xl border border-surface-800 bg-surface-900 p-6 shadow-2xl">
            <h3 className="text-base font-semibold text-white">Delete agent update</h3>
            <p className="mt-2 text-sm text-slate-400">
              The package files will be removed from the server. Agents that already downloaded it are unaffected.
            </p>
            <div className="mt-4 flex justify-end gap-3">
              <button
                onClick={() => setConfirmDelete(null)}
                className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 hover:bg-surface-800"
              >
                Cancel
              </button>
              <button
                onClick={() => deleteMutation.mutate(confirmDelete)}
                disabled={deleteMutation.isPending}
                className="rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-500 disabled:opacity-50"
              >
                {deleteMutation.isPending ? 'Deleting…' : 'Delete'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

function AgentUpdateUploadModal({ onClose, onUploaded }: { onClose: () => void; onUploaded: () => void }) {
  const [file, setFile] = useState<File | null>(null)
  const [version, setVersion] = useState('')
  const [rolloutPercent, setRolloutPercent] = useState<number | ''>(100)
  const [minVersion, setMinVersion] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState('')

  const uploadMutation = useMutation({
    mutationFn: () =>
      agentUpdatesApi.upload(file!, {
        version,
        rolloutPercent: rolloutPercent !== '' ? rolloutPercent : undefined,
        minVersion: minVersion || undefined,
        notes: notes || undefined,
      }),
    onSuccess: onUploaded,
    onError: (e: Error) => setError(e.message),
  })

  const inputClass =
    'mt-1.5 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
      <div className="w-full max-w-md rounded-2xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h3 className="text-lg font-semibold text-white">Upload agent update</h3>
        <p className="mt-1 text-xs text-slate-500">
          Upload the zip from <code>publish-agent-update.ps1</code>. The server signs the manifest and deactivates
          older versions.
        </p>

        {error && (
          <div className="mt-4 rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm text-red-400">
            {error}
          </div>
        )}

        <div className="mt-5 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-300">Package zip</label>
            <input
              type="file"
              accept=".zip"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              className={`${inputClass} file:mr-3 file:rounded-md file:border-0 file:bg-surface-700 file:px-3 file:py-1 file:text-sm file:text-slate-200`}
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-slate-300">Version</label>
              <input
                value={version}
                onChange={(e) => setVersion(e.target.value)}
                placeholder="1.1.0"
                className={inputClass}
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-300">Rollout %</label>
              <input
                type="number"
                min={1}
                max={100}
                value={rolloutPercent}
                onChange={(e) => setRolloutPercent(e.target.value === '' ? '' : Number(e.target.value))}
                className={inputClass}
              />
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Minimum agent version (optional)</label>
            <input
              value={minVersion}
              onChange={(e) => setMinVersion(e.target.value)}
              placeholder="e.g. 1.0.5 — older agents skip this update"
              className={inputClass}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Notes (optional)</label>
            <input
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="What changed"
              className={inputClass}
            />
          </div>
        </div>

        <div className="mt-6 flex justify-end gap-3">
          <button
            onClick={onClose}
            className="rounded-lg border border-surface-700 px-4 py-2 text-sm font-medium text-slate-300 hover:bg-surface-800"
          >
            Cancel
          </button>
          <button
            onClick={() => uploadMutation.mutate()}
            disabled={!file || !version || uploadMutation.isPending}
            className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400 disabled:opacity-50"
          >
            {uploadMutation.isPending ? 'Uploading…' : 'Upload & sign'}
          </button>
        </div>
      </div>
    </div>
  )
}
         
