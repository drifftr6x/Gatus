import { Routes, Route, Navigate } from 'react-router-dom'
import { AppShell } from '@/components/app-shell'
import { AuthProvider } from '@/hooks/useAuth'
import { ProtectedRoute } from '@/components/protected-route'
import { DashboardPage } from '@/pages/dashboard'
import { DevicesPage } from '@/pages/devices'
import { PoliciesPage } from '@/pages/policies'
import { ContentPage } from '@/pages/content'
import { SettingsPage } from '@/pages/settings'
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
                  <Route path="/policies" element={<PoliciesPage />} />
                  <Route path="/content" element={<ContentPage />} />
                  <Route path="/settings" element={<SettingsPage />} />
                </Routes>
              </AppShell>
            </ProtectedRoute>
          }
        />
      </Routes>
    </AuthProvider>
  )
}
