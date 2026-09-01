import { Routes, Route, Navigate } from 'react-router-dom'
import { AppShell } from '@/components/app-shell'
import { AuthProvider } from '@/hooks/useAuth'
import { ProtectedRoute } from '@/components/protected-route'
import { DashboardPage } from '@/pages/dashboard'
import { DevicesPage } from '@/pages/devices'
import { KioskProfilesPage } from '@/pages/kiosk-profiles'
import { RemoteActionsPage } from '@/pages/remote-actions'
import { DeviceDetailPage } from '@/pages/device-detail'
import { GroupsPage } from '@/pages/groups'
import { SchedulesPage } from '@/pages/schedules'
import { ContentPage } from '@/pages/content'
import { AlertsPage } from '@/pages/alerts'
import { AnalyticsPage } from '@/pages/analytics'
import { NotificationsPage } from '@/pages/notifications'
import { SettingsPage } from '@/pages/settings'
import { LogsPage } from '@/pages/logs'
import { LoginPage } from '@/pages/login'

export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/*"
          element={
            <ProtectedRoute>
              <AppShell>
                <Routes>
                  <Route path="/" element={<Navigate to="/dashboard" replace />} />
                  <Route path="/dashboard" element={<DashboardPage />} />
                  <Route path="/devices" element={<DevicesPage />} />
                  <Route path="/kiosk-profiles" element={<KioskProfilesPage />} />
                  <Route path="/remote-actions" element={<RemoteActionsPage />} />
                  <Route path="/devices/:id" element={<DeviceDetailPage />} />
                  <Route path="/groups" element={<GroupsPage />} />
                  <Route path="/schedules" element={<SchedulesPage />} />
                  <Route path="/content" element={<ContentPage />} />
                  <Route path="/alerts" element={<AlertsPage />} />
                  <Route path="/analytics" element={<AnalyticsPage />} />
                  <Route path="/notifications" element={<NotificationsPage />} />
                  <Route path="/settings" element={<SettingsPage />} />
                  <Route path="/logs" element={<LogsPage />} />
                </Routes>
              </AppShell>
            </ProtectedRoute>
          }
        />
      </Routes>
    </AuthProvider>
  )
}
