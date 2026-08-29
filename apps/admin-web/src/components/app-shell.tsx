import { Link, useLocation } from 'react-router-dom'
import {
  LayoutDashboard,
  Monitor,
  CalendarClock,
  FolderOpen,
  FolderTree,
  BellRing,
  BarChart3,
  Settings,
  LogOut,
  ShieldCheck,
  } from 'lucide-react'
import { clsx } from 'clsx'
import { useAuth } from '@/hooks/useAuth'
import { ThemePicker } from '@/components/theme-picker'

const navItems = [
  { href: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { href: '/devices', label: 'Devices', icon: Monitor },
  { href: '/groups', label: 'Groups', icon: FolderTree },
  { href: '/schedules', label: 'Schedules', icon: CalendarClock },
  { href: '/content', label: 'Content', icon: FolderOpen },
  { href: '/alerts', label: 'Alerts', icon: BellRing },
  { href: '/analytics', label: 'Analytics', icon: BarChart3 },
  { href: '/settings', label: 'Settings', icon: Settings },
  ]

export function AppShell({ children }: { children: React.ReactNode }) {
  const location = useLocation()
  const { user, logout } = useAuth()

  return (
    <div className="min-h-screen bg-surface-950">
      {/* Top bar */}
      <header className="sticky top-0 z-40 border-b border-surface-800 bg-surface-900/80 backdrop-blur">
        <div className="mx-auto flex h-14 max-w-screen-2xl items-center justify-between px-6">
          <div className="flex items-center gap-3">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-accent-500 text-white shadow-lg shadow-accent-500/30">
              <ShieldCheck className="h-5 w-5" />
            </div>
            <span className="text-lg font-semibold tracking-tight text-white">
              Gatus Kiosk
            </span>
            <span className="rounded-md bg-surface-800 px-2 py-0.5 text-xs font-medium text-slate-400">
              Admin
            </span>
          </div>
          <div className="flex items-center gap-4">
            <ThemePicker />
            <span className="text-sm text-slate-400">{user?.displayName}</span>
            <button
              onClick={logout}
              className="flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-sm text-slate-400 transition-colors hover:bg-surface-800 hover:text-white"
              title="Sign out"
            >
              <LogOut className="h-4 w-4" />
              Sign out
            </button>
          </div>
        </div>
      </header>

      <div className="mx-auto flex max-w-screen-2xl">
        {/* Sidebar */}
        <aside className="sticky top-14 h-[calc(100vh-3.5rem)] w-56 shrink-0 border-r border-surface-800 p-3">
          <nav className="space-y-1">
            {navItems.map((item) => {
              const isActive = location.pathname.startsWith(item.href)
              return (
                <Link
                  key={item.href}
                  to={item.href}
                  className={clsx(
                    'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                    isActive
                      ? 'bg-accent-500/15 text-accent-300'
                      : 'text-slate-400 hover:bg-surface-800 hover:text-slate-200',
                  )}
                >
                  <item.icon className="h-4.5 w-4.5" />
                  {item.label}
                </Link>
              )
            })}
          </nav>
        </aside>

        {/* Main content */}
        <main className="min-w-0 flex-1 p-6">{children}</main>
      </div>
    </div>
  )
}
