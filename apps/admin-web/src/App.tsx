import { Routes, Route, Navigate } from 'react-router-dom'
import { AppShell } from '@/components/app-shell'
import { DashboardPage } from '@/pages/dashboard'
import { DevicesPage } from '@/pages/devices'
import { PoliciesPage } from '@/pages/policies'
import { ContentPage } from '@/pages/content'
import { SettingsPage } from '@/pages/settings'

export default function App() {
  return (
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
  )
}
