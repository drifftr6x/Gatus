import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { KeyRound } from 'lucide-react'
import { useAuth } from '@/hooks/useAuth'
import { authApi } from '@/lib/api'

export function ChangePasswordPage() {
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    if (newPassword.length < 8) {
      setError('New password must be at least 8 characters')
      return
    }
    if (newPassword !== confirmPassword) {
      setError('New passwords do not match')
      return
    }

    setIsLoading(true)
    try {
      await authApi.changePassword({ currentPassword, newPassword })
      // Server invalidated all sessions — log out and force fresh login
      await logout()
      navigate('/login', { replace: true, state: { message: 'Password changed — sign in with your new password' } })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Password change failed')
      setIsLoading(false)
    }
  }

  const inputClass =
    'mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500'

  return (
    <div className="flex min-h-screen items-center justify-center bg-surface-950 px-4">
      <div className="pointer-events-none absolute inset-0 overflow-hidden">
        <div className="absolute -top-40 left-1/2 h-80 w-[36rem] -translate-x-1/2 rounded-full bg-accent-500/20 blur-3xl" />
      </div>

      <div className="relative w-full max-w-sm space-y-6 rounded-2xl border border-surface-800 bg-surface-900 p-8 shadow-2xl">
        <div className="text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-xl bg-amber-500 text-white shadow-lg shadow-amber-500/30">
            <KeyRound className="h-7 w-7" />
          </div>
          <h2 className="mt-4 text-2xl font-semibold tracking-tight text-white">
            Change Password
          </h2>
          <p className="mt-1 text-sm text-slate-400">
            {user?.mustChangePassword
              ? 'You must set a new password before continuing'
              : 'Update your account password'}
          </p>
        </div>

        {error && (
          <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-sm text-red-400">
            {error}
          </div>
        )}

        <form className="space-y-4" onSubmit={handleSubmit}>
          <div>
            <label className="block text-sm font-medium text-slate-300">Current password</label>
            <input
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              required
              autoComplete="current-password"
              className={inputClass}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">New password</label>
            <input
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
              minLength={8}
              autoComplete="new-password"
              className={inputClass}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Confirm new password</label>
            <input
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              minLength={8}
              autoComplete="new-password"
              className={inputClass}
            />
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="w-full rounded-lg bg-accent-500 px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-accent-400 disabled:opacity-50"
          >
            {isLoading ? 'Changing…' : 'Change password'}
          </button>

          {!user?.mustChangePassword && (
            <button
              type="button"
              onClick={() => navigate(-1)}
              className="w-full rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 hover:bg-surface-800"
            >
              Cancel
            </button>
          )}
        </form>
      </div>
    </div>
  )
}
