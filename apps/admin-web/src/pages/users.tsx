import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { usersApi } from '@/lib/api'
import type { UserDto } from '@/lib/api'
import { Plus, Pencil, Trash2, Shield, ShieldAlert, ShieldCheck, Eye } from 'lucide-react'

const ROLES = ['Viewer', 'Editor', 'Admin', 'SuperAdmin'] as const

const roleColors: Record<string, string> = {
  Viewer: 'bg-slate-500/20 text-slate-400',
  Editor: 'bg-blue-500/20 text-blue-400',
  Admin: 'bg-amber-500/20 text-amber-400',
  SuperAdmin: 'bg-red-500/20 text-red-400',
}

const roleIcons: Record<string, typeof Shield> = {
  Viewer: Eye,
  Editor: Shield,
  Admin: ShieldCheck,
  SuperAdmin: ShieldAlert,
}

const inputClass =
  'mt-1.5 block w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white placeholder-slate-500 outline-none transition-colors focus:border-accent-500 focus:ring-1 focus:ring-accent-500'

export function UsersPage() {
  const queryClient = useQueryClient()
  const [showCreate, setShowCreate] = useState(false)
  const [editUser, setEditUser] = useState<UserDto | null>(null)
  const [deleteUser, setDeleteUser] = useState<UserDto | null>(null)

  const { data: users, isLoading } = useQuery({
    queryKey: ['users'],
    queryFn: () => usersApi.list(),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => usersApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] })
      setDeleteUser(null)
    },
  })

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white">Users</h1>
          <p className="mt-1 text-sm text-slate-400">Manage admin console users and role assignments</p>
        </div>
        <button
          onClick={() => setShowCreate(true)}
          className="inline-flex items-center gap-2 rounded-lg bg-accent-600 px-4 py-2 text-sm font-medium text-white hover:bg-accent-500 transition-colors"
        >
          <Plus className="h-4 w-4" />
          Add User
        </button>
      </div>

      {/* Users Table */}
      <div className="mt-6 rounded-xl border border-surface-800 bg-surface-900 shadow-lg overflow-hidden">
        <table className="w-full">
          <thead>
            <tr className="border-b border-surface-700 bg-surface-800/50">
              <th className="px-5 py-3 text-left text-xs font-medium uppercase tracking-wider text-slate-400">User</th>
              <th className="px-5 py-3 text-left text-xs font-medium uppercase tracking-wider text-slate-400">Role</th>
              <th className="px-5 py-3 text-left text-xs font-medium uppercase tracking-wider text-slate-400">Status</th>
              <th className="px-5 py-3 text-left text-xs font-medium uppercase tracking-wider text-slate-400">Last Login</th>
              <th className="px-5 py-3 text-right text-xs font-medium uppercase tracking-wider text-slate-400">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-surface-800">
            {isLoading ? (
              <tr><td colSpan={5} className="px-5 py-8 text-center text-slate-500">Loading...</td></tr>
            ) : !users || users.length === 0 ? (
              <tr><td colSpan={5} className="px-5 py-8 text-center text-slate-500">No users found</td></tr>
            ) : (
              users.map(user => {
                const RoleIcon = roleIcons[user.role] || Shield
                return (
                  <tr key={user.id} className="hover:bg-surface-800/50 transition-colors">
                    <td className="px-5 py-3">
                      <div>
                        <span className="text-sm font-medium text-white">{user.displayName || `${user.firstName} ${user.lastName}`}</span>
                        <span className="block text-xs text-slate-500">{user.email}</span>
                      </div>
                    </td>
                    <td className="px-5 py-3">
                      <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium ${roleColors[user.role] || roleColors.Viewer}`}>
                        <RoleIcon className="h-3 w-3" />
                        {user.role}
                      </span>
                    </td>
                    <td className="px-5 py-3">
                      {user.isActive ? (
                        <span className="inline-flex items-center gap-1 text-xs text-emerald-400">
                          <span className="h-1.5 w-1.5 rounded-full bg-emerald-400" />
                          Active
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-xs text-slate-500">
                          <span className="h-1.5 w-1.5 rounded-full bg-slate-500" />
                          Disabled
                        </span>
                      )}
                      {user.mustChangePassword && (
                        <span className="ml-2 text-xs text-amber-400" title="Must change password on next login">
                          ⚠ Password reset
                        </span>
                      )}
                    </td>
                    <td className="px-5 py-3 text-sm text-slate-400">
                      {user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString() : 'Never'}
                    </td>
                    <td className="px-5 py-3 text-right">
                      <button
                        onClick={() => setEditUser(user)}
                        className="mr-2 rounded-lg p-1.5 text-slate-400 hover:bg-surface-700 hover:text-white transition-colors"
                        title="Edit"
                      >
                        <Pencil className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => setDeleteUser(user)}
                        className="rounded-lg p-1.5 text-slate-400 hover:bg-red-500/20 hover:text-red-400 transition-colors"
                        title="Delete"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </td>
                  </tr>
                )
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Create/Edit Modal */}
      {(showCreate || editUser) && (
        <UserFormModal
          user={editUser}
          onClose={() => { setShowCreate(false); setEditUser(null) }}
        />
      )}

      {/* Delete Confirmation */}
      {deleteUser && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
          <div className="w-full max-w-md rounded-xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
            <h2 className="text-lg font-semibold text-white">Delete User</h2>
            <p className="mt-2 text-sm text-slate-300">
              Delete <strong className="text-white">{deleteUser.displayName || deleteUser.email}</strong>?
              This cannot be undone.
            </p>
            <div className="mt-6 flex justify-end gap-3">
              <button onClick={() => setDeleteUser(null)} className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 hover:bg-surface-800 transition-colors">Cancel</button>
              <button
                onClick={() => deleteMutation.mutate(deleteUser.id)}
                disabled={deleteMutation.isPending}
                className="rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-500 disabled:opacity-50 transition-colors"
              >
                {deleteMutation.isPending ? 'Deleting...' : 'Delete'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

function UserFormModal({ user, onClose }: { user: UserDto | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [error, setError] = useState('')
  const isEdit = !!user

  const createMutation = useMutation({
    mutationFn: (data: { email: string; password: string; firstName: string; lastName: string; role: string }) =>
      usersApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] })
      onClose()
    },
    onError: (err: any) => setError(err?.message || 'Failed to create user'),
  })

  const updateMutation = useMutation({
    mutationFn: (data: { id: string; payload: Record<string, unknown> }) =>
      usersApi.update(data.id, data.payload as any),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] })
      onClose()
    },
    onError: (err: any) => setError(err?.message || 'Failed to update user'),
  })

  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    if (isEdit) {
      updateMutation.mutate({
        id: user!.id,
        payload: {
          firstName: fd.get('firstName') as string,
          lastName: fd.get('lastName') as string,
          role: fd.get('role') as string,
          isActive: fd.get('isActive') === 'true',
        },
      })
    } else {
      createMutation.mutate({
        email: fd.get('email') as string,
        password: fd.get('password') as string,
        firstName: fd.get('firstName') as string,
        lastName: fd.get('lastName') as string,
        role: fd.get('role') as string,
      })
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">
          {isEdit ? `Edit ${user.displayName || user.email}` : 'Add User'}
        </h2>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          {error && <p className="text-sm text-red-400">{error}</p>}

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-slate-400">First Name</label>
              <input name="firstName" defaultValue={user?.firstName} required className={inputClass} />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-400">Last Name</label>
              <input name="lastName" defaultValue={user?.lastName} required className={inputClass} />
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-400">Email</label>
            <input name="email" type="email" defaultValue={user?.email} required disabled={isEdit} className={`${inputClass} ${isEdit ? 'opacity-50' : ''}`} />
          </div>

          {!isEdit && (
            <div>
              <label className="block text-xs font-medium text-slate-400">Password</label>
              <input name="password" type="password" required minLength={8} placeholder="Min 8 characters" className={inputClass} />
            </div>
          )}

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-slate-400">Role</label>
              <select name="role" defaultValue={user?.role || 'Viewer'} className={inputClass}>
                {ROLES.map(r => <option key={r} value={r}>{r}</option>)}
              </select>
            </div>
            {isEdit && (
              <div>
                <label className="block text-xs font-medium text-slate-400">Status</label>
                <select name="isActive" defaultValue={String(user?.isActive ?? true)} className={inputClass}>
                  <option value="true">Active</option>
                  <option value="false">Disabled</option>
                </select>
              </div>
            )}
          </div>

          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 hover:bg-surface-800 transition-colors">Cancel</button>
            <button
              type="submit"
              disabled={createMutation.isPending || updateMutation.isPending}
              className="rounded-lg bg-accent-600 px-4 py-2 text-sm font-medium text-white hover:bg-accent-500 disabled:opacity-50 transition-colors"
            >
              {createMutation.isPending || updateMutation.isPending ? 'Saving...' : isEdit ? 'Save Changes' : 'Create User'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
