import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Pencil, Trash2, X } from 'lucide-react'
import { usersApi } from '@/lib/api'
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
