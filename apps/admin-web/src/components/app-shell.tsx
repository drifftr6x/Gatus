import { Link, useLocation } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { devicesApi, groupsApi, telemetryApi, alertsApi, deploymentsApi } from '@/lib/api'
import {
  LayoutDashboard,
  Monitor,
  CalendarClock,
  FolderOpen,
  FolderTree,
  BellRing,
  BarChart3,
  Bell,
  Settings,
  LogOut,
  ShieldCheck,
  ScrollText,
  Send,
  } from 'lucide-react'
import { clsx } from 'clsx'
import { useAuth } from '@/hooks/useAuth'
import { useSignalR } from '@/hooks/useSignalR'
import { ThemePicker } from '@/components/theme-picker'
import { useProductConfig } from '@/hooks/useProductConfig'

const navItems = [
  { href: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { href: '/devices', label: 'Devices', icon: Monitor },
  { href: '/kiosk-profiles', label: 'Kiosk Profiles', icon: ShieldCheck },
  { href: '/remote-actions', label: 'Remote Actions', icon: Send },
  { href: '/groups', label: 'Groups', icon: FolderTree },
  { href: '/schedules', label: 'Schedules', icon: CalendarClock },
  { href: '/content', label: 'Content', icon: FolderOpen },
  { href: '/alerts', label: 'Alerts', icon: BellRing },
  { href: '/analytics', label: 'Analytics', icon: BarChart3 },
  { href: '/notifications', label: 'Notifications', icon: Bell },
  { href: '/logs', label: 'Logs', icon: ScrollText },
  { href: '/settings', label: 'Settings', icon: Settings },
  ]

export function AppShell({ children }: { children: React.ReactNode }) {
  const location = useLocation()
  const queryClient = useQueryClient()
  const { user, logout } = useAuth()
  const { isConnected } = useSignalR()
  const { data: product } = useProductConfig()

  const coreItems = navItems.filter((item) =>
    ['/dashboard', '/devices', '/kiosk-profiles', '/remote-actions', '/settings'].includes(item.href),
  )
  const advancedItems = navItems.filter((item) =>
    ['/groups', '/schedules', '/content', '/alerts', '/analytics', '/notifications', '/logs'].includes(item.href),
  )

  const prefetchNavigation = (href: string) => {
    if (href === '/dashboard') {
      void queryClient.prefetchQuery({
        queryKey: ['telemetry-summary'],
        queryFn: telemetryApi.summary,
        staleTime: 15_000,
      })
      void queryClient.prefetchQuery({
        queryKey: ['devices', 'all'],
        queryFn: devicesApi.listAll,
        staleTime: 30_000,
      })
      void queryClient.prefetchQuery({
        queryKey: ['alerts', 'count'],
        queryFn: alertsApi.count,
        staleTime: 15_000,
      })
      void queryClient.prefetchQuery({
        queryKey: ['recent-deployments'],
        queryFn: () => deploymentsApi.list({ limit: 5 }),
        staleTime: 15_000,
      })
    }
    if (href === '/devices' || href === '/groups') {
      void queryClient.prefetchQuery({
        queryKey: ['devices'],
        queryFn: () => devicesApi.list({ pageSize: 500 }),
        staleTime: 30_000,
      })
      void queryClient.prefetchQuery({
        queryKey: ['devices', 'all'],
        queryFn: devicesApi.listAll,
        staleTime: 30_000,
      })
      void queryClient.prefetchQuery({
        queryKey: ['deviceGroups'],
        queryFn: groupsApi.list,
        staleTime: 30_000,
      })
    }
  }

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
              {product?.productName ?? 'Gatus'}
            </span>
            <span className="rounded-md bg-surface-800 px-2 py-0.5 text-xs font-medium text-slate-400">
              {product?.edition ?? 'Core'}
            </span>
          </div>
          <div className="flex items-center gap-4">
            <div
              className={clsx(
                'flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium',
                isConnected
                  ? 'bg-emerald-500/10 text-emerald-400'
                  : 'bg-red-500/10 text-red-400',
              )}
              title={isConnected ? 'Real-time connected' : 'Real-time disconnected'}
            >
              <span className={clsx(
                'h-1.5 w-1.5 rounded-full',
                isConnected ? 'bg-emerald-400 animate-pulse' : 'bg-red-400',
              )} />
              {isConnected ? 'Live' : 'Offline'}
            </div>
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
            <p className="px-3 pb-1 text-[10px] font-semibold uppercase tracking-wider text-slate-600">Core</p>
            {coreItems.map((item) => {
              const isActive = location.pathname.startsWith(item.href)
              return (
                <Link
                  key={item.href}
                  to={item.href}
                  onMouseEnter={() => prefetchNavigation(item.href)}
                  onFocus={() => prefetchNavigation(item.href)}
                  className={clsx(
                    'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                    isActive ? 'bg-accent-500/15 text-accent-300' : 'text-slate-400 hover:bg-surface-800 hover:text-slate-200',
                  )}
                >
                  <item.icon className="h-4.5 w-4.5" />
                  {item.label}
                </Link>
              )
            })}
            <p className="mt-5 px-3 pb-1 text-[10px] font-semibold uppercase tracking-wider text-slate-600">Advanced Features</p>
            {advancedItems.map((item) => {
              const isActive = location.pathname.startsWith(item.href)
              const enabled = product?.features?.[item.href.slice(1) as keyof typeof product.features] ?? true
              if (!enabled) return null
              return (
                <Link
                  key={item.href}
                  to={item.href}
                  onMouseEnter={() => prefetchNavigation(item.href)}
                  onFocus={() => prefetchNavigation(item.href)}
                  className={clsx(
                    'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                    isActive ? 'bg-accent-500/15 text-accent-300' : 'text-slate-400 hover:bg-surface-800 hover:text-slate-200',
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
